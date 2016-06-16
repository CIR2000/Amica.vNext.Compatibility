using Amica.vNext.Compatibility.Helpers;
using Amica.vNext.Models;
using Amica.vNext.Models.Documents;

namespace Amica.vNext.Compatibility.Maps
{
    internal class DocumentMapping : Mapping
    {
        internal DocumentMapping()
        {
            Fields.Add("Data", new FieldMapping {PropertyName = "Date"});
            Fields.Add("NumeroParteNumerica", new FieldMapping {PropertyName = "Number.Numeric"});
            Fields.Add("NumeroParteTesto", new FieldMapping {PropertyName = "Number.String"});
            Fields.Add("RitenutaAcconto", new FieldMapping {PropertyName = "WithholdingTax.Rate"});
            Fields.Add("RitenutaAccontoSuImponibile", new FieldMapping {PropertyName = "WithholdingTax.TaxableShare"});
            Fields.Add("RitenutaAccontoImporto", new FieldMapping {PropertyName = "WithholdingTax.Amount"});
            Fields.Add("IsRitenutaIncludeCassaPrevidenziale", new FieldMapping {PropertyName = "WithholdingTax.IsSocialSecurityIncluded"});
            Fields.Add("CassaPrevidenziale", new FieldMapping { PropertyName = "SocialSecurity[0].Rate" });
            Fields.Add("CassaPrevidenzialeImporto", new FieldMapping { PropertyName = "SocialSecurity[0].Amount" });
            Fields.Add("CassaPrevidenzialeNome", new FieldMapping
            {
                PropertyName = "SocialSecurity[0].Category",
                DownstreamTransform = (x) => SocialSecurityAdapter.GetAmicaDescription((SocialSecurityCategory)x),
                UpstreamTransform = (x) => SocialSecurityAdapter.GetSocialSecurityCategory((string)x)
            });

            Parents.Add(
                "IdTipoDocumento", 
				new DataRelationMapping {
					PropertyName="Category",
					ParentColumn = "IdTipoDocumento",
					ChildProperty = "Code",
                    UpstreamTransform = (x) => DocumentHelpers.Categories[(DocumentCategory)x],
                });

            Parents.Add(
                "Stato", 
				new DataRelationMapping {
					PropertyName="Status",
					ParentColumn = "Stato",
					ChildProperty = "Code",
                    UpstreamTransform = (x) => DocumentHelpers.Statuses[(DocumentStatus)x],
                });

            Parents.Add(
                "IdValuta", 
				new DataRelationMapping {
					PropertyName="Currency",
					ChildProperty = "Name",
					ParentColumn = "Nome",
					RelationName = "FK_Valute_Documenti",
					ChildType = typeof(Currency)
                });

            Parents.Add(
                "IdCausaleDocumento", 
				new DataRelationMapping {
                    PropertyName = "Reason",
                    ParentColumn = "Nome",
                    RelationName = "FK_CausaliDocumenti_Documenti",
                });

            Parents.Add(
                "IdIVACassaPrevidenziale",
                new DataRelationMapping
                {
                    ParentColumn = "Nome",
                    ChildProperty = "Code",
                    PropertyName = "SocialSecurity[0].Vat",
                    RelationName = "FK_CausaliIVA_IVACassaPrevidenziale",
                    ChildType = typeof(Vat),
                });

            Parents.Add(
                "IdAnagrafica", new DataRelationMapping
                {
                    PropertyName = "BillTo",
                    RelationName = "FK_Anagrafiche_Documenti",
                    ChildType = typeof(BillingAddress)
                }
                );

            Parents.Add(
                "IdPagamento", new DataRelationMapping
                {
                    PropertyName = "Payment",
					ParentColumn = "Nome",
					ChildProperty = "Name",
                    RelationName = "FK_Pagamenti_Documenti",
                    ChildType = typeof(Payment)
                }
                );

            //         Children.Add(
            //             new DataRelationMapping
            //             {
            //                 PropertyName = "Items",
            //                 ChildType = typeof(DocumentItem),
            //                 RelationName = "FK_Documenti_Righe",
            //             }
            //);
        }
    }
}
