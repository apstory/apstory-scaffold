namespace Apstory.Scaffold.Model.Config
{
    public class CSharpNamespaces
    {
        public string RootNamespace { get; set; }
        public string ModelNamespace { get; set; }
        public string CommonNamespace { get; set; }
        public string DomainNamespace { get; set; }
        public string DalNamespace { get; set; }
        public string DomainInterfaceNamespace { get; set; }
        public string DalInterfaceNamespace { get; set; }

        public CSharpNamespaces(string rootNamespace)
        {
            RootNamespace = rootNamespace;
            ModelNamespace = $"{rootNamespace}.Model.#SCHEMA#";
            CommonNamespace = $"{rootNamespace}.Common";
            DomainNamespace = $"{rootNamespace}.Domain.#SCHEMA#";
            DalNamespace = $"{rootNamespace}.Dal.#SCHEMA#";
            DomainInterfaceNamespace = $"{DomainNamespace}.Interface.#SCHEMA#";
            DalInterfaceNamespace = $"{DalNamespace}.Interface.#SCHEMA#";
        }
    }
}
