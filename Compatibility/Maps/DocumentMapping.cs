using Amica.vNext.Models;
using Amica.vNext.Models.Documents;

namespace Amica.vNext.Compatibility.Maps
{
    internal class DocumentMapping : Mapping
    {
        internal DocumentMapping() : base()
        {
            Fields.Add("Data", new FieldMapping {FieldName = "Date"});
            Fields.Add("IdTipoDocumento", new FieldMapping { FieldName = "Type"});
			Fields.Add("TotaleFattura", new FieldMapping {FieldName = "Total"});

            Parents.Add(
                    "IdAnagrafica",
                    new DataRelationMapping {
                        FieldName = "Contact",
                        RelationName = "FK_Anagrafiche_Documenti",
                        FieldType = typeof(ContactMinimal)
                    }
                );

            Children.Add(
                new DataRelationMapping
                {
                    FieldName = "Items",
                    FieldType = typeof(DocumentItem),
                    RelationName = "FK_Documenti_Righe",
                }
			);
        }
    }
}
