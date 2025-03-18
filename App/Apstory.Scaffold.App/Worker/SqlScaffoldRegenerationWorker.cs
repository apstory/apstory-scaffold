using Apstory.Scaffold.Domain.Parser;
using Apstory.Scaffold.Domain.Scaffold;
using Apstory.Scaffold.Domain.Service;
using Apstory.Scaffold.Domain.Util;
using Apstory.Scaffold.Model.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Apstory.Scaffold.App.Worker
{
    public class SqlScaffoldRegenerationWorker : BackgroundService
    {
        private readonly IHostApplicationLifetime _lifetime;
        private readonly IConfiguration _configuration;

        private readonly CSharpConfig _csharpConfig;
        private readonly SqlTableCachingService _sqlTableCachingService;
        private readonly SqlDalRepositoryScaffold _sqlDalRepositoryScaffold;
        private readonly SqlScriptFileScaffold _sqlScriptFileScaffold;
        private readonly SqlProjectScaffold _sqlProjectScaffold;
        private readonly SqlModelScaffold _sqlModelScaffold;
        private readonly SqlDalRepositoryInterfaceScaffold _sqlDalRepositoryInterfaceScaffold;
        private readonly SqlDomainServiceScaffold _sqlDomainServiceScaffold;
        private readonly SqlDomainServiceInterfaceScaffold _sqlDomainServiceInterfaceScaffold;
        private readonly SqlForeignDomainServiceScaffold _sqlForeignDomainServiceScaffold;
        private readonly SqlForeignDomainServiceInterfaceScaffold _sqlForeignDomainServiceInterfaceScaffold;
        private readonly SqlDalRepositoryServiceCollectionExtensionScaffold _sqlDalRepositoryServiceCollectionExtensionScaffold;
        private readonly SqlDomainServiceServiceCollectionExtensionScaffold _sqlDomainServiceServiceCollectionExtensionScaffold;

        public SqlScaffoldRegenerationWorker(IHostApplicationLifetime lifetime,
                                             IConfiguration configuration,
                                             CSharpConfig csharpConfig,
                                             SqlTableCachingService sqlTableCachingService,
                                             SqlDalRepositoryScaffold sqlDalRepositoryScaffold,
                                             SqlScriptFileScaffold sqlScriptFileScaffold,
                                             SqlProjectScaffold sqlProjectScaffold,
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
            _sqlTableCachingService = sqlTableCachingService;
            _sqlDalRepositoryScaffold = sqlDalRepositoryScaffold;
            _sqlScriptFileScaffold = sqlScriptFileScaffold;
            _sqlProjectScaffold = sqlProjectScaffold;
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
            var regenArgs = _configuration["regen"];
            var schema = "dbo";

            if (regenArgs is null)
            {
                //Regenerate all found sql tables and procs

                Logger.LogInfo($"Regenerate all tables and all procs");

                var dbSchemas = Directory.EnumerateDirectories(_csharpConfig.Directories.DBDirectory, "*", SearchOption.TopDirectoryOnly)
                                         .Where(folder => !IsInExcludedFolder(folder, _csharpConfig.Directories.DBDirectory));

                foreach (var dbSchema in dbSchemas)
                {
                    var schemaTablesPath = Path.Combine(_csharpConfig.Directories.DBDirectory, dbSchema, "Tables");
                    var allTablePaths = Directory.EnumerateFiles(schemaTablesPath, "*.sql", SearchOption.TopDirectoryOnly);

                    foreach (var tablePath in allTablePaths)
                    {
                        await RegenerateTable(tablePath);
                        await RegenerateTableStoredProcedures(tablePath);
                    }
                }
            }
            else
            {
                var entities = regenArgs.Split(";");
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
                        //Regenerate entire schema's table and procs (-regen=dbo)

                        Logger.LogInfo($"Regenerate entire {schema} schema");

                        var schemaTablesPath = Path.Combine(_csharpConfig.Directories.DBDirectory, schema, "Tables");
                        var allTablePaths = Directory.EnumerateFiles(schemaTablesPath, "*.sql", SearchOption.TopDirectoryOnly);
                        foreach (var tablePath in allTablePaths)
                        {
                            await RegenerateTable(tablePath);
                            await RegenerateTableStoredProcedures(tablePath);
                        }
                    }
                    else
                    {
                        //Regenerate specific entity (-regen=dbo.table // -regen=dbo.table_procName)

                        var tablePath = Path.Combine(_csharpConfig.Directories.DBDirectory, schema, "Tables", $"{entityName}.sql");
                        if (File.Exists(tablePath))
                        {
                            Logger.LogInfo($"Regenerate Table {schema}.{entityName}");

                            await RegenerateTable(tablePath);
                            await RegenerateTableStoredProcedures(tablePath);
                        }
                        else
                        {
                            var storedProcPath = Path.Combine(_csharpConfig.Directories.DBDirectory, schema, "Stored Procedures", $"{entityName}.sql");
                            if (File.Exists(storedProcPath))
                            {
                                Logger.LogInfo($"Regenerate Stored Procedure {schema}.{entityName}");
                                await RegenerateStoredProcedure(storedProcPath);
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

        private bool IsInExcludedFolder(string path, string rootDirectory)
        {
            var excludedFolders = new[] { "bin", "obj", "Security", "Snapshots", "Storage" };
            var relativePath = Path.GetRelativePath(rootDirectory, path);
            return excludedFolders.Any(folder => relativePath.Split(Path.DirectorySeparatorChar).Contains(folder));
        }

        /// <summary>
        /// Regenerates a single table - Creates the csharp model and the db scripts
        /// </summary>
        /// <param name="tablePath"></param>
        /// <returns></returns>
        private async Task RegenerateTable(string tablePath)
        {
            var tableInfo = _sqlTableCachingService.GetLatestTableAndCache(tablePath);

            await _sqlModelScaffold.GenerateCode(tableInfo);
            var scriptResults = await _sqlScriptFileScaffold.GenerateCode(tableInfo, _configuration["variant"]);

            //Add any newly created files into the sqlproj
            var newScripts = scriptResults.Where(s => s.ScaffoldResult == Model.Enum.ScaffoldResult.Created).ToList();
            if (newScripts.Any())
                await _sqlProjectScaffold.GenerateCode(newScripts.Select(s => s.FilePath).ToList());
        }

        /// <summary>
        /// Search for all procs relating to a specific table and regenerates the required cSharp files
        /// </summary>
        /// <param name="tablePath"></param>
        /// <returns></returns>
        private async Task RegenerateTableStoredProcedures(string tablePath)
        {
            var tableName = Path.GetFileName(tablePath).Replace(".sql", "");
            var schemaDirectory = Directory.GetParent(Path.GetDirectoryName(tablePath));
            var storedProcedureDirectory = Path.Combine(schemaDirectory.FullName, "Stored Procedures");

            //Recursively find all related stored procedures for table
            var searchPattern = $"zgen_{tableName}_*.sql";
            Logger.LogInfo($"Searching for all procs in '{storedProcedureDirectory}' matching '{searchPattern}'");
            var allRelatedStoredProcedurePaths = Directory.EnumerateFiles(storedProcedureDirectory, searchPattern, SearchOption.TopDirectoryOnly);

            foreach (var storedProcedurePath in allRelatedStoredProcedurePaths)
                await RegenerateStoredProcedure(storedProcedurePath);
        }

        /// <summary>
        /// Regenerate a single stored proc, updating all the related csharp files
        /// </summary>
        /// <param name="storedProcedurePath"></param>
        /// <returns></returns>
        private async Task RegenerateStoredProcedure(string storedProcedurePath)
        {
            Logger.LogInfo($"[Regenerating Stored Procedure] {storedProcedurePath}");

            try
            {
                var fileName = Path.GetFileName(storedProcedurePath);
                string sqlProcDefinition = FileUtils.SafeReadAllText(storedProcedurePath);
                Logger.LogDebug($"Read [{fileName}]");

                var sqlStoredProcedureInfo = SqlProcedureParser.Parse(sqlProcDefinition);
                Logger.LogDebug($"Parsed [{fileName}]");

                var tableName = fileName.Replace("zgen_", string.Empty).Split("_")[0];
                var directory = Directory.GetParent(Path.GetDirectoryName(storedProcedurePath));
                var tablePath = Path.Combine(directory.FullName, "Tables", $"{tableName}.sql");
                var sqlTableInfo = _sqlTableCachingService.GetCachedTable(tablePath);

                var repoResult = await _sqlDalRepositoryScaffold.GenerateCode(sqlStoredProcedureInfo);
                await _sqlDalRepositoryInterfaceScaffold.GenerateCode(sqlStoredProcedureInfo);
                var domainResult = await _sqlDomainServiceScaffold.GenerateCode(sqlStoredProcedureInfo);
                await _sqlDomainServiceInterfaceScaffold.GenerateCode(sqlStoredProcedureInfo);
                await _sqlForeignDomainServiceScaffold.GenerateCode(sqlTableInfo, sqlStoredProcedureInfo);
                await _sqlForeignDomainServiceInterfaceScaffold.GenerateCode(sqlTableInfo, sqlStoredProcedureInfo);

                if (repoResult == Model.Enum.ScaffoldResult.Created)
                    await _sqlDalRepositoryServiceCollectionExtensionScaffold.GenerateCode(sqlStoredProcedureInfo);

                if (domainResult == Model.Enum.ScaffoldResult.Created)
                    await _sqlDomainServiceServiceCollectionExtensionScaffold.GenerateCode(sqlStoredProcedureInfo);
            }
            catch (Exception ex)
            {
                Logger.LogError($"[Error Stored Procedure] {storedProcedurePath}, {ex.Message}");
            }

            Logger.LogInfo($"[DONE Stored Procedure] {storedProcedurePath}");
        }
    }
}
