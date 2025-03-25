using System.Diagnostics;

namespace Apstory.Scaffold.Model.Typescript
{
    [DebuggerDisplay("[{PropertyName}: {PropertyType}]")]
    public class TSProperty
    {
        public string PropertyName { get; set; }
        public string PropertyType { get; set; }
    }
}
