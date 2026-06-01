using Apstory.Scaffold.Domain.Util;
using Apstory.Scaffold.Model.Sql;
using System.Text.RegularExpressions;

namespace Apstory.Scaffold.Domain.Parser
{
    public class SqlProcedureParser
    {
        public static SqlStoredProcedure Parse(string sqlProcScript, string? storedProcedurePath = null)
        {
            SqlStoredProcedure sqlStoredProcedure = new SqlStoredProcedure();

            sqlProcScript = sqlProcScript.Trim();

            // Extract custom return type from annotation comment
            var returnTypeRx = Regex.Match(sqlProcScript, @".*@ReturnType:\s*(\w+(?:\.\w+)?)", RegexOptions.IgnoreCase);

            if (returnTypeRx.Success)
            {
                sqlStoredProcedure.CustomReturnType = returnTypeRx.Groups[1].Value.Trim();
            }

            var paramsPart = sqlProcScript.Substring(0, sqlProcScript.ToUpper().IndexOf("BEGIN") + 5);

            var fileNameRx = Regex.Match(paramsPart, @"CREATE\s+PROCEDURE\s+\[?(\w+)\]?\.?\[?(\w+)\]?.*?\(?(.*)\)?.*?AS.*?BEGIN", RegexOptions.Singleline);
            sqlStoredProcedure.Schema = fileNameRx.Groups[1].Value;
            sqlStoredProcedure.StoredProcedureName = fileNameRx.Groups[2].Value;
            sqlStoredProcedure.TableName = fileNameRx.Groups[2].Value.Replace("zgen_", "").Split("_")[0].ToPascalCase();
            var parameters = Regex.Split(fileNameRx.Groups[3].Value.Trim().Trim('(', ')'), @",(?![^(]*\))"); //Split commas that are NOT inside parentheses

            sqlStoredProcedure.Parameters = new List<SqlColumn>();
            for (var i = 0; i < parameters.Length; i++)
            {
                var paramLine = parameters[i].Trim('\r', '\n', ' ', '@');

                if (string.IsNullOrWhiteSpace(paramLine))
                    continue;

                var isNullable = paramLine.Contains('=');
                var parsedParameter = ParseParameter(paramLine, sqlStoredProcedure.Schema, storedProcedurePath);

                string defaultValue = string.Empty;
                if (parsedParameter.ColumnName.Equals("SortDirection", StringComparison.OrdinalIgnoreCase))
                    defaultValue = "ASC";

                parsedParameter.IsNullable = isNullable;
                parsedParameter.DefaultValue = defaultValue;

                sqlStoredProcedure.Parameters.Add(parsedParameter);
            }

            return sqlStoredProcedure;
        }

