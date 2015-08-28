using System;
using System.Net;
using System.Net.Http;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Amica.vNext.Models;
using Eve;
using SQLite;

namespace Amica.vNext.Compatibility
{
    /// <summary>
    /// Provides a compatibilty layer between Amica 10's ADO storage system and Eve REST APIs.
    /// </summary>
    public class HttpDataProvider : IDisposable
    {
        private const string DbName = "HttpSync.db";
        private readonly Dictionary<string, string> _resourcesMapping;
        private int? _localCompanyId;
        private bool _hasCompanyIdChanged;

        #region "C O N S T R U C T O R S"


        public HttpDataProvider()
        {
            ActionPerformed = ActionPerformed.NoAction;

            _hasCompanyIdChanged = true;
            _resourcesMapping = new Dictionary<string, string> {
                {"Aziende", "companies"},
                {"Nazioni", "countries"}
            };
        }

        public HttpDataProvider(int companyId) : this()
        {
            LocalCompanyId = companyId;
        }

        public HttpDataProvider(string baseAddress, BasicAuthenticator authenticator, int companyId) : this(companyId)
        {
            BaseAddress = baseAddress;
            Authenticator = authenticator;
        }
        public HttpDataProvider(string baseAddress, BasicAuthenticator authenticator) : this()
        {
            BaseAddress = baseAddress;
            Authenticator = authenticator;
        }
        public HttpDataProvider(string baseAddress, int companyId): this(companyId)
        {
            BaseAddress = baseAddress;
        }
        public HttpDataProvider(string baseAddress): this()
        {
            BaseAddress = baseAddress;
        }
        public HttpDataProvider(BasicAuthenticator authenticator, int companyId) : this(companyId)
        {
            Authenticator = authenticator;
        }
        public HttpDataProvider(BasicAuthenticator authenticator) : this()
        {
            Authenticator = authenticator;
        }
        #endregion

        public void Dispose() { }
            
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

            using (var db = new SQLiteConnection(DbName))
            {
                ActionPerformed = ActionPerformed.NoAction;
                HttpResponse = null;

                // ensure table exists 
                db.CreateTable<HttpMapping>();

                var targetRow = (row.RowState != DataRowState.Deleted) ? row : RetrieveDeletedRowValues(row);

                // 'cast' source DataRow into the corresponding object instance.
                object obj = FromAmica.To<T>(targetRow);
                var shouldRetrieveRemoteCompanyId = (obj is BaseModelWithCompanyId);

                // retrieve remote meta field values from mapping datastore.
                var mapping = GetMapping(targetRow, db, shouldRetrieveRemoteCompanyId);
                if (mapping == null) return default(T);

                // and update corresponding properties.
                ((BaseModel)obj).UniqueId = mapping.RemoteId;
                ((BaseModel)obj).ETag = mapping.ETag;
                if (shouldRetrieveRemoteCompanyId) {
                    ((BaseModelWithCompanyId) obj).CompanyId = mapping.RemoteCompanyId;
                }

                var rc = new EveClient(BaseAddress, Authenticator);

                HttpStatusCode statusCode;
                ActionPerformed action;
                DelegateDbMethod dbMethod;
                var retObj = default(T);

                switch (mapping.RemoteId) {
                    case null:
                        retObj = await rc.PostAsync<T>(mapping.Resource, obj);
                        dbMethod = db.Insert;
                        action = ActionPerformed.Added;
                        statusCode = HttpStatusCode.Created;
                        break;
                    default:
                        switch (row.RowState) {
                            case DataRowState.Modified:
                                retObj = await rc.PutAsync<T>(mapping.Resource, obj);
                                dbMethod = db.Update;
                                action = ActionPerformed.Modified;
                                statusCode = HttpStatusCode.OK;
                                break;
                            case DataRowState.Deleted:
                                await rc.DeleteAsync(mapping.Resource, obj);
                                dbMethod = db.Delete;
                                action = ActionPerformed.Deleted;
                                statusCode = HttpStatusCode.NoContent;
                                break;
                            default:
                                // TODO better exception.. or maybe just fail silently?
                                throw new Exception("Cannot determine how the DataRow should be processed.");
                        }
                        break;
                }
                HttpResponse = rc.HttpResponse;
                ActionPerformed = ( HttpResponse != null && HttpResponse.StatusCode == statusCode) ? action :  ActionPerformed.Aborted;

                if (action != ActionPerformed.Aborted) {
                    if (retObj != null) { 
                        // update mapping datatore with remote service meta fields.
                        mapping.RemoteId = ((BaseModel)((object)retObj)).UniqueId;
                        mapping.ETag = ((BaseModel)((object)retObj)).ETag;
                        mapping.LastUpdated = ((BaseModel)((object)retObj)).Updated;
                    }
                    dbMethod(mapping);
                }
                return retObj;
            }
        }

