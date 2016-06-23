using System;

namespace Amica.vNext.Compatibility.Maps
{
    public class FieldMapping
    {
		public FieldMapping()
        {
            UpstreamTransform = (x, obj) => x;
            DownstreamTransform = x => x;
        }
        public string PropertyName { get; set; }
        public string ChildProperty { get; set; }
        public string ParentColumn { get; set; }
        //public Func<object, object> UpstreamTransform { get; set; }
        public Func<object, object> DownstreamTransform { get; set; }
		public Func<object, object, object> UpstreamTransform { get; set; }
    }

}
