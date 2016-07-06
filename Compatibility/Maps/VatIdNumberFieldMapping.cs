using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Amica.vNext.Compatibility.Maps
{
    class VatIdNumberFieldMapping : FieldMapping
    {
		public VatIdNumberFieldMapping()
        {
            PropertyName = "VatIdentificationNumber";
            UpstreamTransform = (key, row, obj) => (row[key].ToString().ToUpper().StartsWith("IT")) ? row[key].ToString() : "IT" + row[key].ToString();
        }
    }
}
