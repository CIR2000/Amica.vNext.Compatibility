using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Amica.vNext.Compatibility.Maps
{
    class Company
    {
        static internal Map<string, string> Make()
        {
            var map = new Map<string, string>();

            map.Add("Nome", "Name");

            return map;
        }
    }
}
