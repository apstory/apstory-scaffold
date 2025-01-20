using Apstory.Scaffold.Model.Sql;
using System.Text.RegularExpressions;

namespace Apstory.Scaffold.Domain.Parser
{
    public static class SqlTableParser
    {
        public static SqlTable Parse(string sql)
        {
            var table = new SqlTable();

            // Match table name and schema
            var tableMatch = Regex.Match(sql, @"CREATE TABLE\s+\[([^\]]+)\]\.\[([^\]]+)\]");
            if (tableMatch.Success)
            {
                table.Schema = tableMatch.Groups[1].Value;
                table.TableName = tableMatch.Groups[2].Value;
            }

            var lines = sql.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            var columnLines = lines.Where(line => line.Trim().EndsWith("NULL", StringComparison.OrdinalIgnoreCase) ||
                                          line.Trim().EndsWith("NULL,", StringComparison.OrdinalIgnoreCase))
                                   .ToList();

            // Define regex to extract column details
            var columnRegex = new Regex(@"\[(\w+)\]\s+(\w+)\s?(\(?\d+\))?.*?(NOT NULL|NULL)", RegexOptions.IgnoreCase);

            // Parse columns
            foreach (var line in columnLines)
            {
                var match = columnRegex.Match(line.Trim());
                var column = new SqlColumn
                {
                    ColumnName = match.Groups[1].Value,
                    DataType = match.Groups[2].Value,
                    DataTypeLength = match.Groups[3].Value.Trim(')', '('),
                    IsNullable = !match.Groups[4].Value.StartsWith("NOT", StringComparison.OrdinalIgnoreCase),
                };

                table.Columns.Add(column);
            }


            //var constraintRegex = new Regex(@"CONSTRAINT\s+\[([^\]]+)\]\s+(PRIMARY KEY|FOREIGN KEY).*?\(\[(\w+)\].*?REFERENCES\s+\[(\w+)\]\.\[(\w+)\]\s+\(\[(\w+)\]\))?");
            var constraintRegex = new Regex(@"CONSTRAINT\s+\[([^\]]+)\]\s+(PRIMARY KEY|FOREIGN KEY).*?\(\[(\w+)\](.*)");
            var foreignConstraintRegex = new Regex(@".*?REFERENCES\s+\[(\w+)\]\.\[(\w+)\]\s+\(\[(\w+)\]\)");
            var constraintMatches = constraintRegex.Matches(sql);
            foreach (Match match in constraintMatches)
            {
                var tableConstraint = new SqlConstraint
                {
                    ConstraintName = match.Groups[1].Value,
                    ConstraintType = match.Groups[2].Value,
                    Column = match.Groups[3].Value
                };

                if (tableConstraint.ConstraintType.Equals("FOREIGN KEY", StringComparison.InvariantCultureIgnoreCase))
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

            return table;
        }
    }
}
