using Apstory.Scaffold.Model.Sql;

namespace Apstory.Scaffold.Domain.Util
{
    public static class SqlScaffoldUtils
    {
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

            if (column.IsNullable || forceNullable)
                if (csharpType != "string" && !csharpType.StartsWith("List<"))
                    return $"{csharpType}?";

            return csharpType;
        }
    }
}
