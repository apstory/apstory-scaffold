using Apstory.Scaffold.Domain.Service;
using Apstory.Scaffold.Domain.Util;
using Apstory.Scaffold.Model.Config;
using Apstory.Scaffold.Model.Enum;
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

        public async Task<ScaffoldResult> DeleteCode(SqlStoredProcedure sqlStoredProcedure)
        {
            var scaffoldingResult = ScaffoldResult.Updated;
            var dalRepositoryPath = GetFilePath(sqlStoredProcedure);

            if (!File.Exists(dalRepositoryPath))
                return ScaffoldResult.Skipped;

            try
            {
                await _lockingService.AcquireLockAsync(dalRepositoryPath);

                var existingFileContent = FileUtils.SafeReadAllText(dalRepositoryPath);
                var syntaxTree = CSharpSyntaxTree.ParseText(existingFileContent);

                var updatedFileContent = RemoveMethodCall(syntaxTree.GetRoot(), sqlStoredProcedure);
                if (string.IsNullOrEmpty(updatedFileContent))
                {
                    File.Delete(dalRepositoryPath);
                    Logger.LogSuccess($"[Deleted Repository] {dalRepositoryPath}");
                    scaffoldingResult = ScaffoldResult.Deleted;
                }
                else
                {
                    FileUtils.WriteTextAndDirectory(dalRepositoryPath, updatedFileContent);
                    Logger.LogSuccess($"[Updated Repository] {dalRepositoryPath} removed method {sqlStoredProcedure.StoredProcedureName}");
                }

                return scaffoldingResult;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                _lockingService.ReleaseLock(dalRepositoryPath);
            }
        }

        public async Task<ScaffoldResult> GenerateCode(SqlStoredProcedure sqlStoredProcedure)
        {
            var scaffoldingResult = ScaffoldResult.Updated;
            var dalRepositoryPath = GetFilePath(sqlStoredProcedure);
            var methodBody = GenerateStoredProcedureMethod(sqlStoredProcedure);
            var existingFileContent = string.Empty;

            try
            {
                await _lockingService.AcquireLockAsync(dalRepositoryPath);

                SyntaxNode syntaxNode;
                if (!File.Exists(dalRepositoryPath))
                {
                    scaffoldingResult = ScaffoldResult.Created;
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
                    scaffoldingResult = ScaffoldResult.Skipped;
#endif
                }

                if (scaffoldingResult == ScaffoldResult.Created)
                {
                    var dalBasePath = Path.GetDirectoryName(_config.Directories.DalDirectory.ToSchemaString("dbo"));
                    var baseRepositoryPath = Path.Combine(dalBasePath, "BaseRepository.cs");
                    if (!File.Exists(baseRepositoryPath))
                    {
                        FileUtils.WriteTextAndDirectory(baseRepositoryPath, GetBaseRepositoryFile());
                        Logger.LogSuccess($"[Created Base Repository] {baseRepositoryPath}");
                    }

                    var dapperExtensionsPath = Path.Combine(dalBasePath, "Utils", "DapperExtensions.cs");
                    if (!File.Exists(dapperExtensionsPath))
                    {
                        FileUtils.WriteTextAndDirectory(dapperExtensionsPath, GetDapperExtensionsFile());
                        Logger.LogSuccess($"[Created Dapper Extensions] {dapperExtensionsPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[Repository] {ex.Message}");
            }
            finally
            {
                _lockingService.ReleaseLock(dalRepositoryPath);
            }

            return scaffoldingResult;
        }

        private string RemoveMethodCall(SyntaxNode root, SqlStoredProcedure sqlStoredProcedure)
        {
            var className = GetClassName(sqlStoredProcedure);
            var methodName = sqlStoredProcedure.GetMethodName();

            var classDeclaration = root.DescendantNodes()
                                       .OfType<ClassDeclarationSyntax>()
                                       .FirstOrDefault(c => c.Identifier.Text == className);

            if (classDeclaration is null)
                return string.Empty;

            var method = classDeclaration.Members
                                         .OfType<MethodDeclarationSyntax>()
                                         .FirstOrDefault(m => m.Identifier.Text == methodName);

            if (method is null)
                return root.NormalizeWhitespace().ToFullString();

            var updatedRoot = root.RemoveNode(method, SyntaxRemoveOptions.KeepLeadingTrivia | SyntaxRemoveOptions.KeepTrailingTrivia);

            if (!updatedRoot.DescendantNodes().OfType<MethodDeclarationSyntax>().Any())
                return string.Empty;

            return updatedRoot.NormalizeWhitespace().ToFullString();
        }

        private string AddUpdateMethodCall(SyntaxNode root, SqlStoredProcedure sqlStoredProcedure, string methodBody)
        {
            var className = GetClassName(sqlStoredProcedure);
            var methodName = sqlStoredProcedure.GetMethodName();

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
            var fileName = $"{GetClassName(sqlStoredProcedure)}.#SCHEMA#.Gen.cs".ToSchemaString(sqlStoredProcedure.Schema);
            var dalRepositoryPath = Path.Combine(_config.Directories.DalDirectory.ToSchemaString(sqlStoredProcedure.Schema), fileName);
            return dalRepositoryPath;
        }

        private SyntaxNode CreateCSharpFileOutline(SqlStoredProcedure sqlStoredProcedure)
        {
            var root = SyntaxFactory.CompilationUnit()
                                    .AddUsings(
                                        SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(GetDalInterfaceNamespace(sqlStoredProcedure))),
                                        SyntaxFactory.UsingDirective(SyntaxFactory.ParseName($"{_config.Namespaces.DalNamespace.ToSchemaString("dbo")}.Utils")),
                                        SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("Microsoft.Data.SqlClient")),
                                        SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Data")),
                                        SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("Dapper")))
                                    .AddMembers(SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(GetDalNamespace(sqlStoredProcedure)))
                                    .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(CreateCSharpClass(sqlStoredProcedure))));

            return root;
        }

        private ClassDeclarationSyntax CreateCSharpClass(SqlStoredProcedure sqlStoredProcedure)
        {
            return SyntaxFactory.ClassDeclaration(GetClassName(sqlStoredProcedure))
                                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
                                .WithBaseList(SyntaxFactory.BaseList(
                                                SyntaxFactory.SeparatedList<BaseTypeSyntax>(new[]
                                                {
                                                    SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName("BaseRepository")),
                                                    SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(GetInterfaceName(sqlStoredProcedure)))
                                                })))
                                .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(
                                    SyntaxFactory.ConstructorDeclaration(GetClassName(sqlStoredProcedure))
                                        .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                                        .WithParameterList(SyntaxFactory.ParameterList(
                                            SyntaxFactory.SingletonSeparatedList(
                                                SyntaxFactory.Parameter(SyntaxFactory.Identifier("connectionString"))
                                                    .WithType(SyntaxFactory.ParseTypeName("string")))))
                                        .WithInitializer(SyntaxFactory.ConstructorInitializer(
                                            SyntaxKind.BaseConstructorInitializer,
                                            SyntaxFactory.ArgumentList(
                                                SyntaxFactory.SingletonSeparatedList(
                                                    SyntaxFactory.Argument(SyntaxFactory.IdentifierName("connectionString"))))))
                                        .WithBody(SyntaxFactory.Block())));
        }


        private string GenerateStoredProcedureMethod(SqlStoredProcedure sqlStoredProcedure)
        {
            var sb = new StringBuilder();
            var methodName = sqlStoredProcedure.GetMethodName();
            bool hasReturnValues = false;
            bool useSeperateParameters = !methodName.StartsWith("InsUpd");
            bool returnLists = useSeperateParameters;
            bool returnsData = !methodName.StartsWith("Del");

            if (useSeperateParameters)
            {
                if (returnsData)
                    sb.Append($"public async Task<List<{GetModelNamespace(sqlStoredProcedure)}.{sqlStoredProcedure.TableName}>> {methodName}(");
                else
                    sb.Append($"public async Task {methodName}(");

                foreach (var param in sqlStoredProcedure.Parameters)
                    if (!param.ColumnName.Equals("RetMsg", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrEmpty(param.DefaultValue))
                            sb.Append($"{param.ToCSharpTypeString(returnsData)} {param.ColumnName.ToCamelCase()} = \"{param.DefaultValue}\",");
                        else
                            sb.Append($"{param.ToCSharpTypeString(returnsData)} {param.ColumnName.ToCamelCase()},");
                    }

                sb.Remove(sb.Length - 1, 1);
                sb.AppendLine(")");
            }
            else
                sb.Append($"public async Task<{GetModelNamespace(sqlStoredProcedure)}.{sqlStoredProcedure.TableName}> {methodName}({GetModelNamespace(sqlStoredProcedure)}.{sqlStoredProcedure.TableName} {sqlStoredProcedure.TableName.ToCamelCase()})");

            sb.AppendLine("{");

            if (returnsData)
                if (returnLists)
                    sb.AppendLine($"    List<{GetModelNamespace(sqlStoredProcedure)}.{sqlStoredProcedure.TableName}> ret{sqlStoredProcedure.TableName} = new List<{GetModelNamespace(sqlStoredProcedure)}.{sqlStoredProcedure.TableName}>();");
                else if (returnsData)
                    sb.AppendLine($"    {GetModelNamespace(sqlStoredProcedure)}.{sqlStoredProcedure.TableName} ret{sqlStoredProcedure.TableName};");


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
                {
                    if (param.DataType.StartsWith("udtt", StringComparison.OrdinalIgnoreCase))
                        sb.AppendLine($"    dParams.Add(\"{param.ColumnName}\", {param.ColumnName.ToCamelCase()}.ToDataTable().AsTableValuedParameter(\"{sqlStoredProcedure.Schema}.{param.DataType}\"));");
                    if (param.DataType.StartsWith("GeoLocation", StringComparison.OrdinalIgnoreCase))
                        sb.AppendLine($"    dParams.Add(\"{param.ColumnName}\", {param.ColumnName.ToCamelCase()}.ToString());");
                    else
                        sb.AppendLine($"    dParams.Add(\"{param.ColumnName}\", {(!useSeperateParameters ? $"{sqlStoredProcedure.TableName.ToCamelCase()}.{param.ColumnName.ToPascalCase()}" : param.ColumnName.ToCamelCase())});");
                }
            }

            sb.AppendLine();
            sb.AppendLine("    using (SqlConnection connection = GetConnection())");
            sb.AppendLine("    {");

            if (returnsData)
            {
                if (returnLists)
                    sb.AppendLine($"        ret{sqlStoredProcedure.TableName} = (await connection.QueryAsync<{GetModelNamespace(sqlStoredProcedure)}.{sqlStoredProcedure.TableName}>(\"{sqlStoredProcedure.Schema}.{sqlStoredProcedure.StoredProcedureName}\", dParams, commandType: System.Data.CommandType.StoredProcedure)).AsList();");
                else
                    sb.AppendLine($"        ret{sqlStoredProcedure.TableName} = (await connection.QueryFirstOrDefaultAsync<{GetModelNamespace(sqlStoredProcedure)}.{sqlStoredProcedure.TableName}>(\"{sqlStoredProcedure.Schema}.{sqlStoredProcedure.StoredProcedureName}\", dParams, commandType: System.Data.CommandType.StoredProcedure));");
            }
            else
                sb.AppendLine($"        await connection.ExecuteAsync(\"{sqlStoredProcedure.Schema}.{sqlStoredProcedure.StoredProcedureName}\", dParams, commandType: System.Data.CommandType.StoredProcedure);");


            sb.AppendLine("    }");
            sb.AppendLine();

            if (hasReturnValues)
            {
                sb.AppendLine("    string retMsg = dParams.Get<string>(\"RetMsg\");");
                sb.AppendLine("    int retVal = dParams.Get<int>(\"RetVal\");");
                sb.AppendLine("    if (retVal == 1) { throw new Exception(retMsg); }");
            }

            if (returnsData)
                sb.AppendLine($"    return ret{sqlStoredProcedure.TableName};");

            sb.AppendLine("}");

            return sb.ToString();
        }

        private string GetInterfaceName(SqlStoredProcedure sqlStoredProcedure)
        {
            return $"I{sqlStoredProcedure.TableName.ToPascalCase()}Repository";
        }

        private string GetClassName(SqlStoredProcedure sqlStoredProcedure)
        {
            return $"{sqlStoredProcedure.TableName.ToPascalCase()}Repository";
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


        private string GetBaseRepositoryFile()
        {
            var ns = _config.Namespaces.DalNamespace.ToSchemaString("dbo");
            return "using Microsoft.Data.SqlClient;\r\n\r\nnamespace " + ns + "\r\n{\r\n\tpublic partial class BaseRepository\r\n\t{\r\n\t\tprivate string _connectionString;\r\n\t\tpublic BaseRepository(string connectionString)\r\n\t\t{\r\n\t\t\t_connectionString = connectionString;\r\n\t\t}\r\n\t\tprotected SqlConnection GetConnection()\r\n\t\t{\r\n\t\t\treturn new SqlConnection(_connectionString);\r\n\t\t}\r\n\t}\r\n}\r\n";
        }

        private string GetDapperExtensionsFile()
        {
            var ns = _config.Namespaces.DalNamespace.ToSchemaString("dbo");
            return "using System.ComponentModel;\r\nusing System.Data;\r\n\r\nnamespace " + ns + ".Utils\r\n{\r\n\tpublic static class DapperExtensions\r\n\t{\r\n\t\tpublic static DataTable ToDataTable<T>(this List<T> iList)\r\n\t\t{\r\n\t\t\tDataTable dataTable = new DataTable();\r\n\t\t\tdataTable.Columns.Add(\"Id\", typeof(T));\r\n\r\n\t\t\tforeach (T iListItem in iList)\r\n\t\t\t\tdataTable.Rows.Add(iListItem);\r\n\r\n\t\t\treturn dataTable;\r\n\t\t}\r\n\r\n\t\tpublic static DataTable ToDataTable<T>(this List<T> iList, string columnName)\r\n\t\t{\r\n\t\t\tDataTable dataTable = new DataTable();\r\n\t\t\tPropertyDescriptorCollection propertyDescriptorCollection =\r\n\t\t\t\tTypeDescriptor.GetProperties(typeof(T));\r\n\t\t\tfor (int i = 0; i < propertyDescriptorCollection.Count; i++)\r\n\t\t\t{\r\n\t\t\t\tPropertyDescriptor propertyDescriptor = propertyDescriptorCollection[i];\r\n\t\t\t\tType type = propertyDescriptor.PropertyType;\r\n\r\n\t\t\t\tif (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))\r\n\t\t\t\t\ttype = Nullable.GetUnderlyingType(type);\r\n\r\n\t\t\t\tif (propertyDescriptor.Name == columnName)\r\n\t\t\t\t{\r\n\t\t\t\t\tdataTable.Columns.Add(propertyDescriptor.Name, type);\r\n\t\t\t\t}\r\n\t\t\t}\r\n\t\t\tobject[] values = new object[propertyDescriptorCollection.Count];\r\n\t\t\tobject v = new object();\r\n\t\t\tforeach (T iListItem in iList)\r\n\t\t\t{\r\n\t\t\t\tfor (int i = 0; i < values.Length; i++)\r\n\t\t\t\t{\r\n\t\t\t\t\tvalues[i] = propertyDescriptorCollection[i].GetValue(iListItem);\r\n\t\t\t\t\tif (propertyDescriptorCollection[i].Name == columnName)\r\n\t\t\t\t\t{\r\n\t\t\t\t\t\tv = values[i];\r\n\t\t\t\t\t\tdataTable.Rows.Add(v);\r\n\t\t\t\t\t}\r\n\t\t\t\t}\r\n\t\t\t}\r\n\t\t\treturn dataTable;\r\n\t\t}\r\n\t}\r\n}";
        }
    }
}
