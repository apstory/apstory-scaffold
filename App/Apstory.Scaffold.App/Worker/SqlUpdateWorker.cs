using Apstory.Scaffold.Domain.Util;
using Apstory.Scaffold.Model.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Diagnostics;
using Apstory.Scaffold.Domain.Parser;
using Apstory.Scaffold.Domain.Service;

namespace Apstory.Scaffold.App.Worker
{
    public class SqlUpdateWorker : BackgroundService
    {
        private readonly IHostApplicationLifetime _lifetime;
        private readonly IConfiguration _configuration;
        private readonly CSharpConfig _csharpConfig;
        private readonly SqlTableUpdateService _tableUpdateService;

        public SqlUpdateWorker(IHostApplicationLifetime lifetime,
                               IConfiguration configuration,
                               CSharpConfig csharpConfig,
                               SqlTableUpdateService tableUpdateService)
        {
            _lifetime = lifetime;
            _configuration = configuration;
            _csharpConfig = csharpConfig;
            _tableUpdateService = tableUpdateService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var connectionString = _configuration["sqlDestination"];
            var args = _configuration["sqlPush"];
            var blockOnDataLossStr = _configuration["blockOnDataLoss"];
            
            // Default to true (block on data loss by default)
            bool blockOnDataLoss = true;
            if (!string.IsNullOrEmpty(blockOnDataLossStr) && !bool.TryParse(blockOnDataLossStr, out blockOnDataLoss))
            {
                Logger.LogWarn($"Invalid value for -blockondataloss: '{blockOnDataLossStr}'. Using default: true");
                blockOnDataLoss = true;
            }

            Logger.LogInfo($"Block on Data Loss: {blockOnDataLoss}");

            if (connectionString is null)
            {
                Logger.LogError($"-sqlDestination requires a database connection string");
                _lifetime.StopApplication();
                return;
            }

            if (args is null)
            {
                Logger.LogInfo($"No -sqlPush parameter, checking git status");
                var gitLogs = await ExecuteGitStatus();

                var validSqlEntries = gitLogs.Where(s => !string.IsNullOrEmpty(s) && s.Trim().EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
                                             .Select(s => s.Replace("\tmodified:", string.Empty).Replace("\tnew file:", string.Empty).Trim()).ToList();

                if (!validSqlEntries.Any())
                {
                    Logger.LogError($"No modified sql files found");
                    _lifetime.StopApplication();
                    return;
                }

                Logger.LogInfo($"Found {validSqlEntries.Count()} modified sql files");
                List<string> entityArgs = new List<string>();

                var tablesFirstEntries = validSqlEntries.OrderByDescending(s => s.Contains("/Tables/") || s.Contains("\\Tables\\")).ToList();
                foreach (var sqlEntry in tablesFirstEntries)
                {
                    var schema = sqlEntry.GetSchemaFromPath();
                    var fileName = Path.GetFileName(sqlEntry).Replace(".sql", string.Empty);
                    entityArgs.Add($"{schema}.{fileName}");
                }

                args = string.Join(";", entityArgs);
            }

            var entities = args.Split(";");
            foreach (var regenEntry in entities)
            {
                var argSplit = regenEntry.Split(".");
                var schema = "dbo";
                var entityName = string.Empty;

                if (argSplit.Length > 1)
                {
                    schema = argSplit[0];
                    entityName = argSplit[1];
                }
                else
                {
                    schema = "dbo";
                    entityName = argSplit[0];
                }

                var tablePath = Path.Combine(_csharpConfig.Directories.DBDirectory, schema, "Tables", $"{entityName}.sql");
                if (File.Exists(tablePath))
                {
                    Logger.LogInfo($"Push Table {schema}.{entityName}");
                    await PushTableChanges(connectionString, tablePath, blockOnDataLoss);
                }
                else
                {
                    var storedProcPath = Path.Combine(_csharpConfig.Directories.DBDirectory, schema, "Stored Procedures", $"{entityName}.sql");
                    if (File.Exists(storedProcPath))
                    {
                        Logger.LogInfo($"Push Stored Procedure {schema}.{entityName}");
                        await PushStoredProcedureChanges(connectionString, storedProcPath);
                    }
                    else
                    {
                        var userDefinedTypePath = Path.Combine(_csharpConfig.Directories.DBDirectory, schema, "User Defined Types", $"{entityName}.sql");
                        if (File.Exists(userDefinedTypePath))
                        {
                            Logger.LogInfo($"Push User Type {schema}.{entityName}");
                            await PushUserDefinedType(connectionString, userDefinedTypePath, schema, entityName);
                        }
                        else
                        {
                            string[] files = Directory.GetFiles(_csharpConfig.Directories.DbupDirectory, $"{entityName}.sql", SearchOption.AllDirectories);
                            if (files.Length > 1)
                            {
                                Logger.LogInfo($"Multiple DBUP Scripts found, aborting [{entityName}.sql]");
                                continue;
                            }

                            if (files.Length == 0)
                            {
                                Logger.LogInfo($"No DBUP Scripts found, aborting [{entityName}.sql]");
                                continue;
                            }

                            Logger.LogInfo($"Push DBUP Script: {entityName}.sql");
                            var dbupScript = FileUtils.SafeReadAllText(files[0]);
                            await ExecuteSql(connectionString, dbupScript);
                        }
                    }
                }
            }

            _lifetime.StopApplication();
        }

        private async Task PushUserDefinedType(string connectionString, string userDefinedTypePath, string schema, string typeName)
        {
            try
            {
                var userDefinedTypeScript = FileUtils.SafeReadAllText(userDefinedTypePath);
                bool exists = await ExecuteCheckIfUserDefinedTypeExists(connectionString, schema, typeName);
                if (!exists)
                    await ExecuteSql(connectionString, userDefinedTypeScript);
                else
                    Logger.LogWarn($"Type {schema}.{typeName} already exists, skipping.");

            }
            catch (Exception ex)
            {
                Logger.LogError($"Error pushing user defined type: {ex.Message}");
            }
        }

        private async Task PushTableChanges(string connectionString, string tablePath, bool blockOnDataLoss)
        {
            try
            {
                var createTableScript = FileUtils.SafeReadAllText(tablePath);
                var tableInfo = SqlTableParser.Parse(tablePath, createTableScript);

                bool tableExists = await ExecuteCheckIfTableExists(connectionString, tableInfo.Schema, tableInfo.TableName);

                if (!tableExists)
                {
                    Logger.LogInfo($"Creating Table {tableInfo.Schema}.{tableInfo.TableName}");
                    await ExecuteSql(connectionString, createTableScript);
                }
                else
                {
                    Logger.LogInfo($"Table {tableInfo.Schema}.{tableInfo.TableName} already exists, checking for updates...");
                    
                    // Use the new service to generate update script
                    var updateResult = await _tableUpdateService.GenerateTableUpdateScript(connectionString, tableInfo, blockOnDataLoss);
                    
                    if (!updateResult.Success)
                    {
                        if (updateResult.HasDataLoss && blockOnDataLoss)
                        {
                            Logger.LogError($"Table update blocked due to potential data loss:");
                            foreach (var reason in updateResult.DataLossReasons)
                            {
                                Logger.LogError($"  - {reason}");
                            }
                            Logger.LogError($"To proceed with these changes, run with -blockondataloss false");
                        }
                        else if (!string.IsNullOrEmpty(updateResult.ErrorMessage))
                        {
                            Logger.LogError($"Error generating table update: {updateResult.ErrorMessage}");
                        }
                        return;
                    }
                    
                    if (updateResult.SqlStatements.Count == 0)
                    {
                        Logger.LogInfo($"No changes detected for table {tableInfo.Schema}.{tableInfo.TableName}");
                        return;
                    }
                    
                    // Display warnings if there's data loss but we're proceeding
                    if (updateResult.HasDataLoss && !blockOnDataLoss)
                    {
                        Logger.LogWarn($"WARNING: Proceeding with changes that may cause data loss:");
                        foreach (var reason in updateResult.DataLossReasons)
                        {
                            Logger.LogWarn($"  - {reason}");
                        }
                    }
                    
                    // Execute the update statements
                    Logger.LogInfo($"Applying {updateResult.SqlStatements.Count} change(s) to table {tableInfo.Schema}.{tableInfo.TableName}:");
                    foreach (var sqlStatement in updateResult.SqlStatements)
                    {
                        Logger.LogDebug($"  Executing: {sqlStatement}");
                        await ExecuteSql(connectionString, sqlStatement);
                    }
                    
                    Logger.LogInfo($"Table {tableInfo.Schema}.{tableInfo.TableName} updated successfully.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error pushing table: {ex.Message}");
            }
        }

        private async Task PushStoredProcedureChanges(string connectionString, string storedProcedurePath)
        {
            var storedProcedureText = FileUtils.SafeReadAllText(storedProcedurePath);
            var sqlStatement = Regex.Replace(storedProcedureText, "CREATE\\W+PROCEDURE", "CREATE OR ALTER PROCEDURE", RegexOptions.IgnoreCase);

            await ExecuteSql(connectionString, sqlStatement);
        }

        private async Task ExecuteSql(string connectionString, string sql)
        {
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    await connection.ExecuteAsync(sql);
                }

                Logger.LogInfo("Success.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error pushing sql: {ex.Message}\r\n{sql}");
            }
        }

        private async Task<bool> ExecuteCheckIfUserDefinedTypeExists(string connectionString, string schemaName, string typeName)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                string query = @"SELECT COUNT(*) FROM sys.types  WHERE schema_id = SCHEMA_ID(@SchemaName) AND name = @TypeName";
                int typeCount = await connection.ExecuteScalarAsync<int>(query, new { SchemaName = schemaName, TypeName = typeName });

                return typeCount > 0;  // If count is greater than 0, UDT exists
            }
        }


        private async Task<bool> ExecuteCheckIfTableExists(string connectionString, string schemaName, string tableName)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                string query = @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = @SchemaName AND TABLE_NAME = @TableName";
                int tableCount = connection.ExecuteScalar<int>(query, new { SchemaName = schemaName, TableName = tableName });

                return tableCount > 0;  // If count is greater than 0, table exists
            }
        }


        private async Task<List<string>> ExecuteGitStatus()
        {
            string arguments = $"status";
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = _csharpConfig.Directories.SolutionDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            List<string> logs = new List<string>();
            using (Process process = new Process { StartInfo = psi })
            {
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        logs.Add(e.Data);
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        logs.Add($"Error: {e.Data}");
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();
            }

            return logs;
        }
    }
}
