using Apstory.Scaffold.Domain.Service;
using Apstory.Scaffold.Domain.Util;
using Apstory.Scaffold.Model.Config;
using Apstory.Scaffold.Model.Sql;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace Apstory.Scaffold.Domain.Scaffold
{
    public class SqlDalRepositoryScaffold
    {
        private readonly CSharpConfig _config;
        private readonly LockingService _lockingService;

        public SqlDalRepositoryScaffold(CSharpConfig csharpConfig, LockingService lockingService)
        {
            _config = csharpConfig;
            _lockingService = lockingService;
        }

        public async Task DeleteCode(SqlStoredProcedure sqlStoredProcedure)
        {
            var dalRepositoryPath = GetFilePath(sqlStoredProcedure);
            await _lockingService.AcquireLockAsync(dalRepositoryPath);

            var existingFileContent = FileUtils.SafeReadAllText(dalRepositoryPath);
            var syntaxTree = CSharpSyntaxTree.ParseText(existingFileContent);

            var updatedFileContent = RemoveMethodCall(syntaxTree.GetRoot(), sqlStoredProcedure);
            if (string.IsNullOrEmpty(updatedFileContent))
            {
                File.Delete(dalRepositoryPath);
                Logger.LogSuccess($"[Deleted Repository] {dalRepositoryPath}");
            }
            else
            {
                FileUtils.WriteTextAndDirectory(dalRepositoryPath, updatedFileContent);
                Logger.LogSuccess($"[Updated Repository] {dalRepositoryPath} removed method {sqlStoredProcedure.StoredProcedureName}");
            }

            _lockingService.ReleaseLock(dalRepositoryPath);
        }

        public async Task GenerateCode(SqlStoredProcedure sqlStoredProcedure)
        {
            var dalRepositoryPath = GetFilePath(sqlStoredProcedure);
            var methodBody = GenerateStoredProcedureMethod(sqlStoredProcedure);
            var existingFileContent = string.Empty;

            await _lockingService.AcquireLockAsync(dalRepositoryPath);

            SyntaxNode syntaxNode;
            if (!File.Exists(dalRepositoryPath))
            {
                Logger.LogWarn($"[File does not exist] Creating {dalRepositoryPath}");
                syntaxNode = CreateCSharpFileOutline(sqlStoredProcedure);
            }
            else
            {
                existingFileContent = FileUtils.SafeReadAllText(dalRepositoryPath);
                var syntaxTree = CSharpSyntaxTree.ParseText(existingFileContent);
                syntaxNode = syntaxTree.GetRoot();
            }

            var updatedFileContent = AddUpdateMethodCall(syntaxNode, sqlStoredProcedure, methodBody);
            if (!existingFileContent.Equals(updatedFileContent))
            {
                FileUtils.WriteTextAndDirectory(dalRepositoryPath, updatedFileContent);
                Logger.LogSuccess($"[Created Repository] {dalRepositoryPath} for method {sqlStoredProcedure.StoredProcedureName}");
            }
            else
            {
#if DEBUGFORCESCAFFOLD
                FileUtils.WriteTextAndDirectory(dalRepositoryPath, updatedFileContent);
                Logger.LogSuccess($"[Force Created Repository] {dalRepositoryPath} for method {sqlStoredProcedure.StoredProcedureName}");
#else
                Logger.LogSkipped($"[Skipped Repository] Method {sqlStoredProcedure.StoredProcedureName}");
#endif
            }

            _lockingService.ReleaseLock(dalRepositoryPath);
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

        private string AddUpdateMethodCall(SyntaxNode root, SqlStoredProcedure sqlStoredProcedure, string methodBody)
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

        private string GetFilePath(SqlStoredProcedure sqlStoredProcedure)
        {
            var fileName = $"{GetClassName(sqlStoredProcedure)}.Gen.cs";
            var dalRepositoryPath = Path.Combine(_config.Directories.DalDirectory.ToSchemaString(sqlStoredProcedure.Schema), fileName);
            return dalRepositoryPath;
        }

        private SyntaxNode CreateCSharpFileOutline(SqlStoredProcedure sqlStoredProcedure)
        {
            var root = SyntaxFactory.CompilationUnit()
                                    .AddUsings(
                                        SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(GetInterfaceNamespace(sqlStoredProcedure))),
                                        SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(GetModelNamespace(sqlStoredProcedure))))
                                    .AddMembers(SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(GetDalNamespace(sqlStoredProcedure)))
                                    .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(CreateCSharpClass(sqlStoredProcedure))));

            return root;
        }

        private ClassDeclarationSyntax CreateCSharpClass(SqlStoredProcedure sqlStoredProcedure)
        {
            return SyntaxFactory.ClassDeclaration(GetClassName(sqlStoredProcedure))
                                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
                                .WithBaseList(SyntaxFactory.BaseList(SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(GetInterfaceName(sqlStoredProcedure))))));
        }

        private string GenerateStoredProcedureMethod(SqlStoredProcedure sqlStoredProcedure)
        {
            var sb = new StringBuilder();
            var methodName = GetMethodName(sqlStoredProcedure);
            bool hasReturnValues = false;
            bool useSeperateParameters = !methodName.StartsWith("InsUpd");

            if (useSeperateParameters)
            {
                sb.Append($"public async Task<List<{GetModelNamespace(sqlStoredProcedure)}.{sqlStoredProcedure.TableName}>> {methodName}(");

                foreach (var param in sqlStoredProcedure.Parameters)
                    if (!param.ColumnName.Equals("RetMsg", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrEmpty(param.DefaultValue))
                            sb.Append($"{param.ToCSharpTypeString(true)} {param.ColumnName.ToCamelCase()} = \"{param.DefaultValue}\",");
                        else
                            sb.Append($"{param.ToCSharpTypeString(true)} {param.ColumnName.ToCamelCase()},");
                    }

                sb.Remove(sb.Length - 1, 1);
                sb.AppendLine(")");
            }
            else
                return $"public async Task<{GetModelNamespace(sqlStoredProcedure)}.{sqlStoredProcedure.TableName}> {methodName}({GetModelNamespace(sqlStoredProcedure)}.{sqlStoredProcedure.TableName} {sqlStoredProcedure.TableName.ToCamelCase()})";

            sb.AppendLine("{");
            sb.AppendLine($"    List<{GetModelNamespace(sqlStoredProcedure)}.{sqlStoredProcedure.TableName}> ret{sqlStoredProcedure.TableName} = new List<{GetModelNamespace(sqlStoredProcedure)}<{sqlStoredProcedure.TableName}>>();");
            sb.AppendLine("    DynamicParameters dParams = new DynamicParameters();");

            // Append dynamic parameter setup
            foreach (var param in sqlStoredProcedure.Parameters)
            {
                if (param.ColumnName.Equals("RetMsg", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"    dParams.Add(\"RetMsg\", string.Empty, dbType: DbType.String, direction: ParameterDirection.Output);");
                    sb.AppendLine($"    dParams.Add(\"RetVal\", dbType: DbType.Int32, direction: ParameterDirection.ReturnValue);");
                    hasReturnValues = true;
                }
                else
                    sb.AppendLine($"    dParams.Add(\"{param.ColumnName}\", {(!useSeperateParameters ? $"{sqlStoredProcedure.TableName.ToCamelCase()}." : string.Empty)}{param.ColumnName.ToCamelCase()});");
            }

            sb.AppendLine();
            sb.AppendLine("    using (SqlConnection connection = GetConnection())");
            sb.AppendLine("    {");
            sb.AppendLine($"        ret{sqlStoredProcedure.TableName} = (await connection.QueryAsync<{GetModelNamespace(sqlStoredProcedure)}.{sqlStoredProcedure.TableName}>(\"{sqlStoredProcedure.Schema}.{sqlStoredProcedure.StoredProcedureName}\", dParams, commandType: System.Data.CommandType.StoredProcedure)).AsList();");
            sb.AppendLine("    }");
            sb.AppendLine();

            if (hasReturnValues)
            {
                sb.AppendLine("    string retMsg = dParams.Get<string>(\"RetMsg\");");
                sb.AppendLine("    int retVal = dParams.Get<int>(\"RetVal\");");
                sb.AppendLine("    if (retVal == 1) { throw new Exception(retMsg); }");
            }

            sb.AppendLine($"    return ret{sqlStoredProcedure.TableName};");
            sb.AppendLine("}");

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
            return $"I{sqlStoredProcedure.TableName}Repository";
        }

        private string GetClassName(SqlStoredProcedure sqlStoredProcedure)
        {
            return $"{sqlStoredProcedure.TableName}Repository";
        }

        private string GetModelNamespace(SqlStoredProcedure sqlStoredProcedure)
        {
            return _config.Namespaces.ModelNamespace.ToSchemaString(sqlStoredProcedure.Schema);
        }

        private string GetDalNamespace(SqlStoredProcedure sqlStoredProcedure)
        {
            return _config.Namespaces.DalNamespace.ToSchemaString(sqlStoredProcedure.Schema);
        }

        private string GetInterfaceNamespace(SqlStoredProcedure sqlStoredProcedure)
        {
            return _config.Namespaces.DalInterfaceNamespace.ToSchemaString(sqlStoredProcedure.Schema);
        }
    }
}
