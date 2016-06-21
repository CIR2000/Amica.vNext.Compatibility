using System;
using System.Collections.Generic;
using System.Data;
using Amica.Data;
using Amica.vNext.Models;

namespace Amica.vNext.Compatibility.Maps
{
    public class DataRelationMapping : FieldMapping
    {
		public DataRelationMapping()
        {
            ParentColumn = "Id";
        }
		public Type ChildType { get; set; }
		public string RelationName { get; set; }
        public new Func<object, DataRow, object> DownstreamTransform { get; set; }
    }
}
