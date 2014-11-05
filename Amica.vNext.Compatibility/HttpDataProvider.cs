using System;
using System.Net.Http;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Amica.vNext.Objects;
using Amica.vNext.Http;
using SQLite;

namespace Amica.vNext.Compatibility
{
    /// <summary>
    /// Provides a compatibilty layer between Amica 10's ADO storage system and Eve REST APIs.
    /// </summary>
    public class HttpDataProvider
    {
        private const string DbName = "HttpMapping.db";
        private readonly Dictionary<string, string> _resourcesMapping;
        private readonly SQLiteConnection _db;

        #region "C O N S T R U C T O R S"

        public HttpDataProvider()
        {
            _resourcesMapping = new Dictionary<string, string> {
                {"Aziende", "companies"}
            };

            _db = new SQLiteConnection(DbName);
            _db.CreateTable<HttpMapping>();
        }

        public HttpDataProvider(string baseAddress, BasicAuthenticator authenticator) : this()
        {
            BaseAddress = baseAddress;
            Authenticator = authenticator;
        }
        public HttpDataProvider(string baseAddress) : this()
        {
            BaseAddress = baseAddress;
        }
        public HttpDataProvider(BasicAuthenticator authenticator) : this()
        {
            Authenticator = authenticator;
        }
        #endregion

        /// <summary>
        /// Casts a DataRow to the appropriate object tyoe and then stores it on a remote API endpoint.
        /// </summary>
        /// <typeparam name="T">Type to cast the DataRow to.</typeparam>
        /// <param name="row">DataRow to store.</param>
        /// <returns>T instance updated with API metadata, Or null if the operation was a delete.</returns>
        /// <remarks>The type of operation to be performed is inferred by DataRow's RowState property value.</remarks>
        private async Task<T> UpdateAsync<T>(DataRow row) where T: class
        {
            var targetRow = (row.RowState != DataRowState.Deleted) ? row : RetrieveDeletedRowValues(row);

            object obj = FromAmica.To<T>(targetRow);
            var mapping = GetMapping(targetRow);
            MergeMappingData(ref obj, mapping);

            var rc = new RestClient(BaseAddress, Authenticator)
            {
                ResourceName = mapping.Resource
            };

            var value = default(T);
            switch (mapping.RemoteId) {
                case null:
                    value = await rc.PostAsync<T>(obj);
                    break;
                default:
                    switch (row.RowState)
                    {
                        case DataRowState.Modified:
                            value = await rc.PutAsync<T>(obj);
                            break;
                        case DataRowState.Deleted:
                            await rc.DeleteAsync(obj);
                            break;
                    }
                    break;
            }

            HttpResponse = rc.HttpResponse;

            if (value != null) {
                // POST or PUT
                UpdateMapping(mapping, value);
            }
            else {
                // DELETE
                if (rc.HttpResponse.StatusCode == System.Net.HttpStatusCode.OK) {
                    DeleteMaping(mapping);
                }
            }
            return value;
        }

        /// <summary>
        /// Updates the Object=>DataRow mapping and persists it.
        /// </summary>
        /// <param name="mapping">The HttpMapping instance to update.</param>
        /// <param name="item">The instnace of the object mapped.</param>
        private void UpdateMapping(HttpMapping mapping, object item )
        {
            var source = (BaseClass)item;

            mapping.RemoteId = source.UniqueId;
            mapping.ETag = source.ETag;
            mapping.LastUpdated = source.Updated;

            _db.InsertOrReplace(mapping);

        }

        /// <summary>
        /// Deletes an Object=>DataRow mapping from the mapping database.
        /// </summary>
        /// <param name="mapping">The HttpMapping to remove.</param>
        private void DeleteMaping(HttpMapping mapping)
        {
            _db.Delete<HttpMapping>(mapping.Id);
        }

        /// <summary>
        /// Updates an object with data from a HttpMapping instance.
        /// </summary>
        /// <param name="obj">Object to update.</param>
        /// <param name="mapping">HttpMapping to merge.</param>
        ///<remarks>This is invoked after the object has been casted from a DataRow and before it is sent to the remote. 
        /// DataRow does not contain API metadata so these are retrieved from the mapping database.</remarks>
        private static void MergeMappingData(ref object obj, HttpMapping mapping)
        {
            var t = obj.GetType();

            var uniqueId = t.GetProperty("UniqueId");
            uniqueId.SetValue(obj, mapping.RemoteId, null);

            var eTag = t.GetProperty("ETag");
            eTag.SetValue(obj, mapping.ETag, null);
        }

        /// <summary>
        /// Retrieves the HttpMapping which maps to a specific DataRow.
        /// </summary>
        /// <param name="row">DataRow for which an HttpMapping is needed.</param>
        /// <returns>An HttpMapping object relative to the provided DataRow.</returns>
        private HttpMapping GetMapping(DataRow row)
        {
            var localId = Int32.Parse(row["Id"].ToString());
            var resource = _resourcesMapping[row.Table.TableName];

            HttpMapping entry;
            switch (row.RowState) {
                case DataRowState.Added:
                    entry = new HttpMapping { LocalId = localId, Resource = resource };
                    break;
                default:
                    // ReSharper disable once ReplaceWithSingleCallToFirstOrDefault
                    entry =
                        _db.Table<HttpMapping>()
                            .Where(v => v.LocalId.Equals(localId) && v.Resource.Equals(resource))
                            .FirstOrDefault() ?? new HttpMapping { LocalId = localId, Resource = resource };

                    break;
            }
            return entry;
        }

        /// <summary>
        /// Returns a copy of a DataRow which contains the source original fields values.
        /// </summary>
        /// <param name="row">Source DataRow.</param>
        /// <returns>A DataRow with original field values of the source DataRow.</returns>
        /// <remarks>Field values from DataRows with RowState = Deleted can't be directly retrieved, hence this utility function.</remarks>
        private static DataRow RetrieveDeletedRowValues(DataRow row)
        {
            var dr = row.Table.NewRow();
            foreach (DataColumn col in row.Table.Columns) {
                dr[col] = row[col, DataRowVersion.Original];
            }
            return dr;
        }

        /// <summary>
        /// Stores a companyDataSet.NazioniDataTable.NazioniRow to a remote API endpoint.
        /// </summary>
        /// <param name="row">Source DataRow</param>
        public async Task UpdateNazioniAsync(DataRow row) 
        {
            await UpdateAsync<Country>(row);
        }
        
        /// <summary>
        /// Stores a configDataSet.AziendeDataTable.AziendeRow to a remote API endpoint.
        /// </summary>
        /// <param name="row">Source DataRow</param>
        public async Task UpdateAziendeAsync(DataRow row)
        {
            await UpdateAsync<Company>(row);
        }

        /// <summary>
        /// HttpResponseMessage returned by the latest UpdateAsync method invoked.
        /// </summary>
        public HttpResponseMessage HttpResponse { get; private set; }

		/// <summary>
		/// Gets or sets the remote service base address.
		/// </summary>
		/// <value>The remote service base address.</value>
        public string BaseAddress { get; set; }

		/// <summary>
		/// Gets or sets the authenticator.
		/// </summary>
		/// <value>The authenticator.</value>
        public BasicAuthenticator Authenticator { get; set; }
    }
}
