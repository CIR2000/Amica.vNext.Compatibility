using System;
using System.Collections.Generic;
namespace Amica.vNext.Compatibility.Maps
{
    internal class CountryMapping : Mapping
    {
        internal CountryMapping() : base()
        {
            Fields.Add("Nome", new FieldMapping {FieldName = "Name"});
        }
    }
}
