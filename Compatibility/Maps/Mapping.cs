using System.Collections.Generic;

namespace Amica.vNext.Compatibility.Maps
{
    internal abstract class Mapping : IMapping
    {
        protected readonly Dictionary<string, FieldMapping> _fields;
        protected readonly Dictionary<string, DataRelationMapping> _parents;
        protected readonly List<DataRelationMapping> _children;
		
		public Mapping()
        {
            _fields = new Dictionary<string, FieldMapping>();
            _parents = new Dictionary<string, DataRelationMapping>();
            _children = new List<DataRelationMapping>();
        }

        public List<DataRelationMapping> Children { get { return _children; } }
        public Dictionary<string, FieldMapping> Fields { get { return _fields; } }
        public Dictionary<string, DataRelationMapping> Parents { get { return _parents; } }
    }
}
