﻿using Amica.vNext.Compatibility.Profiles;
using AutoMapper;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Amica.vNext.Compatibility
{
    /// <summary>
    /// Allows to cast Amica 10 ADO DataRows to corresponding Amica.vNext.Objects instances.
    /// </summary>
    public class FromAmica
    {
        static FromAmica()
        {
            Mapper.Configuration.AddProfile<NazioniProfile>();
            Mapper.Configuration.AddProfile<CompanyProfile>();
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
