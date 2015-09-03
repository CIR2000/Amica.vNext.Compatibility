using Amica.Data;
using Eve;
using NUnit.Framework;
using SQLite;
using System;
using System.Data;
using System.IO;
using System.Net;
using System.Net.Http;

namespace Amica.vNext.Compatibility.Tests
{
    [TestFixture]
    public class TestHttpDataProvider
    {
        private SQLiteConnection _db;
        // We are running Windows in a VirtualBox VM so in order to access the OSX Host 'localhost'
        // where a local instance of the REST API is running, we use standard 10.0.2.2:5000
        private const string Service = "http://10.0.2.2:5000/";
        private const string DbName = "HttpSync.db";

        [SetUp]
        public void Init()
        {
            // ensure the file does not exist before instantiaton.
            File.Delete(DbName);

            _db = new SQLiteConnection(DbName);
        }

        [TearDown]
        public void TearDown()
        {
            if (_db != null) {
                _db.Dispose();
            }

        }

        /// <summary>
        /// Test that the databases structure is aligned with the current HttpMapping class structure.
        /// </summary>
        [Test]
        public void DatabaseStructure()
        {
            using (new HttpDataProvider()) {
                var props = typeof (HttpMapping).GetProperties();
                var tableInfo = _db.GetTableInfo("HttpMapping");
                for (var i = 0; i < tableInfo.Count; i++) {
                    Assert.AreEqual(tableInfo[i].Name, props[i].Name);
                }
            }
        }

        [Test]
        public void DefaultProperties()
        {
            using (var dp = new HttpDataProvider()) {
                Assert.IsNull(dp.BaseAddress);
                Assert.IsNull(dp.Authenticator);
                Assert.IsNull(dp.HttpResponse);
                Assert.AreEqual(dp.ActionPerformed, ActionPerformed.NoAction);
                Assert.AreEqual(dp.SyncDatabaseName, DbName);
            }
        }

        [Test]
        public void CustomConstructors()
        {
            const string baseAddress = "baseaddress";
            var auth = new BasicAuthenticator("username", "password");

            using (var dp = new HttpDataProvider(1)) {
                Assert.AreEqual(dp.LocalCompanyId, 1);
            }

            using (var dp = new HttpDataProvider(auth)) {
                Assert.AreEqual(dp.Authenticator, auth);
            }
            using (var dp = new HttpDataProvider(auth, 1)) {
                Assert.AreEqual(dp.Authenticator, auth);
                Assert.AreEqual(dp.LocalCompanyId, 1);
            }

            using (var dp = new HttpDataProvider(baseAddress)) {
                Assert.AreEqual(dp.BaseAddress, baseAddress);
            }
            using (var dp = new HttpDataProvider(baseAddress, 1)) {
                Assert.AreEqual(dp.BaseAddress, baseAddress);
                Assert.AreEqual(dp.LocalCompanyId, 1);
            }

            using (var dp = new HttpDataProvider(baseAddress, auth)) {
                Assert.AreEqual(dp.Authenticator, auth);
                Assert.AreEqual(dp.BaseAddress, baseAddress);
            }
            using (var dp = new HttpDataProvider(baseAddress, auth, 1)) {
                Assert.AreEqual(dp.Authenticator, auth);
                Assert.AreEqual(dp.BaseAddress, baseAddress);
                Assert.AreEqual(dp.LocalCompanyId, 1);
            }
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
        public void ModifyKnownAziendeRow()
        {
            var ds = new configDataSet();
            var n = ds.Aziende.NewAziendeRow();
            n.Nome = "company";
            n.Id = 99;
            ds.Aziende.AddAziendeRow(n);
            using (var dp = GetHttpDataProvider())
            {
                dp.UpdateAziendeAsync(n).Wait();
                Assert.AreEqual(dp.HttpResponse.StatusCode, HttpStatusCode.Created);

                n.AcceptChanges();
                n.SetModified();

                n.Nome = "modified company";
                ValidateKnownRow(n, "companies");
            }
        }

        [Test]
        public void DeleteKnownAziendeRow()
        {
            
            var ds = new configDataSet();
            var row = ds.Aziende.NewAziendeRow();
            row.Nome = "company";
            row.Id = 99;
            ds.Aziende.AddAziendeRow(row);

            using (var dp = GetHttpDataProvider()) {
                dp.UpdateAziendeAsync(row).Wait();

                row.AcceptChanges();
                row.Delete();
            }
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

        public  void ValidateUnknownRow(DataRow r, string endpoint)
        {

            // make sure remote remote endpoint is completely empty
            var rc = new HttpClient {BaseAddress = new Uri(Service)};
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}",endpoint)).Result.StatusCode == HttpStatusCode.NoContent);

            using (var dp = GetHttpDataProvider()) {

                // perform the operation
                dp.UpdateAziendeAsync(r).Wait();
                Assert.AreEqual(ActionPerformed.Added, dp.ActionPerformed);
                Assert.AreEqual(HttpStatusCode.Created, dp.HttpResponse.StatusCode);
            }
            ValidateSyncDb(r, endpoint);
        }

        public  void ValidateKnownRow(DataRow r, string endpoint)
        {
            using (var dp = GetHttpDataProvider()) {
                // perform the operation
                dp.UpdateAziendeAsync(r).Wait();
                Assert.AreEqual(dp.ActionPerformed, ActionPerformed.Modified);
                Assert.AreEqual(dp.HttpResponse.StatusCode, HttpStatusCode.OK);
            }
            ValidateSyncDb(r, endpoint);
        }

        public  void ValidateBadUnknownRow(DataRow r, string endpoint)
        {

            // make sure remote remote endpoint is completely empty
            var rc = new HttpClient {BaseAddress = new Uri(Service)};
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}",endpoint)).Result.StatusCode == HttpStatusCode.NoContent);

            using (var dp = GetHttpDataProvider()) {

                // perform the operation
                dp.UpdateAziendeAsync(r).Wait();
                Assert.AreEqual(ActionPerformed.Aborted, dp.ActionPerformed);
                Assert.AreEqual(422, (int) dp.HttpResponse.StatusCode);

                // test that row mapping record is still non-existant
                Assert.AreEqual(0, _db.Table<HttpMapping>().Count());
            }
        }

