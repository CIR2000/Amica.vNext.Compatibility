using System.Collections.Generic;
using System.Data;
using System.Linq;
using Amica.Data;
using AutoMapper;

namespace Amica.vNext.Objects
{
    public class FromAmica
    {
        static FromAmica()
        {
            Mapper.Configuration.AddProfile<NazioniProfile>();
        }


        public static List<T> ToList<T>(DataTable dt)
        {
            //if (typeof(T) == typeof(Country)) {
            var t = (companyDataSet.NazioniDataTable) dt;
            return Mapper.Map<List<companyDataSet.NazioniRow>, List<T>>(t.ToList());
            //}
        }
    }
}
