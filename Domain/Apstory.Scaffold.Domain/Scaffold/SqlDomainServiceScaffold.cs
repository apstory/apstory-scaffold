using Apstory.Scaffold.Domain.Util;
using Apstory.Scaffold.Model.Config;
using Apstory.Scaffold.Model.Sql;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using System.Text;
using Apstory.Scaffold.Domain.Service;

namespace Apstory.Scaffold.Domain.Scaffold
{
    public class SqlDomainServiceScaffold
    {
        private readonly CSharpConfig _config;
        private readonly LockingService _lockingService;

        public SqlDomainServiceScaffold(CSharpConfig csharpConfig, LockingService lockingService)
        {
            _config = csharpConfig;
            _lockingService = lockingService;
        }

        public async Task DeleteCode(SqlStoredProcedure sqlStoredProcedure)
        {
            var domainServicePath = GetFilePath(sqlStoredProcedure);
            await _lockingService.AcquireLockAsync(domainServicePath);

            var existingFileContent = FileUtils.SafeReadAllText(domainServicePath);
            var syntaxTree = CSharpSyntaxTree.ParseText(existingFileContent);

            var updatedFileContent = RemoveMethodCall(syntaxTree.GetRoot(), sqlStoredProcedure);
            if (string.IsNullOrEmpty(updatedFileContent))
            {
                File.Delete(domainServicePath);
                Logger.LogSuccess($"[Deleted Service] {domainServicePath}");
            }
            else
            {
                FileUtils.WriteTextAndDirectory(domainServicePath, updatedFileContent);
                Logger.LogSuccess($"[Updated Service] {domainServicePath} removed method {sqlStoredProcedure.StoredProcedureName}");
            }

            _lockingService.ReleaseLock(domainServicePath);
        }

        public async Task GenerateCode(SqlStoredProcedure sqlStoredProcedure)
        {
            var methodBody = GenerateStoredProcedureMethod(sqlStoredProcedure);
            var domainServicePath = GetFilePath(sqlStoredProcedure);
            var existingFileContent = string.Empty;

            await _lockingService.AcquireLockAsync(domainServicePath);

            SyntaxNode syntaxNode;
            if (!File.Exists(domainServicePath))
            {
                Logger.LogWarn($"[File does not exist] Creating {domainServicePath}");
                syntaxNode = CreateCSharpFileOutline(sqlStoredProcedure);
            }
            else
            {
                existingFileContent = FileUtils.SafeReadAllText(domainServicePath);
                var syntaxTree = CSharpSyntaxTree.ParseText(existingFileContent);
                syntaxNode = syntaxTree.GetRoot();
            }

            var updatedFileContent = CreateOrUpdateMethod(syntaxNode, sqlStoredProcedure, methodBody);
            if (!existingFileContent.Equals(updatedFileContent))
            {
                FileUtils.WriteTextAndDirectory(domainServicePath, updatedFileContent);
                Logger.LogSuccess($"[Created Service] {domainServicePath} for method {sqlStoredProcedure.StoredProcedureName}");
            }
            else
            {
#if DEBUGFORCESCAFFOLD
                FileUtils.WriteTextAndDirectory(domainServicePath, updatedFileContent);
                Logger.LogSuccess($"[Force Created Service] {domainServicePath} for method {sqlStoredProcedure.StoredProcedureName}");
#else
                Logger.LogSkipped($"[Skipped Service] Method {sqlStoredProcedure.StoredProcedureName}");
#endif
            }

            _lockingService.ReleaseLock(domainServicePath);
        }