        private void ValidateSyncDb(DataRow r, string endpoint)
        {
            int localId;
            Int32.TryParse(r["Id"].ToString(), out localId);

            // test that row mapping record is actually stored in syncdb.
            var objs = _db.Table<HttpMapping>().Where(v => v.Resource == endpoint && v.LocalId ==  localId); 
            Assert.AreEqual(objs.Count(), 1);

            // test that mapping is valid.
            var mapping = objs.First();
            Assert.IsNotNull(mapping.RemoteId);
            Assert.IsNotNull(mapping.ETag);
            Assert.IsNotNull(mapping.LastUpdated);
            Assert.IsTrue(mapping.Id > 0);
            // next two asserts are superfluous, we just add them for coherence
            Assert.AreEqual(mapping.LocalId, 99);
            Assert.AreEqual(mapping.Resource, endpoint);

            // test that remote item exists at the specified endpoint.
            var rc = new EveClient (Service);
            var response = rc.GetAsync(endpoint, mapping.RemoteId).Result;
            Assert.AreEqual(response.StatusCode, HttpStatusCode.OK);
        }

        private void ValidateKnownDeletedRow(DataRow r, string endpoint)
        {
            using (var dp = GetHttpDataProvider())
            {
                var localId = (int) r["Id", DataRowVersion.Original];

                var objs = _db.Table<HttpMapping>().Where(v => v.Resource == endpoint && v.LocalId ==  localId); 
                var mapping = objs.First();

                // perform the operation
                dp.UpdateAziendeAsync(r).Wait();
                Assert.AreEqual(ActionPerformed.Deleted, dp.ActionPerformed);
                Assert.AreEqual(HttpStatusCode.NoContent, dp.HttpResponse.StatusCode);

                // test that row mapping record has been removed
                objs = _db.Table<HttpMapping>().Where(v => v.Resource == endpoint && v.LocalId == localId);
                Assert.AreEqual(objs.Count(), 0);

                // test that remote item does not exist at its previous endpoint.
                var rc = new EveClient { BaseAddress = new Uri(Service) };
                var response = rc.GetAsync(string.Format("{0}/{1}", endpoint, mapping.RemoteId)).Result;
                Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
            }
            
        }
        private void ValidateUnknownDeletedRow(DataRow r, string endpoint)
        {
            using (var dp = GetHttpDataProvider())
            {
                var localId = (int) r["Id", DataRowVersion.Original];

                // perform the operation
                dp.UpdateAziendeAsync(r).Wait();
                // since we did not have this row we did no action at all
                Assert.AreEqual(dp.ActionPerformed, ActionPerformed.NoAction);
                // therefore, we got no HttpResponse back.
                Assert.IsNull(dp.HttpResponse);

                // test that row mapping record is still non-existant
                var objs = _db.Table<HttpMapping>().Where(v => v.Resource == endpoint && v.LocalId == localId);
                Assert.AreEqual(objs.Count(), 0);
            }
        }

        private static HttpDataProvider GetHttpDataProvider()
        {
            // We are running Windows in a VirtualBox VM so in order to access the OSX Host 'localhost'
            // where a local instance of the REST API is running, we use standard 10.0.2.2:5000
            return new HttpDataProvider(Service);
        }

    }
}
