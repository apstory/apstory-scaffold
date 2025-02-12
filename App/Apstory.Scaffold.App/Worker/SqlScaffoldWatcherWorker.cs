using Apstory.Scaffold.Domain.Parser;
using Apstory.Scaffold.Domain.Scaffold;
using Apstory.Scaffold.Domain.Service;
using Apstory.Scaffold.Domain.Util;
using Apstory.Scaffold.Model.Config;
using Apstory.Scaffold.Model.Sql;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;

namespace Apstory.Scaffold.App.Worker
{
    public class SqlScaffoldWatcherWorker : BackgroundService
    {
        private static readonly ConcurrentDictionary<string, CancellationTokenSource> _debounceCancellations = new ConcurrentDictionary<string, CancellationTokenSource>();
        private static readonly TimeSpan _debounceTime = TimeSpan.FromMilliseconds(150);

        private readonly CSharpConfig _csharpConfig;
        private readonly SqlTableCachingService _sqlTableCachingService;
        private readonly SqlDalRepositoryScaffold _sqlDalRepositoryScaffold;
        private readonly SqlScriptFileScaffold _sqlScriptFileScaffold;
        private readonly SqlModelScaffold _sqlModelScaffold;
        private readonly SqlProjectScaffold _sqlProjectScaffold;
        private readonly SqlDalRepositoryInterfaceScaffold _sqlDalRepositoryInterfaceScaffold;
        private readonly SqlDomainServiceScaffold _sqlDomainServiceScaffold;
        private readonly SqlDomainServiceInterfaceScaffold _sqlDomainServiceInterfaceScaffold;
        private readonly SqlForeignDomainServiceScaffold _sqlForeignDomainServiceScaffold;
        private readonly SqlForeignDomainServiceInterfaceScaffold _sqlForeignDomainServiceInterfaceScaffold;
        private readonly SqlDalRepositoryServiceCollectionExtensionScaffold _sqlDalRepositoryServiceCollectionExtensionScaffold;
        private readonly SqlDomainServiceServiceCollectionExtensionScaffold _sqlDomainServiceServiceCollectionExtensionScaffold;

        private Dictionary<string, FileSystemWatcher> tableWatcher = new();
        private Dictionary<string, FileSystemWatcher> storedProcecdureWatcher = new();

        public SqlScaffoldWatcherWorker(CSharpConfig csharpConfig,
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

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Recursively find all valid subfolders in the project folder
            var dbSchemas = Directory.EnumerateDirectories(_csharpConfig.Directories.DBDirectory, "*", SearchOption.AllDirectories)
                                     .Where(folder => !IsInExcludedFolder(folder, _csharpConfig.Directories.DBDirectory));

            foreach (var schema in dbSchemas)
            {
                var tablesFolder = Path.Combine(schema, "Tables");
                if (Directory.Exists(tablesFolder))
                    SetupSqlTableWatcher(schema, tablesFolder);

                var procsFolder = Path.Combine(schema, "Stored Procedures");
                if (Directory.Exists(procsFolder))
                    SetupSqlProcsWatcher(schema, procsFolder);

            }

            return Task.CompletedTask;
        }

        private bool IsInExcludedFolder(string path, string rootDirectory)
        {
            var excludedFolders = new[] { "bin", "obj", "Security", "Snapshots", "Storage" };
            var relativePath = Path.GetRelativePath(rootDirectory, path);
            return excludedFolders.Any(folder => relativePath.Split(Path.DirectorySeparatorChar).Contains(folder));
        }

        private void SetupSqlTableWatcher(string schema, string folderPath)
        {
            Logger.LogInfo($"Watching tables folder: {folderPath}");

            var watcher = new FileSystemWatcher(folderPath, "*.sql")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                InternalBufferSize = 64 * 1024,
                IncludeSubdirectories = false
            };

            watcher.Changed += OnSqlTableFileChanged;
            watcher.Created += OnSqlTableFileChanged;
            watcher.Deleted += OnSqlTableFileChanged;
            watcher.Error += TableWatcher_Error;

            watcher.EnableRaisingEvents = true;

            tableWatcher[schema] = watcher;
        }

        private void TableWatcher_Error(object sender, ErrorEventArgs e)
        {
            Logger.LogError($"[Table Watcher Error] {e.GetException().Message}");
        }

        private void SetupSqlProcsWatcher(string schema, string folderPath)
        {
            Logger.LogInfo($"Watching procs folder: {folderPath}");

            var watcher = new FileSystemWatcher(folderPath, "*.sql")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                InternalBufferSize = 64 * 1024,
                IncludeSubdirectories = false
            };

            watcher.Changed += OnSqlProcFileChanged;
            watcher.Created += OnSqlProcFileChanged;
            watcher.Deleted += OnSqlProcFileChanged;
            watcher.Error += StoredProcecdureWatcher_Error;

