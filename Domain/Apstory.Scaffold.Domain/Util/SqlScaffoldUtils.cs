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

            return declaration.Replace("#SCHEMA#", schema.ToPascalCase());
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
                _ => throw new Exception($"ToDapperTypeString lookup exception: {sqlType}")
            };
        }

        public static string ToCSharpTypeString(this SqlColumn column, bool forceNullable = false)
        {
            string csharpType = column.DataType.ToLower() switch
            {
                "int" => "int",
                "bit" => "bool",
                "varchar" => "string",
                "nvarchar" => "string",
                "datetime" => "DateTime",
                "float" => "double",
                "decimal" => "decimal",
                "bigint" => "long",
                "smallint" => "short",
                "tinyint" => "byte",
                "udtt_ints" => "List<int>",
                "udtt_tinyints" => "List<int>",
                "udtt_uniqueidentifiers" => "List<Guid>",
                "uniqueidentifier" => "Guid",
                _ => throw new Exception($"ToCSharpTypeString lookup exception: {column.DataType}")
            };

            var hasDefaultValue = !string.IsNullOrWhiteSpace(column.DefaultValue);
            if (column.IsNullable || hasDefaultValue || forceNullable)
                if (csharpType != "string" && !csharpType.StartsWith("List<"))
                    return $"{csharpType}?";

            return csharpType;
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
    }
}
