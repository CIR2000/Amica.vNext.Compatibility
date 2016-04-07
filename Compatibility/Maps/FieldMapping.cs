using System;

namespace Amica.vNext.Compatibility.Maps
{
    public class FieldMapping
    {
		public FieldMapping()
        {
            Transform = x => x;
        }
        public string PropertyName { get; set; }
        public string ChildProperty { get; set; }
        public string ParentColumn { get; set; }
        public Func<object, object> Transform { get; set; }
    }

}