            watcher.EnableRaisingEvents = true;
            storedProcecdureWatcher[schema] = watcher;
        }

        private void StoredProcecdureWatcher_Error(object sender, ErrorEventArgs e)
        {
            Logger.LogError($"[Stored Procedure Watcher Error] {e.GetException().Message}");
        }

        private async void OnSqlProcFileChanged(object sender, FileSystemEventArgs e)
        {
            // Cancel the existing debounce task if it exists
            if (_debounceCancellations.TryGetValue(e.FullPath, out var existingCts))
                existingCts.Cancel();

            // Create a new CancellationTokenSource for this debounce
            var cts = new CancellationTokenSource();
            _debounceCancellations[e.FullPath] = cts;

            try
            {
                // Wait for the debounce time to expire
                await Task.Delay(_debounceTime, cts.Token);

                // Execute the action only if the debounce was not canceled
                await HandleStoredProcedureChange(e.ChangeType, e.FullPath);

            }
            catch (TaskCanceledException)
            {
                // The debounce was canceled; this is expected, so ignore it
            }
            catch (Exception ex)
            {

            }
            finally
            {
                // Clean up the completed debounce
                _debounceCancellations.TryRemove(e.FullPath, out _);
                cts.Dispose();
            }

            Logger.LogInfo($"[DONE DEBOUNCE] {e.FullPath}");
        }

        private async Task HandleStoredProcedureChange(WatcherChangeTypes changeType, string filePath)
        {
            Logger.LogInfo($"[Changed Stored Procedure] {filePath}");

            try
            {
                if (changeType == WatcherChangeTypes.Created ||
                    changeType == WatcherChangeTypes.Changed)
                {
                    var fileName = Path.GetFileName(filePath);
                    string sqlProcDefinition = FileUtils.SafeReadAllText(filePath);
                    Logger.LogDebug($"Read [{fileName}]");

                    var sqlStoredProcedureInfo = SqlProcedureParser.Parse(sqlProcDefinition);
                    Logger.LogDebug($"Parsed [{fileName}]");

                    var tableName = fileName.Replace("zgen_", string.Empty).Split("_")[0];
                    var directory = Directory.GetParent(Path.GetDirectoryName(filePath));
                    var tablePath = Path.Combine(directory.FullName, "Tables", $"{tableName}.sql");
                    var sqlTableInfo = _sqlTableCachingService.GetCachedTable(tablePath);

                    var repoResult = await _sqlDalRepositoryScaffold.GenerateCode(sqlStoredProcedureInfo);
                    await _sqlDalRepositoryInterfaceScaffold.GenerateCode(sqlStoredProcedureInfo);
                    var domainResult = await _sqlDomainServiceScaffold.GenerateCode(sqlStoredProcedureInfo);
                    await _sqlDomainServiceInterfaceScaffold.GenerateCode(sqlStoredProcedureInfo);
                    await _sqlForeignDomainServiceScaffold.GenerateCode(sqlTableInfo, sqlStoredProcedureInfo);
                    await _sqlForeignDomainServiceInterfaceScaffold.GenerateCode(sqlStoredProcedureInfo);

                    if (repoResult == Model.Enum.ScaffoldResult.Created)
                        await _sqlDalRepositoryServiceCollectionExtensionScaffold.GenerateCode(sqlStoredProcedureInfo);

                    if (domainResult == Model.Enum.ScaffoldResult.Created)
                        await _sqlDomainServiceServiceCollectionExtensionScaffold.GenerateCode(sqlStoredProcedureInfo);
                }

                if (changeType == WatcherChangeTypes.Deleted)
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
            }
            catch (Exception ex)
            {
                Logger.LogError($"[Error Stored Procedure] {filePath}, {ex.Message}");
            }

            Logger.LogInfo($"[DONE Stored Procedure] {filePath}");
        }

        private async void OnSqlTableFileChanged(object sender, FileSystemEventArgs e)
        {
            Logger.LogInfo($"[{e.ChangeType} Table] {e.FullPath}");

            try
            {
                if (e.ChangeType == WatcherChangeTypes.Created ||
                    e.ChangeType == WatcherChangeTypes.Changed)
                {
                    var tableInfo = _sqlTableCachingService.GetLatestTableAndCache(e.FullPath);

                    await _sqlModelScaffold.GenerateCode(tableInfo);
                    var scriptResults = await _sqlScriptFileScaffold.GenerateCode(tableInfo);

                    //Add any newly created files into the sqlproj
                    var newScripts = scriptResults.Where(s => s.ScaffoldResult == Model.Enum.ScaffoldResult.Created).ToList();
                    if (newScripts.Any())
                        await _sqlProjectScaffold.GenerateCode(newScripts.Select(s => s.FilePath).ToList());
                }

                if (e.ChangeType == WatcherChangeTypes.Deleted)
                {
                    _sqlTableCachingService.RemoveCached(e.FullPath);

                    var fileName = Path.GetFileName(e.FullPath);
                    var tableInfo = new SqlTable();

                    tableInfo.TableName = fileName.Replace(".sql", string.Empty);
                    tableInfo.Schema = GetSchemaFromPath(e.FullPath);

                    await _sqlModelScaffold.DeleteCode(tableInfo);
                    await _sqlScriptFileScaffold.DeleteCode(tableInfo);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[Error Table] {e.FullPath}, {ex.Message}");
            }

            Logger.LogInfo($"[DONE Table] {e.FullPath}");
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
