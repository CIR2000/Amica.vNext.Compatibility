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


            var nr = dp.Aziende.NewAziendeRow();
            nr.Nome = "anna";
            nr.Id = 105;
            dp.Aziende.AddAziendeRow(nr);
            //nr.AcceptChanges();
            //nr.Delete();
            //nr.SetModified();

            //var a = dp.AreeGeografiche.NewAreeGeograficheRow();
            //nr.Nome = "nome";
            //nr.Id = 99;
            //dp.AreeGeografiche.AddAreeGeograficheRow(a);

            //var countries = FromAmica.ToList<Country>(dp.Nazioni);
            //var country = FromAmica.To<Country>(nr);
            var hdp = new HttpDataProvider("http://amica-test.herokuapp.com", new BasicAuthenticator("token1", ""));

            try
            {
                await hdp.UpdateAziendeAsync(nr);
            }
            catch (Exception e) 
            {
                throw e;
            }

            Console.WriteLine(hdp.HttpResponse.StatusCode);

        }
    }
}
