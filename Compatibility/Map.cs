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
            Topology.Add(typeof(BillingAddress), new ContactMinimalMapping());
            Topology.Add(typeof(Currency), new CurrencyMapping());
            Topology.Add(typeof(ShippingAddress), new AddressExWithNameMapping());
            Topology.Add(typeof(Vat), new VatMapping());
            Topology.Add(typeof(PaymentMethod), new PaymentMethodMapping());
            Topology.Add(typeof(Fee), new FeeMapping());
            Topology.Add(typeof(Payment), new PaymentMapping());
            Topology.Add(typeof(ContactDetailsEx), new ContactDetailsExMapping());
        }

#region TO

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
        /// Returns an object casted by a supported Amica10 DataRow.
        /// </summary>
        /// <typeparam name="T">The type of the object to be returned</typeparam>
        /// <param name="row">A supprted Amica10 DataRow</param>
        /// <returns></returns>
        public static T To<T>(DataRow row)
        {
            var instance = Factory<T>.Create();
            DataRowToObject(row, instance);
            return instance;
        }

        internal static void DataRowToObject(DataRow row, object target)
        {
            if (!Topology.ContainsKey(target.GetType())) return;

            var mapping = Topology[target.GetType()];

            ProcessDataRowFields(row, target, mapping);
            ProcessDataRowParents(row, target, mapping);
            ProcessDataRowChildren(row, target, mapping);
        }
		internal static void ProcessDataRowFields(DataRow row, object target, IMapping mapping)
        {
			foreach (var fieldMapping in mapping.Fields)
            {
                object activeTarget;
                var transformedSourceValue = GetTransformedColumnValue(row, fieldMapping.Key, fieldMapping.Value.UpstreamTransform);
                if (transformedSourceValue == DBNull.Value) continue;

                var prop = GetProperty(target, fieldMapping.Value.PropertyName, out activeTarget, appendToEmptyList: true);
                if (activeTarget == null) continue;
                var value = GetAdjustedColumnValue(row, fieldMapping.Key, fieldMapping.Value.UpstreamTransform, prop);

                prop.SetValue(activeTarget, value, null);
            }
        }
		internal static void ProcessDataRowParents(DataRow row, object target, IMapping mapping)
        {
			foreach (var parentMapping in mapping.Parents)
            {
                object realTarget, value;
				var prop = GetProperty(target, parentMapping.Value.PropertyName, out realTarget);

                var dataRelation = parentMapping.Value;


                if (dataRelation.RelationName == null)
                {
                    value = dataRelation.UpstreamTransform(row[dataRelation.ParentColumn]);
                }
                else
                {
					var parentRow = row.GetParentRow(dataRelation.RelationName);

					if (parentMapping.Value.ChildType == null)
					{
						value = GetAdjustedColumnValue(parentRow, dataRelation.ParentColumn, dataRelation.UpstreamTransform, prop);
					}
					else
					{
                        if (parentRow != null)
                        {
							value = Activator.CreateInstance(dataRelation.ChildType);

                            var cachedMeta = HttpDataProvider.GetHttpMappingByRow(parentRow);
							if (cachedMeta != null)
							{
								if (value is BaseModel)
                                {
									((BaseModel)value).UniqueId = cachedMeta.RemoteId;
									((BaseModel)value).ETag = cachedMeta.ETag;
									if (dataRelation.ChildType == typeof(BaseModelWithCompanyId)) {
										((BaseModelWithCompanyId) value).CompanyId = cachedMeta.RemoteCompanyId;
									}
                                }
                                else
                                {
									// maybe the object still wants to map to to a related object
									// (e.g. Document.BillTo still refers to a Contact.
									var p = value.GetType().GetProperty("UniqueId");
                                    if (p != null)
                                        p.SetValue(value, cachedMeta.RemoteId, null);
                                }

                            }

                            DataRowToObject(parentRow, value);
                        }
                        else value = null;
					}
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
				var constructedListType = listType.MakeGenericType(childMapping.ChildType);
				var list = (IList)Activator.CreateInstance(constructedListType);

				foreach (var childRow in childRows)
                {
					var listItem = Activator.CreateInstance(childMapping.ChildType);
					DataRowToObject(childRow, listItem);
                    list.Add(listItem);
                }
			    destProp.SetValue(target, list, null);
            }
        }

        private static object GetTransformedColumnValue(DataRow row, string columnName, Func<object, object> transform)
        {
            if (row == null) return null;

            var column = row.Table.Columns[columnName];
            return transform(row[column]);
        }

		private static object GetAdjustedColumnValue(DataRow row, string columnName, Func<object, object> transform, PropertyInfo prop)
        {
            if (row == null) return null;

            var column = row.Table.Columns[columnName];
            var transformed = transform(row[column]);

			object val;
			if (prop.PropertyType.IsEnum)
				val = (int)Enum.Parse(prop.PropertyType, transformed.ToString());
			else if (column.ColumnName == "Id")
				val = HttpDataProvider.GetRemoteRowId(row);
			else
				val = (string.IsNullOrEmpty(transformed.ToString())) 
					? null 
					: Convert.ChangeType(transformed, prop.PropertyType);
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
                if (target == null) continue;
                var val = prop.GetValue(target, null);
                var maxLength = row.Table.Columns[fieldMapping.Key].MaxLength;

				var transformed = fieldMapping.Value.DownstreamTransform(val);
                // Since one cant set a DataColumn's MaxLength unless it is of string type, we rely on
                // MaxLength and choose not to test the DataColumn type.
                row[fieldMapping.Key] = (maxLength > 0 && transformed != null && ((string)transformed).Length > maxLength)
                    ? ((string)transformed).Substring(0, maxLength)
                    : transformed;
            }
        }

        internal static void ProcessObjectProperties(object source, DataRow row, IMapping mapping)
        {
            foreach (var parentMapping in mapping.Parents)
            {
                object realSource, value;

                var dataRelation = parentMapping.Value;

                var keyField = (dataRelation.ChildProperty != null) ?
                    dataRelation.PropertyName + "." + dataRelation.ChildProperty :
                    dataRelation.PropertyName;

				try
                {
					var prop = GetProperty(source, keyField, out realSource);
					if (dataRelation.RelationName == null)
					{
						value = prop.GetValue(realSource, null);
					}
					else
					{
						if (dataRelation.ParentColumn != "Id")
						{
							var parentTable = row.Table.ParentRelations[dataRelation.RelationName].ParentTable;
							var parentColumn = parentTable.Columns[dataRelation.ParentColumn];
							var parentValue = prop.GetValue(realSource, null);

							if (parentValue == null) continue;

							var parents = parentTable.Select($"{parentColumn} = '{parentValue}'");

							var targetRow = (parents.Length>0) ? parents[0] : parentTable.NewRow();

							if (dataRelation.ChildType != null)
								ProcessSimpleProperties(realSource, targetRow, Topology[dataRelation.ChildType]);
							else
								targetRow[parentColumn] = parentValue;

							if (targetRow.RowState == DataRowState.Detached)
								parentTable.Rows.Add(targetRow);

							value = targetRow["Id"];
						}
						else
						{
							var parentObject = prop.GetValue(realSource, null);
							value = (parentObject != null) ? HttpDataProvider.GetLocalRowId((IUniqueId)parentObject) : DBNull.Value;
						}
					}
                }
				catch (Exception ex) {
                    value = DBNull.Value;
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
        private static PropertyInfo GetProperty(object source, string name, out object target, bool appendToEmptyList = false)
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
					if (IsList(target))
                    { 
						if (((IList)target).Count == 0)
                        {
							if (!appendToEmptyList)
                            {
								target = null;
								return null;
                            }
                            ((IList)target).Add(Activator.CreateInstance(prop.PropertyType.GetGenericArguments()[0]));
                        }
						// assume first list item is mapped to DataColumn
						target = ((IList)target)[0];
					}
					prop = target.GetType().GetProperty(
						(part.EndsWith("]")) ? part.Substring(0, part.Length - 3) : part);
                    if (prop == null) break;
                    if (part == lastPart) continue;
					if (prop.GetValue(target, null) == null)
                        prop.SetValue(target, Activator.CreateInstance(prop.PropertyType), null);
					target = prop.GetValue(target, null);
				}

            }
			if (prop == null)
					throw new ArgumentException(string.Format("Invalid property name: {0}", name), name);

            return prop;
        }
		private static bool IsList(object o)
			{
				if(o == null) return false;
				return o is IList &&
					   o.GetType().IsGenericType &&
					   o.GetType().GetGenericTypeDefinition().IsAssignableFrom(typeof(List<>));
			}

		internal static HttpDataProvider HttpDataProvider { get; set; }
    }
}
