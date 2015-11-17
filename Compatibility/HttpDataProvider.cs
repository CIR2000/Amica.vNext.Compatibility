using System;
using System.Net;
using System.Net.Http;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Amica.Data;
using Amica.vNext.Models;
using SQLite;

// TODO
// 1. When a row's parent company is not found we currently raise an exception. Should auto-create the parent instead? Or something else?
// 2. When a remote change fails, what do we do? Raise exception, silently fail, etc?
// 3. Any recovery plan like support for transactions?

namespace Amica.vNext.Compatibility
{
    /// <summary>
    /// Provides a compatibilty layer between Amica 10's ADO storage system and Eve REST APIs.
    /// </summary>
    public class HttpDataProvider : IDisposable
    {
        private const string DbName = "HttpSync.db";
        private readonly string _sentinelClientId;

        private readonly Dictionary<string, string> _resourcesMapping;
        private bool _hasCompanyIdChanged;
        private int? _localCompanyId;
        private DataProvider _dataProvider;
        private readonly List<DataTable> _updatesPerformed;
        private readonly SQLiteConnection _db;

        #region "C O N S T R U C T O R S"

        public HttpDataProvider()
        {
            ActionPerformed = ActionPerformed.NoAction;

            _hasCompanyIdChanged = true;
            _updatesPerformed = new List<DataTable>();
            _resourcesMapping = new Dictionary<string, string> {
                {"Aziende", "companies"},
                {"Nazioni", "countries"}
            };

            _db = new SQLiteConnection(DbName);

	    // TODO set BaseAddress using the appropriate DiscoveryService class method/property.
	    BaseAddress = "http://10.0.2.2:5000";

	    // TODO replace with hard-coded client id in production
            _sentinelClientId = Environment.GetEnvironmentVariable("SentinelClientId");
        }

        public HttpDataProvider(DataProvider dataProvider) : this()
        {
            DataProvider = dataProvider;
        }
        public HttpDataProvider(DataProvider dataProvider, string username, string password) : this(dataProvider)
        {
            Username = username;
            Password = password;
        }
        #endregion

        public void Dispose()
        {
            if (_db != null) _db.Dispose(); 
        }
            
        private delegate int DelegateDbMethod(object obj);

        /// <summary>
        /// Casts a DataRow to the appropriate object tyoe and then stores it on a remote API endpoint.
        /// </summary>
        /// <typeparam name="T">Type to cast the DataRow to.</typeparam>
        /// <param name="row">DataRow to store.</param>
        /// <param name="batch">Wether this update is part of a batch operation or not.</param>
        /// <returns>T instance updated with API metadata, Or null if the operation was a delete.</returns>
        /// <remarks>The type of operation to be performed is inferred by DataRow's RowState property value.</remarks>
        private async Task<T> UpdateAsync<T>(DataRow row, bool batch) where T: BaseModel, new()
        {

            ActionPerformed = ActionPerformed.NoAction;
            HttpResponse = null;

            // TODO this might be redundant as we have this guard in place already in both UpdateRowAsync and UpdateAsync(DataSet).
            if (!batch) UpdatesPerformed.Clear();

            // ensure table exists 
            _db.CreateTable<HttpMapping>();

            var targetRow = (row.RowState != DataRowState.Deleted) ? row : RetrieveDeletedRowValues(row);

            // 'cast' source DataRow into the corresponding object instance.
            object obj = Map.To<T>(targetRow);
            var shouldRetrieveRemoteCompanyId = (obj is BaseModelWithCompanyId);
            

            // retrieve remote meta field values from mapping datastore.
            var mapping = GetMapping(targetRow, shouldRetrieveRemoteCompanyId);
            if (mapping == null) return default(T);

            // and update corresponding properties.
            ((BaseModel)obj).UniqueId = mapping.RemoteId;
            ((BaseModel)obj).ETag = mapping.ETag;
            if (shouldRetrieveRemoteCompanyId) {
                ((BaseModelWithCompanyId) obj).CompanyId = mapping.RemoteCompanyId;
            }

            var retObj = default(T);
            using (var adam = new RemoteRepository {Username = Username, Password = Password, ClientId = _sentinelClientId})
            {
				HttpStatusCode statusCode;
				ActionPerformed action;
				DelegateDbMethod dbMethod;

				switch (mapping.RemoteId) {
					case null:
				        retObj = await adam.Insert((T)obj);
						dbMethod = _db.Insert;
						action = ActionPerformed.Added;
						statusCode = HttpStatusCode.Created;
						break;
					default:
						switch (row.RowState) {
							case DataRowState.Modified:
						        retObj = await adam.Replace((T) obj);
								dbMethod = _db.Update;
								action = ActionPerformed.Modified;
								statusCode = HttpStatusCode.OK;
								break;
							case DataRowState.Deleted:
						        await adam.Delete((T) obj);
								dbMethod = _db.Delete;
								action = ActionPerformed.Deleted;
								statusCode = HttpStatusCode.NoContent;
								break;
							default:
								// TODO better exception.. or maybe just fail silently?
								throw new Exception("Cannot determine how the DataRow should be processed.");
						}
						break;
				}
				HttpResponse = adam.HttpResponseMessage;
				ActionPerformed = ( HttpResponse != null && HttpResponse.StatusCode == statusCode) ? action :  ActionPerformed.Aborted;

				if (ActionPerformed != ActionPerformed.Aborted) {
					if (retObj != null) { 
						// update mapping datatore with remote service meta fields.
						mapping.RemoteId = retObj.UniqueId;
						mapping.ETag = retObj.ETag;
						mapping.LastUpdated = retObj.Updated;
					}
					dbMethod(mapping);
				}
                
            }
            return retObj;
        }

