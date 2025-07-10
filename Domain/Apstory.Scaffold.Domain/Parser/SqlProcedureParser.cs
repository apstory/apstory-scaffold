using Apstory.Scaffold.Domain.Util;
using Apstory.Scaffold.Model.Sql;
using System.Text.RegularExpressions;

namespace Apstory.Scaffold.Domain.Parser
{
    public class SqlProcedureParser
    {
        public static SqlStoredProcedure Parse(string sqlProcScript)
        {
            SqlStoredProcedure sqlStoredProcedure = new SqlStoredProcedure();

            var paramsPart = sqlProcScript.Substring(0, sqlProcScript.ToUpper().IndexOf("BEGIN") + 5);

            var fileNameRx = Regex.Match(paramsPart, @"CREATE\s+PROCEDURE\s+\[?(\w+)\]?\.?\[?(\w+)\]?.*?\(?(.*)\)?.*?AS.*?BEGIN", RegexOptions.Singleline);
            sqlStoredProcedure.Schema = fileNameRx.Groups[1].Value;
            sqlStoredProcedure.StoredProcedureName = fileNameRx.Groups[2].Value;
            sqlStoredProcedure.TableName = fileNameRx.Groups[2].Value.Replace("zgen_", "").Split("_")[0].ToPascalCase();
            var parameters = fileNameRx.Groups[3].Value.Trim().Split(",");

            sqlStoredProcedure.Parameters = new List<SqlColumn>();
            for (var i = 0; i < parameters.Length; i++)
            {
                var paramLine = parameters[i].Trim('\r', '\n', ' ', '@');
                var paramRxDef = Regex.Match(paramLine, @"(\w+)\s+(\w+)\s*(\(?\w+\)?)?");
                var lengthOrReadonly = paramRxDef.Groups[3].Value.Trim('(', ')');
                var isNullable = paramLine.Contains('=');

                string defaultValue = string.Empty;
                if (paramRxDef.Groups[1].Value.Equals("SortDirection", StringComparison.OrdinalIgnoreCase))
                    defaultValue = "ASC";

                sqlStoredProcedure.Parameters.Add(new SqlColumn()
                {
                    ColumnName = paramRxDef.Groups[1].Value,
                    DataType = paramRxDef.Groups[2].Value,
                    DataTypeLength = lengthOrReadonly.Equals("READONLY", StringComparison.OrdinalIgnoreCase) ? string.Empty : lengthOrReadonly,
                    IsNullable = isNullable,
                    IsReadonly = lengthOrReadonly.Equals("READONLY", StringComparison.OrdinalIgnoreCase),
                    DefaultValue = defaultValue
                });
            }

            return sqlStoredProcedure;
        }
    }
}
