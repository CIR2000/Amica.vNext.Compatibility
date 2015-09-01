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
            //var dp = new configDataSet();
            var dp = new companyDataSet();

            //var nr = dp.Aziende.NewAziendeRow();
            //nr.Nome = "newanna";
            //nr.Id = 106;
            //dp.Aziende.AddAziendeRow(nr);
            //nr.AcceptChanges();
            //nr.Delete();
            //nr.SetModified();

            //var nr = dp.Nazioni.NewNazioniRow();
            //nr.Nome = "nome";
            //nr.Id = 100;
            //dp.Nazioni.AddNazioniRow(nr);

            //var countries = FromAmica.ToList<Country>(dp.Nazioni);
            //var country = FromAmica.To<Country>(nr);
            var hdp = new HttpDataProvider("http://10.0.2.2:5000", 105);

            try
            {

                //await hdp.UpdateNazioniAsync(nr);
                //await hdp.UpdateAziendeAsync(nr);
                //await hdp.GetAziendeAsync(dp.Aziende);
                await hdp.GetNazioniAsync(dp.Nazioni);
            }
            catch (Exception e) 
            {
                throw e;
            }

            Console.WriteLine(hdp.HttpResponse.StatusCode);

        }
    }
}
