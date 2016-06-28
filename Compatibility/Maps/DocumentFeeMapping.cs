using Amica.vNext.Models;

namespace Amica.vNext.Compatibility.Maps
{
    internal class DocumentFeeMapping : Mapping
    {
        internal DocumentFeeMapping() : base()
        {
            Fields.Add("ImportoNetto", new FieldMapping {PropertyName = "Amount"});
            Fields.Add("IsPagamento", new FieldMapping { PropertyName = "IsFromPayment"});

            Parents.Add(
                "IdSpesa",
                new DataRelationMapping
                {
					ParentColumn = "Nome",
					PropertyName = "Name",
					RelationName = "FK_Spese_SpeseDocumenti",
                });

            Parents.Add(
                "IdCausaleIVA",
                new DataRelationMapping
                {
					ParentColumn = "Codice",
					ChildProperty = "Code",
                    PropertyName = "Vat",
					ChildType = typeof(Vat),
					RelationName = "FK_CausaliIVA_SpeseDocumenti"
                });

        }
    }
}
