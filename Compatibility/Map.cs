using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Amica.vNext.Compatibility.Maps;
using Amica.vNext.Models;
using Amica.vNext.Models.Documents;
using SQLite;
using Company = Amica.vNext.Models.Company;

namespace Amica.vNext.Compatibility
{
    public class Map
    {
        private static readonly Dictionary<Type, Dictionary<string, MapInfo>> Topology =
            new Dictionary<Type, Dictionary<string, MapInfo>>();

        static Map()
        {
            Topology.Add(typeof (Country), new CountryMapInfo());
            Topology.Add(typeof (Company), new CompanyMapInfo());
            Topology.Add(typeof (Document), new DocumentMapInfo());
            Topology.Add(typeof(Contact), new ContactMapInfo());
            Topology.Add(typeof (ContactMinimal), new ContactMinimalMapInfo());
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
            var instance = new T();
            DataRowToObject(dr, instance);
            return instance;
        }

        internal static void DataRowToObject(DataRow sourceRow, object target)
        {
            var map = Topology[target.GetType()];

            foreach (DataColumn c in sourceRow.Table.Columns)
            {
                var propName = c.ColumnName;
                if (!map.ContainsKey(propName)) continue;

                var mapInfo = map[propName];
				var destProp = target.GetType().GetProperty(mapInfo.Destination);
                object val;

                if (mapInfo.ParentRelation == null)
                {
                    if (destProp.PropertyType.IsEnum)
                        val = (int) Enum.Parse(destProp.PropertyType, sourceRow[c].ToString());
					else if (c.ColumnName == "Id")
						val = HttpDataProvider.GetRemoteRowId(sourceRow);
					else
						val = Convert.ChangeType(sourceRow[c], destProp.PropertyType);
                }
                else 
                {
                    val = Activator.CreateInstance(mapInfo.ParentType);
					var parentRow = sourceRow.GetParentRow(mapInfo.ParentRelation);
					DataRowToObject(parentRow, val);
                }
			   destProp.SetValue(target, val, null);
            }
        }

        #endregion

        internal static void From(object source, DataRow row)
        {
            var sourceType = source.GetType();
            var map =  Topology[sourceType];

            foreach (var c in from DataColumn c in row.Table.Columns where c != row.Table.PrimaryKey[0] select c)
            {
                var fieldName = c.ColumnName;
                if (!map.ContainsKey(fieldName)) continue;

                var mapInfo = map[fieldName];

                var prop = sourceType.GetProperty(mapInfo.Destination);
                if (prop == null) continue;

                object value;
                if (mapInfo.ParentRelation == null)
                {
                    value = prop.GetValue(source, null);
                }
                else
                {
                    var parentObject = prop.GetValue(source, null);
                    int result;
                    if (int.TryParse((parentObject.GetType().GetProperty("UniqueId").ToString()), out result))
                        value = result;
                    else
                        value = DBNull.Value;
                }
                row[c.ColumnName] = value;
            }
            
        }
		internal static HttpDataProvider HttpDataProvider { get; set; }
    }
}
