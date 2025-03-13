using Apstory.Scaffold.Domain.Util;
using Apstory.Scaffold.Model.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Diagnostics;
using Apstory.Scaffold.Domain.Parser;
using static System.Runtime.InteropServices.Marshalling.IIUnknownCacheStrategy;

namespace Apstory.Scaffold.App.Worker
{
    public class SqlUpdateWorker : BackgroundService
    {
        private readonly IHostApplicationLifetime _lifetime;
        private readonly IConfiguration _configuration;
        private readonly CSharpConfig _csharpConfig;

        public SqlUpdateWorker(IHostApplicationLifetime lifetime,
                               IConfiguration configuration,
                               CSharpConfig csharpConfig)
        {
            _lifetime = lifetime;
            _configuration = configuration;
            _csharpConfig = csharpConfig;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var connectionString = _configuration["sqlDestination"];
            var args = _configuration["sqlPush"];

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
                    await PushTableChanges(connectionString, tablePath);
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
                            Logger.LogError($"Could not find a table, stored procedure or type {schema}.{entityName}");
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

        private async Task PushTableChanges(string connectionString, string tablePath)
        {
            //In SQL Server, you cannot directly specify the column position when adding a column to an existing table 
            //The workaround is to:
            //1. Create a new table
            //2. Copy data across to new table
            //3. Rename old table to _backup
            //4. Rename new table to old table
            //5. Drop old table
            //6. Be careful of foreign keys, they sneaky...

            try
            {
                //TLDR: this is too much work, we will simply try create the table if it does not exist:
                var createTableScript = FileUtils.SafeReadAllText(tablePath);
                var tableInfo = SqlTableParser.Parse(createTableScript);

                bool tableExists = await ExecuteCheckIfTableExists(connectionString, tableInfo.Schema, tableInfo.TableName);

                if (!tableExists)
                {
                    Logger.LogInfo($"Creating Table {tableInfo.Schema}.{tableInfo.TableName}");
                    await ExecuteSql(connectionString, createTableScript);
                }
                else
                {
                    Logger.LogWarn($"Table {tableInfo.Schema}.{tableInfo.TableName} already exists, skipping.");
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
