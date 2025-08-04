using Apstory.Scaffold.Domain.Parser;
using Apstory.Scaffold.Domain.Scaffold;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Apstory.Scaffold.App.Worker
{
    public class SqlLiteWorker : BackgroundService
    {
        private readonly IHostApplicationLifetime _lifetime;
        private readonly IConfiguration _configuration;
        private readonly SqlLiteScaffold _sqlLiteScaffold;

        public SqlLiteWorker(IHostApplicationLifetime lifetime,
                             IConfiguration configuration,
                             SqlLiteScaffold sqlLiteScaffold)
        {
            _lifetime = lifetime;
            _configuration = configuration;
            _sqlLiteScaffold = sqlLiteScaffold;
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var tsModelPath = _configuration["tsmodel"];
            var dalFolder = _configuration["tsdalfolder"];

            var tsModel = TypeScriptModelParser.ParseModelFile(tsModelPath);

            tsModel.Properties = tsModel.Properties.Where(p => !p.PropertyType.Equals("SqlGeography", StringComparison.OrdinalIgnoreCase))
                                                   .ToList();
            var results = await _sqlLiteScaffold.GenerateCode(tsModel, dalFolder);
            
            _lifetime.StopApplication();
        }
    }
}
