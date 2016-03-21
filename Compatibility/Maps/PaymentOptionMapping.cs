﻿using Amica.vNext.Models.ItalianPA;

namespace Amica.vNext.Compatibility.Maps
{
    internal class PaymentOptionMapping : Mapping
    {
        internal PaymentOptionMapping()
        {
            Fields.Add("Id", new FieldMapping {PropertyName = "UniqueId"});
            Fields.Add("Nome", new FieldMapping { PropertyName = "Name"});
            Fields.Add("IsRiBa", new FieldMapping { PropertyName = "IsRiBa" });

            Parents.Add(
                "CodicePagamentoPA",
                new DataRelationMapping
                {
                    PropertyName = "ModalitaPagamentoPA",
					ColumnName = "CodicePagamentoPA",
                    TargetCollection = PACollections.ModalitaPagamentoPA,
					KeyField = "Code"
                });
        }
    }
}
