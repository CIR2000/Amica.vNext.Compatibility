using System.Collections.Generic;
using System.Data;
using System.Linq;
using AutoMapper;

namespace Amica.vNext.Compatibility
{
    public class FromAmica
    {
        static FromAmica()
        {
            Mapper.Configuration.AddProfile<NazioniProfile>();
        }

        /// <summary>
        /// Returns a List of objects casted by a Amica10 DataTable source.
        /// </summary>
        /// <typeparam name="T">The type of objects to be returned.</typeparam>
        /// <param name="dt">A Supported Amica10 DataTable.</param>
        /// <returns></returns>
        public  static List<T> ToList<T>(DataTable dt)
        {
                return Mapper.Map<List<DataRow>, List<T>>(dt.AsEnumerable().ToList());
        }

        /// <summary>
        /// Returns an object casted by a Asupported Amica10 DataRow.
        /// </summary>
        /// <typeparam name="T">The type of the object to be returned</typeparam>
        /// <param name="dr">A supprted Amica10 DataRow</param>
        /// <returns></returns>
        public static T To<T>(DataRow dr) 
        {
                return Mapper.Map<T>(dr);
        }

    }
}
