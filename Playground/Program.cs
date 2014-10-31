﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amica.vNext.Compatibility;
using Amica.vNext.Objects;
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


            var nr = dp.Aziende.NewAziendeRow();
            nr.Nome = "nome";
            nr.Id = 99;
            dp.Aziende.AddAziendeRow(nr);

            //var a = dp.AreeGeografiche.NewAreeGeograficheRow();
            //nr.Nome = "nome";
            //nr.Id = 99;
            //dp.AreeGeografiche.AddAreeGeograficheRow(a);

            //var countries = FromAmica.ToList<Country>(dp.Nazioni);
            //var country = FromAmica.To<Country>(nr);
            var hdp = new HttpDataProvider();
            await hdp.UpdateAziendaAsync(nr);
            
        }
    }
}
