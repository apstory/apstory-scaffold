using Apstory.Scaffold.App.Worker;
using Apstory.Scaffold.Domain.Scaffold;
using Apstory.Scaffold.Domain.Service;
using Apstory.Scaffold.Domain.Util;
using Apstory.Scaffold.Model.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

class Program
{
    static void Main(string[] args)
    {
        // Build configuration from command-line arguments
        try
        {
            var parsedArgs = ParseArgs(args);
            var configuration = new ConfigurationBuilder()
                .AddCommandLine(parsedArgs, new Dictionary<string, string> {
                { "-sqlproject", "sqlproject" },
                { "-namespace", "namespace" },
                { "-regen", "regen" },
                { "-delete", "delete" },
                { "-sqlpush", "sqlpush" },
                { "-sqldestination", "sqldestination" },
                { "-variant", "variant" },
                { "-help", "help" },
                { "-tsModel", "tsmodel" },
                { "-ngSearchPage", "ngsearchpage" },
                { "-tsdalfolder", "tsdalfolder" },
                })
                .Build();

            if (args.Contains("-help"))
            {
                Console.WriteLine("Available Command-Line Switches:");
                Console.WriteLine("-sqlproject <path>       : Overrides the SQL project path instead of letting the application search for it.");
                Console.WriteLine("-namespace <name>        : Overrides the namespace for scaffolded code instead of fetching it from the sqlproj.");
                Console.WriteLine("-regen <params>          : Executes immediate regeneration of files. Will regenerate all found schemas when no additional information supplied. Can specify a schema 'dbo', a table 'dbo.tablename', or a procedure 'dbo.zgen_procname' to regenerate. Can send multiple entities with ;");
                Console.WriteLine("-delete <params>         : Deletes all generated entries. Can leave empty, specify a table 'dbo.tablename', or a procedure 'dbo.zgen_procname' to delete. Can send multiple entities with ;");
                Console.WriteLine("-sqlpush <params>        : Pushes changes to database. Can leave empty to detect git changes. Specify a table 'dbo.tablename' (Limited Functionality), or a procedure 'dbo.zgen_procname' to push. Requires -sqldestination switch as well. Please note: No table updates are pushed, only the initial creates can be pushed. Can send multiple entities with ;");
                Console.WriteLine("-sqldestination <params> : Pushes changes to database. This is the connection string of the database.");
                Console.WriteLine("-variant <params>        : Possible variants: 'merge' - merge will cause InsUpd procs to be generated with a merge statement that allows users to insert their own id on uniqueidentifiers");
                Console.WriteLine("-tsmodel                 : Typescript model to read structure from.");
                Console.WriteLine("-ngsearchpage            : Location to generate angular search page to.");
                Console.WriteLine("-tsdalfolder             : Location to generate typescript dal service to.");

                return;
            }

            string overrideSqlProjectPath = configuration["sqlproject"];
            string overrideNamespace = configuration["namespace"];
            var delete = args.Contains("-delete");
            var regenerate = args.Contains("-regen");
            var sqlpush = args.Contains("-sqldestination");
            var ngSearchPage = args.Contains("-ngsearchpage");
            var tsDalFolder = args.Contains("-tsdalfolder");

            int flags = 0;
            if (args.Contains("-regen")) flags = (flags << 1) | 1;
            if (args.Contains("-sqldestination")) flags = (flags << 1) | 1;
            if (args.Contains("-ngsearchpage")) flags = (flags << 1) | 1;
            if (args.Contains("-tsdalfolder")) flags = (flags << 1) | 1;
            if (args.Contains("-delete")) flags = (flags << 1) | 1;

            //Ensure 0 or only 1 flag is set
            if (flags > 1)
            {
                Console.WriteLine("Only specify one of the following: clean, regen, sqldestination, ngsearchpage, tsdalfolder or delete");
                return;
            }

            Console.WriteLine($"Override Sql Project Path: {overrideSqlProjectPath}");
            Console.WriteLine($"Override Namespace: {overrideNamespace}");

            CSharpConfig csharpConfig = new CSharpConfig();
            if (!tsDalFolder)
            {

                string basePath = Environment.CurrentDirectory;
                if (overrideSqlProjectPath is null)
                    overrideSqlProjectPath = SearchForSqlProjectFile(basePath);

                Logger.LogDebug($"Using SQL Project: {overrideSqlProjectPath}");
                csharpConfig = SetupProjectConfiguration(basePath, overrideSqlProjectPath, overrideNamespace);
            }

            var host = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddConfiguration(configuration); // Merge command-line args into DI config
                })
                .ConfigureLogging(logging =>
                {
                    logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.None); // Suppress hosting logs
                })
                .ConfigureServices((context, services) =>
                {
                    services.AddMemoryCache();

                    services.AddSingleton(csharpConfig);
                    services.AddSingleton<LockingService>();
                    services.AddSingleton<SqlTableCachingService>();

                    services.AddTransient<SqlDalRepositoryScaffold>();
                    services.AddTransient<SqlScriptFileScaffold>();
                    services.AddTransient<SqlProjectScaffold>();
                    services.AddTransient<SqlModelScaffold>();
                    services.AddTransient<SqlDalRepositoryInterfaceScaffold>();
                    services.AddTransient<SqlDomainServiceScaffold>();
                    services.AddTransient<SqlDomainServiceInterfaceScaffold>();
                    services.AddTransient<SqlForeignDomainServiceScaffold>();
                    services.AddTransient<SqlForeignDomainServiceInterfaceScaffold>();
                    services.AddTransient<SqlDalRepositoryServiceCollectionExtensionScaffold>();
                    services.AddTransient<SqlDomainServiceServiceCollectionExtensionScaffold>();
                    services.AddTransient<SqlLiteScaffold>();

                    if (regenerate)
                        services.AddHostedService<SqlScaffoldRegenerationWorker>();
                    else if (delete)
                        services.AddHostedService<SqlScaffoldDeleteWorker>();
                    else if (sqlpush)
                        services.AddHostedService<SqlUpdateWorker>();
                    else if (ngSearchPage)
                        services.AddHostedService<TypescriptSearchPageWorker>();
                    else if (tsDalFolder)
                        services.AddHostedService<SqlLiteWorker>();
                    else
                        services.AddHostedService<SqlScaffoldWatcherWorker>();
                })
                .Build();

            host.Run();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.Message);
        }
    }

    private static string SearchForSqlProjectFile(string directory)
    {
        Logger.LogInfo($"Searching for SQL Projects in '{directory}'");
        if (!Directory.Exists(directory))
        {
            Logger.LogError($"Directory does not exist: '{directory}'");
            throw new Exception("Directory does not exist");
        }

        var sqlProjFiles = Directory.EnumerateFiles(directory, "*.sqlproj", SearchOption.AllDirectories);

        if (!sqlProjFiles.Any())
            throw new Exception("No SQL Project found. Aborting");

        var sqlProjFile = sqlProjFiles.First();
        if (sqlProjFiles.Count() > 1)
        {
            sqlProjFile = sqlProjFiles.FirstOrDefault(s => s.Contains("/DB/") || s.Contains(@"\DB\"));
            if (sqlProjFile is null)
                sqlProjFile = sqlProjFiles.First();

            Logger.LogWarn($"Multiple SQL Projects found, Selected: {sqlProjFile}");
        }

        return sqlProjFile;
    }

    private static CSharpConfig SetupProjectConfiguration(string rootDirectory, string sqlProjectFile, string overrideNamespace)
    {
        Logger.LogDebug("Configuring Project");
        string rootNamespace = string.Empty;
        if (string.IsNullOrWhiteSpace(overrideNamespace))
        {
            var projectTxt = FileUtils.SafeReadAllText(sqlProjectFile);

            var rootNamespaceRx = Regex.Match(projectTxt, @"<RootNamespace>(.*)</RootNamespace>", RegexOptions.Singleline);
            rootNamespace = rootNamespaceRx.Groups[1].Value.Replace(".DB", "");
        }
        else
            rootNamespace = overrideNamespace;

        var csharpConfig = new CSharpConfig(rootDirectory, rootNamespace, sqlProjectFile);

        return csharpConfig;
    }

    private static string[] ParseArgs(string[] args)
    {
        var parsedArgs = new List<string>();
        string lastKey = null;

        foreach (var arg in args)
        {
            if (arg.StartsWith("-")) // It's a key
            {
                if (lastKey != null) // Previous key had no value, treat it as a flag
                {
                    parsedArgs.Add(lastKey);
                    parsedArgs.Add("");
                }
                lastKey = arg;
            }
            else // It's a value
            {
                if (lastKey != null)
                {
                    parsedArgs.Add(lastKey);
                    parsedArgs.Add(arg);
                    lastKey = null;
                }
                else
                {
                    // Unexpected value without a key (ignore or handle error)
                }
            }
        }

        // Handle trailing flag
        if (lastKey != null)
        {
            parsedArgs.Add(lastKey);
            parsedArgs.Add("");
        }

        return parsedArgs.ToArray();
    }
}
