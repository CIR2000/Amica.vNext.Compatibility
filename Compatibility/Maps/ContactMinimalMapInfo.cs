using System.Collections.Generic;
using Amica.vNext.Models;

namespace Amica.vNext.Compatibility.Maps
{
    internal class ContactMinimalMapInfo : Dictionary<string, MapInfo>
    {
        internal ContactMinimalMapInfo()
        {
            Add("Id", new MapInfo {Destination = "UniqueId"});
            Add("RagioneSociale1", new MapInfo {Destination = "Name"});
            Add("Indirizzo", new MapInfo {Destination = "Address"});
            Add("PartitaIVA", new MapInfo {Destination = "Vat"});
        }
    }
}
