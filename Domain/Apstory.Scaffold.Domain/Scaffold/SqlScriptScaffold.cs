using Apstory.Scaffold.Domain.Service;
using Apstory.Scaffold.Domain.Util;
using Apstory.Scaffold.Model.Config;
using Apstory.Scaffold.Model.Sql;
using System.Text.RegularExpressions;

namespace Apstory.Scaffold.Domain.Scaffold
{
    public partial class SqlScriptFileScaffold
    {
        private readonly CSharpConfig _config;
        private readonly LockingService _lockingService;

        public SqlScriptFileScaffold(CSharpConfig csharpConfig, LockingService lockingService)
        {
            _config = csharpConfig;
            _lockingService = lockingService;
        }

        public async Task GenerateCode(SqlTable sqlTable)
        {
            string lockName = $"{sqlTable.Schema}.{sqlTable.TableName}";

            await WriteScriptToDisk(sqlTable, GenerateInsertUpdateProcedure(sqlTable));
            await WriteScriptToDisk(sqlTable, GenerateDelHrdProcedure(sqlTable));
            await WriteScriptToDisk(sqlTable, GenerateDelSftProcedure(sqlTable));
            await WriteScriptToDisk(sqlTable, GenerateGetByIdProcedure(sqlTable));
            await WriteScriptToDisk(sqlTable, GenerateGetByPrimaryKeyIdsProcedure(sqlTable));

            if (sqlTable.Constraints.Any(s => s.ConstraintType == Model.Enum.ConstraintType.ForeignKey))
            {
                await WriteScriptToDisk(sqlTable, GenerateGetByForeignKeyIdsProcedure(sqlTable));
                await WriteScriptToDisk(sqlTable, GenerateGetByForeignKeyIdsPagingProcedure(sqlTable));
            }

            if (sqlTable.Indexes.Any())
            {
                foreach (var script in GenerateGetByIndexedColumnProcedures(sqlTable))
                {
                    await WriteScriptToDisk(sqlTable, script);
                }
            }
        }

        public void DeleteCode(SqlTable sqlTable)
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

        private async Task WriteScriptToDisk(SqlTable sqlTable, string script)
        {
            var directory = Path.Combine(_config.Directories.DBDirectory, sqlTable.Schema, "Stored Procedures");
            var fileNameRx = Regex.Match(script, @"CREATE\s+PROCEDURE\s+\[?(\w+)\]?\.?\[?(\w+)\]?");
            var schema = fileNameRx.Groups[1].Value;
            var procName = fileNameRx.Groups[2].Value + ".sql";
            var filePath = Path.Join(directory, procName);
            var existingFileContent = string.Empty;

            try
            {
                await _lockingService.AcquireLockAsync(filePath);

                if (File.Exists(filePath))
                    existingFileContent = FileUtils.SafeReadAllText(filePath);

                if (!existingFileContent.Equals(script))
                {
                    FileUtils.WriteTextAndDirectory(filePath, script);
                    Logger.LogSuccess($"[Created SQL Stored Procedure] {filePath}");
                }
                else
                {
#if DEBUGFORCESCAFFOLD
                    FileUtils.WriteTextAndDirectory(filePath, script);
                    Logger.LogSuccess($"[Force Created SQL Stored Procedure] {filePath}");
#else
                Logger.LogSkipped($"[Skipped SQL Stored Procedure] {filePath}");
#endif
                }

            }
            catch (Exception ex)
            {
                Logger.LogError($"[Service] {ex.Message}");
            }
            finally
            {
                _lockingService.ReleaseLock(filePath);
            }
        }
    }
}
