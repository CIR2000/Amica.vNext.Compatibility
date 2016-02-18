using System.Collections.Generic;

namespace Amica.vNext.Compatibility.Maps
{
    internal interface IMapping
    {
        Dictionary<string, FieldMapping> Fields { get; }
		Dictionary<string, DataRelationMapping> Parents { get; }
		List<DataRelationMapping> Children { get; }
    }
}
