using System.Collections.Generic;

namespace Amica.vNext.Compatibility.Maps
{
    internal class CompanyMapInfo : Dictionary<string, MapInfo>
    {
        internal CompanyMapInfo()
        {
            Add("Nome", new MapInfo {Destination = "Name"});
        }
    }
}