        private string CreateOrUpdateMethod(SyntaxNode root, SqlStoredProcedure sqlStoredProcedure, string methodBody)
        {
            var className = GetClassName(sqlStoredProcedure);
            var methodName = GetMethodName(sqlStoredProcedure);

            var classDeclaration = root.DescendantNodes()
                                       .OfType<ClassDeclarationSyntax>()
                                       .FirstOrDefault(c => c.Identifier.Text == className);

            if (classDeclaration is null)
            {
                // Add the class if it doesn't exist
                var newClass = CreateCSharpClass(sqlStoredProcedure);

                root = ((CompilationUnitSyntax)root).AddMembers(newClass);
                classDeclaration = root.DescendantNodes()
                                       .OfType<ClassDeclarationSyntax>()
                                       .First(c => c.Identifier.Text == className);
            }

            // Find the method declaration
            var method = classDeclaration.Members
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
                var updatedClass = classDeclaration.AddMembers(updatedMethod);
                updatedRoot = root.ReplaceNode(classDeclaration, updatedClass);
            }

            return updatedRoot.NormalizeWhitespace().ToFullString();
        }

        private string RemoveMethodCall(SyntaxNode root, SqlStoredProcedure sqlStoredProcedure)
        {
            var className = GetClassName(sqlStoredProcedure);
            var methodName = GetMethodName(sqlStoredProcedure);

            var classDeclaration = root.DescendantNodes()
                                       .OfType<ClassDeclarationSyntax>()
                                       .FirstOrDefault(c => c.Identifier.Text == className);

            if (classDeclaration is null)
                return string.Empty;

            var method = classDeclaration.Members
                                         .OfType<MethodDeclarationSyntax>()
                                         .FirstOrDefault(m => m.Identifier.Text == methodName);

            var updatedRoot = root.RemoveNode(method, SyntaxRemoveOptions.KeepLeadingTrivia | SyntaxRemoveOptions.KeepTrailingTrivia);

            if (!updatedRoot.DescendantNodes().OfType<MethodDeclarationSyntax>().Any())
                return string.Empty;

            return updatedRoot.NormalizeWhitespace().ToFullString();
        }

        private string GetFilePath(SqlStoredProcedure sqlStoredProcedure)
        {
            var fileName = $"{GetClassName(sqlStoredProcedure)}.Gen.cs";
            var domainServicePath = Path.Combine(_config.Directories.DomainDirectory.ToSchemaString(sqlStoredProcedure.Schema), fileName);
            return domainServicePath;
        }

