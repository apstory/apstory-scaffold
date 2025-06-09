using Apstory.Scaffold.Domain.Util;
using Apstory.Scaffold.Model.Enum;
using Apstory.Scaffold.Model.Sql;
using System.Text.RegularExpressions;

namespace Apstory.Scaffold.Domain.Parser
{
    public static class SqlTableParser
    {
        public static SqlTable Parse(string sql)
        {
            var table = new SqlTable();

            var procedureMatch = Regex.Match(sql, @"CREATE\s+PROCEDURE\s+\[([^\]]+)\]\.\[([^\]]+)\]");
            if (procedureMatch.Success)
                throw new Exception($"Procedure found inside table folder");

            // Match table name and schema
            var tableMatch = Regex.Match(sql, @"CREATE TABLE\s+\[([^\]]+)\]\.\[([^\]]+)\]");
            if (tableMatch.Success)
            {
                table.Schema = tableMatch.Groups[1].Value;
                table.TableName = tableMatch.Groups[2].Value.ToPascalCase();
            }

            //Only take the part that creates the table
            var lowercaseSql = sql.ToLower();
            var firstCreateIdx = lowercaseSql.IndexOf("create ");
            lowercaseSql = lowercaseSql.Substring(firstCreateIdx + 6, lowercaseSql.Length - 6);
            var secondCreateIdx = lowercaseSql.IndexOf("create ");

            string tableCreationSql = sql;
            if (secondCreateIdx > -1)
                tableCreationSql = sql.Substring(0, secondCreateIdx + 6);

            // Regex pattern to match single-line and multi-line comments
            string pattern = @"(--.*?$)|(/\*.*?\*/)";
            string cleanedSql = Regex.Replace(tableCreationSql, pattern, "", RegexOptions.Multiline | RegexOptions.Singleline);

            var lines = cleanedSql.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            var columnLines = lines.Where(line => !line.Trim().Contains(" KEY ", StringComparison.InvariantCultureIgnoreCase) &&
                                                  !line.Trim().Contains(" UNIQUE ", StringComparison.InvariantCultureIgnoreCase) &&
                                                  !line.Trim().Equals("GO", StringComparison.InvariantCultureIgnoreCase))
                                   .Skip(1) //Skip first the Create Table line
                                   .Select(s => s.Trim('(', ')', ';', ','))
                                   .Where(s => !string.IsNullOrWhiteSpace(s));

            // Define regex to extract column details
            var columnRegex = new Regex(@"\[(\w+)\]\s*(\w+)\s?(\(?\d+\))?.*?(DEFAULT\s*\(.*\))?\s*(NOT\s+NULL|NULL)", RegexOptions.IgnoreCase);

            // Parse columns
            foreach (var line in columnLines)
            {
                var match = columnRegex.Match(line.Trim().Replace("[sys].[geography]", "Geography"));
                var column = new SqlColumn
                {
                    ColumnName = match.Groups[1].Value,
                    DataType = match.Groups[2].Value,
                    DataTypeLength = match.Groups[3].Value.Trim(')', '('),
                    DefaultValue = match.Groups[4].Value,
                    IsNullable = !match.Groups[5].Value.StartsWith("NOT", StringComparison.OrdinalIgnoreCase),
                };

                if (!string.IsNullOrEmpty(column.ColumnName))
                    table.Columns.Add(column);
                else
                    Logger.LogWarn($"Empty column detected in '{sql}'");
            }


            //var constraintRegex = new Regex(@"CONSTRAINT\s+\[([^\]]+)\]\s+(PRIMARY KEY|FOREIGN KEY).*?\(\[(\w+)\].*?REFERENCES\s+\[(\w+)\]\.\[(\w+)\]\s+\(\[(\w+)\]\))?");
            var constraintRegex = new Regex(@"CONSTRAINT\s+\[([^\]]+)\]\s+(PRIMARY KEY|FOREIGN KEY).*?\(\[(\w+)\](.*)");
            var foreignConstraintRegex = new Regex(@".*?REFERENCES\s+\[(\w+)\]\.\[(\w+)\]\s+\(\[(\w+)\]\)");
            var constraintMatches = constraintRegex.Matches(cleanedSql);
            foreach (Match match in constraintMatches)
            {
                ConstraintType constraintType;
                var normalizedConstraint = match.Groups[2].Value.Replace(" ", string.Empty);
                if (!Enum.TryParse(normalizedConstraint, true, out constraintType))
                    throw new Exception($"Unknown constraint type: '{normalizedConstraint}'");

                var tableConstraint = new SqlConstraint
                {
                    ConstraintName = match.Groups[1].Value,
                    ConstraintType = constraintType,
                    Column = match.Groups[3].Value
                };

                if (tableConstraint.ConstraintType == Model.Enum.ConstraintType.ForeignKey)
                {
                    var fkDetails = foreignConstraintRegex.Match(match.Groups[4].Value);

                    tableConstraint.RefSchema = fkDetails.Groups[1].Value;
                    tableConstraint.RefTable = fkDetails.Groups[2].Value;
                    tableConstraint.RefColumn = fkDetails.Groups[3].Value;
                }

                table.Constraints.Add(tableConstraint);
            }


            // Match indexes
            var indexMatches = Regex.Matches(sql, @"CREATE\s+(UNIQUE\s+)?(CLUSTERED|NONCLUSTERED)\s+INDEX\s+\[([^\]]+)\]\s+ON\s+\[\w+\]\.\[([^\]]+)\]\(\[([^\]]+)\]");
            foreach (Match indexMatch in indexMatches)
            {
                table.Indexes.Add(new SqlIndex
                {
                    IndexName = indexMatch.Groups[3].Value,
                    IsUnique = indexMatch.Groups[1].Success,
                    IndexType = indexMatch.Groups[2].Value,
                    Column = indexMatch.Groups[5].Value.Trim()
                });
            }

            if (!table.Constraints.Any(s => s.ConstraintType == ConstraintType.PrimaryKey))
            {
                var primaryKeyRegex = new Regex(@"PRIMARY\s+KEY\s+CLUSTERED\s+\(\[(.*)\].*");
                var primaryMatch = primaryKeyRegex.Match(sql);

                var primaryKeyConstraint = new SqlConstraint
                {
                    ConstraintName = "Unnamed",
                    ConstraintType = ConstraintType.PrimaryKey,
                    Column = primaryMatch.Groups[1].Value
                };

                table.Constraints.Add(primaryKeyConstraint);
            }

            return table;
        }
    }
}
