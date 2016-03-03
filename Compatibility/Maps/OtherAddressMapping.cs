using Amica.vNext.Models;

namespace Amica.vNext.Compatibility.Maps
{
    internal class OtherAddressMapping : Mapping
    {
        internal OtherAddressMapping()
        {
            Fields.Add("RagioneSociale1", new FieldMapping { PropertyName = "Name"});
			Fields.Add("Indirizzo", new FieldMapping {PropertyName = "Street"});
			Fields.Add("Località", new FieldMapping {PropertyName = "Town"});
			Fields.Add("CAP", new FieldMapping {PropertyName = "PostalCode"});
			Fields.Add("Provincia", new FieldMapping {PropertyName = "StateOrProvince"});
			Fields.Add("Telefono1", new FieldMapping {PropertyName = "Phone"});
			Fields.Add("Telefono2", new FieldMapping {PropertyName = "Mobile"});
			Fields.Add("Fax", new FieldMapping {PropertyName = "Fax"});
			Fields.Add("Email", new FieldMapping {PropertyName = "Mail"});
        }
    }

}
