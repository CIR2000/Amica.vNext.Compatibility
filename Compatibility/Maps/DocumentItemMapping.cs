using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Amica.vNext.Models;
using Amica.vNext.Models.Documents;

namespace Amica.vNext.Compatibility.Maps
{
    internal class DocumentItemMapping : Mapping
    {
        internal DocumentItemMapping() : base()
        {
            Fields.Add("CodiceArticolo", new FieldMapping {PropertyName = "Detail.Sku"});
            Fields.Add("Descrizione", new FieldMapping { PropertyName = "Detail.Description"});
            Fields.Add("Colore", new FieldMapping { PropertyName = "Detail.Color"});
            Fields.Add("UnitàMisura", new FieldMapping { PropertyName = "Detail.UnitOfMeasure"});
            Fields.Add("TagExtra", new FieldMapping { PropertyName = "Detail.Notes"});
            Fields.Add("TagData", new FieldMapping { PropertyName = "Detail.Lot.Expiration" });
            Fields.Add("Tag", new FieldMapping
            {
                PropertyName = "Detail",
                DownstreamTransform = (x) => SetTag(x),
                //UpstreamTransform = (x, obj) => SocialSecurityAdapter.GetSocialSecurityCategory((string)x)
            });

            Fields.Add("Sconto1", new FieldMapping
            {
                PropertyName = "VariationCollection",
                DownstreamTransform = (x) => SetSconto(x, 1),
                UpstreamTransform = (x, obj) => SetScontoVariation(x, obj),
            });
            Fields.Add("Sconto2", new FieldMapping
            {
                PropertyName = "VariationCollection",
                DownstreamTransform = (x) => SetSconto(x, 2),
                UpstreamTransform = (x, obj) => SetScontoVariation(x, obj),
            });
            Fields.Add("Sconto3", new FieldMapping
            {
                PropertyName = "VariationCollection",
                DownstreamTransform = (x) => SetSconto(x, 3),
                UpstreamTransform = (x, obj) => SetScontoVariation(x, obj),
            });
            Fields.Add("Sconto4", new FieldMapping
            {
                PropertyName = "VariationCollection",
                DownstreamTransform = (x) => SetSconto(x, 4),
                UpstreamTransform = (x, obj) => SetScontoVariation(x, obj),
            });
            Fields.Add("ScontoIncondizionato", new FieldMapping
            {
                PropertyName = "VariationCollection",
                DownstreamTransform = (x) => SetScontoIncondizionato(x),
                UpstreamTransform = (x, obj) => SetScontoIncondizionatoVariation(x, obj),
            });

            Fields.Add("Quantità", new FieldMapping { PropertyName = "Quantity"});
            Fields.Add("QuantitàEvasa", new FieldMapping { PropertyName = "ProcessedQuantity"});
            Fields.Add("Prezzo", new FieldMapping { PropertyName = "Price"});
            Fields.Add("PrezzoNetto", new FieldMapping { PropertyName = "NetPrice"});
            Fields.Add("PrezzoIvato", new FieldMapping { PropertyName = "PriceVatInclusive"});
            Fields.Add("ImportoNetto", new FieldMapping { PropertyName = "Total"});
            Fields.Add("IsRitenuta", new FieldMapping { PropertyName = "WithholdingTax"});

            Parents.Add(
                "IdCausaleIVA",
                new DataRelationMapping
                {
					ParentColumn = "Codice",
					ChildProperty = "Code",
                    PropertyName = "Vat",
					ChildType = typeof(Vat),
					RelationName = "FK_CausaliIVA_Righe"
                });


            Parents.Add(
                "IdMagazzino",
                new DataRelationMapping
                {
					ParentColumn = "Nome",
					ChildProperty = "Name",
                    PropertyName = "Warehouse",
					ChildType = typeof(Warehouse),
					RelationName = "FK_Magazzini_Righe"
                });
        }
		internal static object SetScontoIncondizionatoVariation(object value, object obj)
        {
            var sconto = Convert.ToDecimal(value);
            var item = (DocumentItem)obj;
			if (sconto != 0)
            {
				item.VariationCollection.Add(
					new Variation
					{
						Amount = sconto,
						Category = new VariationCategory { Category = DocumentVariation.Discount }
					});
            }
            return item.VariationCollection;
        }
		internal static object SetScontoVariation(object value, object obj)
        {
            var sconto = (double)value;
            var item = (DocumentItem)obj;
			if (sconto != 0)
            {
				item.VariationCollection.Add(
					new Variation
					{
						Rate = sconto,
						Category = new VariationCategory { Category = DocumentVariation.Discount }
					});
            }
            return item.VariationCollection;
        }
		internal static object SetSconto(object o, int slot)
        {
            var variations = (List<Variation>)o;
            var currentSlot = 1;

			foreach (var v in variations)
            {
                if (v.Category.Category == DocumentVariation.Discount) {
                    if (currentSlot == slot)
                    {
                        return v.Rate;
                    }
                    else
                    {
                        currentSlot += 1;
                    }
                }
            }
            return 0;
        }
		internal static object SetScontoIncondizionato (object o)
        {
            var variations = (List<Variation>)o;

			foreach (var v in variations)
            {
                if (v.Category.Category == DocumentVariation.Discount && v.Amount > 0)
                {
                    return v.Amount;
                }
            }
            return 0;
        }

		internal static object SetTag(object o)
        {
            var detail = (DocumentItemDetail)o;
            return (detail.SerialNumber != null) ? detail.SerialNumber : (detail.Size != null) ? detail.Size.Number : detail.Lot.Number;
        }
    }
}
