using System.Collections.Generic;
using Amica.vNext.Models;

namespace Amica.vNext.Compatibility.Maps
{
    internal class DocumentMapInfo : Dictionary<string, MapInfo>
    {
        internal DocumentMapInfo()
        {
            Add("Data", new MapInfo {Destination = "Date"});
            Add("IdTipoDocumento", new MapInfo {Destination = "Type"});
            Add("TotaleFattura", new MapInfo {Destination = "Total"});
            Add("IdAnagrafica", new MapInfo
            {
                Destination = "Contact",
                ParentRelation = "FK_Anagrafiche_Documenti",
                ParentType = typeof (ContactMinimal)
            });
        }
    }
}
