using Apstory.Scaffold.Domain.Parser;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Apstory.Scaffold.App.Worker
{
    public class SqlLiteWorker : BackgroundService
    {
        private readonly IHostApplicationLifetime _lifetime;
        private readonly IConfiguration _configuration;

        public SqlLiteWorker(IHostApplicationLifetime lifetime,
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

            //If TSModel does not have IsSynced -> It should be added

            //Base class:
            //Static in base class -> SetDeviceUID(userdeviceId) -> Creates the first part of guid and saves it.
            //Create newUUID()
            //  -> DeviceId+Timestamp+Anything else (normal guid generation)

            //TODO: Generate sqlite screen :-o
            //InsUpd() -> Base class to create new id when not present
            //Get()
            //GetByPKId
            //foreach GetByFKId

            //Count(isSynced)
            //foreach CountByFKId(id, isSynced)

            _lifetime.StopApplication();
        }
    }
}
