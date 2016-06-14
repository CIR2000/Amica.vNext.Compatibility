using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Amica.vNext.Models.Documents;

namespace Amica.vNext.Compatibility.Helpers
{
	public class CassaPrevidenziale
    {
        public string Descrizione { get; internal set; }
        public SocialSecurityCategory Category{ get; internal set; }
    }
    public class SocialSecurityAdapter 
    {
        private static List<CassaPrevidenziale> amica;
		static SocialSecurityAdapter()
        {
            amica = new List<CassaPrevidenziale>();
            amica.Add(new CassaPrevidenziale { Category = DocumentHelpers.SocialSecurityCategories[SocialSecurityCategoryType.TC01], Descrizione = "Cassa avvocati e procuratori legali" });
            amica.Add(new CassaPrevidenziale { Category = DocumentHelpers.SocialSecurityCategories[SocialSecurityCategoryType.TC02], Descrizione = "Cassa avvocati e procuratori legali" });
            amica.Add(new CassaPrevidenziale { Category = DocumentHelpers.SocialSecurityCategories[SocialSecurityCategoryType.TC03], Descrizione = "Cassa dottori commercialisti" });
            amica.Add(new CassaPrevidenziale { Category = DocumentHelpers.SocialSecurityCategories[SocialSecurityCategoryType.TC03], Descrizione = "Cass geometri"});
            amica.Add(new CassaPrevidenziale { Category = DocumentHelpers.SocialSecurityCategories[SocialSecurityCategoryType.TC04], Descrizione = "Cassa ingegneri e architetti"});
            amica.Add(new CassaPrevidenziale { Category = DocumentHelpers.SocialSecurityCategories[SocialSecurityCategoryType.TC05], Descrizione = "Cassa notariato"});
            amica.Add(new CassaPrevidenziale { Category = DocumentHelpers.SocialSecurityCategories[SocialSecurityCategoryType.TC06], Descrizione = "Cassa ragionieri e periti commerciali"});
            amica.Add(new CassaPrevidenziale { Category = DocumentHelpers.SocialSecurityCategories[SocialSecurityCategoryType.TC07], Descrizione = "ENASARCO"});
            amica.Add(new CassaPrevidenziale { Category = DocumentHelpers.SocialSecurityCategories[SocialSecurityCategoryType.TC08], Descrizione = "ENPACL"});
            amica.Add(new CassaPrevidenziale { Category = DocumentHelpers.SocialSecurityCategories[SocialSecurityCategoryType.TC09], Descrizione = "ENPAM"});
            amica.Add(new CassaPrevidenziale { Category = DocumentHelpers.SocialSecurityCategories[SocialSecurityCategoryType.TC10], Descrizione = "ENPAF"});
            amica.Add(new CassaPrevidenziale { Category = DocumentHelpers.SocialSecurityCategories[SocialSecurityCategoryType.TC11], Descrizione = "ENPAV"});
            amica.Add(new CassaPrevidenziale { Category = DocumentHelpers.SocialSecurityCategories[SocialSecurityCategoryType.TC12], Descrizione = "ENPAIA"});
            amica.Add(new CassaPrevidenziale { Category = DocumentHelpers.SocialSecurityCategories[SocialSecurityCategoryType.TC13], Descrizione = "Cassa imprese di spedizione e agenzie marittime"});
            amica.Add(new CassaPrevidenziale { Category = DocumentHelpers.SocialSecurityCategories[SocialSecurityCategoryType.TC14], Descrizione = "INPGI"});
            amica.Add(new CassaPrevidenziale { Category = DocumentHelpers.SocialSecurityCategories[SocialSecurityCategoryType.TC15], Descrizione = "ONAOSI"});
            amica.Add(new CassaPrevidenziale { Category = DocumentHelpers.SocialSecurityCategories[SocialSecurityCategoryType.TC16], Descrizione = "CASAGIT"});
            amica.Add(new CassaPrevidenziale { Category = DocumentHelpers.SocialSecurityCategories[SocialSecurityCategoryType.TC17], Descrizione = "EPPI"});
            amica.Add(new CassaPrevidenziale { Category = DocumentHelpers.SocialSecurityCategories[SocialSecurityCategoryType.TC18], Descrizione = "EPAP"});
            amica.Add(new CassaPrevidenziale { Category = DocumentHelpers.SocialSecurityCategories[SocialSecurityCategoryType.TC19], Descrizione = "ENPAB"});
            amica.Add(new CassaPrevidenziale { Category = DocumentHelpers.SocialSecurityCategories[SocialSecurityCategoryType.TC20], Descrizione = "ENPAPI"});
            amica.Add(new CassaPrevidenziale { Category = DocumentHelpers.SocialSecurityCategories[SocialSecurityCategoryType.TC21], Descrizione = "ENPAP"});
            amica.Add(new CassaPrevidenziale { Category = DocumentHelpers.SocialSecurityCategories[SocialSecurityCategoryType.TC22], Descrizione = "INPS"});
        }
		public static string GetAmicaDescription(SocialSecurityCategory ss)
        {
			foreach (var c in amica)
            {
                if (c.Category.Category.Equals(ss.Category)) return c.Descrizione;
            }
            throw new ArgumentOutOfRangeException();
        }
		public static SocialSecurityCategory GetSocialSecurityCategory(string descrizione)
        {
			foreach (var c in amica)
            {
                if (c.Descrizione == descrizione) return c.Category;
            }
            throw new ArgumentOutOfRangeException();
        }
    }
}
