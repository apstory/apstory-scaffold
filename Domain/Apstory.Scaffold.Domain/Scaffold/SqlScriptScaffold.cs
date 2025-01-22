using Apstory.Scaffold.Domain.Util;
using Apstory.Scaffold.Model.Config;
using Apstory.Scaffold.Model.Sql;
using System.Text.RegularExpressions;

namespace Apstory.Scaffold.Domain.Scaffold
{
    public partial class SqlScriptFileScaffold
    {
        private readonly CSharpConfig _config;

        public SqlScriptFileScaffold(CSharpConfig csharpConfig)
        {
            _config = csharpConfig;
        }

        public void GenerateCode(SqlTable sqlTable)
        {
            WriteScriptToDisk(sqlTable, GenerateInsertUpdateProcedure(sqlTable));
            WriteScriptToDisk(sqlTable, GenerateDelHrdProcedure(sqlTable));
            WriteScriptToDisk(sqlTable, GenerateGetByIdProcedure(sqlTable));
            WriteScriptToDisk(sqlTable, GenerateGetByPrimaryKeyIdsProcedure(sqlTable));

            if (sqlTable.Constraints.Any(s => s.ConstraintType == Model.Enum.ConstraintType.ForeignKey))
            {
                WriteScriptToDisk(sqlTable, GenerateGetByForeignKeyIdsProcedure(sqlTable));
                WriteScriptToDisk(sqlTable, GenerateGetByForeignKeyIdsPagingProcedure(sqlTable));
            }

            if (sqlTable.Indexes.Any())
            {
                foreach (var script in GenerateGetByIndexedColumnProcedures(sqlTable))
                {
                    WriteScriptToDisk(sqlTable, script);
                }
            }
        }

        public async Task DeleteCode(SqlTable sqlTable)
        {
            var directory = Path.Combine(_config.Directories.DBDirectory, sqlTable.Schema, "Stored Procedures");
            var procNamePattern = $"zgen_{sqlTable.TableName}_*.sql";

            var filesToDelete = Directory.GetFiles(directory, procNamePattern);
            for (int i = 0; i < filesToDelete.Length; i++)
            {
                Logger.LogSuccess($"[Deleted SQL Stored Procedure] {filesToDelete[i]}");
                File.Delete(filesToDelete[i]);
            }
        }

        private void WriteScriptToDisk(SqlTable sqlTable, string script)
        {
            var directory = Path.Combine(_config.Directories.DBDirectory, sqlTable.Schema, "Stored Procedures");
            var fileNameRx = Regex.Match(script, @"CREATE\s+PROCEDURE\s+\[?(\w+)\]?\.?\[?(\w+)\]?");
            var schema = fileNameRx.Groups[1].Value;
            var procName = fileNameRx.Groups[2].Value + ".sql";
            var filePath = Path.Join(directory, procName);
            var existingFileContent = string.Empty;

            if (File.Exists(filePath))
                existingFileContent = FileUtils.SafeReadAllText(filePath);

            if (!existingFileContent.Equals(script))
            {
                File.WriteAllText(filePath, script);
                Logger.LogSuccess($"[Created SQL Stored Procedure] {filePath}");
            }
            else
            {
#if DEBUGFORCESCAFFOLD
                File.WriteAllText(filePath, script);
                Logger.LogSuccess($"[Force Created SQL Stored Procedure] {filePath}");
#else
                Logger.LogSkipped($"[Skipped SQL Stored Procedure] {filePath}");
#endif
            }
        }
    }
}
