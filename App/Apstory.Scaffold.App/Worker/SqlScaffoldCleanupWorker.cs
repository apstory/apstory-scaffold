using Apstory.Scaffold.Domain.Scaffold;
using Apstory.Scaffold.Domain.Util;
using Apstory.Scaffold.Model.Config;
using Apstory.Scaffold.Model.Sql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Apstory.Scaffold.App.Worker
{
    public class SqlScaffoldCleanupWorker : BackgroundService
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

        public SqlScaffoldCleanupWorker(IHostApplicationLifetime lifetime,
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
            var cleanArgs = _configuration["clean"];
            var dbSchemas = Directory.EnumerateDirectories(_csharpConfig.Directories.DBDirectory, "*", SearchOption.TopDirectoryOnly)
                                     .Where(folder => !IsInExcludedFolder(folder, _csharpConfig.Directories.DBDirectory));

            foreach (var dbSchema in dbSchemas)
            {
                var schemaStoredProcedurePath = Path.Combine(_csharpConfig.Directories.DBDirectory, dbSchema, "Stored Procedures");
                var allGeneratedStoredProcedures = Directory.EnumerateFiles(schemaStoredProcedurePath, "zgen_*.sql", SearchOption.TopDirectoryOnly);

                foreach (var storedProcedurePath in allGeneratedStoredProcedures)
                    await CleanStoredProcedure(storedProcedurePath);

                var schemaTablesPath = Path.Combine(_csharpConfig.Directories.DBDirectory, dbSchema, "Tables");
                var allTables = Directory.EnumerateFiles(schemaTablesPath, "*.sql", SearchOption.TopDirectoryOnly);

                foreach (var tablePath in allTables)
                    await CleanTable(tablePath);
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
        /// Cleans up a single table, removing the model and the associated stored proc scripts
        /// </summary>
        /// <param name="tablePath"></param>
        /// <returns></returns>
        private async Task CleanTable(string tablePath)
        {
            Logger.LogInfo($"[Cleaning Table] {tablePath}");
            try
            {
                var fileName = Path.GetFileName(tablePath);
                var tableInfo = new SqlTable();

                tableInfo.TableName = fileName.Replace(".sql", string.Empty);
                tableInfo.Schema = GetSchemaFromPath(tablePath);

                await _sqlModelScaffold.DeleteCode(tableInfo);
                await _sqlScriptFileScaffold.DeleteCode(tableInfo);
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
        private async Task CleanStoredProcedure(string filePath)
        {
            Logger.LogInfo($"[Cleaning Stored Procedure] {filePath}");

            try
            {
                var fileName = Path.GetFileName(filePath);
                var fileParts = fileName.Replace("zgen_", string.Empty).Replace(".sql", string.Empty).Split("_");
                var sqlStoredProcedureInfo = new SqlStoredProcedure();
                sqlStoredProcedureInfo.TableName = fileParts[0];
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
