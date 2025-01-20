namespace Apstory.Scaffold.Model.Config
{
    public class CSharpConfig
    {
        public CSharpNamespaces Namespaces { get; set; }
        public CSharpDirectories Directories { get; set; }

        public CSharpConfig(string solutionDirectory, string rootNamespace, string sqlProjectFile)
        {
            Namespaces = new CSharpNamespaces(rootNamespace);
            Directories = new CSharpDirectories(solutionDirectory, rootNamespace, sqlProjectFile);
        }
    }
}
