using Amica.vNext.Models;

namespace Amica.vNext.Compatibility.Maps
{
    internal class ContactMapping : Mapping
    {

        internal ContactMapping()
        {
            Fields.Add("Id", new FieldMapping {PropertyName = "UniqueId"});
            Fields.Add("RagioneSociale1", new FieldMapping { PropertyName = "Name"});
			Fields.Add("Codice", new FieldMapping {PropertyName = "IdCode"});
			Fields.Add("CodiceFiscale", new FieldMapping {PropertyName = "TaxIdentificationNumber"});
            Fields.Add("PartitaIVA", new VatIdNumberFieldMapping());
			Fields.Add("IndicePA", new FieldMapping {PropertyName = "PublicAdministrationIndex"});
			Fields.Add("Indirizzo", new FieldMapping {PropertyName = "Address.Street"});
			Fields.Add("http", new FieldMapping {PropertyName = "Address.WebSite"});

			Fields.Add("BancaNome", new FieldMapping {PropertyName = "Bank.Name"});
			Fields.Add("BancaIBAN", new FieldMapping {PropertyName = "Bank.IbanCode"});

			Fields.Add("IsAttivo", new FieldMapping {PropertyName = "Is.Active"});
			Fields.Add("IsPersonaGiuridica", new FieldMapping {PropertyName = "Is.Company"});
			Fields.Add("IsCliente", new FieldMapping {PropertyName = "Is.Client"});
			Fields.Add("IsFornitore", new FieldMapping {PropertyName = "Is.Vendor"});
			Fields.Add("IsAgente", new FieldMapping {PropertyName = "Is.Agent"});
			Fields.Add("IsVettore", new FieldMapping {PropertyName = "Is.Courier"});
			Fields.Add("IsCapoArea", new FieldMapping {PropertyName = "Is.AreaManager"});

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

			Parents.Add(
                "IdValuta",
                new DataRelationMapping
                {
                    PropertyName = "Currency",
					KeyField = "Name",
                    ColumnName = "Nome",
                    RelationName = "FK_Valute_Anagrafiche",
					TargetType = typeof(Currency)
                } );

            Children.Add(
                new DataRelationMapping
                {
                    PropertyName = "OtherAddresses",
                    TargetType = typeof(AddressExWithName),
                    RelationName = "FK_Anagrafiche_Indirizzi",
                }
			);
        }
    }

}
