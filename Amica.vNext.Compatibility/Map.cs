using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Amica.vNext.Models;

namespace Amica.vNext.Compatibility
{
    public class Map
    {
        private static readonly Dictionary<Type, Map<string, string>> Topology =
            new Dictionary<Type, Map<string, string>>();

        static Map()
        {
            Topology.Add(typeof (Country), Maps.Country.Make());
            Topology.Add(typeof (Company), Maps.Company.Make());
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
                var propName = map.Forward[c.ColumnName];
                if (propName == null) {
                    continue;
                }
                var propInfo = type.GetProperty(propName);

                var val = Convert.ChangeType(dr[c], propInfo.PropertyType);
                propInfo.SetValue(instance, val, null);
            }
            return instance;
        }

#endregion

        private static void LoadDataRow<T>(DataTable table, T instance)
        {
            // TODO

            var type = typeof (T);
            var map =  Topology[type];
            var fields = type.GetFields();
            object[] values;
            foreach (var field in fields)
            {
                var columnName = map.Reverse[field.Name];
                if (columnName == null) {
                    continue;
                }
            // WIP
            }
        }

        public static void From<T>(DataTable table, List<T> list)
        {
            // TODO
            foreach (var obj in list)
            {
                LoadDataRow(table, obj);
            }
            // WIP
        }
    }
}
