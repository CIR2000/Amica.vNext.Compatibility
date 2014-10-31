using System;
using System.Reflection;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Amica.Data;
using NUnit.Framework;

namespace Amica.vNext.Compatibility.Tests
{
    [TestFixture]
    public class TestHttpDataProvider
    {
        /// <summary>
        /// Test that a database is created when an instance of HttpDataProvider is created.
        /// </summary>
        [Test]
        public void DatabaseCreation()
        {
            // ensure the file does not exist.
            //var dbFileName = Path.Combine(Environment.CurrentDirectory, "HttpMapping.db");
            //File.Delete(dbFileName);

            var dp = new HttpDataProvider();
            //Assert.IsTrue(File.Exists(dbFileName));

            var adp = new companyDataSet();


            var nr = adp.Nazioni.NewNazioniRow();
            nr.Nome = "nome";
            nr.Id = 99;
            adp.Nazioni.AddNazioniRow(nr);

            //var a = dp.AreeGeografiche.NewAreeGeograficheRow();
            //nr.Nome = "nome";
            //nr.Id = 99;
            //dp.AreeGeografiche.AddAreeGeograficheRow(a);

            //var countries = FromAmica.ToList<Country>(dp.Nazioni);
            //var country = FromAmica.To<Country>(nr);
            var hdp = new HttpDataProvider();
            hdp.UpdateNazioniAsync(nr);

        }
    }
}
