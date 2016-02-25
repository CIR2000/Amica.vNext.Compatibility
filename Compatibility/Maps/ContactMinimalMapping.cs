namespace Amica.vNext.Compatibility.Maps
{
    internal class ContactMinimalMapping : Mapping
    {
        internal ContactMinimalMapping()
        {
            Fields.Add("Id", new FieldMapping {FieldName = "UniqueId"});
            Fields.Add("RagioneSociale1", new FieldMapping { FieldName = "Name"});
			Fields.Add("Indirizzo", new FieldMapping {FieldName = "Street"});
			Fields.Add("PartitaIVA", new FieldMapping {FieldName = "Vat"});
        }
    }
}
