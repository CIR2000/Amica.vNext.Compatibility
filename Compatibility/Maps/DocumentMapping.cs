﻿using System;
using System.Collections.Generic;
using System.Data;
using Amica.Data;
using Amica.vNext.Compatibility.Helpers;
using Amica.vNext.Models;
using Amica.vNext.Models.Documents;
using static Amica.Data.companyDataSet;

namespace Amica.vNext.Compatibility.Maps
{
    internal class DocumentMapping : Mapping
    {
        internal DocumentMapping()
        {
            Fields.Add("Data", new FieldMapping {PropertyName = "Date"});
            Fields.Add("NumeroParteNumerica", new FieldMapping {PropertyName = "Number.Numeric"});
            Fields.Add("NumeroParteTesto", new FieldMapping {PropertyName = "Number.String"});
            Fields.Add("DataValidità", new FieldMapping {PropertyName = "ExpirationDate"});
            Fields.Add("DataInizioScadenze", new FieldMapping {PropertyName = "Payment.BaseDateForPayments"});
            Fields.Add("BancaNome", new FieldMapping {PropertyName = "Bank.Name"});
            Fields.Add("BancaIBAN", new FieldMapping {PropertyName = "Bank.IbanCode"});
            Fields.Add("RitenutaAcconto", new FieldMapping {PropertyName = "WithholdingTax.Rate"});
            Fields.Add("RitenutaAccontoSuImponibile", new FieldMapping {PropertyName = "WithholdingTax.TaxableShare"});
            Fields.Add("RitenutaAccontoImporto", new FieldMapping {PropertyName = "WithholdingTax.Amount"});
            Fields.Add("IsRitenutaIncludeCassaPrevidenziale", new FieldMapping {PropertyName = "WithholdingTax.IsSocialSecurityIncluded"});
            Fields.Add("CassaPrevidenziale", new FieldMapping { PropertyName = "SocialSecurityCollection[0].Rate" });
            Fields.Add("CassaPrevidenzialeImporto", new FieldMapping { PropertyName = "SocialSecurityCollection[0].Amount" });
            Fields.Add("CassaPrevidenzialeNome", new FieldMapping
            {
                PropertyName = "SocialSecurityCollection[0].Category",
                DownstreamTransform = (x) => SocialSecurityAdapter.GetAmicaDescription((SocialSecurityCategory)x),
                UpstreamTransform = (key, row, obj) => SocialSecurityAdapter.GetSocialSecurityCategory((string)row[key])
            });

            Fields.Add("Abbuono", new FieldMapping {PropertyName = "Rebate"});


            Fields.Add("Colli", new FieldMapping { PropertyName = "Shipping.Volume" });
            Fields.Add("Peso", new FieldMapping { PropertyName = "Shipping.Weight" });
            Fields.Add("PesoUM", new FieldMapping { PropertyName = "Shipping.UnitOfMeasure" });
            Fields.Add("AspettoBeni", new FieldMapping { PropertyName = "Shipping.Appearance" });
            Fields.Add("AutistaNome", new FieldMapping { PropertyName = "Shipping.Driver.Name" });
            Fields.Add("AutistaPatente", new FieldMapping { PropertyName = "Shipping.Driver.LicenseID" });
            Fields.Add("AutistaTarga", new FieldMapping { PropertyName = "Shipping.Driver.PlateID" });

            Fields.Add("DataTrasporto", new FieldMapping { PropertyName = "Shipping.Date" });
            Fields.Add("OraTrasporto", new FieldMapping
            {
                PropertyName = "Shipping.Date",
                UpstreamTransform = (key, row, obj) => SetOraTrasportoUpstream(row[key], obj),
            });

            Fields.Add("Sconto", new FieldMapping
            {
                PropertyName = "VariationCollection",
                DownstreamTransform = (x) => SetSconto(x),
				UpstreamTransform = (key, row, obj) => SetScontoVariation(row[key], obj)
            });
            Fields.Add("ScontoIncondizionato", new FieldMapping
            {
                PropertyName = "VariationCollection",
                DownstreamTransform = (x) => SetScontoIncondizionato(x),
				UpstreamTransform = (key, row, o) => SetScontoIncondizionatoVariation(row[key], o)
            });
            Fields.Add("ScontoPagamento", new FieldMapping
            {
                PropertyName = "VariationCollection",
                DownstreamTransform = (x) => SetScontoPagamento(x),
				UpstreamTransform = (key, row, o) => SetScontoPagamentoVariation(row[key], o)
            });
            Fields.Add("Cambio", new FieldMapping { PropertyName = "Currency.ExchangeRate" });
            Fields.Add("Note", new FieldMapping { PropertyName = "Notes" });

            Parents.Add(
                "Porto",
                new DataRelationMapping
                {
                    PropertyName = "Shipping.Terms",
                    ParentColumn = "Porto",
                    ChildProperty = "Code",
                    UpstreamTransform = (key, row, obj) => DocumentHelpers.TransportTerms[(DocumentShippingTerm)row[key]]
                });

            Parents.Add(
                "MezzoTrasporto",
                new DataRelationMapping
                {
                    PropertyName = "Shipping.TransportMode",
                    ParentColumn = "MezzoTrasporto",
                    ChildProperty = "Code",
                    UpstreamTransform = (key, row, obj) => DocumentHelpers.TransportModes[(DocumentTransportMode)row[key]]
                });

            Parents.Add(
                "IdVettore",
                new DataRelationMapping
                {
                    PropertyName = "Shipping.Courier",
                    RelationName = "FK_Anagrafiche_Documenti2",
                    ChildType = typeof(ContactDetailsEx)
                });

            Parents.Add(
                "IdTipoDocumento", 
				new DataRelationMapping {
					PropertyName="Category",
					ParentColumn = "IdTipoDocumento",
					ChildProperty = "Code",
                    UpstreamTransform = (key, row, obj) => DocumentHelpers.Categories[(DocumentCategory)row[key]],
                });

            Parents.Add(
                "Stato", 
				new DataRelationMapping {
					PropertyName="Status",
					ParentColumn = "Stato",
					ChildProperty = "Code",
                    UpstreamTransform = (key, row, obj) => DocumentHelpers.Statuses[(DocumentStatus)row[key]],
                });

            Parents.Add(
                "IdValuta", 
				new DataRelationMapping {
					PropertyName="Currency.Current",
					ChildProperty = "Name",
					ParentColumn = "Nome",
					RelationName = "FK_Valute_Documenti",
					ChildType = typeof(Currency)
                });

            Parents.Add(
                "IdCausaleDocumento", 
				new DataRelationMapping {
                    PropertyName = "Reason",
                    ParentColumn = "Nome",
                    RelationName = "FK_CausaliDocumenti_Documenti",
                });

            Parents.Add(
                "IdIVACassaPrevidenziale",
                new DataRelationMapping
                {
                    ParentColumn = "Nome",
                    ChildProperty = "Code",
                    PropertyName = "SocialSecurityCollection[0].Vat",
                    RelationName = "FK_CausaliIVA_IVACassaPrevidenziale",
                    ChildType = typeof(Vat),
                });

            Parents.Add(
                "IdAnagrafica", new DataRelationMapping
                {
                    PropertyName = "BillTo",
                    RelationName = "FK_Anagrafiche_Documenti",
                    ChildType = typeof(BillingAddress)
                });

            Parents.Add(
                "IdDestinazione", new DataRelationMapping
                {
                    PropertyName = "ShipTo",
                    RelationName = "FK_Indirizzi_Documenti",
                    ChildType = typeof(ShippingAddress),
                    DownstreamTransform = (doc, row) => GetIndirizziId(doc, row)
                });

            Parents.Add(
                "IdAgente", new DataRelationMapping
                {
                    PropertyName = "Agent",
                    RelationName = "FK_Anagrafiche_Documenti1",
                    ChildType = typeof(ContactDetailsEx)
                }); 

            Parents.Add(
                "IdPagamento", new DataRelationMapping
                {
                    PropertyName = "Payment.Current",
					ParentColumn = "Nome",
					ChildProperty = "Name",
                    RelationName = "FK_Pagamenti_Documenti",
                    ChildType = typeof(Payment)
                });


            Children.Add(
                new DataRelationMapping
                {
                    PropertyName = "ItemCollection",
                    RelationName = "FK_Documenti_Righe",
                    ChildType = typeof(DocumentItem),
                });

            Children.Add(
                new DataRelationMapping
                {
                    PropertyName = "FeeCollection",
                    RelationName = "FK_Documenti_SpeseDocumenti",
                    ChildType = typeof(DocumentFee),
                });
        }

