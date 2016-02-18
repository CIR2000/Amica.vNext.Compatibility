using System.Collections.Generic;

namespace Amica.vNext.Compatibility.Maps
{
    internal class CompanyMapping : Mapping
    {
        internal CompanyMapping() : base()
        {
            Fields.Add("Nome", new FieldMapping {FieldName = "Name"});
        }
    }

}
