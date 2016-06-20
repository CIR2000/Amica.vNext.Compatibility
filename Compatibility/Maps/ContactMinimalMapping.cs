namespace Amica.vNext.Compatibility.Maps
{
    internal class ContactMinimalMapping : Mapping
    {
        internal ContactMinimalMapping()
        {
            Fields.Add("Id", new FieldMapping {PropertyName = "UniqueId"});
            Fields.Add("RagioneSociale1", new FieldMapping { PropertyName = "Name"});
			Fields.Add("Indirizzo", new FieldMapping {PropertyName = "Street"});
            Fields.Add("CodiceFiscale", new FieldMapping { PropertyName = "TaxIdentificationNumber" });
            Fields.Add("PartitaIVA", new VatIdNumberFieldMapping());
            Fields.Add("Località", new FieldMapping { PropertyName = "Town" });
            Fields.Add("CAP", new FieldMapping { PropertyName = "PostalCode" });
            Fields.Add("Provincia", new FieldMapping { PropertyName = "StateOrProvince" });

            Parents.Add(
                "IdNazione",
                new DataRelationMapping
                {
                    PropertyName = "Country",
                    ParentColumn = "Nome",
                    RelationName = "FK_Nazioni_Anagrafiche",
                }
				);

        }
    }
}
