using Apstory.Scaffold.Domain.Service;
using Apstory.Scaffold.Domain.Util;
using Apstory.Scaffold.Model;
using Apstory.Scaffold.Model.Config;
using Apstory.Scaffold.Model.Enum;
using Apstory.Scaffold.Model.Hardcoded;
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

        public async Task<List<ScaffoldFileResult>> GenerateCode(SqlTable sqlTable, string variant)
        {
            string lockName = $"{sqlTable.Schema}.{sqlTable.TableName}";
            bool generateMergeVariant = Variant.Merge.Equals(variant, StringComparison.OrdinalIgnoreCase);

            List<ScaffoldFileResult> results = new List<ScaffoldFileResult>();
            results.Add(await WriteScriptToDisk(sqlTable, GenerateInsertUpdateProcedure(sqlTable, generateMergeVariant)));
            results.Add(await WriteScriptToDisk(sqlTable, GenerateDelHrdProcedure(sqlTable)));
            results.Add(await WriteScriptToDisk(sqlTable, GenerateDelSftProcedure(sqlTable)));
            results.Add(await WriteScriptToDisk(sqlTable, GenerateGetByIdProcedure(sqlTable)));
            results.Add(await WriteScriptToDisk(sqlTable, GenerateGetByPrimaryKeyIdsProcedure(sqlTable)));

            if (sqlTable.Constraints.Any(s => s.ConstraintType == Model.Enum.ConstraintType.ForeignKey))
            {
                results.Add(await WriteScriptToDisk(sqlTable, GenerateGetByForeignKeyIdsProcedure(sqlTable)));
                results.Add(await WriteScriptToDisk(sqlTable, GenerateGetByForeignKeyIdsPagingProcedure(sqlTable)));
            }

            if (sqlTable.Indexes.Any())
            {
                foreach (var script in GenerateGetByIndexedColumnProcedures(sqlTable))
                {
                    results.Add(await WriteScriptToDisk(sqlTable, script));
                }
            }

            var typesDirectory = Path.Combine(_config.Directories.DBDirectory, sqlTable.Schema, "User Defined Types");
            var udttIntsPath = Path.Combine(typesDirectory, "udtt_Ints.sql");
            if (!File.Exists(udttIntsPath))
            {
                FileUtils.WriteTextAndDirectory(udttIntsPath, "CREATE TYPE [dbo].[udtt_Ints] AS TABLE ([Id] INT NULL);");
                results.Add(new ScaffoldFileResult(ScaffoldResult.Created, udttIntsPath));

            }

            var udttUniqueIdentifiersPath = Path.Combine(typesDirectory, "udtt_Uniqueidentifiers.sql");
            if (!File.Exists(udttUniqueIdentifiersPath))
            {
                FileUtils.WriteTextAndDirectory(udttUniqueIdentifiersPath, "CREATE TYPE [dbo].[udtt_Uniqueidentifiers] AS TABLE ([Id] UNIQUEIDENTIFIER NULL);");
                results.Add(new ScaffoldFileResult(ScaffoldResult.Created, udttUniqueIdentifiersPath));
            }

            return results;
        }

        public Task<ScaffoldResult> DeleteCode(SqlTable sqlTable)
        {
            var didSkip = false;
            var directory = Path.Combine(_config.Directories.DBDirectory, sqlTable.Schema, "Stored Procedures");
            var procNamePattern = $"zgen_{sqlTable.TableName}_*.sql";

            var filesToDelete = Directory.GetFiles(directory, procNamePattern);
            for (int i = 0; i < filesToDelete.Length; i++)
            {
                Logger.LogSuccess($"[Deleted SQL Stored Procedure] {filesToDelete[i]}");

                if (File.Exists(filesToDelete[i]))
                    File.Delete(filesToDelete[i]);
                else
                    didSkip = true;
            }

            if (didSkip)
                return Task.FromResult(ScaffoldResult.Skipped);

            return Task.FromResult(ScaffoldResult.Deleted);
        }

        private async Task<ScaffoldFileResult> WriteScriptToDisk(SqlTable sqlTable, string script)
        {
            var scaffoldingResult = ScaffoldResult.Updated;
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
                else
                    scaffoldingResult = ScaffoldResult.Created;

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
                    scaffoldingResult = ScaffoldResult.Skipped;
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

            return new ScaffoldFileResult(scaffoldingResult, filePath);
        }
    }
}
