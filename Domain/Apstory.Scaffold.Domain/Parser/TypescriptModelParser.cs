using Apstory.Scaffold.Domain.Util;
using Apstory.Scaffold.Model.Typescript;
using System.Text.RegularExpressions;

namespace Apstory.Scaffold.Domain.Parser
{
    public static class TypeScriptModelParser
    {
        private static readonly Regex PropertyRegex = new(@"\s*(?:public\s+)?(\w+)\??:\s*(\w+);", RegexOptions.Compiled);

        public static TSModel ParseModelFile(string filePath)
        {
            var lines = File.ReadAllLines(filePath);
            var model = new TSModel();
            var classNameMatch = Regex.Match(lines.FirstOrDefault(l => l.Contains("export class")) ?? "", @"export class (\w+)");

            if (!classNameMatch.Success)
                throw new Exception("Could not determine class name.");

            model.TSModelName = classNameMatch.Groups[1].Value;

            foreach (var line in lines)
            {
                var match = PropertyRegex.Match(line);
                if (!match.Success) continue;

                string name = match.Groups[1].Value;
                string type = match.Groups[2].Value;

                if (name.Equals("totalRows", StringComparison.OrdinalIgnoreCase)) continue;

                string relatedClass = FindRelatedClass(lines, name);
                if (name.EndsWith("Id"))
                {
                    if (string.IsNullOrWhiteSpace(model?.PrimaryKey?.PropertyName) && relatedClass.Equals("Unknown"))
                        model.PrimaryKey = new TSProperty() { PropertyName = name, PropertyType = type };
                    else
                        model.ForeignKeys[name] = relatedClass;

                    model.Properties.Add(new TSProperty() { PropertyName = name, PropertyType = type });
                }
                else if (relatedClass.Equals("Unknown"))
                    model.Properties.Add(new TSProperty() { PropertyName = name, PropertyType = type });
            }

            return model;
        }

        private static string FindRelatedClass(string[] lines, string foreignKey)
        {
            // Look for an import statement that could match this foreign key
            string referenceName = foreignKey.Replace("Id", "").ToPascalCase();
            foreach (var line in lines)
            {
                if (line.Contains($"{{ {referenceName} }}"))
                {
                    return referenceName;
                }
            }
            return "Unknown";
        }
    }
}