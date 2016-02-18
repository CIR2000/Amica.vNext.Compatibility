using System.Collections.Generic;
using Amica.vNext.Models;

namespace Amica.vNext.Compatibility.Maps
{
    internal class ContactMapping : Mapping
    {
        internal ContactMapping() : base()
        {
            Fields.Add("Id", new FieldMapping {FieldName = "UniqueId"});
            Fields.Add("RagioneSociale1", new FieldMapping { FieldName = "Name"});
			Fields.Add("Indirizzo", new FieldMapping {FieldName = "Address"});
			Fields.Add("PartitaIVA", new FieldMapping {FieldName = "Vat"});
        }
    }

}
