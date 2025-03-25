namespace Apstory.Scaffold.Model.Typescript
{
    public class TSModel
    {
        public string TSModelName { get; set; } = string.Empty;
        public TSProperty PrimaryKey { get; set; } = new();
        public List<TSProperty> Properties { get; set; } = new();
        public Dictionary<string, string> ForeignKeys { get; set; } = new();
    }
}
