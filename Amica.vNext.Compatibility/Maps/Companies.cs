using System.Collections.Generic;

namespace Amica.vNext.Compatibility.Maps
{
    class Companies : Dictionary<string, string>
    {
        public Companies()
        {
            Add("Nome", "Name");
        }
    }
}
