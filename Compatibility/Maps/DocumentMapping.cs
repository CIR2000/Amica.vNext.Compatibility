using Amica.vNext.Models;
using Amica.vNext.Models.Documents;

namespace Amica.vNext.Compatibility.Maps
{
    internal class DocumentMapping : Mapping
    {
        internal DocumentMapping()
        {
            Fields.Add("Data", new FieldMapping {PropertyName = "Date"});
            Fields.Add("IdTipoDocumento", new FieldMapping { PropertyName = "Type"});
			Fields.Add("TotaleFattura", new FieldMapping {PropertyName = "Total"});

            Parents.Add(
                    "IdAnagrafica",
                    new DataRelationMapping {
                        PropertyName = "Contact",
                        RelationName = "FK_Anagrafiche_Documenti",
                        ChildType = typeof(BillingAddress)
                    }
                );

            Children.Add(
                new DataRelationMapping
                {
                    PropertyName = "Items",
                    ChildType = typeof(DocumentItem),
                    RelationName = "FK_Documenti_Righe",
                }
			);
        }
    }
}
