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
            UpstreamTransform = (x, obj) => (x.ToString().ToUpper().StartsWith("IT")) ? x.ToString() : "IT" + x.ToString();
        }
    }
}
