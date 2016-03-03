using Amica.Data;
using Eve;
using NUnit.Framework;
using SQLite;
using System;
using System.Data;
using System.Net;
using System.Net.Http;
using Amica.vNext.Models;
using Amica.vNext.Models.Documents;

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
		    var contact = new Contact
		    {
		        CompanyId = company.UniqueId,
		        Name = "Name",
		        Vat = "Vat",
				IdCode = "id_code",
				TaxIdCode = "tax_id_code",
				MarketArea = "Lombardia",
				PublicAdministrationIndex = "123456",
				Currency = new Currency
                {
					Name = "Euro",
					Code = "EUR",
					Symbol="€"
                },
		        Address = new AddressEx
                {
                    Street = "Street",
					Country = "Italia"
                },
				Bank = new Bank
				{
					Name = "Bank",
					IbanCode="Iban",
					BicSwiftCode="Swift"
				},
		    };
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

            var a = companyDs.Anagrafiche[0];
            Assert.That(a.RagioneSociale1, Is.EqualTo(contact.Name));
            Assert.That(a.Codice, Is.EqualTo(contact.IdCode));
            Assert.That(a.CodiceFiscale, Is.EqualTo(contact.TaxIdCode));
            Assert.That(a.Indirizzo, Is.EqualTo(contact.Address.Street));
            Assert.That(a.PartitaIVA, Is.EqualTo(contact.Vat));
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

            // remotely edit the contact 
            contact.MarketArea = "Emilia";
            contact.Currency.Name = "US Dollar";
            contact.Currency.Code = "USD";
            contact.Address.Country = "USA";
            contact.MarketArea = "new marketarea";
            contact.Name = "New Name";
            contact.IdCode = "New IdCode";
            contact.TaxIdCode = "New TaxIdCode";
            contact.Is.Client = true;
            contact.Is.AreaManager = true;
            contact.Bank.Name = "new bank name";
            contact.Bank.IbanCode = "new iban code";
            contact.PublicAdministrationIndex = "newidx";

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

            a = companyDs.Anagrafiche[0];
            Assert.That(a.RagioneSociale1, Is.EqualTo(contact.Name));
            Assert.That(a.Codice, Is.EqualTo(contact.IdCode));
            Assert.That(a.CodiceFiscale, Is.EqualTo(contact.TaxIdCode));
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
            Assert.That(a.AreeGeograficheRow.Nome, Is.EqualTo(contact.MarketArea));
            Assert.That(a.ValuteRow.Nome, Is.EqualTo(contact.Currency.Name));
            Assert.That(a.ValuteRow.Sigla, Is.EqualTo(contact.Currency.Code));


            await adam.DeleteAsync(contact);
            Assert.That(adam.HttpResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

            System.Threading.Thread.Sleep(SleepLength);

            await _httpDataProvider.GetAsync(companyDs);
            Assert.That(_httpDataProvider.ActionPerformed, Is.EqualTo(ActionPerformed.Read));
            Assert.That(companyDs.Anagrafiche.Count, Is.EqualTo(0));
            Assert.That(companyDs.Nazioni.Count, Is.EqualTo(2));
            Assert.That(companyDs.AreeGeografiche.Count, Is.EqualTo(2));
            Assert.That(companyDs.Valute.Count, Is.EqualTo(2));
        }


        [Test]
        public async void DownloadDocuments()
        {
            // make sure remote remote endpoint is completely empty
            var rc = new HttpClient {BaseAddress = new Uri(Service)};
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "companies")).Result.StatusCode == HttpStatusCode.NoContent);
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "documents")).Result.StatusCode == HttpStatusCode.NoContent);
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "contacts")).Result.StatusCode == HttpStatusCode.NoContent);


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
            var adam = new EveClient (Service);
		    var companies = await adam.GetAsync<Company>("companies");
            var company = companies[0];

			// create vnext contact and post it
		    var contact = new Contact
		    {
		        CompanyId = company.UniqueId,
		        Name = "Name",
		        Vat = "Vat",
				MarketArea = "Lombardia",
				Currency = new Currency
                {
					Name = "Euro",
					Code = "EUR",
					Symbol="€"
                },
		        Address = new AddressEx
                {
                    Street = "Street",
					Country = "Italia"
                }
		    };
		    contact = await adam.PostAsync<Contact>("contacts", contact);

			// new vnext invoice, complete with contact and items, and post it
		    var doc = new Invoice
		    {
		        CompanyId = company.UniqueId,
		        Total = 100,
		        Contact = new ContactMinimal(contact)
		    };

		    var item = new DocumentItem
		    {
		        Description = "descriptrion1",
		        Sku = "sku1"
		    };
		    doc.Items.Add(item);

            adam.ResourceName = "documents";
            doc = await adam.PostAsync<Invoice>(doc);

            var doc2 = new Invoice
            {
                CompanyId = company.UniqueId,
                Total = 99,
                Contact = doc.Contact,
            };
			doc2.Items.Add(new DocumentItem { Description = "description3", Sku = "sku3" });

            doc2 = await adam.PostAsync<Invoice>(doc2);

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
            Assert.That(companyDs.Documenti.Count, Is.EqualTo(2));
            Assert.That(companyDs.Righe.Count, Is.EqualTo(2));
            Assert.That(companyDs.Nazioni.Count, Is.EqualTo(1));
            Assert.That(companyDs.AreeGeografiche.Count, Is.EqualTo(1));

            var a = companyDs.Anagrafiche[0];
            var d1 = companyDs.Documenti[0];
            var d2 = companyDs.Documenti[1];
            var ri1 = companyDs.Righe[0];
            var ri2 = companyDs.Righe[1];
            var n = companyDs.Nazioni[0];
            var ag = companyDs.AreeGeografiche[0];
            Assert.That(a.RagioneSociale1, Is.EqualTo(doc.Contact.Name));
            Assert.That(a.Indirizzo, Is.EqualTo(doc.Contact.Street));
            Assert.That(a.NazioniRow.Nome, Is.EqualTo(doc.Contact.Country));
            Assert.That(a.PartitaIVA, Is.EqualTo(doc.Contact.Vat));

            Assert.That(d1.IdAnagrafica, Is.EqualTo(a.Id));
            Assert.That(d1.TotaleFattura, Is.EqualTo(doc.Total));
            Assert.That(d1.IdTipoDocumento, Is.EqualTo((int)doc.Type));

            Assert.That(ri1.IdDocumento, Is.EqualTo(d1.Id));
            Assert.That(ri1.CodiceArticolo, Is.EqualTo(doc.Items[0].Sku));
            Assert.That(ri1.Descrizione, Is.EqualTo(doc.Items[0].Description));
            Assert.That(ri2.IdDocumento, Is.EqualTo(d2.Id));
            Assert.That(ri2.CodiceArticolo, Is.EqualTo(doc2.Items[0].Sku));
            Assert.That(ri2.Descrizione, Is.EqualTo(doc2.Items[0].Description));

            Assert.That(n.Nome, Is.EqualTo(doc.Contact.Country));
            Assert.That(ag.Nome, Is.EqualTo(contact.MarketArea));

            // now remotely update the document by changing 1 item and adding a new one
            doc.Items[0].Sku = "updated sku1";
		    item = new DocumentItem
		    {
		        Description = "new description",
		        Sku = "new sku"
		    };
		    doc.Items.Add(item);

            doc = await adam.PutAsync<Invoice>(doc);

			// test that it syncs fine on Amica classic
            await _httpDataProvider.GetAsync(companyDs);
            Assert.That(_httpDataProvider.ActionPerformed, Is.EqualTo(ActionPerformed.Read));
            Assert.That(companyDs.Anagrafiche.Count, Is.EqualTo(1));
            Assert.That(companyDs.Documenti.Count, Is.EqualTo(2));
            Assert.That(companyDs.Righe.Count, Is.EqualTo(3));

            a = companyDs.Anagrafiche[0];
            d1 = companyDs.Documenti[0];
            d2 = companyDs.Documenti[1];
            var righe = companyDs.Righe.Select("", "IdDocumento");
            ri1 = (companyDataSet.RigheRow)righe[0];
            ri2 = (companyDataSet.RigheRow)righe[1];
            var ri3 = (companyDataSet.RigheRow)righe[2];

            Assert.That(a.RagioneSociale1, Is.EqualTo(doc.Contact.Name));
            Assert.That(a.Indirizzo, Is.EqualTo(doc.Contact.Street));
            Assert.That(a.PartitaIVA, Is.EqualTo(doc.Contact.Vat));

            Assert.That(d1.IdAnagrafica, Is.EqualTo(a.Id));
            Assert.That(d1.TotaleFattura, Is.EqualTo(doc.Total));
            Assert.That(d1.IdTipoDocumento, Is.EqualTo((int)doc.Type));

            Assert.That(ri1.IdDocumento, Is.EqualTo(d1.Id));
            Assert.That(ri1.CodiceArticolo, Is.EqualTo(doc.Items[0].Sku));
            Assert.That(ri1.Descrizione, Is.EqualTo(doc.Items[0].Description));
            Assert.That(ri2.IdDocumento, Is.EqualTo(d1.Id));
            Assert.That(ri2.CodiceArticolo, Is.EqualTo(doc.Items[1].Sku));
            Assert.That(ri2.Descrizione, Is.EqualTo(doc.Items[1].Description));
            Assert.That(ri3.IdDocumento, Is.EqualTo(d2.Id));
            Assert.That(ri3.CodiceArticolo, Is.EqualTo(doc2.Items[0].Sku));
            Assert.That(ri3.Descrizione, Is.EqualTo(doc2.Items[0].Description));

            // On remote, add a new contact and update the document with it
            // create vnext contact and post it
            var newContact = new Contact
            {
                CompanyId = company.UniqueId,
		        Name = "new name",
		        Vat = "new vat",
		        Address = new AddressEx
                {
                    Street = "Street",
					Country = "Russia"
                }
		    };
		    newContact = await adam.PostAsync<Contact>("contacts", newContact);
            doc.Contact = new ContactMinimal(newContact);
            doc = await adam.PutAsync<Invoice>(doc);

			System.Threading.Thread.Sleep(SleepLength);

			// test that it syncs fine on Amica classic
            await _httpDataProvider.GetAsync(companyDs);
            Assert.That(_httpDataProvider.ActionPerformed, Is.EqualTo(ActionPerformed.Read));
            Assert.That(companyDs.Anagrafiche.Count, Is.EqualTo(2));
            Assert.That(companyDs.Documenti.Count, Is.EqualTo(2));
            Assert.That(companyDs.Righe.Count, Is.EqualTo(3));
            Assert.That(companyDs.Nazioni.Count, Is.EqualTo(2));

            var anagrafiche = companyDs.Anagrafiche.Select("", "Id");
            a = (companyDataSet.AnagraficheRow)anagrafiche[1];
            var docs = companyDs.Documenti.Select("", "Id");
            d1 = (companyDataSet.DocumentiRow)docs[0];
            righe = companyDs.Righe.Select("", "IdDocumento");
            ri1 = (companyDataSet.RigheRow)righe[0];
            ri2 = (companyDataSet.RigheRow)righe[1];

            Assert.That(a.RagioneSociale1, Is.EqualTo(doc.Contact.Name));
            Assert.That(a.Indirizzo, Is.EqualTo(doc.Contact.Street));
            Assert.That(a.NazioniRow.Nome, Is.EqualTo(doc.Contact.Country));
            Assert.That(a.PartitaIVA, Is.EqualTo(doc.Contact.Vat));

            Assert.That(d1.IdAnagrafica, Is.EqualTo(a.Id));
            Assert.That(d1.TotaleFattura, Is.EqualTo(doc.Total));
            Assert.That(d1.IdTipoDocumento, Is.EqualTo((int)doc.Type));

            Assert.That(ri1.IdDocumento, Is.EqualTo(d1.Id));
            Assert.That(ri1.CodiceArticolo, Is.EqualTo(doc.Items[0].Sku));
            Assert.That(ri1.Descrizione, Is.EqualTo(doc.Items[0].Description));
            Assert.That(ri2.IdDocumento, Is.EqualTo(d1.Id));
            Assert.That(ri2.CodiceArticolo, Is.EqualTo(doc.Items[1].Sku));
            Assert.That(ri2.Descrizione, Is.EqualTo(doc.Items[1].Description));

            // test that when a doc is deleted remotely, local sync
            // gets rid of both main and child rows

            await adam.DeleteAsync(doc);
            Assert.That(adam.HttpResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

			System.Threading.Thread.Sleep(SleepLength);

            await _httpDataProvider.GetAsync(companyDs);
            //Assert.That(_httpDataProvider.ActionPerformed, Is.EqualTo(ActionPerformed.Read));
            Assert.That(companyDs.Anagrafiche.Count, Is.EqualTo(2));
            Assert.That(companyDs.Nazioni.Count, Is.EqualTo(2));
            Assert.That(companyDs.Documenti.Count, Is.EqualTo(1));
            Assert.That(companyDs.Righe.Count, Is.EqualTo(1));
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
            a.PartitaIVA = "vat";
            a.Codice = "idcode";
            a.CodiceFiscale = "taxidcode";
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
            a.BancaIBAN = "iban";
            a.IndicePA = "paidx";
            ds.Anagrafiche.AddAnagraficheRow(a);

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
            Assert.That(a.PartitaIVA, Is.EqualTo(contact.Vat));
            Assert.That(a.Codice, Is.EqualTo(contact.IdCode));
            Assert.That(a.CodiceFiscale, Is.EqualTo(contact.TaxIdCode));
            Assert.That(a.Indirizzo, Is.EqualTo(contact.Address.Street));
            Assert.That(a.IsPersonaGiuridica, Is.EqualTo(contact.Is.Company));
            Assert.That(a.IsAttivo, Is.EqualTo(contact.Is.Active));
            Assert.That(a.IsCliente, Is.EqualTo(contact.Is.Client));
            Assert.That(a.IsFornitore, Is.EqualTo(contact.Is.Vendor));
            Assert.That(a.IsVettore, Is.EqualTo(contact.Is.Courier));
            Assert.That(a.IsAgente, Is.EqualTo(contact.Is.Agent));
            Assert.That(a.IsCapoArea, Is.EqualTo(contact.Is.AreaManager));
            Assert.That(a.BancaNome, Is.EqualTo(contact.Bank.Name));
            Assert.That(a.BancaIBAN, Is.EqualTo(contact.Bank.IbanCode));
            Assert.That(a.IndicePA, Is.EqualTo(contact.PublicAdministrationIndex));
            Assert.That(a.NazioniRow.Nome, Is.EqualTo(contact.Address.Country));
            Assert.That(a.AreeGeograficheRow.Nome, Is.EqualTo(contact.MarketArea));
            Assert.That(a.ValuteRow.Nome, Is.EqualTo(contact.Currency.Name));
            Assert.That(a.ValuteRow.Sigla, Is.EqualTo(contact.Currency.Code));

            ds.AcceptChanges();

			// test that changing a row locally will sync fine upstream
            a.RagioneSociale1 = "changed rs";
            a.IsCapoArea = false;
            a.Codice = "new idcode";
            a.CodiceFiscale = "new taxidcode";
            a.BancaNome = "new bank name";
            a.BancaIBAN = "new bank iban";
            a.IndicePA = "npaidx";

            await _httpDataProvider.UpdateAsync(ds);
            contact = await adam.GetAsync<Contact>(contact);
            Assert.That(contact.Name, Is.EqualTo(a.RagioneSociale1));
            Assert.That(contact.Is.AreaManager, Is.EqualTo(a.IsCapoArea));
            Assert.That(contact.IdCode, Is.EqualTo(a.Codice));
            Assert.That(contact.TaxIdCode, Is.EqualTo(a.CodiceFiscale));
            Assert.That(contact.Bank.Name, Is.EqualTo(a.BancaNome));
            Assert.That(contact.Bank.IbanCode, Is.EqualTo(a.BancaIBAN));
            Assert.That(contact.PublicAdministrationIndex, Is.EqualTo(a.IndicePA));

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

            var c = ds.Anagrafiche.NewAnagraficheRow();
            c.RagioneSociale1 = "rs1";
            c.PartitaIVA = "vat";
            c.Indirizzo = "address";
		    c.IdNazione = n.Id;
            c.IdAreaGeografica = ag.Id;
            c.IdValuta = v.Id;
            ds.Anagrafiche.AddAnagraficheRow(c);


            var d = ds.Documenti.NewDocumentiRow();
            d.IdAnagrafica = c.Id;
            d.IdTipoDocumento = (int)DocumentType.Invoice;
            d.TotaleFattura = 99;
            d.Data = DateTime.Now;
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

		    cds.AcceptChanges();
		    ds.AcceptChanges();

			// Changing a Contact should not affect the ContatMinimal in the Document.
            ds.Anagrafiche.Rows[0]["PartitaIVA"] = "vat1";
            ds.Nazioni.Rows[0]["Nome"] = "Russia";
            await _httpDataProvider.UpdateAsync(ds);
			Assert.AreEqual(ActionPerformed.Modified, _httpDataProvider.ActionPerformed);
			Assert.AreEqual(HttpStatusCode.OK, _httpDataProvider.HttpResponse.StatusCode);
            ValidateSyncDb(d, "documents");
            ValidateSyncDb(ds.Anagrafiche.Rows[0], "contacts");

            var adam = new EveClient (Service);
		    var contacts = await adam.GetAsync<Contact>("contacts");
		    var contact = contacts[0];
		    Assert.AreEqual("vat1", contact.Vat);
		    Assert.AreEqual("Russia", contact.Address.Country);
		    Assert.AreEqual(ag.Nome, contact.MarketArea);
		    Assert.AreEqual(v.Nome, contact.Currency.Name);

		    var docs = await adam.GetAsync<Document>("documents");
		    var doc = docs[0];
		    Assert.AreEqual(contact.UniqueId, doc.Contact.UniqueId);
		    Assert.AreEqual("vat", doc.Contact.Vat);
		    Assert.AreEqual("Italia", doc.Contact.Country);

		    Assert.That(doc.Items.Count, Is.EqualTo(1));
		    var docItem = doc.Items[0];
		    Assert.That(docItem.Sku, Is.EqualTo("Sku"));
		    Assert.That(docItem.Description, Is.EqualTo("Description"));


            doc.Contact.Vat = "vat2";
            doc.Contact.Country = "USA";
            await adam.PutAsync("documents", doc);
            Assert.AreEqual(HttpStatusCode.OK, _httpDataProvider.HttpResponse.StatusCode);
            await _httpDataProvider.GetAsync(ds);
            Assert.AreEqual(ActionPerformed.Read, _httpDataProvider.ActionPerformed);
            // Anagrafiche field and document reference have not changed
            Assert.AreEqual("vat1", ds.Anagrafiche.Rows[0]["PartitaIva"]);
            Assert.AreEqual("Russia", ds.Nazioni.Rows[0]["Nome"]);
            Assert.AreEqual(ds.Anagrafiche.Rows[0]["Id"], ds.Documenti.Rows[0]["IdAnagrafica"]);

            // test that a locally deleted object is deleted fine also remotely
            ds.Documenti[0].Delete();
            await _httpDataProvider.UpdateAsync(ds);
            doc = await adam.GetAsync<Invoice>("documents", doc);
            Assert.That(doc, Is.Null);
            Assert.That(adam.HttpResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
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

            var contact = rc.PostAsync<Contact>("contacts", new Contact() {Name = "Contact1", Vat = "Vat", CompanyId = company.UniqueId}).Result;
            Assert.AreEqual(HttpStatusCode.Created, rc.HttpResponse.StatusCode);

            var doc = rc.PostAsync<Document>("documents", new Invoice() { Contact = new ContactMinimal(contact), CompanyId = company.UniqueId }).Result;
            Assert.AreEqual(HttpStatusCode.Created, rc.HttpResponse.StatusCode);

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
            r = await rc.DeleteAsync("documents", doc);
			Assert.AreEqual(HttpStatusCode.NoContent, r.StatusCode);


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
