using Apstory.Scaffold.Domain.Parser;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Apstory.Scaffold.App.Worker
{
    public class TypescriptSearchPageWorker : BackgroundService
    {
        private readonly IHostApplicationLifetime _lifetime;
        private readonly IConfiguration _configuration;

        public TypescriptSearchPageWorker(IHostApplicationLifetime lifetime,
                                          IConfiguration configuration)
        {
            _lifetime = lifetime;
            _configuration = configuration;
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var tsModelPath = _configuration["tsmodel"];
            var outputPath = _configuration["ngsearchpage"];

            var tsModel = TypeScriptModelParser.ParseModelFile(tsModelPath);

            //TODO: Generate Search Page based on the tsModel

            _lifetime.StopApplication();
        }
    }
}
