using Amica.vNext.Models;

namespace Amica.vNext.Compatibility.Maps
{
    internal class PriceListMapping : Mapping
    {

        internal PriceListMapping()
        {
            Fields.Add("Id", new FieldMapping { PropertyName = "UniqueId" });
            Fields.Add("Nome", new FieldMapping { PropertyName = "Name" });
        }
    }

}
