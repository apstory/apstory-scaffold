﻿using Apstory.Scaffold.Domain.Util;
using Apstory.Scaffold.Model.Config;
using Apstory.Scaffold.Model.Sql;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using System.Text;
using Apstory.Scaffold.Domain.Service;
using Apstory.Scaffold.Model.Enum;

namespace Apstory.Scaffold.Domain.Scaffold
{
    public class SqlForeignDomainServiceInterfaceScaffold
    {
        private readonly CSharpConfig _config;
        private readonly LockingService _lockingService;

        public SqlForeignDomainServiceInterfaceScaffold(CSharpConfig csharpConfig, LockingService lockingService)
        {
            _config = csharpConfig;
            _lockingService = lockingService;
        }

        public async Task<ScaffoldResult> DeleteCode(SqlStoredProcedure sqlStoredProcedure)
        {
            var methodName = GetMethodNameWithFK(sqlStoredProcedure);
            if (methodName.StartsWith("InsUpd", StringComparison.OrdinalIgnoreCase) ||
                methodName.StartsWith("Del", StringComparison.OrdinalIgnoreCase))
                return ScaffoldResult.Skipped;

            var domainServiceInterfacePath = GetFilePath(sqlStoredProcedure);
            if (!File.Exists(domainServiceInterfacePath))
                return ScaffoldResult.Skipped;

            try
            {
                var scaffoldingResult = ScaffoldResult.Updated;
                await _lockingService.AcquireLockAsync(domainServiceInterfacePath);

                var existingFileContent = FileUtils.SafeReadAllText(domainServiceInterfacePath);
                var syntaxTree = CSharpSyntaxTree.ParseText(existingFileContent);

                var updatedFileContent = RemoveMethodCall(syntaxTree.GetRoot(), sqlStoredProcedure);
                if (string.IsNullOrEmpty(updatedFileContent))
                {
                    File.Delete(domainServiceInterfacePath);
                    Logger.LogSuccess($"[Deleted Repository Interface] {domainServiceInterfacePath}");
                    scaffoldingResult = ScaffoldResult.Deleted;
                }
                else
                {
                    FileUtils.WriteTextAndDirectory(domainServiceInterfacePath, updatedFileContent);
                    Logger.LogSuccess($"[Updated Repository Interface] {domainServiceInterfacePath} removed method {sqlStoredProcedure.StoredProcedureName}");
                }

                return scaffoldingResult;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                _lockingService.ReleaseLock(domainServiceInterfacePath);
            }
        }