        /// <summary>
        /// Retrieves the HttpMapping which maps to a specific DataRow.
        /// </summary>
        /// <param name="row">DataRow for which an HttpMapping is needed.</param>
        /// <param name="db">SQLiteConnection to be used for the lookup.</param>
        /// <param name="shouldRetrieveRemoteCompanyId">Wether the remote company id should be retrieved or not.</param>
        /// <returns>An HttpMapping object relative to the provided DataRow.</returns>
        private HttpMapping GetMapping(DataRow row, SQLiteConnection db, bool shouldRetrieveRemoteCompanyId)
        {
            var localId = (int)row["id"];
            var resource = _resourcesMapping[row.Table.TableName];

            HttpMapping entry;
            switch (row.RowState) {
                case DataRowState.Added:
                    int? localCompanyId = null;
                    string remoteCompanyId = null;
                    if (shouldRetrieveRemoteCompanyId) {
                        if (_hasCompanyIdChanged) {
                            RetrieveRemoteCompanyId(db);
                        }
                        remoteCompanyId = RemoteCompanyId;
                        localCompanyId = LocalCompanyId;
                    }
                    entry = new HttpMapping { 
                        LocalId = localId, 
                        Resource = resource, 
                        LocalCompanyId = localCompanyId, 
                        RemoteCompanyId = remoteCompanyId
                    };
                    break;
                case DataRowState.Modified:
                    // ReSharper disable once ReplaceWithSingleCallToFirstOrDefault
                    entry = db.Table<HttpMapping>()
                        .Where(v => 
                            v.LocalId.Equals(localId) && 
                            v.Resource.Equals(resource) && 
                            v.LocalCompanyId.Equals(LocalCompanyId)
                            )
                        .FirstOrDefault() ?? new HttpMapping {
                            LocalId = localId, 
                            Resource = resource, 
                            LocalCompanyId = LocalCompanyId
                        };
                    break;
                case DataRowState.Detached:
                    // if the row is Deleted, it will come in in Detached state
                    // ReSharper disable once ReplaceWithSingleCallToFirstOrDefault
                    entry = db.Table<HttpMapping>()
                        .Where(v => 
                            v.LocalId.Equals(localId) && 
                            v.Resource.Equals(resource) && 
                            v.LocalCompanyId.Equals(LocalCompanyId)
                            )
                        .FirstOrDefault();
                    break;
                default:
                    entry = null;
                    break;
            }
            return entry;
        }

        /// <summary>
        /// Retrieves the RemoteCompanyId which matches a LocalCompanyId and sets the corresponding property.
        /// </summary>
        /// <param name="db">SQLiteConnection to be used for the lookup.</param>
        private void RetrieveRemoteCompanyId(SQLiteConnection db)
        {
            if (LocalCompanyId == null) {
                // ReSharper disable once NotResolvedInText
                throw new ArgumentNullException("LocalCompanyId","Parameter cannot be null for this datasource.");
            }
            var company = db.Table<HttpMapping>()
                .Where(v => 
                    v.LocalId.Equals(LocalCompanyId) && 
                    v.Resource.Equals("companies")
                    )
                    .FirstOrDefault();
            if (company == null){
                // TODO custom exception?
                throw new Exception("Cannot locate parent company record for this datarow.");
            }
            RemoteCompanyId = company.RemoteId;
            _hasCompanyIdChanged = false;
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

        #region "G E T  M E T H O D S"

        private async Task GetAsync<T>(DataTable dt) where T : class
        {
            using (var db = new SQLiteConnection(DbName))
            {
                var resource = _resourcesMapping[dt.TableName];

                // ensure table exists 
                db.CreateTable<HttpMapping>();

                Expression<Func<HttpMapping, bool>> filter;

                var shouldQueryOnCompanyId = (typeof (BaseModelWithCompanyId).IsAssignableFrom(typeof (T)));
                if (shouldQueryOnCompanyId)
                {
                    RetrieveRemoteCompanyId(db);
                    filter = m => m.Resource.Equals(resource) && m.LocalCompanyId.Equals(LocalCompanyId);
                }
                else
                {
                    filter = m => m.Resource.Equals(resource);
                }

                // retrieve IMS
                var imsEntry = db.Table<HttpMapping>()
                    .Where(filter)
                    .OrderByDescending(v =>
                        v.LastUpdated
                    )
                    .FirstOrDefault();
                var ims = (imsEntry != null) ? imsEntry.LastUpdated : DateTime.MinValue;


                

                // request changes
                var rc = new EveClient(BaseAddress, Authenticator);
                var changes = await rc.GetAsync<T>(resource, ims);


                // compare downstream changes with sync db

                // add and edit rows accordingly; updating the sync db accordignly

                HttpResponse = rc.HttpResponse;
                ActionPerformed = ( HttpResponse != null && HttpResponse.StatusCode == HttpStatusCode.OK) ? ActionPerformed.Read :  ActionPerformed.Aborted;
            }
        }

        public async Task GetAziendeAsync(DataTable dt)
        {
            await GetAsync<Company>(dt);
        }
        public async Task GetNazioniAsync(DataTable dt)
        {
            await GetAsync<Country>(dt);
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

        /// <summary>
        /// Returns the action performed by the latest Update method invoked.
        /// </summary>
        public ActionPerformed ActionPerformed { get; internal set; }

        /// <summary>
        /// Gets or sets the local company id.
        /// </summary>
        public int? LocalCompanyId {
            get { 
                return _localCompanyId; 
            }
            set {
                if (value == _localCompanyId) {
                    return;
                }
                _localCompanyId = value;
                _hasCompanyIdChanged = true;
            }
        }


        /// <summary>
        ///  Gets or sets the remote company id.
        /// </summary>
        public string RemoteCompanyId { get; private set; }
        #endregion

    }
}
