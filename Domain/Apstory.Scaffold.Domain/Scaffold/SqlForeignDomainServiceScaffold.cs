using Apstory.Scaffold.Domain.Util;
using Apstory.Scaffold.Model.Config;
using Apstory.Scaffold.Model.Sql;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using System.Text;
using Apstory.Scaffold.Domain.Service;
using Apstory.Scaffold.Model.Enum;
using System.Data;
using System.Linq;

namespace Apstory.Scaffold.Domain.Scaffold
{
    public class SqlForeignDomainServiceScaffold
    {
        private readonly CSharpConfig _config;
        private readonly LockingService _lockingService;

        public SqlForeignDomainServiceScaffold(CSharpConfig csharpConfig, LockingService lockingService)
        {
            _config = csharpConfig;
            _lockingService = lockingService;
        }

        public async Task DeleteCode(SqlStoredProcedure sqlStoredProcedure)
        {
            var methodName = GetMethodName(sqlStoredProcedure);
            if (methodName.StartsWith("InsUpd", StringComparison.OrdinalIgnoreCase) ||
                methodName.StartsWith("Del", StringComparison.OrdinalIgnoreCase))
                return;

            var domainServicePath = GetFilePath(sqlStoredProcedure);
            if (!File.Exists(domainServicePath))
                return;

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

        public async Task GenerateCode(SqlTable sqlTable, SqlStoredProcedure sqlStoredProcedure)
        {
            var methodName = GetMethodName(sqlStoredProcedure);
            if (methodName.StartsWith("InsUpd", StringComparison.OrdinalIgnoreCase) ||
                methodName.StartsWith("Del", StringComparison.OrdinalIgnoreCase))
                return;

            //Do not generate if we dont have any FK constraints
            var foreignConstraints = sqlTable.Constraints.Where(s => s.ConstraintType == Model.Enum.ConstraintType.ForeignKey);
            if (!foreignConstraints.Any())
                return;

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

            syntaxNode = UpdateAndGeneratePartialClass(syntaxNode, sqlTable, sqlStoredProcedure);

            var updatedFileContent = syntaxNode.NormalizeWhitespace().ToString();
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

            //If there are no publicly callable methods, return empty string which will delete the file
            if (!updatedRoot.DescendantNodes()
                            .OfType<MethodDeclarationSyntax>()
                            .Where(method => method.Modifiers.Any(SyntaxKind.PublicKeyword)).Any())
                return string.Empty;

            return updatedRoot.NormalizeWhitespace().ToFullString();
        }

        private string GetFilePath(SqlStoredProcedure sqlStoredProcedure)
        {
            var fileName = $"{GetClassName(sqlStoredProcedure)}.Foreign.Gen.cs";
            var domainServicePath = Path.Combine(_config.Directories.DomainDirectory.ToSchemaString(sqlStoredProcedure.Schema), fileName);
            return domainServicePath;
        }

        private SyntaxNode CreateCSharpFileOutline(SqlStoredProcedure sqlStoredProcedure)
        {
            var root = SyntaxFactory.CompilationUnit()
                                    .AddUsings(
                                        SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(GetDomainInterfaceNamespace(sqlStoredProcedure))),
                                        SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(GetDalInterfaceNamespace(sqlStoredProcedure))),
                                        SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(GetModelNamespace(sqlStoredProcedure.Schema))))
                                    .AddMembers(SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(GetDomainNamespace(sqlStoredProcedure)))
                                    .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(
                                        RoslynUtils.CreateClass(GetClassName(sqlStoredProcedure), GetInterfaceName(sqlStoredProcedure), SyntaxKind.PublicKeyword, SyntaxKind.PartialKeyword))));

            return root;
        }

        public SyntaxNode UpdateAndGeneratePartialClass(SyntaxNode root, SqlTable sqlTable, SqlStoredProcedure sqlStoredProcedure)
        {
            var className = GetClassName(sqlStoredProcedure);
            var classDeclaration = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.Identifier.Text == className);

            if (classDeclaration == null)
                throw new System.Exception($"Class {className} not found in the provided code.");

            var foreignKeyConstraints = sqlTable.Constraints.Where(c => c.ConstraintType == ConstraintType.ForeignKey).ToList();

            var fields = foreignKeyConstraints.Select(constraint =>
                RoslynUtils.CreateField($"_{constraint.RefTable.ToCamelCase()}Repo", $"I{constraint.RefTable}Repository", SyntaxKind.PrivateKeyword, SyntaxKind.ReadOnlyKeyword));

            var parameters = foreignKeyConstraints.Select(constraint =>
                RoslynUtils.CreateParameter($"{constraint.RefTable.ToCamelCase()}Repo", $"I{constraint.RefTable}Repository")).ToList();
            parameters.Insert(0, RoslynUtils.CreateParameter($"repo", $"I{sqlTable.TableName}Repository"));

            var assignments = foreignKeyConstraints.Select(constraint =>
                RoslynUtils.CreateAssignmentExpression($"_{constraint.RefTable.ToCamelCase()}Repo", $"{constraint.RefTable.ToCamelCase()}Repo")).ToList();
            assignments.Insert(0, RoslynUtils.CreateAssignmentExpression($"_repo", $"repo"));

            var constructor = SyntaxFactory.ConstructorDeclaration(className)
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(parameters)))
                .WithBody(SyntaxFactory.Block(assignments));

            var protectedMethods = foreignKeyConstraints.Select(constraint => GenerateAppendMethod(sqlTable, constraint));

            var publicMethods = classDeclaration.Members.OfType<MethodDeclarationSyntax>()
                                                        .Where(m => m.Modifiers.Any(SyntaxKind.PublicKeyword))
                                                        .ToList();

            var updatedMethod = SyntaxFactory.ParseMemberDeclaration(GenerateStoredProcedureMethod(sqlTable, sqlStoredProcedure)) as MethodDeclarationSyntax;
            var existingMethodIndex = publicMethods.FindIndex(s => s.Identifier.Text.Equals(updatedMethod.Identifier.Text));

            if (existingMethodIndex >= 0)
                publicMethods[existingMethodIndex] = updatedMethod;
            else
                publicMethods.Add(updatedMethod);

            protectedMethods = protectedMethods.OrderBy(s => s.Identifier.Text).ToList();
            publicMethods = publicMethods.OrderBy(s => s.Identifier.Text).ToList();

            var allTogether = fields.Cast<MemberDeclarationSyntax>()
                                    .Union([constructor])
                                    .Union(publicMethods.Cast<MemberDeclarationSyntax>())
                                    .Union(protectedMethods.Cast<MemberDeclarationSyntax>())
                                    .ToArray();

            var syntaxNode = CreateCSharpFileOutline(sqlStoredProcedure);

            var newClassDeclaration = syntaxNode.DescendantNodes()
                                                .OfType<ClassDeclarationSyntax>()
                                                .FirstOrDefault(c => c.Identifier.Text == className);

            var updatedClassDeclaration = newClassDeclaration.AddMembers(allTogether);

            var updatedRoot = syntaxNode.ReplaceNode(newClassDeclaration, updatedClassDeclaration);
            return updatedRoot;
        }

        private MethodDeclarationSyntax GenerateAppendMethod(SqlTable sqlTable, SqlConstraint constraint)
        {
            var tableName = sqlTable.TableName;
            var refTable = constraint.RefTable;
            var columnName = constraint.Column;
            var modelNs = GetModelNamespace(sqlTable.Schema);

            // Use StringBuilder to construct the method as a string
            var methodBuilder = new StringBuilder();
            methodBuilder.AppendLine($"protected async Task<List<{modelNs}.{tableName}>> Append{refTable}(List<{modelNs}.{tableName}> {tableName.ToCamelCase()}s)");
            methodBuilder.AppendLine("{");
            methodBuilder.AppendLine($"    var distinct{columnName}Ids = {tableName.ToCamelCase()}s.Where(s => s.{columnName}.HasValue).Select(s => s.{columnName}.Value).Distinct().ToList();");
            methodBuilder.AppendLine($"    var distinct{refTable}s = await _{refTable.ToCamelCase()}Repo.Get{refTable}By{refTable}Ids(distinct{columnName}Ids, null);");
            methodBuilder.AppendLine();
            methodBuilder.AppendLine($"    foreach (var {tableName.ToCamelCase()} in {tableName.ToCamelCase()}s)");
            methodBuilder.AppendLine("    {");
            methodBuilder.AppendLine($"        {tableName.ToCamelCase()}.{refTable} = distinct{refTable}s.FirstOrDefault(s => s.{columnName} == {tableName.ToCamelCase()}.{columnName});");
            methodBuilder.AppendLine("    }");
            methodBuilder.AppendLine();
            methodBuilder.AppendLine($"    return {tableName.ToCamelCase()}s;");
            methodBuilder.AppendLine("}");

            // Parse the method string into a MethodDeclarationSyntax
            var methodDeclaration = SyntaxFactory.ParseMemberDeclaration(methodBuilder.ToString()) as MethodDeclarationSyntax;

            return methodDeclaration;
        }

        private string GenerateStoredProcedureMethod(SqlTable sqlTable, SqlStoredProcedure sqlStoredProcedure)
        {
            var sb = new StringBuilder();
            var methodName = GetMethodName(sqlStoredProcedure);

            bool useSeperateParameters = !methodName.StartsWith("InsUpd");
            if (useSeperateParameters)
            {
                sb.Append($"public async Task<List<{GetModelNamespace(sqlStoredProcedure.Schema)}.{sqlStoredProcedure.TableName}>> {GetMethodName(sqlStoredProcedure)}(");

                foreach (var param in sqlStoredProcedure.Parameters)
                    if (!param.ColumnName.Equals("RetMsg", StringComparison.OrdinalIgnoreCase))
                        if (!string.IsNullOrEmpty(param.DefaultValue))
                            sb.Append($"{param.ToCSharpTypeString(true)} {param.ColumnName.ToCamelCase()} = \"{param.DefaultValue}\",");
                        else
                            sb.Append($"{param.ToCSharpTypeString(true)} {param.ColumnName.ToCamelCase()},");

                sb.Remove(sb.Length - 1, 1);
                sb.AppendLine(")");
                sb.AppendLine("{");

                sb.Append($"    var ret{sqlStoredProcedure.TableName} = await _repo.{GetMethodName(sqlStoredProcedure)}(");
                foreach (var param in sqlStoredProcedure.Parameters)
                    if (!param.ColumnName.Equals("RetMsg", StringComparison.OrdinalIgnoreCase))
                        sb.Append($"{param.ColumnName.ToCamelCase()},");

                sb.Remove(sb.Length - 1, 1);
                sb.AppendLine($");");

                foreach (var constraint in sqlTable.Constraints.Where(s => s.ConstraintType == ConstraintType.ForeignKey))
                    sb.AppendLine($"    await Append{constraint.RefTable}(ret{sqlStoredProcedure.TableName});");

                sb.AppendLine($"    return ret{sqlStoredProcedure.TableName};");
                sb.AppendLine("}");

                return sb.ToString();
            }
            else
            {
                sb.AppendLine($"public async Task<{GetModelNamespace(sqlStoredProcedure.Schema)}.{sqlStoredProcedure.TableName}> {methodName}({GetModelNamespace(sqlStoredProcedure.Schema)}.{sqlStoredProcedure.TableName} {sqlStoredProcedure.TableName.ToCamelCase()})");
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
                                                         .Replace("InsUpd", $"InsUpd{sqlStoredProcedure.TableName}") + "IncludeForeignKeys";
        }

        private string GetInterfaceName(SqlStoredProcedure sqlStoredProcedure)
        {
            return $"I{sqlStoredProcedure.TableName}Service";
        }

        private string GetClassName(SqlStoredProcedure sqlStoredProcedure)
        {
            return $"{sqlStoredProcedure.TableName}Service";
        }

        private string GetModelNamespace(string schema)
        {
            return _config.Namespaces.ModelNamespace.ToSchemaString(schema);
        }

        private string GetDomainNamespace(SqlStoredProcedure sqlStoredProcedure)
        {
            return _config.Namespaces.DomainNamespace.ToSchemaString(sqlStoredProcedure.Schema);
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
