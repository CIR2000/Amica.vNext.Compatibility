using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
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
        /// <param name="row">A supprted Amica10 DataRow</param>
        /// <returns></returns>
        public static T To<T>(DataRow row) where T : new()
        {
            var instance = new T();
            DataRowToObject(row, instance);
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
                var prop = GetProperty(target, fieldMapping.Value.PropertyName, out target);
                var value = GetAdjustedColumnValue(row, fieldMapping.Key, prop);

                prop.SetValue(target, value, null);
            }
        }
		internal static void ProcessDataRowParents(DataRow row, object target, IMapping mapping)
        {
			foreach (var parentMapping in mapping.Parents)
            {
                object realTarget, value;

                var prop = GetProperty(target, parentMapping.Value.PropertyName, out realTarget);
				var parentRow = row.GetParentRow(parentMapping.Value.RelationName);

                if (parentMapping.Value.PropertyType == null)
                {
                    value = GetAdjustedColumnValue(parentRow, parentMapping.Value.ColumnName, prop);
                }
                else
                {
					value = Activator.CreateInstance(parentMapping.Value.PropertyType);
					DataRowToObject(parentRow, value);
                }
				prop.SetValue(realTarget, value, null);

            }
        }
		internal static void ProcessDataRowChildren(DataRow row, object target, IMapping mapping)
        {
			foreach (var childMapping in mapping.Children)
            {
                var childRows = row.GetChildRows(childMapping.RelationName);
                var destProp = target.GetType().GetProperty(childMapping.PropertyName);

				var listType = typeof(List<>);
				var constructedListType = listType.MakeGenericType(childMapping.PropertyType);
				var list = (IList)Activator.CreateInstance(constructedListType);

				foreach (var childRow in childRows)
                {
					var listItem = Activator.CreateInstance(childMapping.PropertyType);
					DataRowToObject(childRow, listItem);
                    list.Add(listItem);
                }
			    destProp.SetValue(target, list, null);
            }
        }

		private static object GetAdjustedColumnValue(DataRow row, string columnName, PropertyInfo prop)
        {
            var column = row.Table.Columns[columnName];

			object val;
			if (prop.PropertyType.IsEnum)
				val = (int)Enum.Parse(prop.PropertyType, row[column].ToString());
			else if (column.ColumnName == "Id")
				val = HttpDataProvider.GetRemoteRowId(row);
			else
				val = Convert.ChangeType(row[column], prop.PropertyType);
            return val;
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
            foreach (var fieldMapping in mapping.Fields.Where(fieldMapping => fieldMapping.Key != "Id"))
            {
                if (!row.Table.Columns.Contains(fieldMapping.Key))
                    throw new ArgumentException("Unknown DataColumn", fieldMapping.Key);

                object target;
                var prop = GetProperty(source, fieldMapping.Value.PropertyName, out target);

                row[fieldMapping.Key] = prop.GetValue(target, null);
            }
        }

        internal static void ProcessObjectProperties(object source, DataRow row, IMapping mapping)
        {
            foreach (var parentMapping in mapping.Parents)
            {
                object realSource, value;

                var prop = GetProperty(source, parentMapping.Value.PropertyName, out realSource);
				if (parentMapping.Value.PropertyType == null)
                {
                    var parentTable = row.Table.ParentRelations[parentMapping.Value.RelationName].ParentTable;
					var parentColumn = parentTable.Columns[parentMapping.Value.ColumnName];
                    var parentValue = prop.GetValue(realSource, null);

                    if (parentValue == null) continue;

                    var parent = parentTable.Select($"{parentColumn} = '{parentValue}'");

					if (parent.Length > 0)
                    {
                        value = parent[0]["Id"];
                    }
                    else
                    {
                        var newParentRow = parentTable.NewRow();
                        newParentRow[parentColumn] = parentValue;
                        parentTable.Rows.Add(newParentRow);
                        value = newParentRow["Id"];
                    }
                }
                else
                {
					var parentObject = prop.GetValue(realSource, null);
					value = HttpDataProvider.GetLocalRowId((IUniqueId)parentObject);
                }
                row[parentMapping.Key] = value;
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

                var existingRows = childTable.Select($"{childColumn} = {row["Id"]}");
                foreach (var existingRow in existingRows)
                    existingRow.Delete();

                var prop = sourceType.GetProperty(childMapping.PropertyName);

                if (prop == null)
                    throw new ArgumentException("Unknown property.", childMapping.PropertyName);

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
        private static PropertyInfo GetProperty(object source, string name, out object target)
        {
			PropertyInfo prop = null;
			var nameParts = name.Split('.');
			target = source;

			if (nameParts.Length == 1)
            {
                prop = source.GetType().GetProperty(name);
            }
            else
            {
				var lastPart = nameParts.Last();
				foreach (var part in nameParts)
				{
					prop = target.GetType().GetProperty(part);
                    if (part == lastPart) continue;
					target = prop.GetValue(target, null);
				}

            }
			if (prop == null)
				throw new ArgumentException("Unknown property.", name);

            return prop;
        }

		internal static HttpDataProvider HttpDataProvider { get; set; }
    }
}
