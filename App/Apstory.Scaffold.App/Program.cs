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
                { "-namespace", "namespace" }
            })
            .Build();

        string overrideSqlProjectPath = configuration["sqlproject"];
        string overrideNamespace = configuration["namespace"];

        Console.WriteLine($"Override Sql Project Path: {overrideSqlProjectPath}");
        Console.WriteLine($"Override Namespace: {overrideNamespace}");

        string basePath = Environment.CurrentDirectory;
        if (overrideSqlProjectPath is null)
            overrideSqlProjectPath = SearchForSqlProjectFile(basePath);

        Logger.LogDebug($"Using SQL Project: {overrideSqlProjectPath}");
        var csharpConfig = SetupProjectConfiguration(basePath, overrideSqlProjectPath, overrideNamespace);

        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton(csharpConfig);
                services.AddSingleton(new LockingService());

                services.AddTransient<SqlDalRepositoryScaffold>();
                services.AddTransient<SqlScriptFileScaffold>();
                services.AddTransient<SqlModelScaffold>();
                services.AddTransient<SqlDalRepositoryInterfaceScaffold>();
                services.AddTransient<SqlDomainServiceScaffold>();
                services.AddTransient<SqlDomainServiceInterfaceScaffold>();

                services.AddHostedService<SqlScaffoldWorker>();
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
