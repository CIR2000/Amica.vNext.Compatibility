using Amica.Data;
using Eve;
using NUnit.Framework;
using SQLite;
using System;
using System.Data;
using System.Diagnostics;
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

        //[Test]
		[Test]
        public async void AddDocumentsRow()
        {
            // make sure remote remote endpoint is completely empty
            var rc = new HttpClient {BaseAddress = new Uri(Service)};
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "companies")).Result.StatusCode == HttpStatusCode.NoContent);
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "documents")).Result.StatusCode == HttpStatusCode.NoContent);
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "contacts")).Result.StatusCode == HttpStatusCode.NoContent);
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}", "countries")).Result.StatusCode == HttpStatusCode.NoContent);


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

            var c = ds.Anagrafiche.NewAnagraficheRow();
            c.RagioneSociale1 = "rs1";
            c.PartitaIVA = "vat";
            c.Indirizzo = "address";
		    c.IdNazione = n.Id;
            ds.Anagrafiche.AddAnagraficheRow(c);

            var d = ds.Documenti.NewDocumentiRow();
            d.IdAnagrafica = c.Id;
            d.IdTipoDocumento = (int)DocumentType.Invoice;
            d.TotaleFattura = 99;
            d.Data = DateTime.Now;
            ds.Documenti.AddDocumentiRow(d);

			// perform the operation
            _httpDataProvider.LocalCompanyId = 99;
            //await _httpDataProvider.UpdateDocumentiAsync(d);
            await _httpDataProvider.UpdateAsync(ds);
			Assert.AreEqual(ActionPerformed.Added, _httpDataProvider.ActionPerformed);
			Assert.AreEqual(HttpStatusCode.Created, _httpDataProvider.HttpResponse.StatusCode);
            ValidateSyncDb(d, "documents");
            ValidateSyncDb(c, "contacts");
            ValidateSyncDb(n, "countries");

		    cds.AcceptChanges();
		    ds.AcceptChanges();

			// Changing a Contact should not affect the ContatMinimal in the Document.
            ds.Anagrafiche.Rows[0]["PartitaIVA"] = "vat1";
            await _httpDataProvider.UpdateAsync(ds);
			Assert.AreEqual(ActionPerformed.Modified, _httpDataProvider.ActionPerformed);
			Assert.AreEqual(HttpStatusCode.OK, _httpDataProvider.HttpResponse.StatusCode);
            ValidateSyncDb(d, "documents");
            ValidateSyncDb(ds.Anagrafiche.Rows[0], "contacts");

            var adam = new EveClient (Service);
		    var contacts = await adam.GetAsync<Contact>("contacts");
		    var contact = contacts[0];
		    Assert.AreEqual("vat1", contact.Vat);

		    var docs = await adam.GetAsync<Document>("documents");
		    var doc = docs[0];
		    Assert.AreEqual(contact.UniqueId, doc.Contact.UniqueId);
		    Assert.AreEqual("vat", doc.Contact.Vat);


            doc.Contact.Vat = "vat2";
		    await adam.PutAsync("documents", doc);
			Assert.AreEqual(HttpStatusCode.OK, _httpDataProvider.HttpResponse.StatusCode);
            await _httpDataProvider.GetAsync(ds);
			Assert.AreEqual(ActionPerformed.Read, _httpDataProvider.ActionPerformed);
			// Anagrafiche field and document reference have not changed
            Assert.AreEqual("vat1", ds.Anagrafiche.Rows[0]["PartitaIva"]);
            Assert.AreEqual(ds.Anagrafiche.Rows[0]["Id"], ds.Documenti.Rows[0]["IdAnagrafica"]);

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
                Assert.IsTrue(client.DeleteAsync(string.Format("/{0}", "countries")).Result.StatusCode == HttpStatusCode.NoContent); 
                Assert.IsTrue(client.DeleteAsync(string.Format("/{0}", "documents")).Result.StatusCode == HttpStatusCode.NoContent); 
            }
            
            var rc = new EveClient {BaseAddress = new Uri(Service)};

            // post a new company 
            var company = rc.PostAsync<Company>("companies", new Company() {Name = "Company"}).Result;
            Assert.AreEqual(HttpStatusCode.Created, rc.HttpResponse.StatusCode);

            // post a new country which holds a reference to the previously posted company
            var country = rc.PostAsync<Country>("countries", new Country() {Name = "Country", CompanyId = company.UniqueId}).Result;
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

			System.Threading.Thread.Sleep(1000);

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
			await _httpDataProvider.GetAsync(companyDs);

			// we downloaded one new object and added it to the corresponding table
			Assert.AreEqual(ActionPerformed.Read, _httpDataProvider.ActionPerformed);
			Assert.AreEqual(1, companyDs.Nazioni.Count);
			var nazioniRow = companyDs.Nazioni[0];
			Assert.AreEqual(country.Name, nazioniRow.Nome);
			ValidateSyncDb(nazioniRow, "countries", false);

			// if we try a sync again we don't get anything new since there have been no changes on the remote
			await _httpDataProvider.GetAsync(companyDs);
            Assert.AreEqual(ActionPerformed.ReadNoChanges, _httpDataProvider.ActionPerformed);
            Assert.AreEqual(1, companyDs.Nazioni.Count);

			// test that if the remote object is updated...
			country.Name = "We changed name";
			country = rc.PutAsync<Country>("countries", country).Result;

			System.Threading.Thread.Sleep(1000);

			// ... we can then sync it down effortlessly
			await _httpDataProvider.GetAsync(companyDs);
			Assert.AreEqual(ActionPerformed.Read, _httpDataProvider.ActionPerformed);
			nazioniRow = companyDs.Nazioni[0];
			Assert.AreEqual(country.Name, nazioniRow.Nome);
			ValidateSyncDb(nazioniRow, "countries", false);

			// if we try a sync again we don't get anything new since there have been no changes on the remote
			await _httpDataProvider.GetAsync(companyDs);
			Assert.AreEqual(ActionPerformed.ReadNoChanges, _httpDataProvider.ActionPerformed);
			Assert.AreEqual(1, companyDs.Nazioni.Count);

			System.Threading.Thread.Sleep(1000);

			// if we delete an object on remote...
			var r = rc.DeleteAsync("countries", country).Result;
			Assert.AreEqual(HttpStatusCode.NoContent, r.StatusCode);


			// ... we can then sync the delete down.
			await _httpDataProvider.GetAsync(companyDs);
			Assert.AreEqual(ActionPerformed.Read, _httpDataProvider.ActionPerformed);
			Assert.AreEqual(0, companyDs.Nazioni.Count);
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
