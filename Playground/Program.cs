using System;
using System.Net.Http;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amica.vNext.Compatibility;
using Amica.vNext.Models;
using Amica.Data;
using Eve;

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

            var nr = dp.Aziende.NewAziendeRow();
            nr.Nome = "newanna";
            nr.Id = 107;
            dp.Aziende.AddAziendeRow(nr);
            nr.AcceptChanges();
            //nr.Delete();
            //nr.SetModified();

            //var nr = dp.Nazioni.NewNazioniRow();
            //nr.Nome = "italia";
            //nr.Id = 100;
            //dp.Nazioni.AddNazioniRow(nr);

            //var nr = dp.Nazioni.NewNazioniRow();
            //nr.Nome = "italia";
            //nr.Id = 1;
            //dp.Nazioni.AddNazioniRow(nr);
            //nr.AcceptChanges();

            //dp.Aziende.PrimaryKey = new[] {dp.Aziende.IdColumn};
            //var countries = FromAmica2.ToList<Country>(dp.Nazioni);
            //var country = FromAmica.To<Country>(nr);
            var hdp = new HttpDataProvider("http://10.0.2.2:5000", 106);

            //try
            //{

            //await hdp.UpdateNazioniAsync(nr);
            //await hdp.UpdateAziendeAsync(nr);
            await hdp.GetAziendeAsync(dp);
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
