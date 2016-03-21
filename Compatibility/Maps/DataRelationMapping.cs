using System;
using Amica.vNext.Models;

namespace Amica.vNext.Compatibility.Maps
{
    public class DataRelationMapping : FieldMapping
    {
		public DataRelationMapping()
        {
            ColumnName = "Id";
        }
		public Type TargetType { get; set; }
		public ReadOnlyDictionary<string, object> TargetCollection { get; set; }
		public string RelationName { get; set; }
    }
}