		internal static object SetScontoVariation(object value, object obj)
        {
            var sconto = (double)value;
            var document = (Document)obj;
			if (sconto != 0)
            {
				document.VariationCollection.Add(
					new Variation
					{
						Rate = sconto,
						Category = new VariationCategory { Category = DocumentVariation.Discount }
					});
            }
            return document.VariationCollection;
        }

		internal static object SetScontoIncondizionatoVariation(object value, object obj)
        {
            var sconto = Convert.ToDecimal(value);
            var document = (Document)obj;
			if (sconto != 0)
            {
				document.VariationCollection.Add(
					new Variation
					{
						Amount = sconto,
						Category = new VariationCategory { Category = DocumentVariation.Discount }
					});
            }
            return document.VariationCollection;
        }
		internal static object SetScontoPagamentoVariation(object value, object obj)
        {
            var sconto = (double)value;
            var document = (Document)obj;
			if (sconto != 0)
            {
				document.VariationCollection.Add(
					new Variation
					{
						Rate = sconto,
						Category = new VariationCategory { Category = DocumentVariation.PaymentDiscount }
					});
            }
            return document.VariationCollection;
        }
		internal static object SetSconto(object c)
        {
            var l = (List<Variation>)c;
			foreach (var v in l)
            {
                if (v.Category.Category == DocumentVariation.Discount && v.Rate > 0)
                    return v.Rate;
            }
            return 0;
        }

