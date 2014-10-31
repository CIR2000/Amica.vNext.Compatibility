using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Amica.vNext.Objects;
using Amica.vNext.Http;
using SQLite;

namespace Amica.vNext.Compatibility
{

    public class HttpMapping
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        [Indexed(Name="Local", Order=1, Unique=true)]
        public string LocalTable { get; set; }
        [Indexed(Name="Local", Order=2, Unique=true)]
        public int LocalId { get; set; }
        [Indexed]
        public string RemoteId { get; set; }
        public string ETag { get; set; }
        [Indexed]
        public DateTime? LastUpdated { get; set; }
    }

    public class HttpDataProvider
    {
        private const string DbName = "HttpMapping.db";
        private readonly Dictionary<string, string> _resourcesMapping;
        private readonly SQLiteConnection _db;

        public HttpDataProvider()
        {
            _resourcesMapping = new Dictionary<string, string> {
                {"Aziende", "companies"}
            };

            _db = new SQLiteConnection(DbName);
            _db.CreateTable<HttpMapping>();
        }

        private async Task<T> UpdateAsync<T>(DataRow row) where T: class
        {
            var obj = FromAmica.To<T>(row);
            var localId = Int32.Parse(row["Id"].ToString());
            var resource = _resourcesMapping[row.Table.TableName];


            var rc = new RestClient("http://amica-test.herokuapp.com", new BasicAuthenticator("token1", ""));
            var value = default(T);

            switch (row.RowState) {
                case DataRowState.Added:
                    value = await rc.PostAsync<T>(resource, obj);
                    if (value != null) { 
                            LogNewRow(resource, localId, value);
                    }
                    break;
                case DataRowState.Modified:
                    break;
                case DataRowState.Deleted:
                    break;
            }
            return value;
        }

        private void LogNewRow(string resource, int localId, object item )
        {
            var source = (BaseClass)item;
            if (source == null) {
                throw new ArgumentException("item");
            }

            _db.Insert(new HttpMapping
            {
                LocalId = localId,
                LocalTable = resource,
                ETag = source.ETag,
                RemoteId = source.UniqueId,
                LastUpdated = source.Updated
            });

        }
        public async Task UpdateNazioniAsync(DataRow row)
        {
            await UpdateAsync<Country>(row);
        }
        
        public async Task UpdateAziendaAsync(DataRow row)
        {
            await UpdateAsync<Company>(row);
        }
        

    }
}
