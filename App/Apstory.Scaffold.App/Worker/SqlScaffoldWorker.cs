using Apstory.Scaffold.Domain.Parser;
using Apstory.Scaffold.Domain.Scaffold;
using Apstory.Scaffold.Domain.Util;
using Apstory.Scaffold.Model.Config;
using Apstory.Scaffold.Model.Sql;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;

namespace Apstory.Scaffold.App.Worker
{
    public class SqlScaffoldWorker : BackgroundService
    {
        private readonly CSharpConfig _csharpConfig;
        private readonly SqlDalRepositoryScaffold _sqlDalRepositoryScaffold;
        private readonly SqlScriptFileScaffold _sqlScriptFileScaffold;
        private readonly SqlModelScaffold _sqlModelScaffold;
        private readonly SqlDalRepositoryInterfaceScaffold _sqlDalRepositoryInterfaceScaffold;
        private readonly SqlDomainServiceScaffold _sqlDomainServiceScaffold;
        private readonly SqlDomainServiceInterfaceScaffold _sqlDomainServiceInterfaceScaffold;

        private static readonly ConcurrentDictionary<string, Timer> _debounceTimers = new ConcurrentDictionary<string, Timer>();
        private static readonly TimeSpan _debounceTime = TimeSpan.FromMilliseconds(50);

        public SqlScaffoldWorker(CSharpConfig csharpConfig,
                                 SqlDalRepositoryScaffold sqlDalRepositoryScaffold,
                                 SqlScriptFileScaffold sqlScriptFileScaffold,
                                 SqlModelScaffold sqlModelScaffold,
                                 SqlDalRepositoryInterfaceScaffold sqlDalRepositoryInterfaceScaffold,
                                 SqlDomainServiceScaffold sqlDomainServiceScaffold,
                                 SqlDomainServiceInterfaceScaffold sqlDomainServiceInterfaceScaffold)
        {
            _csharpConfig = csharpConfig;
            _sqlDalRepositoryScaffold = sqlDalRepositoryScaffold;
            _sqlScriptFileScaffold = sqlScriptFileScaffold;
            _sqlModelScaffold = sqlModelScaffold;
            _sqlDalRepositoryInterfaceScaffold = sqlDalRepositoryInterfaceScaffold;
            _sqlDomainServiceScaffold = sqlDomainServiceScaffold;
            _sqlDomainServiceInterfaceScaffold = sqlDomainServiceInterfaceScaffold;
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
                    SetupSqlTableWatcher(tablesFolder);

                var procsFolder = Path.Combine(schema, "Stored Procedures");
                if (Directory.Exists(procsFolder))
                    SetupSqlProcsWatcher(procsFolder);

            }

            return Task.CompletedTask;
        }

        private bool IsInExcludedFolder(string path, string rootDirectory)
        {
            var excludedFolders = new[] { "bin", "obj", "Security", "Snapshots", "Storage" };
            var relativePath = Path.GetRelativePath(rootDirectory, path);
            return excludedFolders.Any(folder => relativePath.Split(Path.DirectorySeparatorChar).Contains(folder));
        }

        private void SetupSqlTableWatcher(string folderPath)
        {
            Logger.LogInfo($"Watching tables folder: {folderPath}");

            FileSystemWatcher watcher = new FileSystemWatcher(folderPath, "*.sql")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                IncludeSubdirectories = false
            };

            watcher.Changed += OnSqlTableFileChanged;
            watcher.Created += OnSqlTableFileChanged;
            watcher.Deleted += OnSqlTableFileChanged;