		internal static object SetScontoIncondizionato(object c)
        {
            var l = (List<Variation>)c;
			foreach (var v in l)
            {
                if (v.Category.Category == DocumentVariation.Discount && v.Amount > 0)
                    return v.Amount;
            }
            return 0;
        }
		internal static object SetScontoPagamento(object c)
        {
            var l = (List<Variation>)c;
			foreach (var v in l)
            {
                if (v.Category.Category == DocumentVariation.PaymentDiscount)
                    return v.Rate;
            }
            return 0;
        }
		internal static object SetOraTrasportoUpstream(object value, object obj)
        {
            var document = (Document)obj;
            if (document.Shipping == null) return DBNull.Value;

            var date = document.Shipping.Date;
            if (date == null) return DBNull.Value;
            var zeroTimeDate = date.Subtract(new TimeSpan(date.Hour, date.Minute, date.Second));

            var time = (DateTime)value;

            return zeroTimeDate.Add(new TimeSpan(time.Hour, time.Minute, time.Second));
        }
		internal static object GetIndirizziId(object d, object row)
        {
            var document = (Document)d;
            if (document.ShipTo == null) return DBNull.Value;

            var documentiRow = ((DocumentiRow)row);

            var target = document.ShipTo.Street;
            if (target == null) return DBNull.Value;

            var indirizziRows = documentiRow.AnagraficheRowByFK_Anagrafiche_Documenti.GetIndirizziRows();

			foreach (var i in indirizziRows)
            {
                if (i.Indirizzo == document.ShipTo.Street && i.Località == document.ShipTo.Town)
                    return i.Id;
            }

            var indirizzi = ((companyDataSet)documentiRow.Table.DataSet).Indirizzi;
            var indir = indirizzi.NewIndirizziRow();
            indir.IdAnagrafica = documentiRow.IdAnagrafica;
            indir.Indirizzo = document.ShipTo.Street;
            indir.Località = document.ShipTo.Town;
            indir.Telefono1 = document.ShipTo.Phone;
            indir.Telefono2 = document.ShipTo.Mobile;
            indir.Fax = document.ShipTo.Fax;
            indir.Email = document.ShipTo.Mail;
            indir.CAP = document.ShipTo.PostalCode;
            indir.IsAttivo = true;
            indir.Provincia = document.ShipTo.StateOrProvince;
            indir.RagioneSociale1 = document.ShipTo.Name;
            indirizzi.AddIndirizziRow(indir);
            return indir.Id;
        }
    }
}