        public async Task<ScaffoldResult> GenerateCode(SqlTable sqlTable, SqlStoredProcedure sqlStoredProcedure)
        {
            var methodName = GetMethodNameWithFK(sqlStoredProcedure);
            if (methodName.StartsWith("InsUpd", StringComparison.OrdinalIgnoreCase) ||
                methodName.StartsWith("Del", StringComparison.OrdinalIgnoreCase))
                return ScaffoldResult.Skipped;

            //Do not generate if we dont have any FK constraints
            var foreignConstraints = sqlTable.Constraints.Where(s => s.ConstraintType == Model.Enum.ConstraintType.ForeignKey);
            if (!foreignConstraints.Any())
                return ScaffoldResult.Skipped;

            var scaffoldingResult = ScaffoldResult.Updated;
            var methodBody = GenerateInterfaceMethod(sqlStoredProcedure);
            var domainInterfacePath = GetFilePath(sqlStoredProcedure);
            var existingFileContent = string.Empty;

            try
            {
                await _lockingService.AcquireLockAsync(domainInterfacePath);

                SyntaxNode syntaxNode;
                if (!File.Exists(domainInterfacePath))
                {
                    scaffoldingResult = ScaffoldResult.Created;
                    Logger.LogWarn($"[File does not exist] Creating {domainInterfacePath}");
                    syntaxNode = CreateCSharpFileOutline(sqlStoredProcedure);
                }
                else
                {
                    existingFileContent = FileUtils.SafeReadAllText(domainInterfacePath);
                    var syntaxTree = CSharpSyntaxTree.ParseText(existingFileContent);
                    syntaxNode = syntaxTree.GetRoot();
                }

                var updatedFileContent = CreateOrUpdateMethod(syntaxNode, sqlStoredProcedure, methodBody);
                if (!existingFileContent.Equals(updatedFileContent))
                {
                    FileUtils.WriteTextAndDirectory(domainInterfacePath, updatedFileContent);
                    Logger.LogSuccess($"[Created Foreign Service Interface] {domainInterfacePath} for method {sqlStoredProcedure.StoredProcedureName}");
                }
                else
                {
#if DEBUGFORCESCAFFOLD
                    FileUtils.WriteTextAndDirectory(domainInterfacePath, updatedFileContent);
                    Logger.LogSuccess($"[Force Created Foreign Service Interface] {domainInterfacePath} for method {sqlStoredProcedure.StoredProcedureName}");
#else
                    Logger.LogSkipped($"[Skipped Service Interface] Method {sqlStoredProcedure.StoredProcedureName}");
                    scaffoldingResult = ScaffoldResult.Skipped;
#endif
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[Foreign Service Interface] {ex.Message}");
            }
            finally
            {
                _lockingService.ReleaseLock(domainInterfacePath);
            }

            return scaffoldingResult;
        }

        private string CreateOrUpdateMethod(SyntaxNode root, SqlStoredProcedure sqlStoredProcedure, string methodBody)
        {
            var interfaceName = GetInterfaceName(sqlStoredProcedure);
            var methodName = GetMethodNameWithFK(sqlStoredProcedure);

            var interfaceDeclaration = root.DescendantNodes()
                                           .OfType<InterfaceDeclarationSyntax>()
                                           .FirstOrDefault(c => c.Identifier.Text == interfaceName);

            if (interfaceDeclaration is null)
            {
                // Add the class if it doesn't exist
                var newInterface = CreateCSharpInterface(sqlStoredProcedure);
                root = ((CompilationUnitSyntax)root).AddMembers(newInterface);
                interfaceDeclaration = root.DescendantNodes()
                                       .OfType<InterfaceDeclarationSyntax>()
                                       .First(c => c.Identifier.Text == interfaceName);
            }

            // Find the method declaration
            var method = interfaceDeclaration.Members
                                             .OfType<MethodDeclarationSyntax>()
                                             .FirstOrDefault(m => m.Identifier.Text == methodName);


            var updatedMethod = SyntaxFactory.ParseMemberDeclaration(methodBody);

            SyntaxNode updatedRoot;
            if (method is not null)
            {
                updatedRoot = root.ReplaceNode(method, updatedMethod);
            }
            else
            {
                var updatedClass = interfaceDeclaration.AddMembers(updatedMethod);
                updatedRoot = root.ReplaceNode(interfaceDeclaration, updatedClass);
            }

            return updatedRoot.NormalizeWhitespace().ToFullString();
        }

        private string RemoveMethodCall(SyntaxNode root, SqlStoredProcedure sqlStoredProcedure)
        {
            var interfaceName = GetInterfaceName(sqlStoredProcedure);
            var methodName = GetMethodNameWithFK(sqlStoredProcedure);

            var interfaceDeclaration = root.DescendantNodes()
                                       .OfType<InterfaceDeclarationSyntax>()
                                       .FirstOrDefault(c => c.Identifier.Text == interfaceName);

            if (interfaceDeclaration is null)
                return string.Empty;

            var method = interfaceDeclaration.Members
                                             .OfType<MethodDeclarationSyntax>()
                                             .FirstOrDefault(m => m.Identifier.Text == methodName);

            if (method is null)
                return root.NormalizeWhitespace().ToFullString();

            var updatedRoot = root.RemoveNode(method, SyntaxRemoveOptions.KeepLeadingTrivia | SyntaxRemoveOptions.KeepTrailingTrivia);

            if (!updatedRoot.DescendantNodes().OfType<MethodDeclarationSyntax>().Any())
                return string.Empty;

            return updatedRoot.NormalizeWhitespace().ToFullString();
        }

        private string GetFilePath(SqlStoredProcedure sqlStoredProcedure)
        {
            var fileName = $"{GetInterfaceName(sqlStoredProcedure)}.#SCHEMA#.Foreign.Gen.cs".ToSchemaString(sqlStoredProcedure.Schema);
            var domainInterfacePath = Path.Combine(_config.Directories.DomainInterfaceDirectory.ToSchemaString(sqlStoredProcedure.Schema), fileName);
            return domainInterfacePath;
        }

        private SyntaxNode CreateCSharpFileOutline(SqlStoredProcedure sqlStoredProcedure)
        {
            var root = SyntaxFactory.CompilationUnit()
                                    .AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(GetModelNamespace(sqlStoredProcedure))))
                                    .AddMembers(SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(GetDomainInterfaceNamespace(sqlStoredProcedure)))
                                    .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(CreateCSharpInterface(sqlStoredProcedure))));

            return root;
        }

        private InterfaceDeclarationSyntax CreateCSharpInterface(SqlStoredProcedure sqlStoredProcedure)
        {
            return SyntaxFactory.InterfaceDeclaration(GetInterfaceName(sqlStoredProcedure))
                                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.PartialKeyword)));
        }

        private string GenerateInterfaceMethod(SqlStoredProcedure sqlStoredProcedure)
        {
            var methodName = GetMethodNameWithFK(sqlStoredProcedure);

            bool useSeperateParameters = !methodName.StartsWith("InsUpd");
            if (useSeperateParameters)
            {
                var sb = new StringBuilder();
                sb.Append($"Task<List<{GetModelNamespace(sqlStoredProcedure)}.{sqlStoredProcedure.TableName}>> {methodName}(");

                foreach (var param in sqlStoredProcedure.Parameters)
                    if (!param.ColumnName.Equals("RetMsg", StringComparison.OrdinalIgnoreCase))
                        if (!string.IsNullOrEmpty(param.DefaultValue))
                            sb.Append($"{param.ToCSharpTypeString(true)} {param.ColumnName.ToCamelCase()} = \"{param.DefaultValue}\",");
                        else
                            sb.Append($"{param.ToCSharpTypeString(true)} {param.ColumnName.ToCamelCase()},");

                sb.Remove(sb.Length - 1, 1);
                sb.AppendLine(");");
                return sb.ToString();
            }
            else
                return $"Task<{GetModelNamespace(sqlStoredProcedure)}.{sqlStoredProcedure.TableName}> {methodName}({GetModelNamespace(sqlStoredProcedure)}.{sqlStoredProcedure.TableName} {sqlStoredProcedure.TableName.ToCamelCase()});";
        }

        private string GetMethodNameWithFK(SqlStoredProcedure sqlStoredProcedure)
        {
            return sqlStoredProcedure.GetMethodName() + "IncludeForeignKeys";
        }

        private string GetInterfaceName(SqlStoredProcedure sqlStoredProcedure)
        {
            return $"I{sqlStoredProcedure.TableName.ToPascalCase()}Service";
        }

        private string GetModelNamespace(SqlStoredProcedure sqlStoredProcedure)
        {
            return _config.Namespaces.ModelNamespace.ToSchemaString(sqlStoredProcedure.Schema);
        }

        private string GetDomainInterfaceNamespace(SqlStoredProcedure sqlStoredProcedure)
        {
            return _config.Namespaces.DomainInterfaceNamespace.ToSchemaString(sqlStoredProcedure.Schema);
        }
    }
}
