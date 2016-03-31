using Amica.vNext.Models.ItalianPA;

namespace Amica.vNext.Compatibility.Maps
{
    internal class PaymentMethodMapping : Mapping
    {
        internal PaymentMethodMapping()
        {
            Fields.Add("Id", new FieldMapping {PropertyName = "UniqueId"});
            Fields.Add("Nome", new FieldMapping { PropertyName = "Name"});
            Fields.Add("IsRiBa", new FieldMapping { PropertyName = "IsBankReceipt" });

            Parents.Add(
                "CodicePagamentoPA",
                new DataRelationMapping<string, object>()
                {
                    PropertyName = "ModalitaPagamentoPA",
					ParentColumn = "CodicePagamentoPA",
                    TargetCollection = PACollections.ModalitaPagamentoPA,
					ChildProperty = "Code"
                });
        }
    }
}
