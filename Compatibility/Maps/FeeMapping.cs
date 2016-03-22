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
                    PropertyName = "Vat",
					KeyField = "Code",
					ColumnName = "Codice",
					TargetType = typeof(Vat),
					RelationName = "FK_CausaliIVA_Spese"
                });
        }
    }
}
