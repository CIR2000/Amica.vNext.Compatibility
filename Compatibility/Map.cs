using System;
using System.Collections;
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
        private static readonly Dictionary<Type, IMapping> Topology =
            new Dictionary<Type, IMapping>();

        static Map()
        {
            Topology.Add(typeof(Country), new CountryMapping());
            Topology.Add(typeof(Company), new CompanyMapping());
            Topology.Add(typeof (Document), new DocumentMapping());
            Topology.Add(typeof (DocumentItem), new DocumentItemMapping());
            Topology.Add(typeof(Contact), new ContactMapping());
            Topology.Add(typeof(ContactMinimal), new ContactMinimalMapping());
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

        internal static void DataRowToObject(DataRow row, object target)
        {
            var mapping = Topology[target.GetType()];

            ProcessFields(row, target, mapping);
            ProcessParentRows(row, target, mapping);
            ProcessChildRows(row, target, mapping);
        }
		internal static void ProcessFields(DataRow row, object target, IMapping mapping)
        {
			foreach (var fieldMapping in mapping.Fields)
            {
                var column = row.Table.Columns[fieldMapping.Key];
                var destProp = target.GetType().GetProperty(fieldMapping.Value.FieldName);

                object val;

				if (destProp.PropertyType.IsEnum)
					val = (int) Enum.Parse(destProp.PropertyType, row[column].ToString());
				else if (column.ColumnName == "Id")
					val = HttpDataProvider.GetRemoteRowId(row);
				else
					val = Convert.ChangeType(row[column], destProp.PropertyType);

			    destProp.SetValue(target, val, null);
            }
        }
		internal static void ProcessParentRows(DataRow row, object target, IMapping mapping)
        {
			foreach (var parentMapping in mapping.Parents)
            {
                var destProp = target.GetType().GetProperty(parentMapping.Value.FieldName);

                var nestedObject = Activator.CreateInstance(parentMapping.Value.FieldType);
                var parentRow = row.GetParentRow(parentMapping.Value.RelationName);
				DataRowToObject(parentRow, nestedObject);

			    destProp.SetValue(target, nestedObject, null);
            }
        }
		internal static void ProcessChildRows(DataRow row, object target, IMapping mapping)
        {
			foreach (var childMapping in mapping.Children)
            {
                var childRows = row.GetChildRows(childMapping.RelationName);
                var destProp = target.GetType().GetProperty(childMapping.FieldName);

				var listType = typeof(List<>);
				var constructedListType = listType.MakeGenericType(childMapping.FieldType);
				var list = (IList)Activator.CreateInstance(constructedListType);

				foreach (var childRow in childRows)
                {
					var listItem = Activator.CreateInstance(childMapping.FieldType);
					DataRowToObject(childRow, listItem);
                    list.Add(listItem);
                }
			    destProp.SetValue(target, list, null);
            }
        }

        #endregion

        internal static void From(object source, DataRow row)
        {
            return;
    //        var sourceType = source.GetType();
    //        var map =  Topology[sourceType];

    //        foreach (var c in from DataColumn c in row.Table.Columns where c != row.Table.PrimaryKey[0] select c)
    //        {
    //            var fieldName = c.ColumnName;
    //            if (!map.ContainsKey(fieldName)) continue;

    //            var mapInfo = map[fieldName];

    //            var prop = sourceType.GetProperty(mapInfo.Destination);
    //            if (prop == null) continue;
				//row.Table.ChildRelations[""].
    //            object value;
    //            if (mapInfo.ParentRelation == null)
    //            {
    //                value = prop.GetValue(source, null);
    //            }
    //            else
    //            {
    //                var parentObject = prop.GetValue(source, null);
    //                value = HttpDataProvider.GetLocalRowId((IUniqueId)parentObject);
    //            }
    //            row[c.ColumnName] = value;
    //        }
            
        }
		internal static HttpDataProvider HttpDataProvider { get; set; }
    }
}
