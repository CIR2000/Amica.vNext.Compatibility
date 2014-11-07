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
    public class HttpDataProvider : IDisposable
    {
        private const string DbName = "HttpMapping.db";
        private readonly Dictionary<string, string> _resourcesMapping;
        private SQLiteConnection _db;

        #region "C O N S T R U C T O R S"

        public HttpDataProvider()
        {
            _resourcesMapping = new Dictionary<string, string> {
                {"Aziende", "companies"}
            };

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

        public void Dispose()
        {
            //Dispose();
        }

        private delegate int DelegateDbMethod(object obj);

        /// <summary>
        /// Casts a DataRow to the appropriate object tyoe and then stores it on a remote API endpoint.
        /// </summary>
        /// <typeparam name="T">Type to cast the DataRow to.</typeparam>
        /// <param name="row">DataRow to store.</param>
        /// <returns>T instance updated with API metadata, Or null if the operation was a delete.</returns>
        /// <remarks>The type of operation to be performed is inferred by DataRow's RowState property value.</remarks>
        private async Task<T> UpdateAsync<T>(DataRow row) where T: class
        {

            using (_db = new SQLiteConnection(DbName))
            {
                // ensure table there
                _db.CreateTable<HttpMapping>();
                
                var targetRow = (row.RowState != DataRowState.Deleted) ? row : RetrieveDeletedRowValues(row);

                // 'cast' source DataRow into the corresponding object instance.
                object obj = FromAmica.To<T>(targetRow);

                // retrieve remote meta field values from mapping datastore.
                var mapping = GetMapping(targetRow);
                // and update corresponding properties.
                ((BaseClass)obj).UniqueId = mapping.RemoteId;
                ((BaseClass)obj).ETag = mapping.ETag;

                var rc = new RestClient(BaseAddress, Authenticator);

                var retObj = default(T);
                DelegateDbMethod dbMethod;
                switch (mapping.RemoteId) {
                    case null:
                        retObj = await rc.PostAsync<T>(mapping.Resource, obj);
                        dbMethod = _db.Insert;
                        break;
                    default:
                        switch (row.RowState) {
                            case DataRowState.Modified:
                                retObj = await rc.PutAsync<T>(mapping.Resource, obj);
                                dbMethod = _db.Update;
                                break;
                            case DataRowState.Deleted:
                                await rc.DeleteAsync(mapping.Resource, obj);
                                dbMethod = _db.Delete;
                                break;
                            default:
                                // TODO better exception.. or maybe just fail sinlently?
                                throw new Exception("Cannot determine how the DataRow should be processed.");
                        }
                        break;
                }
                HttpResponse = rc.HttpResponse;

                if (retObj != null) {
                    // update mapping datatore with remote service meta fields.
                    mapping.RemoteId = ((BaseClass)((object)retObj)).UniqueId;
                    mapping.ETag = ((BaseClass)((object)retObj)).ETag;
                    mapping.LastUpdated = ((BaseClass)((object)retObj)).Updated;
                    dbMethod(mapping);
                }
                return retObj;
            }
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
                    entry = new HttpMapping { LocalId = localId, Resource = resource};
                    break;
                default:
                    // ReSharper disable once ReplaceWithSingleCallToFirstOrDefault
                    entry =
                        _db.Table<HttpMapping>()
                            .Where(v => v.LocalId.Equals(localId) && v.Resource.Equals(resource))
                            .FirstOrDefault() ?? new HttpMapping { LocalId = localId, Resource = resource};

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

        #region "U P D A T E  M E T H O D S"

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

        #endregion

        #region "P R O P E R T I E S"

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

        /// <summary>
        /// Returns the name of the local database used for keeping Amica and remote service in sync.
        /// </summary>
        public string SyncDatabaseName { get { return DbName; } }

        #endregion

    }
}
