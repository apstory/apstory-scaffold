using Apstory.Scaffold.Domain.Parser;
using Apstory.Scaffold.Domain.Scaffold;
using Apstory.Scaffold.Domain.Service;
using Apstory.Scaffold.Domain.Util;
using Apstory.Scaffold.Model.Config;
using Apstory.Scaffold.Model.Sql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using static System.Runtime.InteropServices.Marshalling.IIUnknownCacheStrategy;

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
                    if (entityName.EndsWith("FilterPaging", StringComparison.OrdinalIgnoreCase))
                    {
                        var storedProcPath = Path.Combine(_csharpConfig.Directories.DBDirectory, schema, "Stored Procedures", $"{entityName}.sql");

                        // Handle the case where regenArgs ends with "FilterPaging"
                        await GenerateSearchStoredProcedure(storedProcPath);
                        return;
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
            var scriptResults = await _sqlScriptFileScaffold.GenerateCode(tableInfo);

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

        /// <summary>
        /// Regenerate  search stored procs, updating all the related csharp files
        /// </summary>
        /// <param name="searchStoredProcedurePath"></param>
        /// <returns></returns>
        private async Task GenerateSearchStoredProcedure(string parentSearchStoredProcedurePath)
        {
            Logger.LogInfo($"[Regenerating Search Procedures] {parentSearchStoredProcedurePath}");

            try
            {
                var parentFileName = Path.GetFileNameWithoutExtension(parentSearchStoredProcedurePath);
                string parentSqlProcDefinition = FileUtils.SafeReadAllText(parentSearchStoredProcedurePath);
                var parentSqlStoredProcedureInfo = SqlProcedureParser.ParseSearchProc(parentSqlProcDefinition);

                Logger.LogDebug($"Read [{parentFileName}]");

                var executedProceduresWithParams = GetExecutedProceduresWithParams(parentSqlStoredProcedureInfo,parentSqlProcDefinition);

                var tableNameMatch = Regex.Match(parentFileName, @"^(\w+)_FilterPaging$");
                if (!tableNameMatch.Success)
                {
                    Logger.LogError($"Could not extract table name from parent search procedure file name: {parentFileName}");
                    return;
                }
                var tableName = tableNameMatch.Groups[1].Value;
                var directory = Directory.GetParent(Path.GetDirectoryName(parentSearchStoredProcedurePath));
                var tablePath = Path.Combine(directory.FullName, "Tables", $"{tableName}.sql");
                var sqlTableInfo = _sqlTableCachingService.GetCachedTable(tablePath);

                if (sqlTableInfo == null)
                {
                    Logger.LogError($"Could not retrieve SqlTable information for: {tableName}");
                    return;
                }
                foreach (var executedProcInfo in executedProceduresWithParams)
                {
                    var fullProcName = executedProcInfo.Item1;
                    var parameters = executedProcInfo.Item2; // This is now List<Tuple<string, string>>
                                                             //  var executedProcNameParts = fullProcName.Trim('[', ']').Split('.');
                    var executedProcNameParts = fullProcName.Split('.').Select(part => part.Trim('[', ']')).ToArray();


                    if (executedProcNameParts.Length == 2)
                    {
                        var executedProcSchema = executedProcNameParts[0];
                        var executedProcBaseName = executedProcNameParts[1];

                        if (executedProcSchema.Equals(sqlTableInfo.Schema, StringComparison.OrdinalIgnoreCase) &&
                            executedProcBaseName.StartsWith($"{tableName}_FilterPaging_", StringComparison.OrdinalIgnoreCase))
                        {
                            // Create a new SqlStoredProcedure object
                            var sqlSearchProcInfo = new SqlStoredProcedure
                            {
                                Schema = executedProcSchema,
                                TableName = sqlTableInfo.TableName,
                                StoredProcedureName = executedProcBaseName,
                                Parameters = parameters
                            };

                            // Call the existing GenerateSearchProcCode method
                            var results = await _sqlScriptFileScaffold.GenerateSearchProcCode(sqlTableInfo,parentSearchStoredProcedurePath, sqlSearchProcInfo);


                        }
                    }
                }
                    await _sqlModelScaffold.GenerateSearchProcCode(sqlTableInfo,parentSqlStoredProcedureInfo);


                var repoResult = await _sqlDalRepositoryScaffold.GenerateSearchProcCode(parentSqlStoredProcedureInfo);
                await _sqlDalRepositoryInterfaceScaffold.GenerateSearchProcCode(parentSqlStoredProcedureInfo);
                var domainResult = await _sqlDomainServiceScaffold.GenerateSearchProcCode(parentSqlStoredProcedureInfo);
                await _sqlDomainServiceInterfaceScaffold.GenerateSearchProcCode(parentSqlStoredProcedureInfo);
                await _sqlForeignDomainServiceScaffold.GenerateSearchProcCode(sqlTableInfo, parentSqlStoredProcedureInfo);
               await _sqlForeignDomainServiceInterfaceScaffold.GenerateSearchProcCode(sqlTableInfo, parentSqlStoredProcedureInfo);

                if (repoResult == Model.Enum.ScaffoldResult.Created)
                    await _sqlDalRepositoryServiceCollectionExtensionScaffold.GenerateCode(parentSqlStoredProcedureInfo);

                if (domainResult == Model.Enum.ScaffoldResult.Created)
                    await _sqlDomainServiceServiceCollectionExtensionScaffold.GenerateCode(parentSqlStoredProcedureInfo);
               
               
            }
            catch (Exception ex)
            {
                Logger.LogError($"[Error Regenerating Search Procedures] {parentSearchStoredProcedurePath}, {ex.Message}");
            }

            Logger.LogInfo($"[DONE Regenerating Search Procedures] {parentSearchStoredProcedurePath}");
        }
        private static List<Tuple<string, List<SqlColumn>>> GetExecutedProceduresWithParams(SqlStoredProcedure parentSqlStoredProcedureInfo, string sqlProcedureDefinition)
        {
            List<Tuple<string, List<SqlColumn>>> executedProcedures = new List<Tuple<string, List<SqlColumn>>>();
            string pattern = @"EXECUTE\s+(?:\[?(\w+)\]?\.)?\[?(\w+)\]?\s*([^;]*)";
            Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);
            MatchCollection matches = regex.Matches(sqlProcedureDefinition);

            foreach (Match match in matches)
            {
                if (match.Groups.Count >= 3)
                {
                    string schema = match.Groups[1].Value;
                    string procName = match.Groups[2].Value;
                    string fullProcName = string.IsNullOrEmpty(schema) ? $"[{procName}]" : $"[{schema}].[{procName}]";
                    string parametersString = match.Groups[3].Value.Trim();
                    List<SqlColumn> parameters = new List<SqlColumn>();

                    if (!string.IsNullOrEmpty(parametersString))
                    {
                        string[] parameterList = parametersString.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string parameter in parameterList)
                        {
                            string trimmedParam = parameter.Trim().TrimStart('@');

                            SqlColumn paramDefinition = FindParameterDefinition(parentSqlStoredProcedureInfo.Parameters, trimmedParam);
                            parameters.Add(paramDefinition);

                        }
                    }

                    executedProcedures.Add(Tuple.Create(fullProcName, parameters));
                }
            }
            return executedProcedures;
        }

        private static SqlColumn FindParameterDefinition(List<SqlColumn> parameters, string searchTerm)
        {
            return parameters.FirstOrDefault(p => p.ColumnName.Equals(searchTerm, StringComparison.OrdinalIgnoreCase));
        }


    }
}
