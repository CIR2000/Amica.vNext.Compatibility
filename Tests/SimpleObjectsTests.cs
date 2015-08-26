using Amica.Data;
using Amica.vNext.Models;
using NUnit.Framework;

namespace Amica.vNext.Compatibility.Tests
{
    [TestFixture]
    public class SimpleObjectsTests
    {
        [Test]
        public void NazioniDataTableToListOfCountry()
        {
            var dp = new companyDataSet();

            var nr = dp.Nazioni.NewNazioniRow();
            nr.Nome = "nome";
            nr.Id = 99;
            dp.Nazioni.AddNazioniRow(nr);

            var countries = FromAmica.ToList<Country>(dp.Nazioni);
            Assert.AreEqual(countries.Count, 1);

            var country = countries[0];
            Assert.AreEqual(country.Name, "nome");

        }
        [Test]
        public void NazioniRowToCountry()
        {
            var dp = new companyDataSet();

            var nr = dp.Nazioni.NewNazioniRow();
            nr.Nome = "nome";
            nr.Id = 99;
            dp.Nazioni.AddNazioniRow(nr);

            var country = FromAmica.To<Country>(nr);
            Assert.AreEqual(country.Name, "nome");

        }
    }
}
