using System;
using System.Collections.Generic;
namespace Amica.vNext.Compatibility.Maps
{
    internal class CountryMapInfo : Dictionary<string, MapInfo>
    {
        internal CountryMapInfo()
        {
            Add("Nome", new MapInfo {Destination = "Name"});
        }
    }
}
