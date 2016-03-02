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
                        TargetType = typeof(ContactMinimal)
                    }
                );

            Children.Add(
                new DataRelationMapping
                {
                    PropertyName = "Items",
                    TargetType = typeof(DocumentItem),
                    RelationName = "FK_Documenti_Righe",
                }
			);
        }
    }
}
