using System;
using System.Data;

namespace Amica.vNext.Compatibility.Maps
{
    public class FieldMapping
    {
		public FieldMapping()
        {
            UpstreamTransform = (key, row, obj) => row[key];
            DownstreamTransform = x => x;
        }
        public string PropertyName { get; set; }
        public string ChildProperty { get; set; }
        public string ParentColumn { get; set; }
        public Func<object, object> DownstreamTransform { get; set; }
		public Func<string, DataRow, object, object> UpstreamTransform { get; set; }
    }

}
