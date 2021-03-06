﻿using Amica.vNext.Models.ItalianPA;

namespace Amica.vNext.Compatibility.Maps
{
    internal class VatMapping : Mapping
    {
        internal VatMapping()
        {
            Fields.Add("Id", new FieldMapping {PropertyName = "UniqueId"});
            Fields.Add("Nome", new FieldMapping { PropertyName = "Name"});
			Fields.Add("Codice", new FieldMapping {PropertyName = "Code"});
            Fields.Add("Aliquota", new FieldMapping { PropertyName = "Rate" });
            Fields.Add("Indeducibilità", new FieldMapping { PropertyName = "NonDeductible" });
            Fields.Add("IsIntracomunitaria", new FieldMapping { PropertyName = "IsIntraCommunity" });
            Fields.Add("IsSplitPayment", new FieldMapping { PropertyName = "IsSplitPayment" });

            Parents.Add(
                "Natura",
                new DataRelationMapping
                {
                    PropertyName = "NaturaPA",
					ParentColumn = "Natura",
					ChildProperty = "Code",
					UpstreamTransform = (key, row, obj) => PAHelpers.NaturaPA[(string)row[key]],
                });
        }
    }
}
