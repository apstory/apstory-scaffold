using Apstory.Scaffold.Domain.Util;
using Apstory.Scaffold.Model.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Dapper;

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
            }

            if (args is null)
            {
                Logger.LogError($"-sqlPush requires sql entities to push");
                _lifetime.StopApplication();
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
                        Logger.LogError($"Could not find a table or stored procedure {schema}.{entityName}");
                    }
                }
            }

            _lifetime.StopApplication();
        }

        private async Task PushTableChanges(string connectionString, string tablePath)
        {
            Logger.LogWarn("Pushing SQL Table changes are not implemented");

            //TODO: If new table, push that
            //TODO: Figure out table differences via connection string and then push that
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
    }
}
