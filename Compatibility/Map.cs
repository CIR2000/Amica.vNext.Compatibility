using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Amica.vNext.Compatibility.Maps;
using Amica.vNext.Models;
using Amica.vNext.Models.Documents;
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
            Topology.Add(typeof(Document), new DocumentMapping());
            Topology.Add(typeof(Contact), new ContactMapping());
            Topology.Add(typeof(DocumentItem), new DocumentItemMapping());
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

            ProcessDataRowFields(row, target, mapping);
            ProcessDataRowParents(row, target, mapping);
            ProcessDataRowChildren(row, target, mapping);
        }
		internal static void ProcessDataRowFields(DataRow row, object target, IMapping mapping)
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
		internal static void ProcessDataRowParents(DataRow row, object target, IMapping mapping)
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
		internal static void ProcessDataRowChildren(DataRow row, object target, IMapping mapping)
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
            var mapping = Topology[source.GetType()];

            ProcessSimpleProperties(source, row, mapping);
            ProcessObjectProperties(source, row, mapping);
            ProcessListProperties(source, row, mapping);
        }

        internal static void ProcessSimpleProperties(object source, DataRow row, IMapping mapping)
        {
            var sourceType = source.GetType();

            foreach (var fieldMapping in mapping.Fields)
            {
                if (fieldMapping.Key == "Id") continue;

                var prop = sourceType.GetProperty(fieldMapping.Value.FieldName);

                if (prop == null)
                    throw new ArgumentException("Unknown property.", fieldMapping.Value.FieldName);
                if (!row.Table.Columns.Contains(fieldMapping.Key))
                    throw new ArgumentException("Unknown DataColumn", fieldMapping.Key);

                row[fieldMapping.Key] = prop.GetValue(source, null);
            }
        }
        internal static void ProcessObjectProperties(object source, DataRow row, IMapping mapping)
        {
            var sourceType = source.GetType();

            foreach (var parentMapping in mapping.Parents)
            {
                var prop = sourceType.GetProperty(parentMapping.Value.FieldName);

                if (prop == null)
                    throw new ArgumentException("Unknown property.", parentMapping.Value.FieldName);
                if (!row.Table.Columns.Contains(parentMapping.Key))
                    throw new ArgumentException("Unknown DataColumn.", parentMapping.Key);

				var parentObject = prop.GetValue(source, null);
                row[parentMapping.Key] = HttpDataProvider.GetLocalRowId((IUniqueId)parentObject);
            }
        }
        internal static void ProcessListProperties(object source, DataRow row, IMapping mapping)
        {
            var sourceType = source.GetType();

            foreach (var childMapping in mapping.Children)
            {
                var childRelation = row.Table.ChildRelations[childMapping.RelationName];
                var childTable = childRelation.ChildTable;
                var childColumn = childRelation.ChildColumns[0];

                var existingRows = childTable.Select(string.Format("{0} = {1}", childColumn, row["Id"]));
                foreach (var existingRow in existingRows)
                    existingRow.Delete();

                var prop = sourceType.GetProperty(childMapping.FieldName);

                if (prop == null)
                    throw new ArgumentException("Unknown property.", childMapping.FieldName);

				var items = prop.GetValue(source, null);
				foreach(var item in (IList)items)
                {
                    var childRow = childTable.NewRow();
                    childRow[childColumn] = row["Id"];
                    From(item, childRow);
                    childTable.Rows.Add(childRow);
                }
            }
        }
		internal static HttpDataProvider HttpDataProvider { get; set; }
    }
}
