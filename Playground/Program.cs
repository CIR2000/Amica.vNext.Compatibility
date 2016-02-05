using System;
using System.Data;
using System.Threading.Tasks;
using Amica.vNext.Compatibility;
using Amica.Data;
using Nito.AsyncEx;

namespace ConsoleApplication1
{
    class Program
    {
        static  void Main(string[] args)
        {
            try
            {
                AsyncContext.Run(() => Test());
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }
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
            //nz.Nome = "italia2";
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
            var hdp = new HttpDataProvider(new DataProvider("C:\\Amica 10\\Database"), "nicola", "nicola")
            {
                LocalCompanyId = 1
            };
            //hdp.DataProvider.LoadConfigData();

            //try
            //{

            await hdp.GetAsync(cdp);
            //await hdp.UpdateAsync(cdp);
            //await hdp.GetAsync((DataSet)cdp);
            //await hdp.UpateAsync(dp);
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
