using System;
using System.Net;
using System.Net.Http;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Amica.Data;
using Amica.vNext.Models;
using Amica.vNext.Storage;
using SQLite;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Amica.vNext.Models.Documents;
using SimpleCache;

// TODO
// Allow 'batch' uploads of data that has not changed? When an account joins the first time, what/if/how do we upload data?
// When a row's parent company is not found we currently raise an exception. Should auto-create the parent instead? Or something else?
// When a remote change fails, what do we do? Raise exception, silently fail, etc?
// Any recovery plan like support for transactions?

namespace Amica.vNext.Compatibility
{
    /// <summary>
    /// Provides a compatibilty layer between Amica 10's ADO storage system and Eve REST APIs.
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    public class HttpDataProvider : IDisposable, IRestoreDefaults
    {

        private const string DbName = "HttpSync.db";

        private bool _hasCompanyIdChanged;
        private int? _localCompanyId;
        private DataProvider _dataProvider;
        private readonly RemoteRepository _adam;
        private readonly Dictionary<string, string> _resourcesMapping;
        private readonly SQLiteConnection _db;

        #region "C O N S T R U C T O R S"

        public HttpDataProvider()
        {
            ActionPerformed = ActionPerformed.NoAction;

            _adam = new RemoteRepository();
            RemoteRepositorySetup();
            RestoreDefaults();
            _hasCompanyIdChanged = true;
            UpdatesPerformed = new List<DataTable>();
            ProcessedTables = new List<string>();

            Map.HttpDataProvider = this;

			// ORDER DOES MATTER!!
            _resourcesMapping = new Dictionary<string, string> {
                {"Aziende", "companies"},
                {"Anagrafiche", "contacts"},
                {"CausaliIVA", "vat"},
                {"ModalitàPagamento", "payment-methods"},
                {"Spese", "fees"},
                {"Pagamenti", "payments"},
                {"Magazzini", "warehouses"},
                {"Taglie", "sizes"},
                {"Documenti", "documents"},
                {"Listini", "price-lists"},
            };

			_db = new SQLiteConnection(DbName);
            _db.CreateTable<HttpMapping>();
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
            _db.Dispose();
            _adam.Dispose();
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
        private async Task<T> UpdateAsync<T>(DataRow row, bool batch) where T: BaseModel
        {

            ActionPerformed = ActionPerformed.NoAction;
            HttpResponse = null;

            // TODO this might be redundant as we have this guard in place already in both UpdateRowAsync and UpdateAsync(DataSet).
            if (!batch) UpdatesPerformed.Clear();

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

            RemoteRepositorySetup();
			HttpStatusCode statusCode;
			ActionPerformed action;
			DelegateDbMethod dbMethod;

			switch (mapping.RemoteId) {
				case null:
					retObj = await _adam.Insert((T)obj);
					dbMethod = _db.Insert;
					action = ActionPerformed.Added;
					statusCode = HttpStatusCode.Created;
					break;
				default:
					switch (row.RowState) {
						case DataRowState.Modified:
							retObj = await _adam.Replace((T) obj);
							dbMethod = _db.Update;
							action = ActionPerformed.Modified;
							statusCode = HttpStatusCode.OK;
							break;
						case DataRowState.Deleted:
							await _adam.Delete((T) obj);
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
			HttpResponse = _adam.HttpResponseMessage;
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
                
            return retObj;
        }
		internal string GetResourceNameFromDataRow(DataRow row)
        {
            if (!_resourcesMapping.ContainsKey(row.Table.TableName)) return null;
            return _resourcesMapping[row.Table.TableName];
        }
		internal HttpMapping GetModifiedMapping( int localId, string resource, bool shouldRetrieveRemoteCompanyId)
        {

			return _db.Table<HttpMapping>()
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
        }

        /// <summary>
        /// Retrieves the HttpMapping which maps to a specific DataRow.
        /// </summary>
        /// <param name="row">DataRow for which an HttpMapping is needed.</param>
        /// <param name="shouldRetrieveRemoteCompanyId">Wether the remote company id should be retrieved or not.</param>
        /// <returns>An HttpMapping object relative to the provided DataRow.</returns>
        internal HttpMapping GetMapping(DataRow row, bool shouldRetrieveRemoteCompanyId)
        {
            var localId = (int)row["id"];

            var resource = GetResourceNameFromDataRow(row);
            if (resource == null) return null;

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
                    entry = GetModifiedMapping(localId, resource, shouldRetrieveRemoteCompanyId);
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

        private void RemoteRepositorySetup()
        {
            _adam.ClientId = ClientId;
			_adam.LocalCache = new SqliteObjectCache { ApplicationName = ApplicationName };
            _adam.UserAccount = new UserAccount
            {
                Username = Username,
                Password = Password
            };
        }

        #region "U P D A T E  M E T H O D S"

        /// <summary>
        /// Stores a companyDataSet.AnagraficheDataTable.AnagraficheRow to a remote API endpoint.
        /// </summary>
        /// <param name="row">Source DataRow</param>
        /// <param name="batch">Wether this is part of a batch operation or not.</param>
        public async Task UpdateAnagraficheAsync(DataRow row, bool batch = false) 
        {
            await UpdateRowAsync<Contact>(row, batch);
        }
        /// <summary>
        /// Stores a companyDataSet.DocumentiDataTable.DocumentiRow to a remote API endpoint.
        /// </summary>
        /// <param name="row">Source DataRow</param>
        /// <param name="batch">Wether this is part of a batch operation or not.</param>
        public async Task UpdateDocumentiAsync(DataRow row, bool batch = false) 
        {
            await UpdateRowAsync<Document>(row, batch);
        }

        /// <summary>
        /// Stores a companyDataSet.CausaliIVADataTable.CausaliIVARow to a remote API endpoint.
        /// </summary>
        /// <param name="row">Source DataRow</param>
        /// <param name="batch">Wether this is part of a batch operation or not.</param>
        public async Task UpdateCausaliIVAAsync(DataRow row, bool batch = false) 
        {
            await UpdateRowAsync<Vat>(row, batch);
        }

        /// <summary>
        /// Stores a companyDataSet.ModalitàPagamentoDataTable.ModalitàPagamentoRow to a remote API endpoint.
        /// </summary>
        /// <param name="row">Source DataRow</param>
        /// <param name="batch">Wether this is part of a batch operation or not.</param>
        public async Task UpdateModalitàPagamentoAsync(DataRow row, bool batch = false) 
        {
            await UpdateRowAsync<PaymentMethod>(row, batch);
        }

        /// <summary>
        /// Stores a companyDataSet.SpeseDataTable.SpeseRow to a remote API endpoint.
        /// </summary>
        /// <param name="row">Source DataRow</param>
        /// <param name="batch">Wether this is part of a batch operation or not.</param>
        public async Task UpdateSpeseAsync(DataRow row, bool batch = false) 
        {
            await UpdateRowAsync<Fee>(row, batch);
        }

        /// <summary>
        /// Stores a companyDataSet.TaglieDataTable.TagliaRow to a remote API endpoint.
        /// </summary>
        /// <param name="row">Source DataRow</param>
        /// <param name="batch">Wether this is part of a batch operation or not.</param>
        public async Task UpdateTaglieAsync(DataRow row, bool batch = false) 
        {
            await UpdateRowAsync<Size>(row, batch);
        }

        /// <summary>
        /// Stores a companyDataSet.MagazziniDataTable.MagazziniRow to a remote API endpoint.
        /// </summary>
        /// <param name="row">Source DataRow</param>
        /// <param name="batch">Wether this is part of a batch operation or not.</param>
        public async Task UpdateMagazziniAsync(DataRow row, bool batch = false) 
        {
            await UpdateRowAsync<Warehouse>(row, batch);
        }

        /// <summary>
        /// Stores a companyDataSet.PagamentiDataTable.PagamentiRow to a remote API endpoint.
        /// </summary>
        /// <param name="row">Source DataRow</param>
        /// <param name="batch">Wether this is part of a batch operation or not.</param>
        public async Task UpdatePagamentiAsync(DataRow row, bool batch = false) 
        {
            await UpdateRowAsync<Payment>(row, batch);
        }

        /// <summary>
        /// Stores a companyDataSet.ListiniDataTable.ListiniRow to a remote API endpoint.
        /// </summary>
        /// <param name="row">Source DataRow</param>
        /// <param name="batch">Wether this is part of a batch operation or not.</param>
        public async Task UpdateListiniAsync(DataRow row, bool batch = false) 
        {
            await UpdateRowAsync<PriceList>(row, batch);
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
        private async Task UpdateRowAsync<T>(DataRow row, bool batch) where T: BaseModel
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

			var cache = new Dictionary<string, int>();

            //foreach (var table in changes.Tables)
            //{
            //    var targetTable = (DataTable)table;

            //    if (targetTable.Rows.Count == 0) continue;
            //    //if (!_resourcesMapping.ContainsKey(targetTable.TableName)) continue;

            //    UpdateParentTables(targetTable, 1, ref cache);
            //    if (!cache.ContainsKey(targetTable.TableName))
            //    {
            //        cache.Add(targetTable.TableName, 1);
            //    }

            //}
            foreach (var tableName in _resourcesMapping.Keys)
            {
                if (!changes.Tables.Contains(tableName)) continue;

                var targetTable = changes.Tables[tableName];
                if (targetTable.Rows.Count == 0) continue;

                UpdateParentTables(targetTable, 1, ref cache);
                if (!cache.ContainsKey(tableName))
                {
                    cache.Add(tableName, 1);
                }
            }

            var sortedCache = from entry in cache orderby entry.Value descending select entry;
			foreach (var c in sortedCache)
			{
                await UpdateTable(dataSet.Tables[c.Key]);
            }
        }

        private void UpdateParentTables(DataTable dt, int level, ref Dictionary<string, int> cache)
        {
			foreach (DataRelation parentRelation in dt.ParentRelations)
			{
                var parentTable = parentRelation.ParentTable;
				if (cache.ContainsKey(parentTable.TableName)) {
					if (cache[parentTable.TableName] < level) {
						cache[parentTable.TableName] = level;
					}
				}
				else {
					if (_resourcesMapping.ContainsKey(parentTable.TableName)) {
						cache.Add(parentTable.TableName, level);
						UpdateParentTables(parentTable, level + 1, ref cache);
					}
				}
			}
        }

        private async Task UpdateTable(DataTable dt)
        {
			if (!_resourcesMapping.ContainsKey(dt.TableName) || UpdatesPerformed.Contains(dt) || dt.Rows.Count == 0)
                return;

			var methodName = $"Update{dt.TableName}Async";

			// TODO handle the case of a write error on a batch of rows. Right now
			// the table is reported as not saved while in fact some rows might be saved
			// which would lead to inconsistency on the local Amica DB.

			foreach (DataRow row in dt.Rows) {
				await ((Task) GetType().GetMethod(methodName).Invoke(this, new object[] {row, true}));
				if (ActionPerformed == ActionPerformed.Aborted) goto End;
			}

			UpdatesPerformed.Add(dt);
            End: ;
        }

        #endregion

        #region "G E T  M E T H O D S"

        /// <summary>
        ///  Implements the Download Changes and Sync them logic.
        /// </summary>
        /// <typeparam name="T">Type of objects to be downloaded.</typeparam>
        /// <param name="dt">DataTable to be synced.</param>
        private async Task GetAndSyncCompanyTable<T>(DataTable dt) where T : BaseModelWithCompanyId
        {
            if (!_resourcesMapping.ContainsKey(dt.TableName)) return;

            var resource = _resourcesMapping[dt.TableName];
            var changes = await GetCompanyResourceAsync<T>(resource);

			// TODO if changes is null then something went wrong, probably with the
			// request (eg., a 401). Should we report back?
            if (changes == null || changes.Count == 0) return;

            SyncTable(resource, dt, changes);

			// TODO confirm that LocalCompanyId will never be 0. maybe even validate against it.
            DataProvider?.Update(LocalCompanyId ?? 0, dt);
        }
        /// <summary>
        ///  Implements the Download Changes and Sync them logic.
        /// </summary>
        /// <typeparam name="T">Type of objects to be downloaded.</typeparam>
        /// <param name="dt">DataTable to be synced.</param>
        private async Task GetAndSyncConfigTable<T>(DataTable dt) where T : BaseModel
        {
            if (!_resourcesMapping.ContainsKey(dt.TableName)) return;

            var resource = _resourcesMapping[dt.TableName];
            var changes = await GetConfigResourceAsync<T>(resource);

			// TODO if changes is null then something went wrong, probably with the
			// request (eg., a 401). Should we report back?
            if (changes == null || changes.Count == 0) return;

            SyncTable(resource, dt, changes);

            DataProvider?.Update(0, dt);
        }

        /// <summary>
        /// Downloads changes happened on a remote resource.
        /// </summary>
        /// <typeparam name="T">Type of the objects to be downloaded.</typeparam>
        /// <param name="resource">Remote resource name.</param>
        /// <returns>A list of changed objects.</returns>
        private async Task<List<T>> GetCompanyResourceAsync<T>(string resource) where T : BaseModelWithCompanyId
        {
            // ensure table exists 
            _db.CreateTable<HttpMapping>();

            if (LocalCompanyId == null)
                throw new ArgumentNullException(nameof(LocalCompanyId));

            // retrieve IMS
            var imsEntry = _db.Table<HttpMapping>()
                .Where(m => m.Resource.Equals(resource) && m.LocalCompanyId.Equals(LocalCompanyId))
                .OrderByDescending(v =>
                    v.LastUpdated
                )
                .FirstOrDefault();
            var ims = (imsEntry != null) ? imsEntry.LastUpdated : DateTime.MinValue;

            RemoteRepositorySetup();
            if (RemoteCompanyId == null)
                RetrieveRemoteCompanyId();

			var changes = await _adam.Get<T>(ims, RemoteCompanyId);

			HttpResponse = _adam.HttpResponseMessage;
			ActionPerformed = (HttpResponse != null && HttpResponse.StatusCode == HttpStatusCode.OK) ?
				((changes.Count > 0) ? ActionPerformed.Read : ActionPerformed.ReadNoChanges) : ActionPerformed.Aborted;


			return changes.ToList();
        }
		internal HttpMapping GetHttpMappingByRow(DataRow row)
        {
            if (!_resourcesMapping.ContainsKey(row.Table.TableName)) return null;

            _db.CreateTable<HttpMapping>();

            var resource = _resourcesMapping[row.Table.TableName];
            var localId = (int) row["Id"];

            var mapping = _db
                .Table<HttpMapping>()
                .FirstOrDefault(
                    m => m.Resource.Equals(resource) && m.LocalCompanyId.Equals(LocalCompanyId) && m.LocalId.Equals(localId));
            return mapping;
        }
        internal string GetRemoteRowId(DataRow row)
        {
            return GetHttpMappingByRow(row)?.RemoteId;
        }
        internal object GetLocalRowId(IUniqueId obj)
        {
            _db.CreateTable<HttpMapping>();

            var mapping = _db
                .Table<HttpMapping>()
                .FirstOrDefault(
                    m => m.RemoteId.Equals(obj.UniqueId));

            if (mapping != null)
                return mapping.LocalId;
			return DBNull.Value;
        }

        /// <summary>
        /// Downloads changes happened on a remote resource.
        /// </summary>
        /// <typeparam name="T">Type of the objects to be downloaded.</typeparam>
        /// <param name="resource">Remote resource name.</param>
        /// <returns>A list of changed objects.</returns>
        private async Task<List<T>> GetConfigResourceAsync<T>(string resource) where T : BaseModel
        {
            // ensure table exists 
            _db.CreateTable<HttpMapping>();

            // retrieve IMS
            var imsEntry = _db.Table<HttpMapping>()
                .Where(m => m.Resource.Equals(resource))
                .OrderByDescending(v =>
                    v.LastUpdated
                )
                .FirstOrDefault();
            var ims = (imsEntry != null) ? imsEntry.LastUpdated : DateTime.MinValue;

            RemoteRepositorySetup();
			var changes = await _adam.Get<T>(ims);

			HttpResponse = _adam.HttpResponseMessage;
			ActionPerformed = (HttpResponse != null && HttpResponse.StatusCode == HttpStatusCode.OK) ?
				((changes.Count > 0) ? ActionPerformed.Read : ActionPerformed.ReadNoChanges) : ActionPerformed.Aborted;

			return changes.ToList();
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

                //if (baseObj.Deleted && entry.LocalId != 0) continue;

                // address the weird lack of primary key on configDataSet tables.
                if (dt.PrimaryKey.Length == 0)
                    // should probably be using "id" as index, but we always
                    // have id as the first column in our datasets.
                    dt.PrimaryKey = new[] {dt.Columns[0]};


                var row = (entry.LocalId == 0) ? dt.NewRow() : dt.Rows.Find(entry.LocalId);
                if (row == null)
                    // TODO should we properly address this, instead of just throwing an exception?
                    throw new Exception("Cannot locate a DataRow that matches the syncdb reference.");


                Map.From(obj, row);

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

                if (baseObj.Deleted)
                {
                    row.Delete();
                }

            }
        }

		private List<string> ProcessedTables { get; }
        private bool _readOnce;

        /// <summary>
        /// Downloads all changes from the server and merges them to a local DataSet instance.
        /// </summary>
        /// <param name="dataSet">Local DataSet.</param>
		/// <remarks>Be careful that this will send a request for each table which has a corresponding endpoint on the remote server.</remarks>
        public async Task GetAsync(DataSet dataSet)
        {
            // TODO: query the Eve OpLog to know which resources/tables have updates, 
            // should greatly reduce the number of superfluous requests.

            ActionPerformed = ActionPerformed.NoAction;
            ProcessedTables.Clear();

            dataSet.EnforceConstraints = false;
            var readOnce = false;

            foreach (var table in dataSet.Tables.Cast<DataTable>().Where(dt => _resourcesMapping.ContainsKey(dt.TableName)))
            {
                await GetAndSyncTableIncludingParents(table);
                if (!readOnce) readOnce = ActionPerformed == ActionPerformed.Read;
            }

            if (readOnce) ActionPerformed = ActionPerformed.Read;

            // good luck
            dataSet.EnforceConstraints = true;
        }

        private async Task GetAndSyncTableIncludingParents(DataTable table)
        {
            _readOnce = false;

			await GetAndSyncParentTables(table);
			await GetAndSyncTable(table);

            if (_readOnce) ActionPerformed = ActionPerformed.Read;
        }
		private async Task GetAndSyncParentTables(DataTable table)
        {
			foreach (var parentTable in table.ParentRelations.Cast<DataRelation>()
				.Select(parentRelation => parentRelation.ParentTable)
				.Where(parentTable => _resourcesMapping.ContainsKey(parentTable.TableName) && !ProcessedTables.Contains(parentTable.TableName) && parentTable != table))
			{
				await GetAndSyncParentTables(parentTable);
                await GetAndSyncTable(parentTable);

			    if (!_readOnce) _readOnce = ActionPerformed == ActionPerformed.Read;

				ProcessedTables.Add(parentTable.TableName);
			}

        }

        private async Task GetAndSyncTable(DataTable table)
        {
            if (ProcessedTables.Contains(table.TableName)) return;

			await (Task)GetType().GetMethod($"GetAndSync{table.TableName}Async", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(this, new object[] { table.DataSet});
			ProcessedTables.Add(table.TableName);
        }

        /// <summary>
        /// Downloads Companies changes from the server and merges them to the Aziende table on the local dataset.
        /// </summary>
        /// <param name="dataSet">configDataSet instance.</param>
        private async Task GetAndSyncAziendeAsync(configDataSet dataSet)
        {
            await GetAndSyncConfigTable<Company>(dataSet.Aziende);
        }

        /// <summary>
        /// Downloads Contacts changes from the server and merges them to the Anagrafiche table on the local dataset.
        /// </summary>
        /// <param name="dataSet">companyDataSet instance.</param>
        private async Task GetAndSyncAnagraficheAsync(companyDataSet dataSet)
        {
            await GetAndSyncCompanyTable<Contact>(dataSet.Anagrafiche);
        }

        /// <summary>
        /// Downloads Countries changes from the server and merges them to the Nazioni table on the local dataset.
        /// </summary>
        /// <param name="dataSet">companyDataSet instance.</param>
        private async Task GetAndSyncDocumentiAsync(companyDataSet dataSet)
        {
            await GetAndSyncCompanyTable<Document>(dataSet.Documenti);
        }

        /// <summary>
        /// Downloads Vat changes from the server and merges them to the CausaliIVA table on the local dataset.
        /// </summary>
        /// <param name="dataSet">companyDataSet instance.</param>
        private async Task GetAndSyncCausaliIVAAsync(companyDataSet dataSet)
        {
            await GetAndSyncCompanyTable<Vat>(dataSet.CausaliIVA);
        }

        /// <summary>
        /// Downloads payment option changes from the server and merges them to the ModalitàPagamento table on the local dataset.
        /// </summary>
        /// <param name="dataSet">companyDataSet instance.</param>
        private async Task GetAndSyncModalitàPagamentoAsync(companyDataSet dataSet)
        {
            await GetAndSyncCompanyTable<PaymentMethod>(dataSet.ModalitàPagamento);
        }

        /// <summary>
        /// Downloads payment option changes from the server and merges them to the Spese table on the local dataset.
        /// </summary>
        /// <param name="dataSet">companyDataSet instance.</param>
        private async Task GetAndSyncSpeseAsync(companyDataSet dataSet)
        {
            await GetAndSyncCompanyTable<Fee>(dataSet.Spese);
        }

        /// <summary>
        /// Downloads Vat changes from the server and merges them to the Pagamenti table on the local dataset.
        /// </summary>
        /// <param name="dataSet">companyDataSet instance.</param>
        private async Task GetAndSyncPagamentiAsync(companyDataSet dataSet)
        {
            await GetAndSyncCompanyTable<Payment>(dataSet.Pagamenti);
        }

        /// <summary>
        /// Downloads Warehouses changes from the server and merges them to the Magazzini table on the local dataset.
        /// </summary>
        /// <param name="dataSet">companyDataSet instance.</param>
        private async Task GetAndSyncMagazziniAsync(companyDataSet dataSet)
        {
            await GetAndSyncCompanyTable<Warehouse>(dataSet.Magazzini);
        }

        /// <summary>
        /// Downloads Warehouses changes from the server and merges them to the Taglie table on the local dataset.
        /// </summary>
        /// <param name="dataSet">companyDataSet instance.</param>
        private async Task GetAndSyncTaglieAsync(companyDataSet dataSet)
        {
            await GetAndSyncCompanyTable<Size>(dataSet.Taglie);
        }

        /// <summary>
        /// Downloads PriceList changes from the server and merges them to the Listini table on the local dataset.
        /// </summary>
        /// <param name="dataSet">companyDataSet instance.</param>
        private async Task GetAndSyncListiniAsync(companyDataSet dataSet)
        {
            await GetAndSyncCompanyTable<PriceList>(dataSet.Listini);
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
        public Uri BaseAddress { get; set; }

		/// <summary>
		///  Username for authentication to the remote service.  
		/// </summary>
		public string Username { get; set; }

		/// <summary>
		///  Password for authentication to the remote service.  
		/// </summary>
		public string Password { get; set; }
		/// <summary>
        /// Client Id for autentication to the remote service..
        /// </summary>
		public string ClientId { get; set; }
		/// <summary>
        /// Application name used by the local cache.
        /// </summary>
		public string ApplicationName { get; set; }
        /// <summary>
        /// Returns the name of the local database used for keeping Amica and remote service in sync.
        /// </summary>
        public string SyncDatabaseName => DbName;

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
        public List<DataTable> UpdatesPerformed { get; }
        public List<DataTable> UpdatesToDo { get; }


        #endregion

        public void RestoreDefaults()
        {
            Username = null;
            Password = null;
            ClientId = null;

            BaseAddress = _adam.DiscoveryUri;
            ApplicationName = "HttpDataProvider";
        }
    }
}
