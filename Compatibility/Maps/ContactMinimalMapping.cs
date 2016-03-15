namespace Amica.vNext.Compatibility.Maps
{
    internal class ContactMinimalMapping : Mapping
    {
        internal ContactMinimalMapping()
        {
            Fields.Add("Id", new FieldMapping {PropertyName = "UniqueId"});
            Fields.Add("RagioneSociale1", new FieldMapping { PropertyName = "Name"});
			Fields.Add("Indirizzo", new FieldMapping {PropertyName = "Street"});
            Fields.Add("PartitaIVA", new VatIdNumberFieldMapping());

            Parents.Add(
                "IdNazione",
                new DataRelationMapping
                {
                    PropertyName = "Country",
                    ColumnName = "Nome",
                    RelationName = "FK_Nazioni_Anagrafiche",
                    //FieldType = typeof (Country)
                }
				);

        }
    }
}
