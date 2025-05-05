using Apstory.Scaffold.Domain.Scaffold;
using Apstory.Scaffold.Domain.Util;
using Apstory.Scaffold.Model.Config;
using Apstory.Scaffold.Model.Sql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Apstory.Scaffold.App.Worker
{
    public class SqlScaffoldDeleteWorker : BackgroundService
    {
        private readonly IHostApplicationLifetime _lifetime;
        private readonly IConfiguration _configuration;

        private readonly CSharpConfig _csharpConfig;
        private readonly SqlDalRepositoryScaffold _sqlDalRepositoryScaffold;
        private readonly SqlScriptFileScaffold _sqlScriptFileScaffold;
        private readonly SqlModelScaffold _sqlModelScaffold;
        private readonly SqlDalRepositoryInterfaceScaffold _sqlDalRepositoryInterfaceScaffold;
        private readonly SqlDomainServiceScaffold _sqlDomainServiceScaffold;
        private readonly SqlDomainServiceInterfaceScaffold _sqlDomainServiceInterfaceScaffold;
        private readonly SqlForeignDomainServiceScaffold _sqlForeignDomainServiceScaffold;
        private readonly SqlForeignDomainServiceInterfaceScaffold _sqlForeignDomainServiceInterfaceScaffold;
        private readonly SqlDalRepositoryServiceCollectionExtensionScaffold _sqlDalRepositoryServiceCollectionExtensionScaffold;
        private readonly SqlDomainServiceServiceCollectionExtensionScaffold _sqlDomainServiceServiceCollectionExtensionScaffold;

        public SqlScaffoldDeleteWorker(IHostApplicationLifetime lifetime,
                                        IConfiguration configuration,
                                        CSharpConfig csharpConfig,
                                        SqlDalRepositoryScaffold sqlDalRepositoryScaffold,
                                        SqlScriptFileScaffold sqlScriptFileScaffold,
                                        SqlModelScaffold sqlModelScaffold,
                                        SqlDalRepositoryInterfaceScaffold sqlDalRepositoryInterfaceScaffold,
                                        SqlDomainServiceScaffold sqlDomainServiceScaffold,
                                        SqlDomainServiceInterfaceScaffold sqlDomainServiceInterfaceScaffold,
                                        SqlForeignDomainServiceScaffold sqlForeignDomainServiceScaffold,
                                        SqlForeignDomainServiceInterfaceScaffold sqlForeignDomainServiceInterfaceScaffold,
                                        SqlDalRepositoryServiceCollectionExtensionScaffold sqlDalRepositoryServiceCollectionExtensionScaffold,
                                        SqlDomainServiceServiceCollectionExtensionScaffold sqlDomainServiceServiceCollectionExtensionScaffold)
        {
            _lifetime = lifetime;
            _configuration = configuration;
            _csharpConfig = csharpConfig;
            _sqlDalRepositoryScaffold = sqlDalRepositoryScaffold;
            _sqlScriptFileScaffold = sqlScriptFileScaffold;
            _sqlModelScaffold = sqlModelScaffold;
            _sqlDalRepositoryInterfaceScaffold = sqlDalRepositoryInterfaceScaffold;
            _sqlDomainServiceScaffold = sqlDomainServiceScaffold;
            _sqlDomainServiceInterfaceScaffold = sqlDomainServiceInterfaceScaffold;
            _sqlForeignDomainServiceScaffold = sqlForeignDomainServiceScaffold;
            _sqlForeignDomainServiceInterfaceScaffold = sqlForeignDomainServiceInterfaceScaffold;
            _sqlDalRepositoryServiceCollectionExtensionScaffold = sqlDalRepositoryServiceCollectionExtensionScaffold;
            _sqlDomainServiceServiceCollectionExtensionScaffold = sqlDomainServiceServiceCollectionExtensionScaffold;
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var cleanArgs = _configuration["delete"];
            var schema = "dbo";

            if (string.IsNullOrEmpty(cleanArgs))
            {
                var dbSchemas = Directory.EnumerateDirectories(_csharpConfig.Directories.DBDirectory, "*", SearchOption.TopDirectoryOnly)
                                         .Where(folder => !IsInExcludedFolder(folder, _csharpConfig.Directories.DBDirectory));

                foreach (var dbSchema in dbSchemas)
                    await DeleteSchema(dbSchema);
            }
            else
            {
                var entities = cleanArgs.Split(";");
                foreach (var regenEntry in entities)
                {
                    var entityName = string.Empty;
                    var argSplit = regenEntry.Split(".");
                    if (argSplit.Length > 1)
                    {
                        schema = argSplit[0];
                        entityName = argSplit[1];
                    }

                    if (string.IsNullOrWhiteSpace(entityName))
                    {
                        //Delete entire schema's table and procs (-regen=dbo)
                        Logger.LogInfo($"Delete entire {schema} schema");

                        await DeleteSchema(schema);
                    }
                    else
                    {
                        //Regenerate specific entity (-regen=dbo.table // -regen=dbo.table_procName)
                        var tablePath = Path.Combine(_csharpConfig.Directories.DBDirectory, schema, "Tables", $"{entityName}.sql");
                        if (File.Exists(tablePath))
                        {
                            Logger.LogInfo($"Delete Table {schema}.{entityName}");
                            var schemaStoredProcedurePath = Path.Combine(_csharpConfig.Directories.DBDirectory, schema, "Stored Procedures");
                            var allGeneratedStoredProcedures = Directory.EnumerateFiles(schemaStoredProcedurePath, $"zgen_{entityName}_*.sql", SearchOption.TopDirectoryOnly);

                            foreach (var storedProcedurePath in allGeneratedStoredProcedures)
                                await DeleteStoredProcedure(storedProcedurePath);

                            await DeleteTable(tablePath);
                        }
                        else
                        {
                            var storedProcPath = Path.Combine(_csharpConfig.Directories.DBDirectory, schema, "Stored Procedures", $"{entityName}.sql");
                            if (File.Exists(storedProcPath))
                            {
                                Logger.LogInfo($"Regenerate Stored Procedure {schema}.{entityName}");
                                await DeleteStoredProcedure(storedProcPath);
                            }
                            else
                            {
                                Logger.LogError($"Could not find a table or stored procedure {schema}.{entityName}");
                            }
                        }
                    }
                }
            }



            _lifetime.StopApplication();
        }

        private async Task DeleteSchema(string dbSchema)
        {
            var schemaStoredProcedurePath = Path.Combine(_csharpConfig.Directories.DBDirectory, dbSchema, "Stored Procedures");
            var allGeneratedStoredProcedures = Directory.EnumerateFiles(schemaStoredProcedurePath, "zgen_*.sql", SearchOption.TopDirectoryOnly);

            foreach (var storedProcedurePath in allGeneratedStoredProcedures)
                await DeleteStoredProcedure(storedProcedurePath);

            var schemaTablesPath = Path.Combine(_csharpConfig.Directories.DBDirectory, dbSchema, "Tables");
            var allTables = Directory.EnumerateFiles(schemaTablesPath, "*.sql", SearchOption.TopDirectoryOnly);

            foreach (var tablePath in allTables)
                await DeleteTable(tablePath);
        }

        private bool IsInExcludedFolder(string path, string rootDirectory)
        {
            var excludedFolders = new[] { "bin", "obj", "Security", "Snapshots", "Storage" };
            var relativePath = Path.GetRelativePath(rootDirectory, path);
            return excludedFolders.Any(folder => relativePath.Split(Path.DirectorySeparatorChar).Contains(folder));
        }

        /// <summary>
        /// Cleans up a single table, removing the model and the associated stored proc scripts
        /// </summary>
        /// <param name="tablePath"></param>
        /// <returns></returns>
        private async Task DeleteTable(string tablePath)
        {
            Logger.LogInfo($"[Cleaning Table] {tablePath}");
            try
            {
                var fileName = Path.GetFileName(tablePath);
                var tableInfo = new SqlTable();

                tableInfo.TableName = fileName.Replace(".sql", string.Empty).ToPascalCase();
                tableInfo.Schema = GetSchemaFromPath(tablePath);

                await _sqlModelScaffold.DeleteCode(tableInfo);
                await _sqlScriptFileScaffold.DeleteCode(tableInfo);

                if (File.Exists(tablePath))
                    File.Delete(tablePath);
            }
            catch (Exception ex)
            {
                Logger.LogError($"[Error Table] {tablePath}, {ex.Message}");
            }

            Logger.LogInfo($"[Done Table] {tablePath}");
        }

        /// <summary>
        /// Clean up a single stored proc, removing all csharp code from the related files
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private async Task DeleteStoredProcedure(string filePath)
        {
            Logger.LogInfo($"[Cleaning Stored Procedure] {filePath}");

            try
            {
                var fileName = Path.GetFileName(filePath);
                var fileParts = fileName.Replace("zgen_", string.Empty).Replace(".sql", string.Empty).Split("_");
                var sqlStoredProcedureInfo = new SqlStoredProcedure();
                sqlStoredProcedureInfo.TableName = fileParts[0].ToPascalCase();
                sqlStoredProcedureInfo.StoredProcedureName = fileName.Replace(".sql", string.Empty);
                sqlStoredProcedureInfo.Schema = GetSchemaFromPath(filePath);

                var repoResult = await _sqlDalRepositoryScaffold.DeleteCode(sqlStoredProcedureInfo);
                await _sqlDalRepositoryInterfaceScaffold.DeleteCode(sqlStoredProcedureInfo);
                var domainResult = await _sqlDomainServiceScaffold.DeleteCode(sqlStoredProcedureInfo);
                await _sqlDomainServiceInterfaceScaffold.DeleteCode(sqlStoredProcedureInfo);
                await _sqlForeignDomainServiceScaffold.DeleteCode(sqlStoredProcedureInfo);
                await _sqlForeignDomainServiceInterfaceScaffold.DeleteCode(sqlStoredProcedureInfo);

                if (repoResult == Model.Enum.ScaffoldResult.Deleted)
                    await _sqlDalRepositoryServiceCollectionExtensionScaffold.DeleteCode(sqlStoredProcedureInfo);

                if (domainResult == Model.Enum.ScaffoldResult.Deleted)
                    await _sqlDomainServiceServiceCollectionExtensionScaffold.DeleteCode(sqlStoredProcedureInfo);

                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch (Exception ex)
            {
                Logger.LogError($"[Error Stored Procedure] {filePath}, {ex.Message}");
            }

            Logger.LogInfo($"[Done Stored Procedure] {filePath}");
        }

        private string GetSchemaFromPath(string path)
        {
            string directory = Path.GetDirectoryName(path);

            if (directory == null)
                throw new ArgumentException("Invalid path provided.");

            // Get the parent directory (schema folder)
            string schema = Directory.GetParent(directory)?.Name;

            if (string.IsNullOrEmpty(schema))
                throw new InvalidOperationException("Schema folder not found in the provided path.");

            return schema;
        }
    }
}
