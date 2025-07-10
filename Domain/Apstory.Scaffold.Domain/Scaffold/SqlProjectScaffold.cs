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

                //Determine the namespace for MSBuild elements
                XName buildKeyword = "Build";
                var itemGroup = doc.Descendants("ItemGroup").FirstOrDefault(group => group.Elements("Build").Any());
                if (itemGroup is null)
                {
                    XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";
                    itemGroup = doc.Descendants(ns + "ItemGroup").FirstOrDefault(group => group.Elements(ns + "Build").Any());

                    if (itemGroup is not null)
                        buildKeyword = ns + "Build";
                }

                if (itemGroup is null)
                {
                    itemGroup = new XElement("ItemGroup");
                    doc.Root.Add(itemGroup);
                }

                var allBuildEntries = doc.Descendants(buildKeyword);
                foreach (var path in newPaths)
                {
                    var normalizedPath = path.Replace($"{_config.Directories.DBDirectory}\\", string.Empty);

                    var exists = allBuildEntries.Any(s => s.Attribute("Include") is not null &&
                                                          s.Attribute("Include").Value.Equals(normalizedPath));

                    if (!exists)
                    {
                        itemGroup.Add(new XElement(buildKeyword, new XAttribute("Include", normalizedPath)));
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
