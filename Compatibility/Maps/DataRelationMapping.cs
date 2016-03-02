using System;

namespace Amica.vNext.Compatibility.Maps
{
    public class DataRelationMapping : FieldMapping
    {
		public DataRelationMapping()
        {
            ColumnName = "Id";
        }
		public Type TargetType { get; set; }
		public string RelationName { get; set; }
    }
}