        private static SqlColumn ParseParameter(string paramLine, string fallbackSchema, string? storedProcedurePath)
        {
            var firstWhitespace = paramLine.IndexOf(' ');
            if (firstWhitespace < 0)
                throw new Exception($"Unable to parse stored procedure parameter: {paramLine}");

            var parameterName = paramLine[..firstWhitespace].Trim();
            var parameterDefinition = paramLine[(firstWhitespace + 1)..].Trim();
            var defaultAssignmentIndex = parameterDefinition.IndexOf('=');
            var definitionWithoutDefault = defaultAssignmentIndex >= 0
                ? parameterDefinition[..defaultAssignmentIndex].Trim()
                : parameterDefinition;
            var isReadonly = Regex.IsMatch(definitionWithoutDefault, @"\bREADONLY\b", RegexOptions.IgnoreCase);

            definitionWithoutDefault = Regex.Replace(definitionWithoutDefault, @"\bREADONLY\b", string.Empty, RegexOptions.IgnoreCase).Trim();
            definitionWithoutDefault = Regex.Replace(definitionWithoutDefault, @"\bOUTPUT\b", string.Empty, RegexOptions.IgnoreCase).Trim();

            var typeMatch = Regex.Match(definitionWithoutDefault,
                @"^(?:(?:\[(?<schemaBracketed>[^\]]+)\]|(?<schema>\w+))\s*\.\s*)?(?:\[(?<typeBracketed>[^\]]+)\]|(?<type>\w+))(?:\s*\((?<length>[^)]*)\))?$",
                RegexOptions.IgnoreCase);

            if (!typeMatch.Success)
                throw new Exception($"Unable to parse stored procedure parameter type: {paramLine}");

            var dataType = GetFirstValue(typeMatch, "typeBracketed", "type");
            var dataTypeSchema = GetFirstValue(typeMatch, "schemaBracketed", "schema");
            if (string.IsNullOrWhiteSpace(dataTypeSchema))
                dataTypeSchema = fallbackSchema;

            var parameter = new SqlColumn()
            {
                ColumnName = parameterName,
                DataType = dataType,
                DataTypeSchema = dataTypeSchema,
                DataTypeLength = typeMatch.Groups["length"].Value,
                IsReadonly = isReadonly,
            };

            if (dataType.StartsWith("udtt_", StringComparison.OrdinalIgnoreCase))
                parameter.UserDefinedTypeColumns = ParseUserDefinedTypeColumns(storedProcedurePath, dataTypeSchema, dataType);

            return parameter;
        }

        private static string GetFirstValue(Match match, params string[] groupNames)
        {
            foreach (var groupName in groupNames)
            {
                var value = match.Groups[groupName].Value;
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }

            return string.Empty;
        }

        private static List<SqlColumn> ParseUserDefinedTypeColumns(string? storedProcedurePath, string schema, string dataType)
        {
            if (string.IsNullOrWhiteSpace(storedProcedurePath) || !File.Exists(storedProcedurePath))
                return new List<SqlColumn>();

            var storedProcedureDirectory = Path.GetDirectoryName(storedProcedurePath);
            var procedureSchemaDirectory = storedProcedureDirectory is null ? null : Directory.GetParent(storedProcedureDirectory)?.FullName;
            var dbRootDirectory = procedureSchemaDirectory is null ? null : Directory.GetParent(procedureSchemaDirectory)?.FullName;

            if (string.IsNullOrWhiteSpace(dbRootDirectory))
                return new List<SqlColumn>();

            var userDefinedTypePath = Path.Combine(dbRootDirectory, schema, "User Defined Types", $"{dataType}.sql");
            if (!File.Exists(userDefinedTypePath))
                return new List<SqlColumn>();

            var userDefinedTypeScript = File.ReadAllText(userDefinedTypePath);
            var userDefinedTypeBody = Regex.Match(userDefinedTypeScript,
                @"AS\s+TABLE\s*\((?<body>.*)\)\s*;?\s*$",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (!userDefinedTypeBody.Success)
                return new List<SqlColumn>();

            var columns = new List<SqlColumn>();
            var columnDefinitions = Regex.Split(userDefinedTypeBody.Groups["body"].Value.Trim(), @",(?![^()]*\))");

            foreach (var columnDefinition in columnDefinitions)
            {
                var trimmedDefinition = columnDefinition.Trim();
                if (string.IsNullOrWhiteSpace(trimmedDefinition))
                    continue;

                var columnMatch = Regex.Match(trimmedDefinition,
                    @"^\[?(?<name>\w+)\]?\s+(?<type>\w+)(?:\s*\((?<length>[^)]*)\))?",
                    RegexOptions.IgnoreCase);

                if (!columnMatch.Success)
                    continue;

                columns.Add(new SqlColumn()
                {
                    ColumnName = columnMatch.Groups["name"].Value,
                    DataType = columnMatch.Groups["type"].Value,
                    DataTypeLength = columnMatch.Groups["length"].Value,
                    DataTypeSchema = schema,
                });
            }

            return columns;
        }
    }
}
