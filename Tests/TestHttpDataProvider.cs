using System;
using System.Net;
using System.Reflection;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Amica.Data;
using NUnit.Framework;
using SQLite;
using Amica.vNext.Http;

namespace Amica.vNext.Compatibility.Tests
{
    [TestFixture]
    public class TestHttpDataProvider
    {
        private SQLiteConnection _db;

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
        public  void NewAziendeRow()
        {
            using (var dp = GetHttpDataProvider()) {

                var ds = new configDataSet();
                var n = ds.Aziende.NewAziendeRow();
                n.Nome = "company";
                n.Id = 99;
                ds.Aziende.AddAziendeRow(n);

                dp.UpdateAziendeAsync(n).Wait();

                Assert.AreEqual(dp.HttpResponse.StatusCode, HttpStatusCode.Created);
            }
            
        }

        private static HttpDataProvider GetHttpDataProvider()
        {
            return new HttpDataProvider("http://amica-test.herokuapp.com", new BasicAuthenticator("token1", ""));
        }

    }
}
