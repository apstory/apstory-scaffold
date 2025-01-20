namespace Apstory.Scaffold.Model.Sql
{
    public class SqlColumn
    {
        public string ColumnName { get; set; }
        public string DataType { get; set; }
        public string DataTypeLength { get; set; }
        public string DefaultValue { get; set; }
        public bool IsNullable { get; set; }
        public bool IsReadonly { get; set; }
    }
}
