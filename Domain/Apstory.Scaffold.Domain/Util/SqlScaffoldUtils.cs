using Apstory.Scaffold.Model.Sql;

namespace Apstory.Scaffold.Domain.Util
{
    public static class SqlScaffoldUtils
    {
        public static string GetMethodName(this SqlStoredProcedure sqlStoredProcedure)
        {
            return sqlStoredProcedure.StoredProcedureName.Replace("zgen_", "")
                                                         .Replace($"{sqlStoredProcedure.TableName}_", "", StringComparison.OrdinalIgnoreCase)
                                                         .Replace("GetBy", $"Get{sqlStoredProcedure.TableName.ToPascalCase()}By")
                                                         .Replace("InsUpd", $"InsUpd{sqlStoredProcedure.TableName.ToPascalCase()}")
                                                         .Replace("DelHrd", $"Del{sqlStoredProcedure.TableName.ToPascalCase()}Hrd")
                                                         .Replace("DelSft", $"Del{sqlStoredProcedure.TableName.ToPascalCase()}Sft")
                                                         .ToPascalCase();

        }

        public static string ToSchemaString(this string declaration, string schema)
        {
            if (schema.Equals("dbo", StringComparison.OrdinalIgnoreCase))
                return declaration.Replace(".#SCHEMA#", string.Empty)
                                  .Replace($"/#SCHEMA#", string.Empty)
                                  .Replace($"\\#SCHEMA#", string.Empty);

            return declaration.Replace("#SCHEMA#", schema.ToUpper());
        }

        public static string ToDapperTypeString(this string sqlType)
        {
            return sqlType.ToLower() switch
            {
                "tinyint" => ", DbType.TinyInt",
                "int" => ", DbType.Int32",
                "bit" => ", DbType.Boolean",
                "varchar" => ", DbType.String",
                "nvarchar" => ", DbType.String",
                "datetime" => ", DbType.DateTime",
                "date" => ", DbType.Date",
                _ => throw new Exception($"ToDapperTypeString lookup exception: {sqlType}")
            };
        }

        public static string ToCSharpTypeString(this SqlColumn column, bool forceNullable, string? modelNamespace = null)
        {
            string csharpType = column.DataType.StartsWith("udtt_", StringComparison.OrdinalIgnoreCase)
                ? column.ToUserDefinedTableTypeString(modelNamespace)
                : ToScalarCSharpTypeString(column.DataType);

            var hasDefaultValue = !string.IsNullOrWhiteSpace(column.DefaultValue);
            if (column.IsNullable || hasDefaultValue || forceNullable)
                if (csharpType != "string" && !csharpType.StartsWith("List<"))
                    return $"{csharpType}?";

            return csharpType;
        }

        public static bool IsScalarUserDefinedTableType(this SqlColumn column)
        {
            if (!column.DataType.StartsWith("udtt_", StringComparison.OrdinalIgnoreCase))
                return false;

            if (column.DataType.Equals("udtt_ints", StringComparison.OrdinalIgnoreCase) ||
                column.DataType.Equals("udtt_tinyints", StringComparison.OrdinalIgnoreCase) ||
                column.DataType.Equals("udtt_uniqueidentifiers", StringComparison.OrdinalIgnoreCase))
                return true;

            return column.UserDefinedTypeColumns.Count == 1
                && column.UserDefinedTypeColumns[0].ColumnName.Equals("Id", StringComparison.OrdinalIgnoreCase)
                && CanMapScalarSqlType(column.UserDefinedTypeColumns[0].DataType);
        }

        public static string ToTableValuedParameterTypeName(this SqlColumn column, string fallbackSchema)
        {
            var schema = string.IsNullOrWhiteSpace(column.DataTypeSchema)
                ? fallbackSchema
                : column.DataTypeSchema;

            return $"{schema}.{column.DataType}";
        }

        public static string GetSchemaFromPath(this string path)
        {
            string directory = Path.GetDirectoryName(path);

            if (directory == null)
                throw new ArgumentException("Invalid path provided.");

            // Get the parent directory (schema folder)
            string schema = Directory.GetParent(directory)?.Name;

            if (string.IsNullOrEmpty(schema))
                throw new InvalidOperationException("Schema folder not found in the provided path.");

            return schema;
        }

        public static string ToCSharpSafeKeyword(this string tableName)
        {
            if (tableName.ToCamelCase() == "event") //We cant use c# keywords like event
                return "evt";

            return tableName.ToCamelCase();
        }

        public static string GetReturnTypeName(this SqlStoredProcedure sqlStoredProcedure)
        {
            return string.IsNullOrWhiteSpace(sqlStoredProcedure.CustomReturnType)
                ? sqlStoredProcedure.TableName
                : sqlStoredProcedure.CustomReturnType;
        }

        public static bool HasCustomReturnType(this SqlStoredProcedure sqlStoredProcedure)
        {
            return !string.IsNullOrWhiteSpace(sqlStoredProcedure.CustomReturnType);
        }

        private static string ToUserDefinedTableTypeString(this SqlColumn column, string? modelNamespace)
        {
            if (column.IsScalarUserDefinedTableType())
            {
                return column.DataType.ToLowerInvariant() switch
                {
                    "udtt_ints" => "List<int>",
                    "udtt_tinyints" => "List<byte>",
                    "udtt_uniqueidentifiers" => "List<Guid>",
                    _ => $"List<{ToScalarCSharpTypeString(column.UserDefinedTypeColumns[0].DataType)}>"
                };
            }

            var entryTypeName = column.DataType["udtt_".Length..].ToPascalCase();
            return string.IsNullOrWhiteSpace(modelNamespace)
                ? $"List<{entryTypeName}>"
                : $"List<{modelNamespace}.{entryTypeName}>";
        }

        private static bool CanMapScalarSqlType(string sqlType)
        {
            try
            {
                _ = ToScalarCSharpTypeString(sqlType);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string ToScalarCSharpTypeString(string sqlType)
        {
            return sqlType.ToLower() switch
            {
                "int" => "int",
                "bit" => "bool",
                "varchar" => "string",
                "nvarchar" => "string",
                "datetime" => "DateTime",
                "date" => "DateTime",
                "float" => "double",
                "decimal" => "decimal",
                "bigint" => "long",
                "smallint" => "short",
                "tinyint" => "byte",
                "uniqueidentifier" => "Guid",
                "geography" => "Microsoft.SqlServer.Types.SqlGeography",
                "time" => "TimeSpan",
                "rowversion" => "byte[]",
                "char" => "string",
                _ => throw new Exception($"ToCSharpTypeString lookup exception: {sqlType}")
            };
        }
    }
}
