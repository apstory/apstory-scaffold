using Apstory.Scaffold.App.Worker;
using Apstory.Scaffold.Domain.Scaffold;
using Apstory.Scaffold.Domain.Service;
using Apstory.Scaffold.Domain.Util;
using Apstory.Scaffold.Model.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.RegularExpressions;

class Program
{
    static void Main(string[] args)
    {
        // Build configuration from command-line arguments
        var configuration = new ConfigurationBuilder()
            .AddCommandLine(args, new Dictionary<string, string> {
                { "-sqlproject", "sqlproject" },
                { "-namespace", "namespace" },
                { "-regen", "regen" },
                { "-clean", "clean" },
                { "-sqlpush", "sqlpush" },
                { "-sqldestination", "sqldestination" },
                { "-help", "help" },
            })
            .Build();

        if (args.Contains("-help"))
        {
            Console.WriteLine("Available Command-Line Switches:");
            Console.WriteLine("-sqlproject <path>       : Overrides the SQL project path instead of letting the application search for it.");
            Console.WriteLine("-namespace <name>        : Overrides the namespace for scaffolded code instead of fetching it from the sqlproj.");
            Console.WriteLine("-regen <params>          : Executes immediate regeneration of files. Will regenerate all found schemas when no additional information supplied. Can specify a schema 'dbo', a table 'dbo.tablename', or a procedure 'dbo.zgen_procname' to regenerate.");
            Console.WriteLine("-sqlpush <params>        : Pushes changes to database. Can specify a schema 'dbo', a table 'dbo.tablename', or a procedure 'dbo.zgen_procname' to push. Requires -sqldestination switch as well");
            Console.WriteLine("-sqldestination <params> : Pushes changes to database. This is the connection string of the database.");
            Console.WriteLine("-clean                   : Deletes existing generated files.");

            return;
        }

        string overrideSqlProjectPath = configuration["sqlproject"];
        string overrideNamespace = configuration["namespace"];
        var regenerate = args.Contains("-regen");
        var clean = args.Contains("-clean");
        var sqlpush = args.Contains("-sqlpush");

        if (clean & regenerate)
        {
            Console.WriteLine("Only specify clean or regen.");

            return;
        }

        Console.WriteLine($"Override Sql Project Path: {overrideSqlProjectPath}");
        Console.WriteLine($"Override Namespace: {overrideNamespace}");

        string basePath = Environment.CurrentDirectory;
        if (overrideSqlProjectPath is null)
            overrideSqlProjectPath = SearchForSqlProjectFile(basePath);

        Logger.LogDebug($"Using SQL Project: {overrideSqlProjectPath}");
        var csharpConfig = SetupProjectConfiguration(basePath, overrideSqlProjectPath, overrideNamespace);

        var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddConfiguration(configuration); // Merge command-line args into DI config
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

                if (clean)
                    services.AddHostedService<SqlScaffoldCleanupWorker>();
                else if (regenerate)
                    services.AddHostedService<SqlScaffoldRegenerationWorker>();
                else if (sqlpush)
                    services.AddHostedService<SqlUpdateWorker>();
                else
                    services.AddHostedService<SqlScaffoldWatcherWorker>();
            })
            .Build();

        host.Run();
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
        {
            Logger.LogError("No SQL Projects found. Aborting");
            throw new Exception("No SQL Projects found. Aborting");
        }

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
}
