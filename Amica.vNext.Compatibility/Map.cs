using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Amica.vNext.Compatibility.Maps;
using Amica.vNext.Models;
using Company = Amica.vNext.Models.Company;

namespace Amica.vNext.Compatibility
{
    public class Map
    {
        private static readonly Dictionary<Type, Dictionary<string, string>> Topology =
            new Dictionary<Type, Dictionary<string, string>>();

        static Map()
        {
            Topology.Add(typeof (Country), new Countries());
            Topology.Add(typeof (Company), new Companies());
        }

#region "T O"

        /// <summary>
        /// Returns a List of objects casted by a Amica10 DataTable source.
        /// </summary>
        /// <typeparam name="T">The type of objects to be returned.</typeparam>
        /// <param name="dt">A Supported Amica10 DataTable.</param>
        /// <returns></returns>
        public static List<T> ToList<T>(DataTable dt) where T : new()
        {
            return (from DataRow r in dt.Rows select To<T>(r)).ToList();
        }

        /// <summary>
        /// Returns an object casted by a Asupported Amica10 DataRow.
        /// </summary>
        /// <typeparam name="T">The type of the object to be returned</typeparam>
        /// <param name="dr">A supprted Amica10 DataRow</param>
        /// <returns></returns>
        public static T To<T>(DataRow dr) where T : new()
        {
            var type = typeof (T);
            var map =  Topology[type];

            var instance = new T();
            foreach (DataColumn c in dr.Table.Columns)
            {
                var propName = c.ColumnName;
                if (!map.ContainsKey(propName)) continue;

                var propInfo = type.GetProperty(map[propName]);
                var val = Convert.ChangeType(dr[c], propInfo.PropertyType);
                propInfo.SetValue(instance, val, null);
            }
            return instance;
        }

#endregion

        public static void From<T>(DataRow row, object obj)
        {
            var type = typeof (T);
            var map =  Topology[type];

            foreach (DataColumn c in from DataColumn c in row.Table.Columns where c != row.Table.PrimaryKey[0] select c)
            {
                var fieldName = c.ColumnName;
                if (!map.ContainsKey(fieldName)) continue;

                var prop = type.GetProperty(map[fieldName]);
                if (prop == null) continue;

                var value = prop.GetValue(obj, null);
                row[c.ColumnName] = value;
            }
        }
    }
}
