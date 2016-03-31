using System;
using System.Collections.Generic;
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
    }

    public class DataRelationMapping<TKey, TValue> : DataRelationMapping
    {
		public IDictionary<TKey, TValue> TargetCollection { get; set; }
    }
}
