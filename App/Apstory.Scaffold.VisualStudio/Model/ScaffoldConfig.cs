namespace Apstory.Scaffold.VisualStudio.Model
{
    public class ScaffoldConfig
    {
        public string Namespace { get; set; }
        public string SqlProject { get; set; }
        public string SqlDestination { get; set; }
        public string Variant { get; set; }
        public string PowershellScript { get; set; } = "gen-typescript.ps1";
    }
}
