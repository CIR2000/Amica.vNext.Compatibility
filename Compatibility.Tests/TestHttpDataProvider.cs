﻿using Amica.Data;
using Eve;
using NUnit.Framework;
using SQLite;
using System;
using System.Data;
using System.Net;
using System.Net.Http;
using Amica.vNext.Models;
using Amica.vNext.Models.Documents;
using Amica.vNext.Models.ItalianPA;
using System.Collections.Generic;
using Amica.vNext.Compatibility.Helpers;

namespace Amica.vNext.Compatibility.Tests
{

    [TestFixture]
    public class TestHttpDataProvider 
    {
        private SQLiteConnection _db;
		private HttpDataProvider _httpDataProvider;

		// Adam has a 1s resolutoin when it comes to If-Modfied-Since datetimes.
		private const int SleepLength = 1000;

        // We are running Windows in a VirtualBox VM so in order to access the OSX Host 'localhost'
        // where a local instance of the REST API is running, we use standard 10.0.2.2:5000
        private const string Service = "http://10.0.2.2:5000/";
        private const string DbName = "HttpSync.db";

        [SetUp]
        public void Init()
        {
            // ensure the file does not exist before instantiaton.
            //File.Delete(DbName);

            _db = new SQLiteConnection(DbName);
            _db.DeleteAll<HttpMapping>();

            _httpDataProvider = new HttpDataProvider()
            {
                ClientId = Environment.GetEnvironmentVariable("SentinelClientId"),
                Username = Environment.GetEnvironmentVariable("SentinelUsername"),
                Password = Environment.GetEnvironmentVariable("SentinelPassword"),
            };

            DefaultFactories.Bootstrap();
        }

        [TearDown]
        public void TearDown()
        {
            if (_db != null) 
                _db.Dispose();

			if (_httpDataProvider != null)
				_httpDataProvider.Dispose();

        }

        /// <summary>
        /// Test that the databases structure is aligned with the current HttpMapping class structure.
        /// </summary>
        [Test]
        public void DatabaseStructure()
        {
			var props = typeof (HttpMapping).GetProperties();
			var tableInfo = _db.GetTableInfo("HttpMapping");
			for (var i = 0; i < tableInfo.Count; i++) {
				Assert.AreEqual(tableInfo[i].Name, props[i].Name);
			}
        }

        [Test]
        public void DefaultProperties()
        {
            using (var dp = new HttpDataProvider()) {
                Assert.IsNull(dp.Username);
                Assert.IsNull(dp.Password);
                Assert.IsNull(dp.ClientId);
                Assert.IsNotNull(dp.BaseAddress);
                Assert.AreEqual(dp.ApplicationName, "HttpDataProvider");
                Assert.IsNull(dp.HttpResponse);
                Assert.AreEqual(dp.ActionPerformed, ActionPerformed.NoAction);
                Assert.AreEqual(dp.SyncDatabaseName, DbName);
            }
        }

        [Test]
        public void CustomConstructors()
        {
            var dataProvider = new DataProvider {ActiveCompanyId = 1};
            using (var dp = new HttpDataProvider(dataProvider)) {
                Assert.AreEqual(dp.LocalCompanyId, 1);
            }

            using (var dp = new HttpDataProvider(dataProvider, "username", "password")) {
                Assert.AreEqual(dp.LocalCompanyId, 1);
                Assert.AreEqual(dp.Username, "username");
                Assert.AreEqual(dp.Password, "password");
            }
        }

