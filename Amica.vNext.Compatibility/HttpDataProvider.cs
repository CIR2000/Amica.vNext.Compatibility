using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Amica.vNext.Objects;
using Amica.vNext.Http;

namespace Amica.vNext.Compatibility
{

    public class HttpDataProvider
    {
        private readonly Dictionary<string, string> _d;

        public HttpDataProvider()
        {
            _d = new Dictionary<string, string>
            {
                {"Nazioni", "countries"}
            };
        }

        public async Task UpdateNazioniAsync(DataRow row)
        {
            await UpdateAsync<Country>(row);
        }
        
        private async Task<T> UpdateAsync<T>(DataRow row)
        {
            var obj = FromAmica.To<T>(row);
            var resource = _d[row.Table.TableName];

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
