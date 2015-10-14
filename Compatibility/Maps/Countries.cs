using System.Collections.Generic;

namespace Amica.vNext.Compatibility.Maps
{
    class Countries : Dictionary<string, string>
    {
        public Countries()
        {
            Add("Nome", "Name");
        }
    }
}
