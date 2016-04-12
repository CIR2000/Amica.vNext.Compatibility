using Amica.vNext.Models;
using Amica.vNext.Models.Documents;

namespace Amica.vNext.Compatibility.Maps
{
    internal class DocumentMapping : Mapping
    {
        internal DocumentMapping()
        {
            Fields.Add("Data", new FieldMapping {PropertyName = "Date"});
			//Fields.Add("TotaleFattura", new FieldMapping {PropertyName = "Total"});

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
