namespace Amica.vNext.Compatibility.Maps
{
    internal class WarehouseMapping : Mapping
    {

        internal WarehouseMapping()
        {
            Fields.Add("Id", new FieldMapping {PropertyName = "UniqueId"});
			Fields.Add("Note", new FieldMapping {PropertyName = "Notes"});
            Fields.Add("Nome", new FieldMapping { PropertyName = "Name"});
			Fields.Add("Indirizzo", new FieldMapping {PropertyName = "Address.Street"});
			Fields.Add("Località", new FieldMapping {PropertyName = "Address.Town"});
            Fields.Add("CAP", new FieldMapping {PropertyName = "Address.PostalCode"});
			Fields.Add("Provincia", new FieldMapping {PropertyName = "Address.StateOrProvince"});
        }
    }

}
