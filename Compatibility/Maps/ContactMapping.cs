namespace Amica.vNext.Compatibility.Maps
{
    internal class ContactMapping : Mapping
    {
        internal ContactMapping()
        {
            Fields.Add("Id", new FieldMapping {PropertyName = "UniqueId"});
            Fields.Add("RagioneSociale1", new FieldMapping { PropertyName = "Name"});
			Fields.Add("PartitaIVA", new FieldMapping {PropertyName = "Vat"});
			Fields.Add("Indirizzo", new FieldMapping {PropertyName = "Address.Street"});

            Parents.Add(
                "IdNazione",
                new DataRelationMapping
                {
                    PropertyName = "Address.Country",
                    ColumnName = "Nome",
                    RelationName = "FK_Nazioni_Anagrafiche",
                });

			Parents.Add(
                "IdAreaGeografica",
                new DataRelationMapping
                {
                    PropertyName = "MarketArea",
                    ColumnName = "Nome",
                    RelationName = "FK_AreeGeografiche_Anagrafiche",
                } );
        }
    }

}
