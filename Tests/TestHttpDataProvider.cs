using System;
using System.Net;
using System.Net.Http;
using System.Data;
using System.Reflection;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Amica.Data;
using Newtonsoft.Json;
using NUnit.Framework;
using SQLite;
using Amica.vNext.Http;
using Amica.vNext.Objects;

namespace Amica.vNext.Compatibility.Tests
{
    [TestFixture]
    public class TestHttpDataProvider
    {
        private SQLiteConnection _db;
        // We are running Windows in a VirtualBox VM so in order to access the OSX Host 'localhost'
        // where a local instance of the REST API is running, we use standard 10.0.2.2:5000
        private const string Service = "http://10.0.2.2:5000/";

        [SetUp]
        public void Init()
        {
            // ensure the file does not exist before instantiaton.
            var dbFileName = Path.Combine(Environment.CurrentDirectory, "HttpMapping.db");
            File.Delete("HttpMapping.db");

            _db = new SQLiteConnection("HttpMapping.db");
        }

        [TearDown]
        public void TearDown()
        {
            _db.Dispose();

        }

        /// <summary>
        /// Test that the databases structure is aligned with the current HttpMapping class structure.
        /// </summary>
        [Test]
        public void DatabaseStructure() 
        {
            using (var dp = new HttpDataProvider()) {

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
                Assert.AreEqual(dp.SyncDatabaseName, "HttpMapping.db");
            }
        }

        [Test]
        public void CustomConstructors()
        {
            const string baseAddress = "baseaddress";
            var auth = new BasicAuthenticator("username", "password");

            using (var dp = new HttpDataProvider(auth)) {
                Assert.AreEqual(dp.Authenticator, auth);
            }

            using (var dp = new HttpDataProvider(baseAddress)) {
                Assert.AreEqual(dp.BaseAddress, baseAddress);
            }

            using (var dp = new HttpDataProvider(baseAddress, auth)) {
                Assert.AreEqual(dp.Authenticator, auth);
                Assert.AreEqual(dp.BaseAddress, baseAddress);
            }
        }

        [Test]
        public void NewAziendeRow()
        {

            var ds = new configDataSet();
            var n = ds.Aziende.NewAziendeRow();
            n.Nome = "company";
            n.Id = 99;
            ds.Aziende.AddAziendeRow(n);
            NewRow<Company>(n, "companies");
        }

        public  void NewRow<T>(DataRow r, string endpoint)
        {

            int localId;
            Int32.TryParse(r["Id"].ToString(), out localId);

            // make sure remote remote endpoint is completely empty
            var rc = new HttpClient {BaseAddress = new Uri(Service)};
            Assert.IsTrue(rc.DeleteAsync(string.Format("/{0}",endpoint)).Result.StatusCode == HttpStatusCode.OK);

            using (var dp = GetHttpDataProvider()) {

                dp.UpdateAziendeAsync(r).Wait();
                Assert.AreEqual(dp.HttpResponse.StatusCode, HttpStatusCode.Created);

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
                var response = rc.GetAsync(string.Format("/{1}/{0}", mapping.RemoteId, endpoint)).Result;
                Assert.AreEqual(response.StatusCode, HttpStatusCode.OK);

                // TODO test remote property values too?
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
