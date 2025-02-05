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
            _sqlModelScaffold = sqlModelScaffold;
            _sqlDalRepositoryInterfaceScaffold = sqlDalRepositoryInterfaceScaffold;
            _sqlDomainServiceScaffold = sqlDomainServiceScaffold;
            _sqlDomainServiceInterfaceScaffold = sqlDomainServiceInterfaceScaffold;
            _sqlForeignDomainServiceScaffold = sqlForeignDomainServiceScaffold;
            _sqlForeignDomainServiceInterfaceScaffold = sqlForeignDomainServiceInterfaceScaffold;
            _sqlDalRepositoryServiceCollectionExtensionScaffold = sqlDalRepositoryServiceCollectionExtensionScaffold;
            _sqlDomainServiceServiceCollectionExtensionScaffold = sqlDomainServiceServiceCollectionExtensionScaffold;
        }


        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var regenArgs = _configuration["regen"];
            var schema = "dbo";
            var entityName = string.Empty;

            if (regenArgs is null)
            {
                //Regenerate all found sql tables and procs
                Logger.LogInfo($"Regenerate all tables and all procs");

                //TODO: Foreach schema
                //TODO: > Regenerate all tables
                //TODO: > Regenerate all procs
            }
            else
            {
                var argSplit = regenArgs.Split(".");
                if (argSplit.Length > 1)
                {
                    schema = argSplit[0];
                    entityName = argSplit[1];
                }
                
                if (string.IsNullOrWhiteSpace(entityName))
                {
                    //Regenerate entire schema's table and procs (-regen=dbo)
                    Logger.LogInfo($"Regenerate entire {schema} schema");

                    //TODO: Regenerate all tables
                    //TODO: Regenerate all procs
                }
                else
                {
                    //Regenerate specific entity (-regen=dbo.table // -regen=dbo.table_procName)

                    var tablePath = Path.Combine(_csharpConfig.Directories.DBDirectory, schema, "Tables", $"{entityName}.sql");
                    if (File.Exists(tablePath))
                    {
                        Logger.LogInfo($"Regenerate Table {schema}.{entityName}");

                        //TODO: Regenerate table
                        //TODO: Regenerate tables procs
                    } else
                    {
                        var storedProcPath = Path.Combine(_csharpConfig.Directories.DBDirectory, schema, "Stored Procedure", $"{entityName}.sql");
                        if (File.Exists(storedProcPath))
                        {
                            Logger.LogInfo($"Regenerate Stored Procedure {schema}.{entityName}");
                            //TODO: Regenerate proc
                        } 
                        else
                        {
                            Logger.LogError($"Could not find a table or stored procedure {schema}.{entityName}");
                        }
                    }
                }
            }

            //Recursively find all valid sql subfolders in the project folder
            //var dbSchemas = Directory.EnumerateDirectories(_csharpConfig.Directories.DBDirectory, "*", SearchOption.AllDirectories)
            //                         .Where(folder => !IsInExcludedFolder(folder, _csharpConfig.Directories.DBDirectory));

            //foreach (var schema in dbSchemas)
            //{
            //    var tablesFolder = Path.Combine(schema, "Tables");
            //    if (Directory.Exists(tablesFolder))
            //        SetupSqlTableWatcher(schema, tablesFolder);

            //    var procsFolder = Path.Combine(schema, "Stored Procedures");
            //    if (Directory.Exists(procsFolder))
            //        SetupSqlProcsWatcher(schema, procsFolder);

            //}

            _lifetime.StopApplication();
            return Task.CompletedTask;
        }

        private bool IsInExcludedFolder(string path, string rootDirectory)
        {
            var excludedFolders = new[] { "bin", "obj", "Security", "Snapshots", "Storage" };
            var relativePath = Path.GetRelativePath(rootDirectory, path);
            return excludedFolders.Any(folder => relativePath.Split(Path.DirectorySeparatorChar).Contains(folder));
        }
    }
}
