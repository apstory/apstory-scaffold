namespace Apstory.Scaffold.Model.Config
{
    public class CSharpDirectories
    {
        public string SolutionDirectory { get; set; }
        public string ModelDirectory { get; set; }
        public string CommonDirectory { get; set; }
        public string DomainDirectory { get; set; }
        public string DalDirectory { get; set; }
        public string DomainInterfaceDirectory { get; set; }
        public string DalInterfaceDirectory { get; set; }
        public string DBDirectory { get; set; }

        public CSharpDirectories(string solutionDirectory, string rootNamespace, string sqlProjectFile)
        {
            DBDirectory = Path.GetDirectoryName(sqlProjectFile);
            SolutionDirectory = solutionDirectory;
            CommonDirectory = Path.Combine(solutionDirectory, "Common", $"{rootNamespace}.Common");
            ModelDirectory = Path.Combine(solutionDirectory, "Model", $"{rootNamespace}.Model", "#SCHEMA#");

            DomainDirectory = Path.Combine(solutionDirectory, "Domain", $"{rootNamespace}.Domain", "Gen");
            DomainInterfaceDirectory = Path.Combine(solutionDirectory, "Domain", $"{rootNamespace}.Domain.Interface", "Gen");
            DalDirectory = Path.Combine(solutionDirectory, "Dal", $"{rootNamespace}.Dal", "#SCHEMA#", "Gen");
            DalInterfaceDirectory = Path.Combine(solutionDirectory, "Dal", $"{rootNamespace}.Dal.Interface", "#SCHEMA#", "Gen");
        }
    }
}