		[Test]
        public async void DownloadPayment()
        {
            // make sure remote target remote endpoints are empty
            var rc = new HttpClient {BaseAddress = new Uri(Service)};
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "companies")).Result.StatusCode == HttpStatusCode.NoContent);
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "payments")).Result.StatusCode == HttpStatusCode.NoContent);
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "payment-methods")).Result.StatusCode == HttpStatusCode.NoContent);
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "fees")).Result.StatusCode == HttpStatusCode.NoContent);


			// add a company and post it to remote, then retrive the unique remote id
            var cds = new configDataSet();
            var r = cds.Aziende.NewAziendeRow();
            r.Nome = "company";
            r.Id = 99;
            cds.Aziende.AddAziendeRow(r);

			await _httpDataProvider.UpdateAziendeAsync(r);
			Assert.AreEqual(ActionPerformed.Added, _httpDataProvider.ActionPerformed);
			Assert.AreEqual(HttpStatusCode.Created, _httpDataProvider.HttpResponse.StatusCode);

            var adam = new EveClient (Service);
		    var companies = await adam.GetAsync<Company>("companies");
            var company = companies[0];

            var payMethod = Factory<PaymentMethod>.Create();
			payMethod.CompanyId = company.UniqueId;
			payMethod.Name = "pm1";
			payMethod.IsBankReceipt = true;
            payMethod.ModalitaPagamentoPA = PAHelpers.ModalitaPagamentoPA["MP01"];
            payMethod = await adam.PostAsync<PaymentMethod>("payment-methods", payMethod);

            var fee = Factory<Fee>.Create();
			fee.Name ="fee1";
			fee.CompanyId = company.UniqueId;
			fee.Amount = 1;
            fee = await adam.PostAsync<Fee>("fees", fee);

            var payment = Factory<Payment>.Create();
			payment.CompanyId = company.UniqueId;
			payment.Name = "payment1";
			payment.ExtraDays = 30;
			payment.ExactDays = true;
            payment.Fee = fee;
            //Bank
            payment.Discount = 0.11;
			payment.FirstPaymentDate = PaymentHelpers.FirstPaymentDates[PaymentDate.EndOfMonth];
			payment.FirstPaymentOption = PaymentHelpers.FirstPaymentOptions[PaymentOption.VatIncluded];
			payment.ForceEndOfMonth = false;
			payment.FirstPaymentDateAdditionalDays = 13;
			payment.Installments = 2;
			payment.InstallmentsEveryNumberOfDays = 4;
            payment.PaymentMethod = payMethod;
            payment = await adam.PostAsync<Payment>("payments", payment);

			// try downloading the new contact into Amica companyDataSet
			var companyDs = new companyDataSet();
            _httpDataProvider.LocalCompanyId = r.Id;

			await _httpDataProvider.GetAsync(companyDs);
            Assert.That(_httpDataProvider.ActionPerformed, Is.EqualTo(ActionPerformed.Read));
            Assert.That(companyDs.Pagamenti.Count, Is.EqualTo(1));
            Assert.That(companyDs.Spese.Count, Is.EqualTo(1));
            Assert.That(companyDs.ModalitàPagamento.Count, Is.EqualTo(1));

            var p = companyDs.Pagamenti[0];
            Assert.That(p.Nome, Is.EqualTo(payment.Name));
            Assert.That(p.GiorniExtra, Is.EqualTo(payment.ExtraDays));
            Assert.That(p.GiorniEsatti, Is.EqualTo(payment.ExactDays));
            Assert.That(p.FineMese, Is.EqualTo(payment.ForceEndOfMonth));
            Assert.That(p.Rate, Is.EqualTo(payment.Installments));
            Assert.That(p.Periodicità, Is.EqualTo(payment.InstallmentsEveryNumberOfDays));
            Assert.That(p.PeriodoPrimaRata, Is.EqualTo((int)payment.FirstPaymentDate.Code));
            Assert.That(p.TipoPrimaRata, Is.EqualTo((int)payment.FirstPaymentOption.Code));
            Assert.That(p.Sconto, Is.EqualTo(payment.Discount));
            Assert.That(p.InizioScadenze, Is.EqualTo(payment.FirstPaymentDateAdditionalDays));
            Assert.That(p.ModalitàPagamentoRow.Nome, Is.EqualTo(payment.PaymentMethod.Name));
            Assert.That(p.ModalitàPagamentoRow.IsRiBa, Is.EqualTo(payment.PaymentMethod.IsBankReceipt));
            Assert.That(p.ModalitàPagamentoRow.CodicePagamentoPA, Is.EqualTo(payment.PaymentMethod.ModalitaPagamentoPA.Code));

            // test that remotely changed vat syncs fine with Amica classic
            payment.Name = "payment2";
            payment.Discount = 0.22;
            payment.PaymentMethod.ModalitaPagamentoPA = PAHelpers.ModalitaPagamentoPA["MP01"];

            System.Threading.Thread.Sleep(SleepLength);
            adam.ResourceName = "payments";
            payment = await adam.PutAsync<Payment>(payment);

            System.Threading.Thread.Sleep(SleepLength);

            await _httpDataProvider.GetAsync(companyDs);
            Assert.That(_httpDataProvider.ActionPerformed, Is.EqualTo(ActionPerformed.Read));
            Assert.That(companyDs.Pagamenti.Count, Is.EqualTo(1));

            p = companyDs.Pagamenti[0];
            Assert.That(p.Nome, Is.EqualTo(payment.Name));
            Assert.That(p.Sconto, Is.EqualTo(payment.Discount));
            Assert.That(p.ModalitàPagamentoRow.Nome, Is.EqualTo(payment.PaymentMethod.Name));
            Assert.That(p.ModalitàPagamentoRow.IsRiBa, Is.EqualTo(payment.PaymentMethod.IsBankReceipt));
            Assert.That(p.ModalitàPagamentoRow.CodicePagamentoPA, Is.EqualTo(payment.PaymentMethod.ModalitaPagamentoPA.Code));

            await adam.DeleteAsync(payment);
            Assert.That(adam.HttpResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

            System.Threading.Thread.Sleep(SleepLength);

            await _httpDataProvider.GetAsync(companyDs);
            Assert.That(_httpDataProvider.ActionPerformed, Is.EqualTo(ActionPerformed.Read));
            Assert.That(companyDs.Pagamenti.Count, Is.EqualTo(0));
        }


		[Test]
        public async void DownloadFee()
        {
            // make sure remote target remote endpoints are empty
            var rc = new HttpClient {BaseAddress = new Uri(Service)};
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "companies")).Result.StatusCode == HttpStatusCode.NoContent);
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "fees")).Result.StatusCode == HttpStatusCode.NoContent);


			// add a company and post it to remote, then retrive the unique remote id
            var cds = new configDataSet();
            var r = cds.Aziende.NewAziendeRow();
            r.Nome = "company";
            r.Id = 99;
            cds.Aziende.AddAziendeRow(r);

			await _httpDataProvider.UpdateAziendeAsync(r);
			Assert.AreEqual(ActionPerformed.Added, _httpDataProvider.ActionPerformed);
			Assert.AreEqual(HttpStatusCode.Created, _httpDataProvider.HttpResponse.StatusCode);

            var adam = new EveClient (Service);
		    var companies = await adam.GetAsync<Company>("companies");
            var company = companies[0];

            var vat = Factory<Vat>.Create();
			vat.CompanyId = company.UniqueId;
			vat.Code = "NEW";
			vat.Name = "NEW VAT";
			vat.Rate = 0.22;
            vat.NaturaPA = Factory<NaturaPA>.Create();
            vat.NaturaPA.Code = "N2";
            vat.NaturaPA.Description = "desc";

            var fee = Factory<Fee>.Create();
			fee.CompanyId = company.UniqueId;
			fee.Name = "fee1";
			fee.Amount = 99;
            fee.Vat = vat;

		    fee = await adam.PostAsync<Fee>("fees", fee);

			// try downloading the new fee into Amica companyDataSet
			var companyDs = new companyDataSet();
            _httpDataProvider.LocalCompanyId = r.Id;

			await _httpDataProvider.GetAsync(companyDs);
            Assert.That(_httpDataProvider.ActionPerformed, Is.EqualTo(ActionPerformed.Read));
            Assert.That(companyDs.Spese.Count, Is.EqualTo(1));
            Assert.That(companyDs.CausaliIVA.Count, Is.EqualTo(1));

            var s = companyDs.Spese[0];
            var i = companyDs.CausaliIVA[0];

            Assert.That(s.Nome, Is.EqualTo(fee.Name));
            Assert.That(s.Importo, Is.EqualTo(fee.Amount));
            Assert.That(s.CausaliIVARow.Codice, Is.EqualTo(fee.Vat.Code));

            // test that remotely changed vat syncs fine with Amica classic
            fee.Name = "fee2";
            fee.Amount = 999;
            fee.Vat.Code = "NEW1";

            System.Threading.Thread.Sleep(SleepLength);
            adam.ResourceName = "fees";
            fee = await adam.PutAsync<Fee>(fee);

            System.Threading.Thread.Sleep(SleepLength);

            await _httpDataProvider.GetAsync(companyDs);
            Assert.That(_httpDataProvider.ActionPerformed, Is.EqualTo(ActionPerformed.Read));
            Assert.That(companyDs.Spese.Count, Is.EqualTo(1));

            s = companyDs.Spese[0];
            Assert.That(s.Nome, Is.EqualTo(fee.Name));
            Assert.That(s.Importo, Is.EqualTo(fee.Amount));
            Assert.That(s.CausaliIVARow.Codice, Is.EqualTo(fee.Vat.Code));
            Assert.That(companyDs.CausaliIVA.Count, Is.EqualTo(2));

            await adam.DeleteAsync(fee);
            Assert.That(adam.HttpResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

            System.Threading.Thread.Sleep(SleepLength);

            await _httpDataProvider.GetAsync(companyDs);
            Assert.That(_httpDataProvider.ActionPerformed, Is.EqualTo(ActionPerformed.Read));
            Assert.That(companyDs.Spese.Count, Is.EqualTo(0));
        }


		[Test]
        public async void DownloadPaymentMethod()
        {
            // make sure remote target remote endpoints are empty
            var rc = new HttpClient {BaseAddress = new Uri(Service)};
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "companies")).Result.StatusCode == HttpStatusCode.NoContent);
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "payment-methods")).Result.StatusCode == HttpStatusCode.NoContent);


			// add a company and post it to remote, then retrive the unique remote id
            var cds = new configDataSet();
            var r = cds.Aziende.NewAziendeRow();
            r.Nome = "company";
            r.Id = 99;
            cds.Aziende.AddAziendeRow(r);

			await _httpDataProvider.UpdateAziendeAsync(r);
			Assert.AreEqual(ActionPerformed.Added, _httpDataProvider.ActionPerformed);
			Assert.AreEqual(HttpStatusCode.Created, _httpDataProvider.HttpResponse.StatusCode);

            var adam = new EveClient (Service);
		    var companies = await adam.GetAsync<Company>("companies");
            var company = companies[0];

            // create vnext contact and post it
            var method = Factory<PaymentMethod>.Create();
			method.CompanyId = company.UniqueId;
			method.Name = "method1";
			method.IsBankReceipt = true;
            method.ModalitaPagamentoPA = Factory<ModalitaPagamentoPA>.Create();
            method.ModalitaPagamentoPA.Code = "code";
            method.ModalitaPagamentoPA.Description = "desc" ;
		    method = await adam.PostAsync<PaymentMethod>("payment-methods", method);

			// try downloading the new contact into Amica companyDataSet
			var companyDs = new companyDataSet();
            _httpDataProvider.LocalCompanyId = r.Id;

			await _httpDataProvider.GetAsync(companyDs);
            Assert.That(_httpDataProvider.ActionPerformed, Is.EqualTo(ActionPerformed.Read));
            Assert.That(companyDs.ModalitàPagamento.Count, Is.EqualTo(1));

            var o = companyDs.ModalitàPagamento[0];
            Assert.That(o.Nome, Is.EqualTo(method.Name));
            Assert.That(o.IsRiBa, Is.EqualTo(method.IsBankReceipt));
            Assert.That(o.CodicePagamentoPA, Is.EqualTo(method.ModalitaPagamentoPA.Code));

            // test that remotely changed vat syncs fine with Amica classic
            method.Name = "option2";
            method.IsBankReceipt	 = false;
            method.ModalitaPagamentoPA = PAHelpers.ModalitaPagamentoPA["MP05"];

            System.Threading.Thread.Sleep(SleepLength);
            adam.ResourceName = "payment-methods";
            method = await adam.PutAsync<PaymentMethod>(method);

            System.Threading.Thread.Sleep(SleepLength);

            await _httpDataProvider.GetAsync(companyDs);
            Assert.That(_httpDataProvider.ActionPerformed, Is.EqualTo(ActionPerformed.Read));
            Assert.That(companyDs.ModalitàPagamento.Count, Is.EqualTo(1));

            o = companyDs.ModalitàPagamento[0];
            Assert.That(o.Nome, Is.EqualTo(method.Name));
            Assert.That(o.IsRiBa, Is.EqualTo(method.IsBankReceipt));
            Assert.That(o.CodicePagamentoPA, Is.EqualTo(method.ModalitaPagamentoPA.Code));

            await adam.DeleteAsync(method);
            Assert.That(adam.HttpResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

            System.Threading.Thread.Sleep(SleepLength);

            await _httpDataProvider.GetAsync(companyDs);
            Assert.That(_httpDataProvider.ActionPerformed, Is.EqualTo(ActionPerformed.Read));
            Assert.That(companyDs.ModalitàPagamento.Count, Is.EqualTo(0));
        }

		[Test]
        public async void DownloadVat()
        {
            // make sure remote target remote endpoints are empty
            var rc = new HttpClient {BaseAddress = new Uri(Service)};
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "companies")).Result.StatusCode == HttpStatusCode.NoContent);
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "vat")).Result.StatusCode == HttpStatusCode.NoContent);


			// add a company and post it to remote, then retrive the unique remote id
            var cds = new configDataSet();
            var r = cds.Aziende.NewAziendeRow();
            r.Nome = "company";
            r.Id = 99;
            cds.Aziende.AddAziendeRow(r);

			await _httpDataProvider.UpdateAziendeAsync(r);
			Assert.AreEqual(ActionPerformed.Added, _httpDataProvider.ActionPerformed);
			Assert.AreEqual(HttpStatusCode.Created, _httpDataProvider.HttpResponse.StatusCode);

            var adam = new EveClient (Service);
		    var companies = await adam.GetAsync<Company>("companies");
            var company = companies[0];

            // create vnext contact and post it
            //var vat = ObjectFactory.CreateVat();
            var vat = Factory<Vat>.Create();
            vat.CompanyId = company.UniqueId;
			vat.Name = "name";
			vat.Code = "123456";
			vat.Rate = 0.1;
			vat.NonDeductible = 0.2;
			vat.IsIntraCommunity = true;
			vat.IsSplitPayment = true;
            vat.NaturaPA = Factory<NaturaPA>.Create();
            vat.NaturaPA.Code = "N1";
            vat.NaturaPA.Description = "description" ;
		    vat = await adam.PostAsync<Vat>("vat", vat);

			// try downloading the new contact into Amica companyDataSet
			var companyDs = new companyDataSet();
            _httpDataProvider.LocalCompanyId = r.Id;

			await _httpDataProvider.GetAsync(companyDs);
            Assert.That(_httpDataProvider.ActionPerformed, Is.EqualTo(ActionPerformed.Read));
            Assert.That(companyDs.CausaliIVA.Count, Is.EqualTo(1));

            var c = companyDs.CausaliIVA[0];
            Assert.That(c.Nome, Is.EqualTo(vat.Name));
            Assert.That(c.Codice, Is.EqualTo(vat.Code.Substring(0, c.Table.Columns["Codice"].MaxLength)));
            Assert.That(c.Aliquota, Is.EqualTo(vat.Rate));
            Assert.That(c.Indeducibilità, Is.EqualTo(vat.NonDeductible));
            Assert.That(c.IsIntracomunitaria, Is.EqualTo(vat.IsIntraCommunity));
            Assert.That(c.IsSplitPayment, Is.EqualTo(vat.IsSplitPayment));
            Assert.That(c.Natura, Is.EqualTo(vat.NaturaPA.Code));

            // test that remotely changed vat syncs fine with Amica classic
            vat.Name = "new name";
            vat.Code = "54321";
            vat.Rate = 0.99;
            vat.NonDeductible = 0.98;
            vat.IsIntraCommunity = false;
            vat.IsSplitPayment = false;
            vat.NaturaPA = PAHelpers.NaturaPA["N1"];

            System.Threading.Thread.Sleep(SleepLength);
            adam.ResourceName = "vat";
            vat = await adam.PutAsync<Vat>(vat);

            System.Threading.Thread.Sleep(SleepLength);

            await _httpDataProvider.GetAsync(companyDs);
            Assert.That(_httpDataProvider.ActionPerformed, Is.EqualTo(ActionPerformed.Read));
            Assert.That(companyDs.CausaliIVA.Count, Is.EqualTo(1));

            c = companyDs.CausaliIVA[0];
            Assert.That(c.Nome, Is.EqualTo(vat.Name));
            Assert.That(c.Codice, Is.EqualTo(vat.Code));
            Assert.That(c.Aliquota, Is.EqualTo(vat.Rate));
            Assert.That(c.Indeducibilità, Is.EqualTo(vat.NonDeductible));
            Assert.That(c.IsIntracomunitaria, Is.EqualTo(vat.IsIntraCommunity));
            Assert.That(c.IsSplitPayment, Is.EqualTo(vat.IsSplitPayment));
            Assert.That(c.Natura, Is.EqualTo(vat.NaturaPA.Code));

            await adam.DeleteAsync(vat);
            Assert.That(adam.HttpResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

            System.Threading.Thread.Sleep(SleepLength);

            await _httpDataProvider.GetAsync(companyDs);
            Assert.That(_httpDataProvider.ActionPerformed, Is.EqualTo(ActionPerformed.Read));
            Assert.That(companyDs.CausaliIVA.Count, Is.EqualTo(0));
        }


        [Test]
        public async void DownloadContact()
        {
            // make sure remote target remote endpoints are empty
            var rc = new HttpClient {BaseAddress = new Uri(Service)};
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "companies")).Result.StatusCode == HttpStatusCode.NoContent);
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "contacts")).Result.StatusCode == HttpStatusCode.NoContent);


			// add a company and post it to remote, then retrive the unique remote id
            var cds = new configDataSet();
            var r = cds.Aziende.NewAziendeRow();
            r.Nome = "company";
            r.Id = 99;
            cds.Aziende.AddAziendeRow(r);

			await _httpDataProvider.UpdateAziendeAsync(r);
			Assert.AreEqual(ActionPerformed.Added, _httpDataProvider.ActionPerformed);
			Assert.AreEqual(HttpStatusCode.Created, _httpDataProvider.HttpResponse.StatusCode);

            var adam = new EveClient (Service);
		    var companies = await adam.GetAsync<Company>("companies");
            var company = companies[0];

            // create vnext contact and post it
            var contact = Factory<Contact>.Create();
			contact.CompanyId = company.UniqueId;
			contact.Name = "Name";
			contact.VatIdentificationNumber = "IT01180680397";
			contact.IdCode = "id_code";
			contact.TaxIdentificationNumber = "RCCNCL70M27B519E";
			contact.MarketArea = "Lombardia";
			contact.PublicAdministrationIndex = "123456";
            contact.Currency = Factory<Currency>.Create();
			contact.Currency.Name = "Euro";
			contact.Currency.Code = "EUR";
            contact.Currency.Symbol = "€";
            contact.Address = Factory<AddressEx>.Create();
			contact.Address.Street = "Street";
			contact.Address.Country = "Italia";
            contact.Address.WebSite = "website";
            contact.Bank = Factory<Bank>.Create();
			contact.Bank.Name = "Bank";
			contact.Bank.IbanCode = "IT88T1927501600CC0010110180";
            contact.Bank.BicSwiftCode = "ABCOITMM";
            contact.OtherAddresses = new List<ShippingAddress>();
            contact.OtherAddresses.Add(Factory<ShippingAddress>.Create());
            contact.OtherAddresses.Add(Factory<ShippingAddress>.Create());
            contact.OtherAddresses[0].Name = "addr1";
            contact.OtherAddresses[1].Name = "addr2";
		    contact = await adam.PostAsync<Contact>("contacts", contact);

			// try downloading the new contact into Amica companyDataSet
			var companyDs = new companyDataSet();
            var t = companyDs.TipiDocumento.NewTipiDocumentoRow();
            t.Id = 4;
            companyDs.TipiDocumento.AddTipiDocumentoRow(t);

            Assert.That(async () => await _httpDataProvider.GetAsync(companyDs),
                Throws.InstanceOf<ArgumentNullException>().With.Property("ParamName").EqualTo("LocalCompanyId"));
            _httpDataProvider.LocalCompanyId = r.Id;

			await _httpDataProvider.GetAsync(companyDs);
            Assert.That(_httpDataProvider.ActionPerformed, Is.EqualTo(ActionPerformed.Read));
            Assert.That(companyDs.Anagrafiche.Count, Is.EqualTo(1));
            Assert.That(companyDs.Nazioni.Count, Is.EqualTo(1));
            Assert.That(companyDs.AreeGeografiche.Count, Is.EqualTo(1));
            Assert.That(companyDs.Valute.Count, Is.EqualTo(1));
            Assert.That(companyDs.Indirizzi.Count, Is.EqualTo(2));

            var a = companyDs.Anagrafiche[0];
            Assert.That(a.RagioneSociale1, Is.EqualTo(contact.Name));
            Assert.That(a.Codice, Is.EqualTo(contact.IdCode));
            Assert.That(a.CodiceFiscale, Is.EqualTo(contact.TaxIdentificationNumber));
            Assert.That(a.Indirizzo, Is.EqualTo(contact.Address.Street));
            Assert.That(a.PartitaIVA, Is.EqualTo(contact.VatIdentificationNumber));
            Assert.That(a.IsAttivo, Is.True);
            Assert.That(a.IsPersonaGiuridica, Is.True);
            Assert.That(a.IsCliente, Is.False);
            Assert.That(a.IsFornitore, Is.False);
            Assert.That(a.IsAgente, Is.False);
            Assert.That(a.IsCapoArea, Is.False);
            Assert.That(a.IsVettore, Is.False);
            Assert.That(a.BancaNome, Is.EqualTo(contact.Bank.Name));
            Assert.That(a.BancaIBAN, Is.EqualTo(contact.Bank.IbanCode));
            Assert.That(a.IndicePA, Is.EqualTo(contact.PublicAdministrationIndex));
            Assert.That(a.NazioniRow.Nome, Is.EqualTo(contact.Address.Country));
            Assert.That(a.AreeGeograficheRow.Nome, Is.EqualTo(contact.MarketArea));
            Assert.That(a.ValuteRow.Nome, Is.EqualTo(contact.Currency.Name));
            Assert.That(a.ValuteRow.Sigla, Is.EqualTo(contact.Currency.Code));

            Assert.That(a.GetChildRows("FK_Anagrafiche_Indirizzi").Length, Is.EqualTo(2));
            Assert.That(companyDs.Indirizzi[0].RagioneSociale1, Is.EqualTo(contact.OtherAddresses[0].Name));
            Assert.That(companyDs.Indirizzi[1].RagioneSociale1, Is.EqualTo(contact.OtherAddresses[1].Name));

            // remotely edit the contact 
            contact.MarketArea = "Emilia";
            contact.Currency.Name = "US Dollar";
            contact.Currency.Code = "USD";
            contact.Address.Country = "USA";
            contact.Address.WebSite = "website";
            contact.MarketArea = "new marketarea";
            contact.Name = new string('A', companyDs.Anagrafiche.RagioneSociale1Column.MaxLength + 1);
            contact.IdCode = "New IdCode";
            contact.TaxIdentificationNumber = "grdsfn66d17h199k".ToUpper();
            contact.Is.Client = true;
            contact.Is.AreaManager = true;
            contact.Bank.Name = "new bank name";
            contact.PublicAdministrationIndex = "newidx";
            contact.OtherAddresses[0].Name = "new addr1";
            contact.OtherAddresses.RemoveAt(1);

            adam.ResourceName = "contacts";
            contact = await adam.PutAsync<Contact>(contact);

			// make it happen that the downloaded Address.Country is already present in Nazioni
            var n = companyDs.Nazioni.NewNazioniRow();
            n.Nome = contact.Address.Country;
            companyDs.Nazioni.AddNazioniRow(n);

            System.Threading.Thread.Sleep(SleepLength);

            // test that remotely changed contact syncs fine with Amica classic
            await _httpDataProvider.GetAsync(companyDs);
            Assert.That(_httpDataProvider.ActionPerformed, Is.EqualTo(ActionPerformed.Read));
            Assert.That(companyDs.Anagrafiche.Count, Is.EqualTo(1));
			// parent tables got a new record
            Assert.That(companyDs.AreeGeografiche.Count, Is.EqualTo(2));
            Assert.That(companyDs.Valute.Count, Is.EqualTo(2));
            Assert.That(companyDs.Nazioni.Count, Is.EqualTo(2));
            Assert.That(companyDs.Indirizzi.Count, Is.EqualTo(1));

            a = companyDs.Anagrafiche[0];
			// also test that an object property which is longer than destination DataColumn MaxLength gets truncated
            Assert.That(a.RagioneSociale1, Is.EqualTo(contact.Name.Substring(0, companyDs.Anagrafiche.RagioneSociale1Column.MaxLength)));
            Assert.That(a.Codice, Is.EqualTo(contact.IdCode));
            Assert.That(a.CodiceFiscale, Is.EqualTo(contact.TaxIdentificationNumber));
            Assert.That(a.BancaNome, Is.EqualTo(contact.Bank.Name));
            Assert.That(a.BancaIBAN, Is.EqualTo(contact.Bank.IbanCode));
            Assert.That(a.IsAttivo, Is.True);
            Assert.That(a.IsPersonaGiuridica, Is.True);
            Assert.That(a.IsCliente, Is.True);
            Assert.That(a.IsFornitore, Is.False);
            Assert.That(a.IsAgente, Is.False);
            Assert.That(a.IsCapoArea, Is.True);
            Assert.That(a.IsVettore, Is.False);
            Assert.That(a.IndicePA, Is.EqualTo(contact.PublicAdministrationIndex));
            Assert.That(a.NazioniRow.Nome, Is.EqualTo(contact.Address.Country));
            Assert.That(a.http, Is.EqualTo(contact.Address.WebSite));
            Assert.That(a.AreeGeograficheRow.Nome, Is.EqualTo(contact.MarketArea));
            Assert.That(a.ValuteRow.Nome, Is.EqualTo(contact.Currency.Name));
            Assert.That(a.ValuteRow.Sigla, Is.EqualTo(contact.Currency.Code));
            Assert.That(a.GetChildRows("FK_Anagrafiche_Indirizzi").Length, Is.EqualTo(1));
            Assert.That(companyDs.Indirizzi[0].RagioneSociale1, Is.EqualTo(contact.OtherAddresses[0].Name));


            await adam.DeleteAsync(contact);
            Assert.That(adam.HttpResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

            System.Threading.Thread.Sleep(SleepLength);

            await _httpDataProvider.GetAsync(companyDs);
            Assert.That(_httpDataProvider.ActionPerformed, Is.EqualTo(ActionPerformed.Read));
            Assert.That(companyDs.Anagrafiche.Count, Is.EqualTo(0));
            Assert.That(companyDs.Nazioni.Count, Is.EqualTo(2));
            Assert.That(companyDs.AreeGeografiche.Count, Is.EqualTo(2));
            Assert.That(companyDs.Valute.Count, Is.EqualTo(2));
            Assert.That(companyDs.Indirizzi.Count, Is.EqualTo(0));
        }


        [Test]
        public async void DownloadDocuments()
        {
            // make sure remote remote endpoint is completely empty
            var rc = new HttpClient { BaseAddress = new Uri(Service) };
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "companies")).Result.StatusCode == HttpStatusCode.NoContent);
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "documents")).Result.StatusCode == HttpStatusCode.NoContent);
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "contacts")).Result.StatusCode == HttpStatusCode.NoContent);
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "payments")).Result.StatusCode == HttpStatusCode.NoContent);
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "payment-methods")).Result.StatusCode == HttpStatusCode.NoContent);
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "fees")).Result.StatusCode == HttpStatusCode.NoContent);


            // add a company
            var cds = new configDataSet();
            var r = cds.Aziende.NewAziendeRow();
            r.Nome = "company";
            r.Id = 99;
            cds.Aziende.AddAziendeRow(r);

            // post it to remote
            await _httpDataProvider.UpdateAziendeAsync(r);

            Assert.AreEqual(ActionPerformed.Added, _httpDataProvider.ActionPerformed);
            Assert.AreEqual(HttpStatusCode.Created, _httpDataProvider.HttpResponse.StatusCode);

            // retrieve it so we can have its remote unique id
            var adam = new EveClient(Service);
            var companies = await adam.GetAsync<Company>("companies");
            var company = companies[0];

            var payMethod = Factory<PaymentMethod>.Create();
			payMethod.CompanyId = company.UniqueId;
			payMethod.Name = "pm1";
			payMethod.IsBankReceipt = true;
            payMethod.ModalitaPagamentoPA = PAHelpers.ModalitaPagamentoPA["MP01"];
            payMethod = await adam.PostAsync<PaymentMethod>("payment-methods", payMethod);

            var fee = Factory<Fee>.Create();
			fee.Name ="fee1";
			fee.CompanyId = company.UniqueId;
			fee.Amount = 1;
            fee = await adam.PostAsync<Fee>("fees", fee);

            var payment = Factory<Payment>.Create();
			payment.CompanyId = company.UniqueId;
			payment.Name = "payment1";
			payment.ExtraDays = 30;
			payment.ExactDays = true;
            payment.Fee = fee;
            //Bank
            payment.Discount = 0.11;
			payment.FirstPaymentDate = PaymentHelpers.FirstPaymentDates[PaymentDate.EndOfMonth];
			payment.FirstPaymentOption = PaymentHelpers.FirstPaymentOptions[PaymentOption.VatIncluded];
			payment.ForceEndOfMonth = false;
			payment.FirstPaymentDateAdditionalDays = 13;
			payment.Installments = 2;
			payment.InstallmentsEveryNumberOfDays = 4;
            payment.PaymentMethod = payMethod;
            payment = await adam.PostAsync<Payment>("payments", payment);

            // create vnext contact and post it
            var contact = Factory<Contact>.Create();
			contact.CompanyId = company.UniqueId;
			contact.Name = "Name";
			contact.VatIdentificationNumber = "IT01180680397";
			contact.TaxIdentificationNumber = "RCCNCL70M27B519E";
			contact.MarketArea = "Lombardia";
			contact.Currency = new Currency
			{
				Name = "Euro",
				Code = "EUR",
				Symbol = "€"
			};
            contact.Address = new AddressEx
            {
                Street = "Street",
                Country = "Italia"
            };
            contact = await adam.PostAsync<Contact>("contacts", contact);

            // new vnext invoice, complete with contact and items, and post it

            var doc = Factory<Document>.Create(typeof(Invoice));

            var vat = Factory<Vat>.Create();
            vat.Code = "code";
            vat.Name = "name";
            vat.Rate = 0.1;

            var ss = Factory<SocialSecurity>.Create();
            ss.Category = DocumentHelpers.SocialSecurityCategories[SocialSecurityCategoryType.TC01];
            ss.Amount = 99;
            ss.Rate = 0.1;
            ss.Taxable = 9;
            ss.Withholding = true;
            ss.Vat = vat;

            //         var shipping = Factory<Shipping>.Create();
            //         shipping.Driver = new Driver { Name = "nicola", LicenseID = "license id", PlateID = "plate id" };
            //         shipping.Appearance = "appearance";
            //shipping.Terms = DocumentHelpers.TransportTerms[DocumentShippingTerm.DeliveredDutyPaid];
            //         shipping.Courier = new ContactDetailsEx
            //         {
            //             Fax = "fax",
            //             Mail = "mail",
            //             Mobile = "mobile",
            //             Name = "name",
            //             PecMail = "pecmail",
            //             Phone = "phone",
            //             WebSite = "website",
            //	UniqueId ="id"
            //         };

            //var i = new Invoice();
            doc.Number = new DocumentNumber { Numeric = 1, String = "hello" };

            doc.CompanyId = company.UniqueId;
			//doc.Category = DocumentHelpers.Categories[DocumentCategory.Invoice];
            doc.Status = DocumentHelpers.Statuses[DocumentStatus.Issued];

            doc.Currency = Factory<Currency>.Create();
            doc.Currency.Name = "US Dollars";
            doc.Currency.Code ="USD";

			doc.Reason = "Vendita";

            doc.WithholdingTax = Factory<WithholdingTax>.Create();
            doc.WithholdingTax.Rate = 99;
			doc.WithholdingTax.IsSocialSecurityIncluded = true;
            doc.WithholdingTax.Amount = 9;
            doc.WithholdingTax.TaxableShare = 10.0;
            doc.SocialSecurity.Add(ss);

            //doc.Shipping = shipping;
            //doc.Total = 100;
            doc.BillTo = new BillingAddress(contact);
            doc.Payment = payment;

            //   var item = new DocumentItem
            //   {
            //       Description = "descriptrion1",
            //       Sku = "sku1"
            //   };
            //   doc.Items.Add(item);

            adam.ResourceName = "documents";
            doc = await adam.PostAsync<Invoice>(doc);

            //         var doc2 = new Invoice
            //         {
            //             CompanyId = company.UniqueId,
            //             Total = 99,
            //             BillTo = doc.BillTo,
            //         };
            //doc2.Items.Add(new DocumentItem { Description = "description3", Sku = "sku3" });

            //         doc2 = await adam.PostAsync<Invoice>(doc2);

            // now try downloading the new document into Amica companyDataSet
            var companyDs = new companyDataSet();
            var t = companyDs.TipiDocumento.NewTipiDocumentoRow();
            t.Id = 4;
            companyDs.TipiDocumento.AddTipiDocumentoRow(t);

            Assert.That(async () => await _httpDataProvider.GetAsync(companyDs),
                Throws.InstanceOf<ArgumentNullException>().With.Property("ParamName").EqualTo("LocalCompanyId"));
            _httpDataProvider.LocalCompanyId = r.Id;

            await _httpDataProvider.GetAsync(companyDs);
            Assert.That(_httpDataProvider.ActionPerformed, Is.EqualTo(ActionPerformed.Read));
            Assert.That(companyDs.Anagrafiche.Count, Is.EqualTo(1));
            Assert.That(companyDs.Documenti.Count, Is.EqualTo(1));
            Assert.That(companyDs.Valute.Count, Is.EqualTo(2));
            Assert.That(companyDs.CausaliDocumenti.Count, Is.EqualTo(1));
            //         Assert.That(companyDs.Righe.Count, Is.EqualTo(2));
            Assert.That(companyDs.Nazioni.Count, Is.EqualTo(1));
            Assert.That(companyDs.AreeGeografiche.Count, Is.EqualTo(1));
            Assert.That(companyDs.Pagamenti.Count, Is.EqualTo(1));
            Assert.That(companyDs.Spese.Count, Is.EqualTo(1));
            Assert.That(companyDs.ModalitàPagamento.Count, Is.EqualTo(1));

            var d = companyDs.Documenti[0];
            Assert.That(d.IdTipoDocumento, Is.EqualTo((int)doc.Category.Code));
            Assert.That(d.Stato, Is.EqualTo((int)doc.Status.Code));
            Assert.That(d.ValuteRow.Sigla, Is.EqualTo(doc.Currency.Code));
            Assert.That(d.CausaliDocumentiRow.Nome, Is.EqualTo(doc.Reason));
            Assert.That(d.NumeroParteNumerica, Is.EqualTo(doc.Number.Numeric));
            Assert.That(d.NumeroParteTesto, Is.EqualTo(doc.Number.String));

            Assert.That(d.AnagraficheRowByFK_Anagrafiche_Documenti.RagioneSociale1, Is.EqualTo(doc.BillTo.Name));
            Assert.That(d.AnagraficheRowByFK_Anagrafiche_Documenti.Indirizzo, Is.EqualTo(doc.BillTo.Street));
            Assert.That(d.AnagraficheRowByFK_Anagrafiche_Documenti.NazioniRow.Nome, Is.EqualTo(doc.BillTo.Country));
            Assert.That(d.AnagraficheRowByFK_Anagrafiche_Documenti.PartitaIVA, Is.EqualTo(doc.BillTo.VatIdentificationNumber));
            Assert.That(d.AnagraficheRowByFK_Anagrafiche_Documenti.CodiceFiscale, Is.EqualTo(doc.BillTo.TaxIdentificationNumber));

            Assert.That(d.RitenutaAcconto, Is.EqualTo(doc.WithholdingTax.Rate));
            Assert.That(d.RitenutaAccontoImporto, Is.EqualTo(doc.WithholdingTax.Amount));
            Assert.That(d.RitenutaAccontoSuImponibile, Is.EqualTo(doc.WithholdingTax.TaxableShare));
            Assert.That(d.IsRitenutaIncludeCassaPrevidenziale, Is.EqualTo(doc.WithholdingTax.IsSocialSecurityIncluded));

            Assert.That(d.CassaPrevidenzialeNome, Is.EqualTo(SocialSecurityAdapter.GetAmicaDescription(doc.SocialSecurity[0].Category)));
            Assert.That(d.CassaPrevidenzialeImporto, Is.EqualTo(doc.SocialSecurity[0].Amount));
            Assert.That(d.CassaPrevidenziale, Is.EqualTo(doc.SocialSecurity[0].Rate));

            Assert.That(d.PagamentiRow.Nome, Is.EqualTo(doc.Payment.Name));
            Assert.That(d.PagamentiRow.TipoPrimaRata, Is.EqualTo((int)doc.Payment.FirstPaymentOption.Code));
            Assert.That(d.PagamentiRow.SpeseRow.Nome, Is.EqualTo(doc.Payment.Fee.Name));
            Assert.That(d.PagamentiRow.ModalitàPagamentoRow.Nome, Is.EqualTo(doc.Payment.PaymentMethod.Name));


            //Assert.That(d.AutistaNome, Is.EqualTo(shipping.Driver.Name));
            //Assert.That(d.AutistaPatente, Is.EqualTo(shipping.Driver.LicenseID));
            //Assert.That(d.AutistaTarga, Is.EqualTo(shipping.Driver.PlateID));
            //Assert.That(d.AspettoBeni, Is.EqualTo(shipping.Appearance));
            //Assert.That(d.Porto, Is.EqualTo(shipping.Terms.Code));
            //Assert.That(d.AnagraficheRowByFK_Anagrafiche_Documenti2.RagioneSociale1, Is.EqualTo(shipping.Courier.Name));
            //         var d2 = companyDs.Documenti[1];
            //         var ri1 = companyDs.Righe[0];
            //         var ri2 = companyDs.Righe[1];
            //         var n = companyDs.Nazioni[0];
            //         var ag = companyDs.AreeGeografiche[0];

            //         Assert.That(d1.IdAnagrafica, Is.EqualTo(a.Id));
            //         Assert.That(d1.TotaleFattura, Is.EqualTo(doc.Total));

            //         Assert.That(ri1.IdDocumento, Is.EqualTo(d1.Id));
            //         Assert.That(ri1.CodiceArticolo, Is.EqualTo(doc.Items[0].Sku));
            //         Assert.That(ri1.Descrizione, Is.EqualTo(doc.Items[0].Description));
            //         Assert.That(ri2.IdDocumento, Is.EqualTo(d2.Id));
            //         Assert.That(ri2.CodiceArticolo, Is.EqualTo(doc2.Items[0].Sku));
            //         Assert.That(ri2.Descrizione, Is.EqualTo(doc2.Items[0].Description));

            //         Assert.That(n.Nome, Is.EqualTo(doc.BillTo.Country));
            //         Assert.That(ag.Nome, Is.EqualTo(contact.MarketArea));

            //         // now remotely update the document by changing 1 item and adding a new one
            //         doc.Items[0].Sku = "updated sku1";
            //   item = new DocumentItem
            //   {
            //       Description = "new description",
            //       Sku = "new sku"
            //   };
            //   doc.Items.Add(item);

            //         doc = await adam.PutAsync<Invoice>(doc);

            //// test that it syncs fine on Amica classic
            //         await _httpDataProvider.GetAsync(companyDs);
            //         Assert.That(_httpDataProvider.ActionPerformed, Is.EqualTo(ActionPerformed.Read));
            //         Assert.That(companyDs.Anagrafiche.Count, Is.EqualTo(1));
            //         Assert.That(companyDs.Documenti.Count, Is.EqualTo(2));
            //         Assert.That(companyDs.Righe.Count, Is.EqualTo(3));

            //         a = companyDs.Anagrafiche[0];
            //         d1 = companyDs.Documenti[0];
            //         d2 = companyDs.Documenti[1];
            //         var righe = companyDs.Righe.Select("", "IdDocumento");
            //         ri1 = (companyDataSet.RigheRow)righe[0];
            //         ri2 = (companyDataSet.RigheRow)righe[1];
            //         var ri3 = (companyDataSet.RigheRow)righe[2];

            //         Assert.That(a.RagioneSociale1, Is.EqualTo(doc.BillTo.Name));
            //         Assert.That(a.Indirizzo, Is.EqualTo(doc.BillTo.Street));
            //         Assert.That(a.PartitaIVA, Is.EqualTo(doc.BillTo.VatIdentificationNumber));

            //         Assert.That(d1.IdAnagrafica, Is.EqualTo(a.Id));
            //         Assert.That(d1.TotaleFattura, Is.EqualTo(doc.Total));
            //         Assert.That(d1.IdTipoDocumento, Is.EqualTo(doc.Category.Code));

            //         Assert.That(ri1.IdDocumento, Is.EqualTo(d1.Id));
            //         Assert.That(ri1.CodiceArticolo, Is.EqualTo(doc.Items[0].Sku));
            //         Assert.That(ri1.Descrizione, Is.EqualTo(doc.Items[0].Description));
            //         Assert.That(ri2.IdDocumento, Is.EqualTo(d1.Id));
            //         Assert.That(ri2.CodiceArticolo, Is.EqualTo(doc.Items[1].Sku));
            //         Assert.That(ri2.Descrizione, Is.EqualTo(doc.Items[1].Description));
            //         Assert.That(ri3.IdDocumento, Is.EqualTo(d2.Id));
            //         Assert.That(ri3.CodiceArticolo, Is.EqualTo(doc2.Items[0].Sku));
            //         Assert.That(ri3.Descrizione, Is.EqualTo(doc2.Items[0].Description));

            //         // On remote, add a new contact and update the document with it
            //         // create vnext contact and post it
            //         var newContact = new Contact
            //         {
            //             CompanyId = company.UniqueId,
            //       Name = "new name",
            //       VatIdentificationNumber = "IT02182030391",
            //       Address = new AddressEx
            //             {
            //                 Street = "Street",
            //		Country = "Russia"
            //             }
            //   };
            //   newContact = await adam.PostAsync<Contact>("contacts", newContact);
            //         doc.BillTo = new BillingAddress(newContact);
            //         doc = await adam.PutAsync<Invoice>(doc);

            //System.Threading.Thread.Sleep(SleepLength);

            //// test that it syncs fine on Amica classic
            //         await _httpDataProvider.GetAsync(companyDs);
            //         Assert.That(_httpDataProvider.ActionPerformed, Is.EqualTo(ActionPerformed.Read));
            //         Assert.That(companyDs.Anagrafiche.Count, Is.EqualTo(2));
            //         Assert.That(companyDs.Documenti.Count, Is.EqualTo(2));
            //         Assert.That(companyDs.Righe.Count, Is.EqualTo(3));
            //         Assert.That(companyDs.Nazioni.Count, Is.EqualTo(2));

            //         var anagrafiche = companyDs.Anagrafiche.Select("", "Id");
            //         a = (companyDataSet.AnagraficheRow)anagrafiche[1];
            //         var docs = companyDs.Documenti.Select("", "Id");
            //         d1 = (companyDataSet.DocumentiRow)docs[0];
            //         righe = companyDs.Righe.Select("", "IdDocumento");
            //         ri1 = (companyDataSet.RigheRow)righe[0];
            //         ri2 = (companyDataSet.RigheRow)righe[1];

            //         Assert.That(a.RagioneSociale1, Is.EqualTo(doc.BillTo.Name));
            //         Assert.That(a.Indirizzo, Is.EqualTo(doc.BillTo.Street));
            //         Assert.That(a.NazioniRow.Nome, Is.EqualTo(doc.BillTo.Country));
            //         Assert.That(a.PartitaIVA, Is.EqualTo(doc.BillTo.VatIdentificationNumber));

            //         Assert.That(d1.IdAnagrafica, Is.EqualTo(a.Id));
            //         Assert.That(d1.TotaleFattura, Is.EqualTo(doc.Total));
            //         Assert.That(d1.IdTipoDocumento, Is.EqualTo(doc.Category.Code));

            //         Assert.That(ri1.IdDocumento, Is.EqualTo(d1.Id));
            //         Assert.That(ri1.CodiceArticolo, Is.EqualTo(doc.Items[0].Sku));
            //         Assert.That(ri1.Descrizione, Is.EqualTo(doc.Items[0].Description));
            //         Assert.That(ri2.IdDocumento, Is.EqualTo(d1.Id));
            //         Assert.That(ri2.CodiceArticolo, Is.EqualTo(doc.Items[1].Sku));
            //         Assert.That(ri2.Descrizione, Is.EqualTo(doc.Items[1].Description));

            //         // test that when a doc is deleted remotely, local sync
            //         // gets rid of both main and child rows

            //         await adam.DeleteAsync(doc);
            //         Assert.That(adam.HttpResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

            //System.Threading.Thread.Sleep(SleepLength);

            //         await _httpDataProvider.GetAsync(companyDs);
            //         //Assert.That(_httpDataProvider.ActionPerformed, Is.EqualTo(ActionPerformed.Read));
            //         Assert.That(companyDs.Anagrafiche.Count, Is.EqualTo(2));
            //         Assert.That(companyDs.Nazioni.Count, Is.EqualTo(2));
            //         Assert.That(companyDs.Documenti.Count, Is.EqualTo(1));
            //         Assert.That(companyDs.Righe.Count, Is.EqualTo(1));
        }

        [Test]
        public async void UploadFee()
        {
            // make sure remote endpoints are empty
            var rc = new HttpClient {BaseAddress = new Uri(Service)};
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "companies")).Result.StatusCode == HttpStatusCode.NoContent);
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "fees")).Result.StatusCode == HttpStatusCode.NoContent);
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "vat")).Result.StatusCode == HttpStatusCode.NoContent);


			// add a company
            var cds = new configDataSet();
            var r = cds.Aziende.NewAziendeRow();
            r.Nome = "company";
            r.Id = 99;
            cds.Aziende.AddAziendeRow(r);

			await _httpDataProvider.UpdateAziendeAsync(r);
			Assert.AreEqual(ActionPerformed.Added, _httpDataProvider.ActionPerformed);
			Assert.AreEqual(HttpStatusCode.Created, _httpDataProvider.HttpResponse.StatusCode);

            var ds = new companyDataSet();

            var i = ds.CausaliIVA.NewCausaliIVARow();
            i.Aliquota = 0.22;
            i.Nome = "Vat1";
            i.Natura = "N1";
            i.Codice = "VAT1";
            ds.CausaliIVA.AddCausaliIVARow(i);

            var s = ds.Spese.NewSpeseRow();
            s.Nome = "fee1";
            s.Importo = 10.1;
            s.IdCausaleIVA = i.Id;
            ds.Spese.AddSpeseRow(s);

            _httpDataProvider.LocalCompanyId = 99;

			// perform the operation
            await _httpDataProvider.UpdateAsync(ds);
			Assert.AreEqual(ActionPerformed.Added, _httpDataProvider.ActionPerformed);
			Assert.AreEqual(HttpStatusCode.Created, _httpDataProvider.HttpResponse.StatusCode);
            ValidateSyncDb(s, "fees");

            var adam = new EveClient(Service) { ResourceName = "fees" };
            var fees = await adam.GetAsync<Fee>();
            Assert.That(fees.Count, Is.EqualTo(1));
            var fee  = fees[0];
            Assert.That(s.Nome, Is.EqualTo(fee.Name));
            Assert.That(s.Importo, Is.EqualTo(fee.Amount));
            Assert.That(s.CausaliIVARow.Codice, Is.EqualTo(fee.Vat.Code));

            ds.AcceptChanges();

            // test that changing a row locally will sync fine upstream
            s.Nome = "fee2";
            s.Importo = 199;
            i.Codice = "VAT11";

            await _httpDataProvider.UpdateAsync(ds);
            fee = await adam.GetAsync<Fee>(fee);
            Assert.That(s.Nome, Is.EqualTo(fee.Name));
            Assert.That(s.Importo, Is.EqualTo(fee.Amount));
            Assert.That(s.CausaliIVARow.Codice, Is.EqualTo(fee.Vat.Code));

            ds.AcceptChanges();

            // test that deleting a ModalitàPagamento locally will also delete it upstream
            s.Delete();
            await _httpDataProvider.UpdateAsync(ds);
            fee = await adam.GetAsync<Fee>(fee);
            Assert.That(fee, Is.Null);
            Assert.That(adam.HttpResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

		[Test]
        public async void UploadPayment()
        {
            // make sure remote endpoints are empty
            var rc = new HttpClient {BaseAddress = new Uri(Service)};
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "companies")).Result.StatusCode == HttpStatusCode.NoContent);
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "payments")).Result.StatusCode == HttpStatusCode.NoContent);
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "payment-methods")).Result.StatusCode == HttpStatusCode.NoContent);


			// add a company
            var cds = new configDataSet();
            var r = cds.Aziende.NewAziendeRow();
            r.Nome = "company";
            r.Id = 99;
            cds.Aziende.AddAziendeRow(r);

			await _httpDataProvider.UpdateAziendeAsync(r);
			Assert.AreEqual(ActionPerformed.Added, _httpDataProvider.ActionPerformed);
			Assert.AreEqual(HttpStatusCode.Created, _httpDataProvider.HttpResponse.StatusCode);

            var ds = new companyDataSet();

            var b = ds.Banche.NewBancheRow();
            b.Nome = "bank1";
            ds.Banche.AddBancheRow(b);

            var mp = ds.ModalitàPagamento.NewModalitàPagamentoRow();
            mp.Nome = "payment-method";
            mp.CodicePagamentoPA = "MP01";
            ds.ModalitàPagamento.AddModalitàPagamentoRow(mp);

            var p = ds.Pagamenti.NewPagamentiRow();
            p.Nome = "payment1";
            p.FineMese = true;
            p.GiorniEsatti = true;
            p.GiorniExtra = 5;
            p.InizioScadenze = 23;
            p.Periodicità = 2;
            p.Rate = 2;
            p.Sconto = 0.1;
            p.PeriodoPrimaRata = (int)Enums.Pagamenti.PeriodoPrimaRata.FineMese;
            p.TipoPrimaRata = (int)Enums.Pagamenti.PrimaRata.ConSpese;
            //p.IdBanca = b.Id;
            p.IdModalitàPagamento = mp.Id;
            ds.Pagamenti.AddPagamentiRow(p);

            _httpDataProvider.LocalCompanyId = 99;

			// perform the operation
            await _httpDataProvider.UpdateAsync(ds);
			Assert.AreEqual(ActionPerformed.Added, _httpDataProvider.ActionPerformed);
			Assert.AreEqual(HttpStatusCode.Created, _httpDataProvider.HttpResponse.StatusCode);
            ValidateSyncDb(p, "payments");

            var adam = new EveClient(Service) { ResourceName = "payments" };
            var payments = await adam.GetAsync<Payment>();
            Assert.That(payments.Count, Is.EqualTo(1));
            var payment  = payments[0];
            Assert.That(p.Nome, Is.EqualTo(payment.Name));
            Assert.That(p.FineMese, Is.EqualTo(payment.ForceEndOfMonth));
            Assert.That(p.GiorniEsatti, Is.EqualTo(payment.ExactDays));
            Assert.That(p.GiorniExtra, Is.EqualTo(payment.ExtraDays));
            Assert.That(p.InizioScadenze, Is.EqualTo(payment.FirstPaymentDateAdditionalDays));
            Assert.That(p.PeriodoPrimaRata, Is.EqualTo((int)payment.FirstPaymentDate.Code));
            Assert.That(p.TipoPrimaRata, Is.EqualTo((int)payment.FirstPaymentOption.Code));
            Assert.That(p.Periodicità, Is.EqualTo(payment.InstallmentsEveryNumberOfDays));
            Assert.That(p.Rate, Is.EqualTo(payment.Installments));
            Assert.That(p.Sconto, Is.EqualTo(payment.Discount));
            Assert.That(p.ModalitàPagamentoRow.Nome, Is.EqualTo(payment.PaymentMethod.Name));
            Assert.That(p.ModalitàPagamentoRow.IsRiBa, Is.EqualTo(payment.PaymentMethod.IsBankReceipt));

            ds.AcceptChanges();

            // test that changing a row locally will sync fine upstream
            p.Nome = "payment2";
            p.FineMese = false;
            p.GiorniExtra = 6;
            p.SetIdModalitàPagamentoNull();

            await _httpDataProvider.UpdateAsync(ds);
            payment = await adam.GetAsync<Payment>(payment);
            Assert.That(p.Nome, Is.EqualTo(payment.Name));
            Assert.That(p.FineMese, Is.EqualTo(payment.ForceEndOfMonth));
            Assert.That(p.GiorniExtra, Is.EqualTo(payment.ExtraDays));
            Assert.That(payment.PaymentMethod, Is.Null);

            ds.AcceptChanges();

            // test that deleting a ModalitàPagamento locally will also delete it upstream
            p.Delete();
            await _httpDataProvider.UpdateAsync(ds);
            payment = await adam.GetAsync<Payment>(payment);
            Assert.That(payment, Is.Null);
            Assert.That(adam.HttpResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

		[Test]
        public async void UploadPaymentMethod()
        {
            // make sure remote endpoints are empty
            var rc = new HttpClient {BaseAddress = new Uri(Service)};
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "companies")).Result.StatusCode == HttpStatusCode.NoContent);
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "payment-methods")).Result.StatusCode == HttpStatusCode.NoContent);


			// add a company
            var cds = new configDataSet();
            var r = cds.Aziende.NewAziendeRow();
            r.Nome = "company";
            r.Id = 99;
            cds.Aziende.AddAziendeRow(r);

			await _httpDataProvider.UpdateAziendeAsync(r);
			Assert.AreEqual(ActionPerformed.Added, _httpDataProvider.ActionPerformed);
			Assert.AreEqual(HttpStatusCode.Created, _httpDataProvider.HttpResponse.StatusCode);

            var ds = new companyDataSet();

            var m = ds.ModalitàPagamento.NewModalitàPagamentoRow();
            m.Nome = "option1";
            m.IsRiBa = true;
            m.CodicePagamentoPA = "MP02";
            ds.ModalitàPagamento.AddModalitàPagamentoRow(m);

            _httpDataProvider.LocalCompanyId = 99;

			// perform the operation
            await _httpDataProvider.UpdateAsync(ds);
			Assert.AreEqual(ActionPerformed.Added, _httpDataProvider.ActionPerformed);
			Assert.AreEqual(HttpStatusCode.Created, _httpDataProvider.HttpResponse.StatusCode);
            ValidateSyncDb(m, "payment-methods");

            var adam = new EveClient(Service) { ResourceName = "payment-methods" };
            var methods = await adam.GetAsync<PaymentMethod>();
            Assert.That(methods.Count, Is.EqualTo(1));
            var method  = methods[0];
            Assert.That(m.Nome, Is.EqualTo(method.Name));
            Assert.That(m.IsRiBa, Is.EqualTo(method.IsBankReceipt));
            Assert.That(m.CodicePagamentoPA, Is.EqualTo(method.ModalitaPagamentoPA.Code));
            Assert.That(method.ModalitaPagamentoPA.Description, 
				Is.EqualTo(((ModalitaPagamentoPA)PAHelpers.ModalitaPagamentoPA[m.CodicePagamentoPA]).Description));

            ds.AcceptChanges();

            // test that changing a row locally will sync fine upstream
            m.Nome = "option2";
            m.IsRiBa = false;
            m.CodicePagamentoPA = "MP06";

            await _httpDataProvider.UpdateAsync(ds);
            method = await adam.GetAsync<PaymentMethod>(method);
            Assert.That(m.Nome, Is.EqualTo(method.Name));
            Assert.That(m.IsRiBa, Is.EqualTo(method.IsBankReceipt));
            Assert.That(m.CodicePagamentoPA, Is.EqualTo(method.ModalitaPagamentoPA.Code));
            Assert.That(method.ModalitaPagamentoPA.Description, 
				Is.EqualTo(((ModalitaPagamentoPA)PAHelpers.ModalitaPagamentoPA[m.CodicePagamentoPA]).Description));

            ds.AcceptChanges();

            // test that deleting a ModalitàPagamento locally will also delete it upstream
            m.Delete();
            await _httpDataProvider.UpdateAsync(ds);
            method = await adam.GetAsync<PaymentMethod>(method);
            Assert.That(method, Is.Null);
            Assert.That(adam.HttpResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

		[Test]
        public async void UploadVat()
        {
            // make sure remote endpoints are empty
            var rc = new HttpClient {BaseAddress = new Uri(Service)};
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "companies")).Result.StatusCode == HttpStatusCode.NoContent);
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "vat")).Result.StatusCode == HttpStatusCode.NoContent);


			// add a company
            var cds = new configDataSet();
            var r = cds.Aziende.NewAziendeRow();
            r.Nome = "company";
            r.Id = 99;
            cds.Aziende.AddAziendeRow(r);

			await _httpDataProvider.UpdateAziendeAsync(r);
			Assert.AreEqual(ActionPerformed.Added, _httpDataProvider.ActionPerformed);
			Assert.AreEqual(HttpStatusCode.Created, _httpDataProvider.HttpResponse.StatusCode);

            var ds = new companyDataSet();

            var c = ds.CausaliIVA.NewCausaliIVARow();
            c.Codice = "12345";
		    c.Nome = "Italia";
            c.Aliquota = 0.22;
            c.Indeducibilità = 0.1;
            c.IsSplitPayment = true;
            c.IsIntracomunitaria = true;
            c.Natura = "N2";
            ds.CausaliIVA.AddCausaliIVARow(c);

            _httpDataProvider.LocalCompanyId = 99;

			// perform the operation
            await _httpDataProvider.UpdateAsync(ds);
			Assert.AreEqual(ActionPerformed.Added, _httpDataProvider.ActionPerformed);
			Assert.AreEqual(HttpStatusCode.Created, _httpDataProvider.HttpResponse.StatusCode);
            ValidateSyncDb(c, "vat");

            var adam = new EveClient(Service) { ResourceName = "vat" };
            var vats = await adam.GetAsync<Vat>();
            Assert.That(vats.Count, Is.EqualTo(1));
            var vat  = vats[0];
            Assert.That(c.Nome, Is.EqualTo(vat.Name));
            Assert.That(c.Codice, Is.EqualTo(vat.Code));
            Assert.That(c.Aliquota, Is.EqualTo(vat.Rate));
            Assert.That(c.IsIntracomunitaria, Is.EqualTo(vat.IsIntraCommunity));
            Assert.That(c.IsSplitPayment, Is.EqualTo(vat.IsSplitPayment));
            Assert.That(c.Natura, Is.EqualTo(vat.NaturaPA.Code));

            ds.AcceptChanges();

            // test that changing a row locally will sync fine upstream
            c.Codice = "54321";
            c.Nome = "USA";
            c.Aliquota = 0.23;
            c.Indeducibilità = 0.2;
            c.IsSplitPayment = false;
            c.IsIntracomunitaria = false;
            c.Natura = "N6";

            await _httpDataProvider.UpdateAsync(ds);
            vat = await adam.GetAsync<Vat>(vat);
            Assert.That(c.Nome, Is.EqualTo(vat.Name));
            Assert.That(c.Codice, Is.EqualTo(vat.Code));
            Assert.That(c.Aliquota, Is.EqualTo(vat.Rate));
            Assert.That(c.IsIntracomunitaria, Is.EqualTo(vat.IsIntraCommunity));
            Assert.That(c.IsSplitPayment, Is.EqualTo(vat.IsSplitPayment));
            Assert.That(c.Natura, Is.EqualTo(vat.NaturaPA.Code));

            ds.AcceptChanges();

            // test that deleting a contact locally will also delete it upstream
            c.Delete();
            await _httpDataProvider.UpdateAsync(ds);
            vat = await adam.GetAsync<Vat>(vat);
            Assert.That(vat, Is.Null);
            Assert.That(adam.HttpResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }



        [Test]
        public async void UploadContact()
        {
            // make sure remote endpoints are empty
            var rc = new HttpClient {BaseAddress = new Uri(Service)};
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "companies")).Result.StatusCode == HttpStatusCode.NoContent);
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "contacts")).Result.StatusCode == HttpStatusCode.NoContent);


			// add a company
            var cds = new configDataSet();
            var r = cds.Aziende.NewAziendeRow();
            r.Nome = "company";
            r.Id = 99;
            cds.Aziende.AddAziendeRow(r);

			await _httpDataProvider.UpdateAziendeAsync(r);
			Assert.AreEqual(ActionPerformed.Added, _httpDataProvider.ActionPerformed);
			Assert.AreEqual(HttpStatusCode.Created, _httpDataProvider.HttpResponse.StatusCode);

            var ds = new companyDataSet();

		    var n = ds.Nazioni.NewNazioniRow();
		    n.Nome = "Italia";
		    ds.Nazioni.AddNazioniRow(n);

		    var ag = ds.AreeGeografiche.NewAreeGeograficheRow();
		    ag.Nome = "Lombardia";
		    ds.AreeGeografiche.AddAreeGeograficheRow(ag);

            var v = ds.Valute.NewValuteRow();
            v.Nome = "Euro";
            v.Sigla = "EUR";
            ds.Valute.AddValuteRow(v);

            var a = ds.Anagrafiche.NewAnagraficheRow();
            a.RagioneSociale1 = "rs1";
            a.PartitaIVA = "01180680397";
            a.Codice = "idcode";
            a.CodiceFiscale = "rccncl70m27b519e";
            a.Indirizzo = "address";
		    a.IdNazione = n.Id;
            a.IdAreaGeografica = ag.Id;
            a.IdValuta = v.Id;
            a.IsAgente = true;
            a.IsCapoArea = true;
            a.IsCliente = true;
            a.IsFornitore = true;
            a.IsPersonaGiuridica = true;
            a.IsAttivo = true;
            a.IsVettore = true;
            a.BancaNome = "bank name";
            a.BancaIBAN = "IT88T1927501600CC0010110180";
            a.IndicePA = "123456";
            a.http = "website";
            ds.Anagrafiche.AddAnagraficheRow(a);

            var i = ds.Indirizzi.NewIndirizziRow();
            i.RagioneSociale1 = "name";
            i.Indirizzo = "indir";
            i.CAP = "cap";
            i.Provincia = "pr";
            i.Località = "loc";
            i.Telefono1 = "tel1";
            i.Telefono2 = "tel2";
            i.Fax = "fax";
            i.Email = "mail";
            i.IdAnagrafica = a.Id;
            ds.Indirizzi.AddIndirizziRow(i);

            _httpDataProvider.LocalCompanyId = 99;

			// perform the operation
            await _httpDataProvider.UpdateAsync(ds);
			Assert.AreEqual(ActionPerformed.Added, _httpDataProvider.ActionPerformed);
			Assert.AreEqual(HttpStatusCode.Created, _httpDataProvider.HttpResponse.StatusCode);
            ValidateSyncDb(a, "contacts");

            var adam = new EveClient(Service) { ResourceName = "contacts" };
            var contacts = await adam.GetAsync<Contact>();
            Assert.That(contacts.Count, Is.EqualTo(1));
            var contact = contacts[0];
            Assert.That(a.RagioneSociale1, Is.EqualTo(contact.Name));
            Assert.That("IT" + a.PartitaIVA, Is.EqualTo(contact.VatIdentificationNumber));
            Assert.That(a.Codice, Is.EqualTo(contact.IdCode));
            Assert.That(a.CodiceFiscale.ToUpper(), Is.EqualTo(contact.TaxIdentificationNumber));
            Assert.That(a.Indirizzo, Is.EqualTo(contact.Address.Street));
            Assert.That(a.IsPersonaGiuridica, Is.EqualTo(contact.Is.Company));
            Assert.That(a.IsAttivo, Is.EqualTo(contact.Is.Active));
            Assert.That(a.IsCliente, Is.EqualTo(contact.Is.Client));
            Assert.That(a.IsFornitore, Is.EqualTo(contact.Is.Vendor));
            Assert.That(a.IsVettore, Is.EqualTo(contact.Is.Courier));
            Assert.That(a.IsAgente, Is.EqualTo(contact.Is.Agent));
            Assert.That(a.IsCapoArea, Is.EqualTo(contact.Is.AreaManager));
            Assert.That(a.http, Is.EqualTo(contact.Address.WebSite));
            Assert.That(a.BancaNome, Is.EqualTo(contact.Bank.Name));
            Assert.That(a.BancaIBAN, Is.EqualTo(contact.Bank.IbanCode));
            Assert.That(a.IndicePA, Is.EqualTo(contact.PublicAdministrationIndex));
            Assert.That(a.NazioniRow.Nome, Is.EqualTo(contact.Address.Country));
            Assert.That(a.AreeGeograficheRow.Nome, Is.EqualTo(contact.MarketArea));
            Assert.That(a.ValuteRow.Nome, Is.EqualTo(contact.Currency.Name));
            Assert.That(a.ValuteRow.Sigla, Is.EqualTo(contact.Currency.Code));
            Assert.That(i.RagioneSociale1, Is.EqualTo(contact.OtherAddresses[0].Name));

            Assert.That(contact.OtherAddresses.Count, Is.EqualTo(1));
            Assert.That(i.Indirizzo, Is.EqualTo(contact.OtherAddresses[0].Street));
            Assert.That(i.CAP, Is.EqualTo(contact.OtherAddresses[0].PostalCode));
            Assert.That(i.Provincia, Is.EqualTo(contact.OtherAddresses[0].StateOrProvince));
            Assert.That(i.Località, Is.EqualTo(contact.OtherAddresses[0].Town));
            Assert.That(i.Telefono1, Is.EqualTo(contact.OtherAddresses[0].Phone));
            Assert.That(i.Telefono2, Is.EqualTo(contact.OtherAddresses[0].Mobile));
            Assert.That(i.Fax, Is.EqualTo(contact.OtherAddresses[0].Fax));
            Assert.That(i.Email, Is.EqualTo(contact.OtherAddresses[0].Mail));

            ds.AcceptChanges();

			// test that changing a row locally will sync fine upstream
            a.RagioneSociale1 = "changed rs";
            a.IsCapoArea = false;
            a.Codice = "new idcode";
            a.CodiceFiscale = "grdsfn66d17h199k".ToUpper();
            a.BancaNome = "new bank name";
            a.IndicePA = "npaidx";
            i.RagioneSociale1 = "changed rs";

            await _httpDataProvider.UpdateAsync(ds);
            contact = await adam.GetAsync<Contact>(contact);
            Assert.That(contact.Name, Is.EqualTo(a.RagioneSociale1));
            Assert.That(contact.Is.AreaManager, Is.EqualTo(a.IsCapoArea));
            Assert.That(contact.IdCode, Is.EqualTo(a.Codice));
            Assert.That(contact.TaxIdentificationNumber, Is.EqualTo(a.CodiceFiscale));
            Assert.That(contact.Bank.Name, Is.EqualTo(a.BancaNome));
            Assert.That(contact.PublicAdministrationIndex, Is.EqualTo(a.IndicePA));
            Assert.That(contact.OtherAddresses.Count, Is.EqualTo(1));
            Assert.That(i.RagioneSociale1, Is.EqualTo(contact.OtherAddresses[0].Name));

            ds.AcceptChanges();

			// test that deleting a contact locally will also delete it upstream
            a.Delete();
            await _httpDataProvider.UpdateAsync(ds);
            contact = await adam.GetAsync<Contact>(contact);
            Assert.That(contact, Is.Null);
            Assert.That(adam.HttpResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

		[Test]
        public async void UploadDocuments()
        {
            // make sure remote remote endpoint is completely empty
            var rc = new HttpClient {BaseAddress = new Uri(Service)};
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "companies")).Result.StatusCode == HttpStatusCode.NoContent);
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "documents")).Result.StatusCode == HttpStatusCode.NoContent);
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "contacts")).Result.StatusCode == HttpStatusCode.NoContent);
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "vat")).Result.StatusCode == HttpStatusCode.NoContent);
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "payments")).Result.StatusCode == HttpStatusCode.NoContent);
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "payment-methods")).Result.StatusCode == HttpStatusCode.NoContent);
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "fees")).Result.StatusCode == HttpStatusCode.NoContent);


			// add a company
            var cds = new configDataSet();
            var r = cds.Aziende.NewAziendeRow();
            r.Nome = "company";
            r.Id = 99;
            cds.Aziende.AddAziendeRow(r);

			await _httpDataProvider.UpdateAziendeAsync(r);
			Assert.AreEqual(ActionPerformed.Added, _httpDataProvider.ActionPerformed);
			Assert.AreEqual(HttpStatusCode.Created, _httpDataProvider.HttpResponse.StatusCode);

            var ds = new companyDataSet();

            var td = ds.TipiDocumento.NewTipiDocumentoRow();
            td.Nome = "Fattura differita";
            td.Id = 4;
            ds.TipiDocumento.AddTipiDocumentoRow(td);

		    var n = ds.Nazioni.NewNazioniRow();
		    n.Nome = "Italia";
		    ds.Nazioni.AddNazioniRow(n);

		    var ag = ds.AreeGeografiche.NewAreeGeograficheRow();
		    ag.Nome = "Lombardia";
		    ds.AreeGeografiche.AddAreeGeograficheRow(ag);

            var v = ds.Valute.NewValuteRow();
            v.Nome = "US Dollars";
            v.Sigla = "USD";
            ds.Valute.AddValuteRow(v);

            var cd = ds.CausaliDocumenti.NewCausaliDocumentiRow();
            cd.Nome = "Vendita";
            ds.CausaliDocumenti.AddCausaliDocumentiRow(cd);

            var ci = ds.CausaliIVA.NewCausaliIVARow();
            ci.Aliquota = 0.22;
            ci.Nome = "Vat1";
            ci.Natura = "N1";
            ci.Codice = "VAT1";
            ds.CausaliIVA.AddCausaliIVARow(ci);

            var c = ds.Anagrafiche.NewAnagraficheRow();
            c.RagioneSociale1 = "rs1";
            c.PartitaIVA = "01180680397";
            c.CodiceFiscale = "RCCNCL70M27B519E";
            c.Indirizzo = "address";
		    c.IdNazione = n.Id;
            c.IdAreaGeografica = ag.Id;
            c.IdValuta = v.Id;
            ds.Anagrafiche.AddAnagraficheRow(c);


            var b = ds.Banche.NewBancheRow();
            b.Nome = "bank1";
            ds.Banche.AddBancheRow(b);

            var mp = ds.ModalitàPagamento.NewModalitàPagamentoRow();
            mp.Nome = "payment-method";
            mp.CodicePagamentoPA = "MP01";
            ds.ModalitàPagamento.AddModalitàPagamentoRow(mp);

            var p = ds.Pagamenti.NewPagamentiRow();
            p.Nome = "payment1";
            p.FineMese = true;
            p.GiorniEsatti = true;
            p.GiorniExtra = 5;
            p.InizioScadenze = 23;
            p.Periodicità = 2;
            p.Rate = 2;
            p.Sconto = 0.1;
            p.PeriodoPrimaRata = (int)Enums.Pagamenti.PeriodoPrimaRata.FineMese;
            p.TipoPrimaRata = (int)Enums.Pagamenti.PrimaRata.ConSpese;
            //p.IdBanca = b.Id;
            p.IdModalitàPagamento = mp.Id;
            ds.Pagamenti.AddPagamentiRow(p);

            var d = ds.Documenti.NewDocumentiRow();
            d.IdAnagrafica = c.Id;
            d.NumeroParteNumerica = 1;
            d.NumeroParteTesto = "string";
            d.Stato = (int)Enums.Documenti.Stato.Emesso;
            d.IdTipoDocumento = (int)Enums.Documenti.Tipo.FatturaDifferita;
            d.IdValuta = v.Id;
            d.IdCausaleDocumento = cd.Id;
            d.TotaleFattura = 99;
            d.Data = DateTime.Now;
            d.IdPagamento = p.Id;

			d.RitenutaAcconto = 99;
			d.RitenutaAccontoSuImponibile = 10.1;
			d.IsRitenutaIncludeCassaPrevidenziale = true;
            d.RitenutaAccontoImporto = 999;

            d.CassaPrevidenziale = 10.2;
            d.CassaPrevidenzialeImporto = 999;
            d.CassaPrevidenzialeNome = "Cass geometri";
            d.IdIVACassaPrevidenziale = ci.Id;
            ds.Documenti.AddDocumentiRow(d);

            var ri = ds.Righe.NewRigheRow();
            ri.IdDocumento = d.Id;
            ri.CodiceArticolo = "Sku";
            ri.Descrizione = "Description";
            ds.Righe.AddRigheRow(ri);

			// perform the operation
            _httpDataProvider.LocalCompanyId = 99;
            await _httpDataProvider.UpdateAsync(ds);
			Assert.AreEqual(ActionPerformed.Added, _httpDataProvider.ActionPerformed);
			Assert.AreEqual(HttpStatusCode.Created, _httpDataProvider.HttpResponse.StatusCode);
            ValidateSyncDb(d, "documents");
            ValidateSyncDb(c, "contacts");

            var adam = new EveClient(Service) { ResourceName = "documents" };
            var docs = await adam.GetAsync<Document>();
            Assert.That(docs.Count, Is.EqualTo(1));
            var doc  = docs[0];
            Assert.That((int)doc.Category.Code, Is.EqualTo(d.IdTipoDocumento));
            Assert.That((int)doc.Status.Code, Is.EqualTo(d.Stato));
            Assert.That(doc.Currency.Code, Is.EqualTo(d.ValuteRow.Sigla));
            Assert.That(doc.Reason, Is.EqualTo(d.CausaliDocumentiRow.Nome));
            Assert.That(doc.WithholdingTax.Amount, Is.EqualTo(d.RitenutaAccontoImporto));
            Assert.That(doc.WithholdingTax.Rate, Is.EqualTo(d.RitenutaAcconto));
            Assert.That(doc.WithholdingTax.TaxableShare, Is.EqualTo(d.RitenutaAccontoSuImponibile));
            Assert.That(doc.WithholdingTax.IsSocialSecurityIncluded, Is.EqualTo(d.IsRitenutaIncludeCassaPrevidenziale));
            //Assert.That(doc.SocialSecurity.Count, Is.EqualTo(1));
            //Assert.That(doc.SocialSecurity[0].Amount, Is.EqualTo(d.CassaPrevidenzialeImporto));
            //Assert.That(doc.SocialSecurity.Name, Is.EqualTo(d.CassaPrevidenzialeNome));
            //Assert.That(doc.SocialSecurity.Rate, Is.EqualTo(d.CassaPrevidenziale));
            //Assert.That(doc.SocialSecurity.Vat.Name, Is.EqualTo(d.CausaliIVARowByFK_CausaliIVA_IVACassaPrevidenziale.Nome));
            //Assert.That(doc.SocialSecurity.Vat.Rate, Is.EqualTo(d.CausaliIVARowByFK_CausaliIVA_IVACassaPrevidenziale.Aliquota));
            //Assert.That(doc.SocialSecurity.Vat.NaturaPA.Code, Is.EqualTo(d.CausaliIVARowByFK_CausaliIVA_IVACassaPrevidenziale.Natura));
            //Assert.That(doc.SocialSecurity.Vat.Code, Is.EqualTo(d.CausaliIVARowByFK_CausaliIVA_IVACassaPrevidenziale.Codice));

            Assert.That(doc.Payment.Name, Is.EqualTo(d.PagamentiRow.Nome));
            Assert.That(doc.Payment.PaymentMethod.Name, Is.EqualTo(d.PagamentiRow.ModalitàPagamentoRow.Nome));

            //   cds.AcceptChanges();
            //   ds.AcceptChanges();

            //// Changing a Contact should not affect the ContatMinimal in the Document.
            //         ds.Anagrafiche.Rows[0]["PartitaIVA"] = "IT02182030391";
            //         ds.Nazioni.Rows[0]["Nome"] = "Russia";
            //         await _httpDataProvider.UpdateAsync(ds);
            //Assert.AreEqual(ActionPerformed.Modified, _httpDataProvider.ActionPerformed);
            //Assert.AreEqual(HttpStatusCode.OK, _httpDataProvider.HttpResponse.StatusCode);
            //         ValidateSyncDb(d, "documents");
            //         ValidateSyncDb(ds.Anagrafiche.Rows[0], "contacts");

            //         var adam = new EveClient (Service);
            //   var contacts = await adam.GetAsync<Contact>("contacts");
            //   var contact = contacts[0];
            //   Assert.AreEqual("IT02182030391", contact.VatIdentificationNumber);
            //   Assert.AreEqual(c.CodiceFiscale, contact.TaxIdentificationNumber);
            //   Assert.AreEqual("Russia", contact.Address.Country);
            //   Assert.AreEqual(ag.Nome, contact.MarketArea);
            //   Assert.AreEqual(v.Nome, contact.Currency.Name);

            //   var docs = await adam.GetAsync<Document>("documents");
            //   var doc = docs[0];
            //   Assert.AreEqual(contact.UniqueId, doc.Contact.UniqueId);
            //   Assert.AreEqual("IT01180680397", doc.Contact.VatIdentificationNumber);
            //   Assert.AreEqual("Italia", doc.Contact.Country);

            //   Assert.That(doc.Items.Count, Is.EqualTo(1));
            //   var docItem = doc.Items[0];
            //   Assert.That(docItem.Sku, Is.EqualTo("Sku"));
            //   Assert.That(docItem.Description, Is.EqualTo("Description"));


            //         doc.Contact.VatIdentificationNumber = "IT92078790398";
            //         doc.Contact.Country = "USA";
            //         await adam.PutAsync("documents", doc);
            //         Assert.AreEqual(HttpStatusCode.OK, _httpDataProvider.HttpResponse.StatusCode);
            //         await _httpDataProvider.GetAsync(ds);
            //         Assert.AreEqual(ActionPerformed.Read, _httpDataProvider.ActionPerformed);
            //         // Anagrafiche field and document reference have not changed
            //         Assert.AreEqual("IT02182030391", ds.Anagrafiche.Rows[0]["PartitaIva"]);
            //         Assert.AreEqual("Russia", ds.Nazioni.Rows[0]["Nome"]);
            //         Assert.AreEqual(ds.Anagrafiche.Rows[0]["Id"], ds.Documenti.Rows[0]["IdAnagrafica"]);

            //         // test that a locally deleted object is deleted fine also remotely
            //         ds.Documenti[0].Delete();
            //         await _httpDataProvider.UpdateAsync(ds);
            //         doc = await adam.GetAsync<Invoice>("documents", doc);
            //         Assert.That(doc, Is.Null);
            //         Assert.That(adam.HttpResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }
        /// <summary>
        /// Test that a new datarow is properly processed
        /// </summary>
        [Test]
        public void AddUnknownAziendeRow()
        {
            var ds = new configDataSet();
            var r = ds.Aziende.NewAziendeRow();
            r.Nome = "company";
            r.Id = 99;
            ds.Aziende.AddAziendeRow(r);
            ValidateUnknownRow(r, "companies");
        }

        [Test]
        public void AddBadAziendeRow()
        {
            
            var ds = new configDataSet();
            var r = ds.Aziende.NewAziendeRow();
            r.Nome = string.Empty;
            r.Id = 99;
            ds.Aziende.AddAziendeRow(r);
            ValidateBadUnknownRow(r, "companies");
        }
        /// <summary>
        /// Test that a modified datarow which is not existing in the sync system is properly processed.
        /// </summary>
        [Test]
        public void ModifyUnknownAziendeRow()
        {

            var ds = new configDataSet();
            var row = ds.Aziende.NewAziendeRow();
            row.Nome = "company";
            row.Id = 99;
            ds.Aziende.AddAziendeRow(row);
            row.AcceptChanges();
            row.SetModified();
            ValidateUnknownRow(row, "companies");
        }

        /// <summary>
        /// Test that a modified datarow that already exists in the sync systems is properly processed.
        /// </summary>
        [Test]
        public async void ModifyKnownAziendeRow()
        {
            var ds = new configDataSet();
            var n = ds.Aziende.NewAziendeRow();
            n.Nome = "company";
            n.Id = 99;
            ds.Aziende.AddAziendeRow(n);

			await _httpDataProvider.UpdateAziendeAsync(n);
			Assert.AreEqual( _httpDataProvider.HttpResponse.StatusCode, HttpStatusCode.Created);

			n.AcceptChanges();
			n.SetModified();

			n.Nome = "modified company";
			ValidateKnownRow(n, "companies");
        }

        [Test]
        public async void DeleteKnownAziendeRow()
        {
            
            var ds = new configDataSet();
            var row = ds.Aziende.NewAziendeRow();
            row.Nome = "company";
            row.Id = 99;
            ds.Aziende.AddAziendeRow(row);

			await _httpDataProvider.UpdateAziendeAsync(row);

			row.AcceptChanges();
			row.Delete();
            ValidateKnownDeletedRow(row, "companies");
        }

        [Test]
        public void DeleteUnknownAziendeRow()
        {
            
            var ds = new configDataSet();
            var row = ds.Aziende.NewAziendeRow();
            row.Nome = "company";
            row.Id = 99;
            ds.Aziende.AddAziendeRow(row);

            row.AcceptChanges();
            row.Delete();
            ValidateUnknownDeletedRow(row, "companies");
        }

        [Test]
        public async void GenericUpdateAsync()
        {
            var ds = new configDataSet();
            var row = ds.Aziende.NewAziendeRow();
            row.Nome = "company";
            row.Id = 99;
            ds.Aziende.AddAziendeRow(row);

			// perform the operation
			await _httpDataProvider.UpdateAsync(ds);
			Assert.AreEqual(ActionPerformed.Added, _httpDataProvider.ActionPerformed);
			Assert.AreEqual(HttpStatusCode.Created, _httpDataProvider.HttpResponse.StatusCode);
			Assert.AreEqual(1, _httpDataProvider.UpdatesPerformed.Count);
			Assert.AreEqual("Aziende", _httpDataProvider.UpdatesPerformed[0].TableName);
            ValidateSyncDb(row, "companies", false);
        }

        [Test]
        public async void GetRemoteChangesAndSyncThemLocally()
        {
	    // Note that in this test we are using the most generic GetAsync.
	    // This is slower but makes sure that reflection code in GetAsync is tested
	    // and also, since that code runs most specialized Get<T>Async methods,
	    // tests all the other variants.

            // clear remote endpoints
            using (var client = new HttpClient {BaseAddress = new Uri(Service)}) {
                Assert.IsTrue(client.DeleteAsync(string.Format("/{0}", "companies")).Result.StatusCode == HttpStatusCode.NoContent);
                Assert.IsTrue(client.DeleteAsync(string.Format("/{0}", "documents")).Result.StatusCode == HttpStatusCode.NoContent); 
                Assert.IsTrue(client.DeleteAsync(string.Format("/{0}", "contacts")).Result.StatusCode == HttpStatusCode.NoContent); 
            }
            
            var rc = new EveClient {BaseAddress = new Uri(Service)};

            // post a new company 
            var company = rc.PostAsync<Company>("companies", new Company() {Name = "Company"}).Result;
            Assert.AreEqual(HttpStatusCode.Created, rc.HttpResponse.StatusCode);

            var contact = rc.PostAsync<Contact>("contacts", new Contact() {Name = "Contact1", VatIdentificationNumber = "IT01180680397", CompanyId = company.UniqueId}).Result;
            Assert.AreEqual(HttpStatusCode.Created, rc.HttpResponse.StatusCode);

            //var doc = rc.PostAsync<Document>("documents", new Invoice() { BillTo = new BillingAddress(contact), CompanyId = company.UniqueId }).Result;
            //Assert.AreEqual(HttpStatusCode.Created, rc.HttpResponse.StatusCode);

			// test that we can download and sync with a new company being posted on the remote
			var configDs = new configDataSet();
			await _httpDataProvider.GetAsync(configDs);
			// we downloaded one new object and added it to the corresponding table
			Assert.AreEqual(ActionPerformed.Read, _httpDataProvider.ActionPerformed);
			Assert.AreEqual(1, configDs.Aziende.Count);

			// we actually downloaded to right object
			var aziendeRow = configDs.Aziende[0];
			Assert.AreEqual(company.Name, aziendeRow.Nome);
			ValidateSyncDb(aziendeRow, "companies", false);

			// if we try a sync again we don't get anything new since there have been no changes on the remote
			await _httpDataProvider.GetAsync(configDs);
			Assert.AreEqual(ActionPerformed.ReadNoChanges, _httpDataProvider.ActionPerformed);
			Assert.AreEqual(1, configDs.Aziende.Count);

			// test that if the remote object is updated...
			company.Name = "We changed name";
			company = rc.PutAsync<Company>("companies", company).Result;

			System.Threading.Thread.Sleep(SleepLength);

			// ... we can then sync it down effortlessly
			await _httpDataProvider.GetAsync(configDs);
			Assert.AreEqual(ActionPerformed.Read, _httpDataProvider.ActionPerformed);
			aziendeRow = configDs.Aziende[0];
			Assert.AreEqual(company.Name, aziendeRow.Nome);
			ValidateSyncDb(aziendeRow, "companies", false);

			// if we try a sync again we don't get anything new since there have been no changes on the remote
			await _httpDataProvider.GetAsync(configDs);
			Assert.AreEqual(ActionPerformed.ReadNoChanges, _httpDataProvider.ActionPerformed);
			Assert.AreEqual(1, configDs.Aziende.Count);

			_httpDataProvider.LocalCompanyId = aziendeRow.Id;

			// test that we can download and sync with a new country posted on the remote
			var companyDs = new companyDataSet();
            var t = companyDs.TipiDocumento.NewTipiDocumentoRow();
            t.Id = 4;
            companyDs.TipiDocumento.AddTipiDocumentoRow(t);

			await _httpDataProvider.GetAsync(companyDs);

			// we downloaded one new object and added it to the corresponding table
			Assert.AreEqual(ActionPerformed.Read, _httpDataProvider.ActionPerformed);
			Assert.AreEqual(1, companyDs.Anagrafiche.Count);
			var anagraficheRow = companyDs.Anagrafiche[0];
			Assert.AreEqual(contact.Name, anagraficheRow.RagioneSociale1);
			ValidateSyncDb(anagraficheRow, "contacts", false);

			// if we try a sync again we don't get anything new since there have been no changes on the remote
			await _httpDataProvider.GetAsync(companyDs);
            Assert.AreEqual(ActionPerformed.ReadNoChanges, _httpDataProvider.ActionPerformed);
            Assert.AreEqual(1, companyDs.Anagrafiche.Count);

			// test that if the remote object is updated...
			contact.Name = "We changed name";
			contact = rc.PutAsync<Contact>("contacts", contact).Result;

			System.Threading.Thread.Sleep(SleepLength);

			// ... we can then sync it down effortlessly
			await _httpDataProvider.GetAsync(companyDs);
			Assert.AreEqual(ActionPerformed.Read, _httpDataProvider.ActionPerformed);
			anagraficheRow = companyDs.Anagrafiche[0];
			Assert.AreEqual(contact.Name, anagraficheRow.RagioneSociale1);
			ValidateSyncDb(anagraficheRow, "contacts", false);

			// if we try a sync again we don't get anything new since there have been no changes on the remote
			await _httpDataProvider.GetAsync(companyDs);
			Assert.AreEqual(ActionPerformed.ReadNoChanges, _httpDataProvider.ActionPerformed);
			Assert.AreEqual(1, companyDs.Anagrafiche.Count);

			System.Threading.Thread.Sleep(SleepLength);

            // if we delete an object on remote...
            var r = await rc.DeleteAsync("contacts", contact);
			Assert.AreEqual(HttpStatusCode.NoContent, r.StatusCode);
   //         r = await rc.DeleteAsync("documents", doc);
			//Assert.AreEqual(HttpStatusCode.NoContent, r.StatusCode);


			// ... we can then sync the delete down.
			await _httpDataProvider.GetAsync(companyDs);
			Assert.AreEqual(ActionPerformed.Read, _httpDataProvider.ActionPerformed);
			Assert.AreEqual(0, companyDs.Anagrafiche.Count);
			Assert.AreEqual(0, companyDs.Documenti.Count);
        }

        public  async void ValidateUnknownRow(DataRow r, string endpoint)
        {

            // make sure remote remote endpoint is completely empty
            var rc = new HttpClient {BaseAddress = new Uri(Service)};
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}",endpoint)).Result.StatusCode == HttpStatusCode.NoContent);

			// perform the operation
			await _httpDataProvider.UpdateAziendeAsync(r);
			Assert.AreEqual(ActionPerformed.Added, _httpDataProvider.ActionPerformed);
			Assert.AreEqual(HttpStatusCode.Created, _httpDataProvider.HttpResponse.StatusCode);
            ValidateSyncDb(r, endpoint);
        }

        public  async void ValidateKnownRow(DataRow r, string endpoint)
        {

			// perform the operation
			await _httpDataProvider.UpdateAziendeAsync(r);
			Assert.AreEqual(_httpDataProvider.ActionPerformed, ActionPerformed.Modified);
			Assert.AreEqual(_httpDataProvider.HttpResponse.StatusCode, HttpStatusCode.OK);
			Assert.AreEqual(1, _httpDataProvider.UpdatesPerformed.Count);
			Assert.AreEqual("Aziende", _httpDataProvider.UpdatesPerformed[0].TableName);
            ValidateSyncDb(r, endpoint);
        }

        public  async void ValidateBadUnknownRow(DataRow r, string endpoint)
        {

            // make sure remote remote endpoint is completely empty
            var rc = new HttpClient {BaseAddress = new Uri(Service)};
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}",endpoint)).Result.StatusCode == HttpStatusCode.NoContent);

			// perform the operation
			await _httpDataProvider.UpdateAziendeAsync(r);
			Assert.AreEqual(ActionPerformed.Aborted, _httpDataProvider.ActionPerformed);
			Assert.AreEqual(422, (int) _httpDataProvider.HttpResponse.StatusCode);
			Assert.AreEqual(0, _httpDataProvider.UpdatesPerformed.Count);

			// test that row mapping record is still non-existant
			Assert.AreEqual(0, _db.Table<HttpMapping>().Count());
        }

        private void ValidateSyncDb(DataRow r, string endpoint, bool shouldTestRemote = true)
        {
            int localId;
            Int32.TryParse(r["Id"].ToString(), out localId);

            // test that row mapping record is actually stored in syncdb.
            var objs = _db.Table<HttpMapping>().Where(v => v.Resource == endpoint && v.LocalId ==  localId); 
            Assert.AreEqual(1, objs.Count());

            // test that mapping is valid.
            var mapping = objs.First();
            Assert.IsNotNull(mapping.RemoteId);
            Assert.IsNotNull(mapping.ETag);
            Assert.IsNotNull(mapping.LastUpdated);
            Assert.IsTrue(mapping.Id > 0);
            Assert.AreEqual(mapping.Resource, endpoint);

            if (!shouldTestRemote) return;
            // test that remote item exists at the specified endpoint.
            var rc = new EveClient (Service);
            var response = rc.GetAsync(endpoint, mapping.RemoteId).Result;
            Assert.AreEqual(response.StatusCode, HttpStatusCode.OK);
        }

        private async void ValidateKnownDeletedRow(DataRow r, string endpoint)
        {
            using (var dp = new HttpDataProvider())
            {
                var localId = (int) r["Id", DataRowVersion.Original];

                var objs = _db.Table<HttpMapping>().Where(v => v.Resource == endpoint && v.LocalId ==  localId); 
                var mapping = objs.First();

                // perform the operation
                await dp.UpdateAziendeAsync(r);
                Assert.AreEqual(ActionPerformed.Deleted, dp.ActionPerformed);
                Assert.AreEqual(HttpStatusCode.NoContent, dp.HttpResponse.StatusCode);
                Assert.AreEqual(1, dp.UpdatesPerformed.Count);
                Assert.AreEqual("Aziende", dp.UpdatesPerformed[0].TableName);

                // test that row mapping record has been removed
                objs = _db.Table<HttpMapping>().Where(v => v.Resource == endpoint && v.LocalId == localId);
                Assert.AreEqual(objs.Count(), 0);

                // test that remote item does not exist at its previous endpoint.
                var rc = new EveClient { BaseAddress = new Uri(Service) };
                var response = rc.GetAsync(string.Format("{0}/{1}", endpoint, mapping.RemoteId)).Result;
                Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
            }
            
        }
        private async void ValidateUnknownDeletedRow(DataRow r, string endpoint)
        {
			var localId = (int) r["Id", DataRowVersion.Original];

			// perform the operation
			await _httpDataProvider.UpdateAziendeAsync(r);
			// since we did not have this row we did no action at all
			Assert.AreEqual(_httpDataProvider.ActionPerformed, ActionPerformed.NoAction);
			// therefore, we got no HttpResponse back.
			Assert.IsNull(_httpDataProvider.HttpResponse);
			Assert.AreEqual(0, _httpDataProvider.UpdatesPerformed.Count);

			// test that row mapping record is still non-existant
			var objs = _db.Table<HttpMapping>().Where(v => v.Resource == endpoint && v.LocalId == localId);
			Assert.AreEqual(objs.Count(), 0);
        }
    }
}
