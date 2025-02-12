using Apstory.Scaffold.Domain.Service;
using Apstory.Scaffold.Domain.Util;
using Apstory.Scaffold.Model.Config;
using Apstory.Scaffold.Model.Enum;
using System.Xml.Linq;

namespace Apstory.Scaffold.Domain.Scaffold
{
    public partial class SqlProjectScaffold
    {
        private readonly CSharpConfig _config;
        private readonly LockingService _lockingService;

        public SqlProjectScaffold(CSharpConfig csharpConfig, LockingService lockingService)
        {
            _config = csharpConfig;
            _lockingService = lockingService;
        }

        public async Task<ScaffoldResult> GenerateCode(List<string> newPaths)
        {
            ScaffoldResult result = ScaffoldResult.Skipped;
            try
            {
                await _lockingService.AcquireLockAsync(_config.SqlProjectFile);

                XDocument doc = XDocument.Load(_config.SqlProjectFile);
                XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";

                //Find or Add ItemGroup to put Build includes into
                var itemGroup = doc.Descendants(ns + "ItemGroup").FirstOrDefault(group => group.Elements(ns + "Build").Any());
                if (itemGroup is null)
                {
                    itemGroup = new XElement(ns + "ItemGroup");
                    doc.Root.Add(itemGroup);
                }

                var allBuildEntries = doc.Descendants(ns + "Build");
                foreach (var path in newPaths)
                {
                    var normalizedPath = path.Replace($"{_config.Directories.DBDirectory}\\", string.Empty);

                    var exists = allBuildEntries.Any(s => s.Attribute("Include") is not null &&
                                                          s.Attribute("Include").Value.Equals(normalizedPath));

                    if (!exists)
                    {
                        itemGroup.Add(new XElement(ns + "Build", new XAttribute("Include", normalizedPath)));
                        result = ScaffoldResult.Updated;
                    }
                }

                doc.Save(_config.SqlProjectFile);
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                _lockingService.ReleaseLock(_config.SqlProjectFile);
            }
            return result;
        }

    }
}
