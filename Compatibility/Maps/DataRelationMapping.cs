using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Amica.vNext.Compatibility.Maps
{
    public class DataRelationMapping : FieldMapping
    {
		public Type FieldType { get; set; }
		public string RelationName { get; set; }
    }
}
