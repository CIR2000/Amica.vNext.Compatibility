using System;
using System.Collections.Generic;
using Amica.vNext.Models;
using Amica.vNext.Models.Documents;

namespace Amica.vNext.Compatibility.Maps
{
    internal class DocumentItemSizeMapping : Mapping
    {

        internal DocumentItemSizeMapping()
        {
            Fields.Add("Nome", new FieldMapping { PropertyName = "Name"});
            Fields.Add("Taglia1", new FieldMapping
            {
                PropertyName = "Number",
                //DownstreamTransform = (x) => ((DocumentItemSize)obj).Number,
                //UpstreamTransform = (x, obj) => ((DocumentItemSize)obj).Number
            });
        }
		private object SetTaglia(object obj, int i)
        {
            var l = (List<string>)obj;
            if (i > l.Count) return null;
            return l[i-1];

        }
    }

}
