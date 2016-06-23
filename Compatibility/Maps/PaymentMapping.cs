using Amica.vNext.Models;

namespace Amica.vNext.Compatibility.Maps
{
    internal class PaymentMapping : Mapping
    {

        internal PaymentMapping()
        {
            Fields.Add("Id", new FieldMapping {PropertyName = "UniqueId"});
            Fields.Add("Nome", new FieldMapping { PropertyName = "Name"});
			Fields.Add("Sconto", new FieldMapping {PropertyName = "Discount"});
			Fields.Add("InizioScadenze", new FieldMapping {PropertyName = "FirstPaymentDateAdditionalDays"});
			Fields.Add("Periodicità", new FieldMapping {PropertyName = "InstallmentsEveryNumberOfDays"});
			Fields.Add("Rate", new FieldMapping {PropertyName = "Installments"});
			Fields.Add("FineMese", new FieldMapping {PropertyName = "ForceEndOfMonth"});
			Fields.Add("GiorniExtra", new FieldMapping {PropertyName = "ExtraDays"});
			Fields.Add("GiorniEsatti", new FieldMapping {PropertyName = "ExactDays"});

            Parents.Add(
                "IdSpesa",
                new DataRelationMapping
                {
					ParentColumn = "Nome",
					ChildProperty = "Name",
                    PropertyName = "Fee",
                    RelationName = "FK_Spese_Pagamenti",
					ChildType = typeof(Fee)
                });

            Parents.Add(
                "IdBanca",
                new DataRelationMapping
                {
					ParentColumn = "Nome",
					ChildProperty = "Name",
                    PropertyName = "Bank",
                    RelationName = "FK_Banche_Pagamenti",
					ChildType = typeof(Bank)
                });

            Parents.Add(
                "IdModalitàPagamento",
                new DataRelationMapping
                {
					ParentColumn = "Nome",
					ChildProperty ="Name",
                    PropertyName = "PaymentMethod",
                    RelationName = "FK_ModalitàPagamento_Pagamenti",
					ChildType = typeof(PaymentMethod)
                });

            Parents.Add(
                "PeriodoPrimaRata",
                new DataRelationMapping
                {
                    PropertyName = "FirstPaymentDate",
					ParentColumn = "PeriodoPrimaRata",
					ChildProperty = "Code",
                    UpstreamTransform = (x, obj) => PaymentHelpers.FirstPaymentDates[(PaymentDate)x],
                });

            Parents.Add(
                "TipoPrimaRata",
                new DataRelationMapping
                {
                    PropertyName = "FirstPaymentOption",
                    ParentColumn = "TipoPrimaRata",
                    ChildProperty = "Code",
                    UpstreamTransform = (x, obj) => PaymentHelpers.FirstPaymentOptions[(PaymentOption)x]
                });
        }
    }

}
