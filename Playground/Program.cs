using System.Data;
using System.Threading.Tasks;
using Amica.vNext.Compatibility;
using Amica.Data;

namespace ConsoleApplication1
{
    class Program
    {
        static  void Main(string[] args)
        {

            Test().Wait();
        }

        static async Task Test()
        {
            var dp = new configDataSet();
            var cdp = new companyDataSet();

            //var nr = dp.Aziende.NewAziendeRow();
            //nr.Nome = "az1";
            //nr.Id = 1;
            //dp.Aziende.AddAziendeRow(nr);
            //nr.AcceptChanges();
            //nr.Delete();
            //nr.SetModified();

            //var nz = cdp.Nazioni.NewNazioniRow();
            //nz.Nome = "italia";
            //nz.Id = 100;
            //cdp.Nazioni.AddNazioniRow(nz);

            //var nr = dp.Nazioni.NewNazioniRow();
            //nr.Nome = "italia";
            //nr.Id = 1;
            //dp.Nazioni.AddNazioniRow(nr);
            //nr.AcceptChanges();

            //dp.Aziende.PrimaryKey = new[] {dp.Aziende.IdColumn};
            //var countries = FromAmica2.ToList<Country>(dp.Nazioni);
            //var country = FromAmica.To<Country>(nr);
            var hdp = new HttpDataProvider("http://10.0.2.2:5000", 1);

            //try
            //{

            await hdp.GetAsync((DataSet)cdp);
            //await hdp.UpateAsync(dp);
            //await hdp.UpateAsync(cdp);
            //await hdp.UpdateNazioniAsync(nr);
            //await hdp.UpdateAziendeAsync(nr);
            //await hdp.GetAziendeAsync(dp);
            //await hdp.GetNazioniAsync(dp);
            //}
            //catch (Exception e) 
            //{
            //throw e;
            //}

            //Console.WriteLine(hdp.HttpResponse.StatusCode);

        }
    }
}
