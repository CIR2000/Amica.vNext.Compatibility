using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Amica.vNext.Compatibility.Maps
{
    public class DataRelationMapping : FieldMapping
    {
		public DataRelationMapping()
        {
            ColumnName = "Id";
        }
		public Type PropertyType { get; set; }
		public string RelationName { get; set; }
    }
}