        /// <summary>
        /// Retrieves the HttpMapping which maps to a specific DataRow.
        /// </summary>
        /// <param name="row">DataRow for which an HttpMapping is needed.</param>
        /// <param name="shouldRetrieveRemoteCompanyId">Wether the remote company id should be retrieved or not.</param>
        /// <returns>An HttpMapping object relative to the provided DataRow.</returns>
        private HttpMapping GetMapping(DataRow row, bool shouldRetrieveRemoteCompanyId)
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
                            RetrieveRemoteCompanyId();
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
                    entry = _db.Table<HttpMapping>()
                        .Where(v =>
                            v.LocalId.Equals(localId) &&
                            v.Resource.Equals(resource) &&
                            (shouldRetrieveRemoteCompanyId && v.LocalCompanyId.Equals(LocalCompanyId) || true) 
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
                    entry = _db.Table<HttpMapping>()
                        .Where(v => 
                            v.LocalId.Equals(localId) && 
                            v.Resource.Equals(resource) && 
                            (shouldRetrieveRemoteCompanyId && v.LocalCompanyId.Equals(LocalCompanyId) || true) 
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
        private void RetrieveRemoteCompanyId()
        {
            if (LocalCompanyId == null) {
                // ReSharper disable once NotResolvedInText
                throw new ArgumentNullException("LocalCompanyId","Parameter cannot be null for this datasource.");
            }
            var company = _db
                .Table<HttpMapping>(
                    )
                    .FirstOrDefault(v => v.LocalId.Equals(LocalCompanyId) && 
                    v.Resource.Equals("companies"));
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
        /// <param name="batch">Wether this is part of a batch operation or not.</param>
        public async Task UpdateNazioniAsync(DataRow row, bool batch = false) 
        {
            await UpdateRowAsync<Country>(row, batch);
        }

        /// <summary>
        /// Stores a configDataSet.AziendeDataTable.AziendeRow to a remote API endpoint.
        /// </summary>
        /// <param name="row">Source DataRow</param>
        /// <param name="batch">Wether this is part of a batch operation or not.</param>
        public async Task UpdateAziendeAsync(DataRow row, bool batch = false)
        {
            await UpdateRowAsync<Company>(row, batch);
        }

        /// <summary>
        /// Casts a DataRow to the mathcing object supported by the server, then sends the object upstream for update.
        /// </summary>
        /// <typeparam name="T">Type to which the DataRow should be casted.</typeparam>
        /// <param name="row">DataRow to be sent to the server.</param>
        /// <param name="batch">Wether this update is part of a batch operation or not.</param>
        /// <returns></returns>
        private async Task UpdateRowAsync<T>(DataRow row, bool batch) where T: BaseModel, new()
        {
            if (!batch) UpdatesPerformed.Clear();
            await UpdateAsync<T>(row, batch);
            if (!batch) UpdatesPerformed.Add(row.Table);
        }

        public async Task UpdateAsync(DataSet dataSet)
        {
            var changes = dataSet.GetChanges();
            if (changes == null) return;

            UpdatesPerformed.Clear();
            foreach (DataTable dt in changes.Tables)
            {
                if (!_resourcesMapping.ContainsKey(dt.TableName)) continue;
                var methodName = string.Format("Update{0}Async", dt.TableName);

                // TODO handle the case of a write error on a batch of rows. Right now
                // the table is reported as not saved while in fact some rows might be saved
                // which would lead to inconsistency on the local Amica DB.
                foreach (DataRow row in dt.Rows) {
                    await ((Task) GetType().GetMethod(methodName).Invoke(this, new object[] {row, true}));
                    if (ActionPerformed == ActionPerformed.Aborted) goto End;
                }
                UpdatesPerformed.Add(dt);
            } 
            End: ;
        }

        #endregion

        #region "G E T  M E T H O D S"

        /// <summary>
        ///  Implements the Download Changes and Sync them logic.
        /// </summary>
        /// <typeparam name="T">Type of objects to be downloaded.</typeparam>
        /// <param name="dt">DataTable to be synced.</param>
        private async Task GetAndSync<T>(DataTable dt) where T : class
        {
            if (!_resourcesMapping.ContainsKey(dt.TableName)) return;

            var resource = _resourcesMapping[dt.TableName];
            var changes = await GetAsync<T>(resource);
	    // TODO if changes is null then something went wrong, probably with the
	    // request (eg., a 401). Should we report back?
            if (changes == null || changes.Count == 0) return;

            SyncTable(resource, dt, changes);

            var companyId = (dt.DataSet is configDataSet) ?  0 : LocalCompanyId ?? 0;
			
            if (DataProvider != null) DataProvider.Update(companyId, dt);
        }

        /// <summary>
        /// Downloads changes happened on a remote resource.
        /// </summary>
        /// <typeparam name="T">Type of the objects to be downloaded.</typeparam>
        /// <param name="resource">Remote resource name.</param>
        /// <returns>A list of changed objects.</returns>
        private async Task<List<T>> GetAsync<T>(string resource) where T : class
        {
            // ensure table exists 
            _db.CreateTable<HttpMapping>();

            // determine proper filter, depending on the base model type
            Expression<Func<HttpMapping, bool>> filter;


            var shouldQueryOnCompanyId = (typeof (BaseModelWithCompanyId).IsAssignableFrom(typeof (T)));
            if (shouldQueryOnCompanyId) {
                filter = m => m.Resource.Equals(resource) && m.LocalCompanyId.Equals(LocalCompanyId);
                // we also want to match documents which belongs to the current company
                RetrieveRemoteCompanyId();
            }
            else {
                filter = m => m.Resource.Equals(resource);
            }

            // retrieve IMS
            var imsEntry = _db.Table<HttpMapping>()
                .Where(filter)
                .OrderByDescending(v =>
                    v.LastUpdated
                )
                .FirstOrDefault();
            var ims = (imsEntry != null) ? imsEntry.LastUpdated : DateTime.MinValue;

            List<T> changes;
            // request changes
            using (var adam = new RemoteRepository {Username = Username, Password = Password, ClientId = _sentinelClientId})
            {
				changes = (List<T>) await adam.Get<T>(ims, RemoteCompanyId);

				HttpResponse = adam.HttpResponseMessage;
				ActionPerformed = ( HttpResponse != null && HttpResponse.StatusCode == HttpStatusCode.OK) ? 
					((changes.Count > 0) ? ActionPerformed.Read : ActionPerformed.ReadNoChanges) :  ActionPerformed.Aborted;

                
            }
			return changes;
        }

        /// <summary>
        /// Updates a DataTable with downstream changes.
        /// </summary>
        /// <typeparam name="T">Type of downstream changes.</typeparam>
        /// <param name="resource">Remote resource name.</param>
        /// <param name="dt">DataTable to be updated with downstream changes.</param>
        /// <param name="changes">The actual downstream changes.</param>
        private void SyncTable<T>(string resource, DataTable dt, IEnumerable<T> changes)
        {
            foreach (var obj in changes)
            {
                var baseObj = obj as BaseModel;
                if (baseObj == null) continue;  // TODO should never happen. maybe trow an exception.
                var entry = _db
                    .Table<HttpMapping>()
                    .FirstOrDefault(v => v.RemoteId.Equals(baseObj.UniqueId)) ??  new HttpMapping();

                // address the weird lack of primary key on configDataSet tables.
                if (dt.PrimaryKey.Length == 0)
                    // should probably be using "id" as index, but we always
                    // have id as the first column in our datasets.
                    dt.PrimaryKey = new[] {dt.Columns[0]};


                DataRow row = (entry.LocalId == 0) ? dt.NewRow() : dt.Rows.Find(entry.LocalId);
                if (row == null)
                    // TODO should we properly address this, instead of just throwing an exception?
                    throw new Exception("Cannot locate a DataRow that matches the syncdb reference.");

                Map.From<T>(row, obj);

                if (baseObj.Deleted) {
                    row.Delete();
                    _db.Delete(entry);
                    continue;
                }

                if (row.RowState == DataRowState.Detached) dt.Rows.Add(row);

                // update the fresh new entry, or refresh existing one.
                entry.LocalId = (int) row[dt.PrimaryKey[0]];
                entry.ETag = baseObj.ETag;
                entry.LastUpdated = baseObj.Updated;
                entry.RemoteId = baseObj.UniqueId;
                entry.Resource = resource;

                var exObj = obj as BaseModelWithCompanyId;
                if (exObj != null) {
                    entry.RemoteCompanyId = exObj.CompanyId;
                    entry.LocalCompanyId = LocalCompanyId;
                }

                if (entry.Id == 0)
                    _db.Insert(entry);
                else
                    _db.Update(entry);
            }
        }

        /// <summary>
        /// Downloads Companies changes from the server and merges them to the Aziende table on the local dataset.
        /// </summary>
        /// <param name="dataSet">configDataSet instance.</param>
        public async Task GetAziendeAsync(configDataSet dataSet)
        {
            await GetAndSync<Company>(dataSet.Aziende);

        }

        /// <summary>
        /// Downloads Countries changes from the server and merges them to the Nazioni table on the local dataset.
        /// </summary>
        /// <param name="dataSet">companyDataSet instance.</param>
        public async Task GetNazioniAsync(companyDataSet dataSet)
        {
            await GetAndSync<Country>(dataSet.Nazioni);
        }


        /// <summary>
        /// Downloads all changes from the server and merges them to a local DataSet instance.
        /// </summary>
        /// <param name="dataSet">Local DataSet.</param>
	/// <remarks>Be careful that this will send a request for each table which has a corresponding endpoint on the remote server.</remarks>
        public async Task GetAsync(DataSet dataSet)
        {
            // TODO: query the Eve OpLog to know which resources/tables have updates, 
            // should greatly reduce the number of superfluous requests.

            dataSet.EnforceConstraints = false;

            foreach (DataTable dt in dataSet.Tables)
            {
                if (!_resourcesMapping.ContainsKey(dt.TableName)) continue;
                var methodName = string.Format("Get{0}Async", dt.TableName);
		        await (Task) GetType().GetMethod(methodName).Invoke(this, new object[] {dataSet});
            }

	        // good luck
            dataSet.EnforceConstraints = true;
        }

        /// <summary>
        /// Downloads all changes from the server and merges them to a local companyDataSet instance.
        /// </summary>
        /// <param name="dataSet">companyDataSet instance.</param>
        public async Task GetAsync(companyDataSet dataSet)
        {
            dataSet.EnforceConstraints = false;

            await GetNazioniAsync(dataSet);
	    // ...

            dataSet.EnforceConstraints = true;
        }

        /// <summary>
        /// Downloads all cheanges from the server and merges them to a local configDataSet instance.
        /// </summary>
        /// <param name="dataSet">configDataSet instance.</param>
        public async Task GetAsync(configDataSet dataSet)
        {
            dataSet.EnforceConstraints = false;

            await GetAziendeAsync(dataSet);
	    // ..

            dataSet.EnforceConstraints = true;
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
        public string BaseAddress { get; private set; }

	/// <summary>
	///  Username for authentication to the remote service.  
	/// </summary>
        public string Username { get; set; }
	/// <summary>
	///  Password for authentication to the remote service.  
	/// </summary>
	public string Password { get; set; }

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

        /// <summary>
        /// Gets or sets the DataProvider to be used to persist data downloaded from the remote server.
        /// </summary>
        public DataProvider DataProvider {
            get { return _dataProvider; }
            set {
                _dataProvider = value;
                LocalCompanyId = DataProvider.ActiveCompanyId;
            }
        }

        /// <summary>
        /// Returns a list of DataTables for which the latest update operation has been successful.
        /// </summary>
        public List<DataTable> UpdatesPerformed {
            get { return _updatesPerformed;}   
        }

        #endregion

    }
}