            watcher.EnableRaisingEvents = true;
        }

        private void SetupSqlProcsWatcher(string folderPath)
        {
            Logger.LogInfo($"Watching procs folder: {folderPath}");

            FileSystemWatcher watcher = new FileSystemWatcher(folderPath, "*.sql")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                IncludeSubdirectories = false
            };

            watcher.Changed += OnSqlProcFileChanged;
            watcher.Created += OnSqlProcFileChanged;
            watcher.Deleted += OnSqlProcFileChanged;

            watcher.EnableRaisingEvents = true;
        }

        private async void OnSqlProcFileChanged(object sender, FileSystemEventArgs e)
        {
            var timer = _debounceTimers.AddOrUpdate(e.FullPath, _ => CreateTimer(e.ChangeType, e.FullPath),
                (_, existingTimer) =>
                {
                    existingTimer.Change(_debounceTime, Timeout.InfiniteTimeSpan);
                    return existingTimer;
                }
            );
        }

        private Timer CreateTimer(WatcherChangeTypes changeType, string filePath)
        {
            return new Timer(_ =>
            {
                // This block is executed when the debounce time expires
                HandleStoredProcedureChange(changeType, filePath);

                // Remove the timer after it's triggered
                _debounceTimers.TryRemove(filePath, out var _);
            }, null, _debounceTime, Timeout.InfiniteTimeSpan);
        }

        private async void HandleStoredProcedureChange(WatcherChangeTypes changeType, string filePath)
        {
            Logger.LogInfo($"[Changed Stored Procedure] {filePath}");

            await Task.Run(async () =>
            {
                if (changeType == WatcherChangeTypes.Created ||
                    changeType == WatcherChangeTypes.Changed)
                {
                    string sqlProcDefinition = FileUtils.SafeReadAllText(filePath);
                    Logger.LogDebug($"Read [{Path.GetFileName(filePath)}]");

                    var procInfo = SqlProcedureParser.Parse(sqlProcDefinition);
                    Logger.LogDebug($"Parsed [{Path.GetFileName(filePath)}]");

                    await _sqlDalRepositoryScaffold.GenerateCode(procInfo);
                    await _sqlDalRepositoryInterfaceScaffold.GenerateCode(procInfo);
                    await _sqlDomainServiceScaffold.GenerateCode(procInfo);
                    await _sqlDomainServiceInterfaceScaffold.GenerateCode(procInfo);
                }

                if (changeType == WatcherChangeTypes.Deleted)
                {
                    var fileName = Path.GetFileName(filePath);
                    var fileParts = fileName.Replace("zgen_", string.Empty).Replace(".sql", string.Empty).Split("_");
                    var procInfo = new SqlStoredProcedure();
                    procInfo.TableName = fileParts[0];
                    procInfo.StoredProcedureName = fileName.Replace(".sql", string.Empty);
                    procInfo.Schema = GetSchemaFromPath(filePath);

                    await _sqlDalRepositoryScaffold.DeleteCode(procInfo);
                    await _sqlDalRepositoryInterfaceScaffold.DeleteCode(procInfo);
                    await _sqlDomainServiceScaffold.DeleteCode(procInfo);
                    await _sqlDomainServiceInterfaceScaffold.DeleteCode(procInfo);
                }
            });
        }

        private async void OnSqlTableFileChanged(object sender, FileSystemEventArgs e)
        {
            Logger.LogInfo($"[{e.ChangeType} Table] {e.FullPath}");

            if (e.ChangeType == WatcherChangeTypes.Created ||
                e.ChangeType == WatcherChangeTypes.Changed)
            {
                var sqlTableDefinition = File.ReadAllText(e.FullPath);
                Logger.LogDebug($"Read [{Path.GetFileName(e.FullPath)}]");

                var tableInfo = SqlTableParser.Parse(sqlTableDefinition);
                Logger.LogDebug($"Parsed [{Path.GetFileName(e.FullPath)}]");

                await _sqlModelScaffold.GenerateCode(tableInfo);
                _sqlScriptFileScaffold.GenerateCode(tableInfo);
            }

            if (e.ChangeType == WatcherChangeTypes.Deleted)
            {
                var fileName = Path.GetFileName(e.FullPath);
                var tableInfo = new SqlTable();

                tableInfo.TableName = fileName.Replace(".sql", string.Empty);
                tableInfo.Schema = GetSchemaFromPath(e.FullPath);

                await _sqlModelScaffold.DeleteCode(tableInfo);
                _sqlScriptFileScaffold.DeleteCode(tableInfo);
            }
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
