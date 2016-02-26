using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Amica.vNext.Compatibility.Maps
{
    internal class DocumentItemMapping : Mapping
    {
        internal DocumentItemMapping() : base()
        {
            Fields.Add("CodiceArticolo", new FieldMapping {PropertyName = "Sku"});
            Fields.Add("Descrizione", new FieldMapping { PropertyName = "Description"});

   //         Parents.Add(
   //                 "IdAnagrafica",
   //                 new DataRelationMapping {
   //                     FieldName = "Contact",
   //                     RelationName = "FK_Anagrafiche_Documenti",
   //                     FieldType = typeof(ContactMinimal)
   //                 }
   //             );

   //         Children.Add(
   //             new DataRelationMapping
   //             {
   //                 FieldName = "Items",
   //                 FieldType = typeof(DocumentItem),
   //                 RelationName = "FK_Documenti_Righe",
   //             }
			//);
        }
    }
}