        private SyntaxNode CreateCSharpFileOutline(SqlStoredProcedure sqlStoredProcedure)
        {
            var root = SyntaxFactory.CompilationUnit()
                                    .AddUsings(
                                        SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(GetDomainInterfaceNamespace(sqlStoredProcedure))),
                                        SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(GetDalInterfaceNamespace(sqlStoredProcedure))),
                                        SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(GetModelNamespace(sqlStoredProcedure))))
                                    .AddMembers(SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(GetDalNamespace(sqlStoredProcedure)))
                                    .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(CreateCSharpClass(sqlStoredProcedure))));

            return root;
        }

        private MemberDeclarationSyntax CreateCSharpClass(SqlStoredProcedure sqlStoredProcedure)
        {
            var className = $"{sqlStoredProcedure.TableName}Service";
            var interfaceName = $"I{sqlStoredProcedure.TableName}Repository";

            // Private readonly field
            var field = SyntaxFactory.FieldDeclaration(
                SyntaxFactory.VariableDeclaration(SyntaxFactory.ParseTypeName(interfaceName))
                    .WithVariables(SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.VariableDeclarator("_repo"))))
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword), SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword)));

            // Constructor
            var constructor = SyntaxFactory.ConstructorDeclaration(className)
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Parameter(SyntaxFactory.Identifier("repo"))
                        .WithType(SyntaxFactory.ParseTypeName(interfaceName)))))
                .WithBody(SyntaxFactory.Block(SyntaxFactory.SingletonList<StatementSyntax>(
                    SyntaxFactory.ExpressionStatement(SyntaxFactory.AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        SyntaxFactory.IdentifierName($"_{char.ToLower(sqlStoredProcedure.TableName[0])}{sqlStoredProcedure.TableName.Substring(1)}Repo"),
                        SyntaxFactory.IdentifierName("repo"))))));

            // Create the class
            return SyntaxFactory.ClassDeclaration(className)
                .WithModifiers(SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                    SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
                .WithMembers(SyntaxFactory.List(new MemberDeclarationSyntax[] { field, constructor }))
                .WithBaseList(SyntaxFactory.BaseList(SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(GetInterfaceName(sqlStoredProcedure))))));
        }

        private string GenerateStoredProcedureMethod(SqlStoredProcedure sqlStoredProcedure)
        {
            var sb = new StringBuilder();
            var methodName = GetMethodName(sqlStoredProcedure);

            bool useSeperateParameters = !methodName.StartsWith("InsUpd");
            if (useSeperateParameters)
            {
                sb.Append($"public async Task<List<{GetModelNamespace(sqlStoredProcedure)}.{sqlStoredProcedure.TableName}>> {methodName}(");

                foreach (var param in sqlStoredProcedure.Parameters)
                    if (!param.ColumnName.Equals("RetMsg", StringComparison.OrdinalIgnoreCase))
                        if (!string.IsNullOrEmpty(param.DefaultValue))
                            sb.Append($"{param.ToCSharpTypeString(true)} {param.ColumnName.ToCamelCase()} = \"{param.DefaultValue}\",");
                        else
                            sb.Append($"{param.ToCSharpTypeString(true)} {param.ColumnName.ToCamelCase()},");

                sb.Remove(sb.Length - 1, 1);
                sb.AppendLine(")");
                sb.AppendLine("{");

                sb.Append($"    return await _repo.{GetMethodName(sqlStoredProcedure)}(");
                foreach (var param in sqlStoredProcedure.Parameters)
                    if (!param.ColumnName.Equals("RetMsg", StringComparison.OrdinalIgnoreCase))
                        sb.Append($"{param.ColumnName.ToCamelCase()},");

                sb.Remove(sb.Length - 1, 1);
                sb.AppendLine($");");
                sb.AppendLine("}");

                return sb.ToString();
            }
            else
            {
                sb.AppendLine($"public async Task<{GetModelNamespace(sqlStoredProcedure)}.{sqlStoredProcedure.TableName}> {methodName}({GetModelNamespace(sqlStoredProcedure)}.{sqlStoredProcedure.TableName} {sqlStoredProcedure.TableName.ToCamelCase()})");
                sb.AppendLine("{");
                sb.AppendLine($"    return await _repo.{GetMethodName(sqlStoredProcedure)}({sqlStoredProcedure.TableName.ToCamelCase()});");
                sb.AppendLine("}");
            }

            return sb.ToString();
        }

        private string GetMethodName(SqlStoredProcedure sqlStoredProcedure)
        {
            return sqlStoredProcedure.StoredProcedureName.Replace("zgen_", "")
                                                         .Replace($"{sqlStoredProcedure.TableName}_", "")
                                                         .Replace("GetBy", $"Get{sqlStoredProcedure.TableName}By")
                                                         .Replace("InsUpd", $"InsUpd{sqlStoredProcedure.TableName}");
        }

        private string GetInterfaceName(SqlStoredProcedure sqlStoredProcedure)
        {
            return $"I{sqlStoredProcedure.TableName}Service";
        }

        private string GetClassName(SqlStoredProcedure sqlStoredProcedure)
        {
            return $"{sqlStoredProcedure.TableName}Service";
        }

        private string GetModelNamespace(SqlStoredProcedure sqlStoredProcedure)
        {
            return _config.Namespaces.ModelNamespace.ToSchemaString(sqlStoredProcedure.Schema);
        }

        private string GetDalNamespace(SqlStoredProcedure sqlStoredProcedure)
        {
            return _config.Namespaces.DalNamespace.ToSchemaString(sqlStoredProcedure.Schema);
        }

        private string GetDalInterfaceNamespace(SqlStoredProcedure sqlStoredProcedure)
        {
            return _config.Namespaces.DalInterfaceNamespace.ToSchemaString(sqlStoredProcedure.Schema);
        }

        private string GetDomainInterfaceNamespace(SqlStoredProcedure sqlStoredProcedure)
        {
            return _config.Namespaces.DomainInterfaceNamespace.ToSchemaString(sqlStoredProcedure.Schema);
        }
    }
}
