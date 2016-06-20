using Amica.vNext.Models;

namespace Amica.vNext.Compatibility.Maps
{
    internal class ContactDetailsExMapping : Mapping
    {

        internal ContactDetailsExMapping()
        {
            Fields.Add("Id", new FieldMapping {PropertyName = "UniqueId"});
            Fields.Add("RagioneSociale1", new FieldMapping { PropertyName = "Name"});
			Fields.Add("http", new FieldMapping {PropertyName = "WebSite"});
			Fields.Add("Telefono1", new FieldMapping {PropertyName = "Phone"});
			Fields.Add("Telefono2", new FieldMapping {PropertyName = "Mobile"});
			Fields.Add("Fax", new FieldMapping {PropertyName = "Fax"});
			Fields.Add("Email", new FieldMapping {PropertyName = "Mail"});
        }
    }

}
