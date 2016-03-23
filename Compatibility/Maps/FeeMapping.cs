using Amica.vNext.Models;

namespace Amica.vNext.Compatibility.Maps
{
    internal class FeeMapping : Mapping
    {
        internal FeeMapping()
        {
            Fields.Add("Id", new FieldMapping {PropertyName = "UniqueId"});
            Fields.Add("Nome", new FieldMapping { PropertyName = "Name"});
            Fields.Add("Importo", new FieldMapping { PropertyName = "Amount" });

            Parents.Add(
                "IdCausaleIVA",
                new DataRelationMapping
                {
					ParentColumn = "Codice",
                    PropertyName = "Vat",
					ChildProperty = "Code",
					ChildType = typeof(Vat),
					RelationName = "FK_CausaliIVA_Spese"
                });
        }
    }
}
