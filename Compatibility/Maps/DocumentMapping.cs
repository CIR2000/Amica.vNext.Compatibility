using Amica.vNext.Models;
using Amica.vNext.Models.Documents;

namespace Amica.vNext.Compatibility.Maps
{
    internal class DocumentMapping : Mapping
    {
        internal DocumentMapping()
        {
            Fields.Add("Data", new FieldMapping {PropertyName = "Date"});
            Fields.Add("RitenutaAcconto", new FieldMapping {PropertyName = "WithholdingTax.Rate"});
            Fields.Add("RitenutaAccontoSuImponibile", new FieldMapping {PropertyName = "WithholdingTax.TaxableShare"});
            Fields.Add("RitenutaAccontoImporto", new FieldMapping {PropertyName = "WithholdingTax.Amount"});
            Fields.Add("IsRitenutaIncludeCassaPrevidenziale", new FieldMapping {PropertyName = "WithholdingTax.IsSocialSecurityIncluded"});
            //Fields.Add("CassaPrevidenziale", new FieldMapping {PropertyName = "SocialSecurity.Rate"});
            //Fields.Add("CassaPrevidenzialeImporto", new FieldMapping {PropertyName = "SocialSecurity.Amount"});
            //Fields.Add("CassaPrevidenzialeNome", new FieldMapping {PropertyName = "SocialSecurity.Name"});

            Parents.Add(
                "IdTipoDocumento", 
				new DataRelationMapping {
					PropertyName="Category",
					ParentColumn = "IdTipoDocumento",
					ChildProperty = "Code",
                    Transform = (x) => DocumentHelpers.Categories[(DocumentCategory)x],
                });

            Parents.Add(
                "Stato", 
				new DataRelationMapping {
					PropertyName="Status",
					ParentColumn = "Stato",
					ChildProperty = "Code",
                    Transform = (x) => DocumentHelpers.Statuses[(DocumentStatus)x],
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

     //       Parents.Add(
     //           "IdIVACassaPrevidenziale",
     //           new DataRelationMapping
     //           {
					//ParentColumn = "Nome",
					//ChildProperty = "Code",
     //               PropertyName = "SocialSecurity.Vat",
					//ChildType = typeof(Vat),
					//RelationName = "FK_CausaliIVA_IVACassaPrevidenziale"
     //           });
   //         Parents.Add(
   //             "IdAnagrafica", 
			//	new DataRelationMapping {
   //                 PropertyName = "Contact",
   //                 RelationName = "FK_Anagrafiche_Documenti",
   //                     ChildType = typeof(BillingAddress)
   //             }
   //             );

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
