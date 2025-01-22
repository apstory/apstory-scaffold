using Apstory.Scaffold.Model.Enum;

namespace Apstory.Scaffold.Model.Sql
{
    public class SqlConstraint
    {
        public string ConstraintName { get; set; }
        public ConstraintType ConstraintType { get; set; }
        public string Column { get; set; }
        public string RefSchema { get; set; }
        public string RefTable { get; set; }
        public string RefColumn { get; set; }
    }
}
