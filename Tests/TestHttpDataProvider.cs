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
        /// <summary>
        /// Test that a database is actually created when an instance of HttpDataProvider is initiated.
        /// </summary>
        [Test]
        public void DatabaseCreatedOnInstantation()
        {
            const string dbName = "HttpMapping.db";

            // ensure the file does not exist before instantiaton.
            var dbFileName = Path.Combine(Environment.CurrentDirectory, dbName);
            File.Delete(dbFileName);

            using (var dp = new HttpDataProvider()) {
                Assert.IsTrue(File.Exists(dbFileName));
            }
        }

        /// <summary>
        /// Test that the databases structure is aligned with the current HttpMapping class structure.
        /// </summary>
        [Test]
        public void DatabaseStructure() 
        {
            using (var dp = new HttpDataProvider()) {
                var db = new SQLiteConnection(dp.SyncDatabaseName);

                var props = typeof (HttpMapping).GetProperties();
                var tableInfo = db.GetTableInfo("HttpMapping");
                for (var i = 0; i < tableInfo.Count; i++) {
                    Assert.AreEqual(tableInfo[i].Name, props[i].Name);
                }
                dp.Dispose();
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

        [Test][NUnit.Framework.Ignore]
        public async void NewNazioniRow()
        {
            using (var dp = GetHttpDataProvider()) {

                var ds = new companyDataSet();
                var n = ds.Nazioni.NewNazioniRow();
                n.Nome = "nazione";
                n.Id = 99;
                ds.Nazioni.AddNazioniRow(n);

                await dp.UpdateNazioniAsync(n);

                Assert.AreEqual(dp.HttpResponse.StatusCode, HttpStatusCode.Created);

                //var a = dp.AreeGeografiche.NewAreeGeograficheRow();
                //nr.Nome = "nome";
                //nr.Id = 99;
                //dp.AreeGeografiche.AddAreeGeograficheRow(a);

                //var countries = FromAmica.ToList<Country>(dp.Nazioni);
                //var country = FromAmica.To<Country>(nr);
                //var hdp = new HttpDataProvider();
                //hdp.UpdateNazioniAsync(nr);
            }
            
        }

        private static HttpDataProvider GetHttpDataProvider()
        {
            return new HttpDataProvider("http://amica-test.herokuapp.com", new BasicAuthenticator("token1", ""));
        }

    }
}
