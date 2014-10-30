using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Amica.vNext.Objects;
using Amica.vNext.Http;
using SQLite;

namespace Amica.vNext.Compatibility
{

    public class AmicaToAdam
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
        private SQLiteConnection _db;

        public HttpDataProvider()
        {
            _resourcesMapping = new Dictionary<string, string>
            {
                {"Nazioni", "countries"}
            };

            _db = new SQLiteConnection(DbName);
            _db.CreateTable<AmicaToAdam>();
        }

        public async Task UpdateNazioniAsync(DataRow row)
        {
            await UpdateAsync<Country>(row);
        }
        
        private async Task<T> UpdateAsync<T>(DataRow row)
        {
            var obj = FromAmica.To<T>(row);
            var resource = _resourcesMapping[row.Table.TableName];

            var rc = new RestClient();
            var value = default(T);

            switch (row.RowState) {
                case DataRowState.Added:
                    value = await rc.PostAsync<T>(resource, obj);
                    break;
                case DataRowState.Modified:
                    break;
                case DataRowState.Deleted:
                    break;
            }
            return value;
        }
    }
}
