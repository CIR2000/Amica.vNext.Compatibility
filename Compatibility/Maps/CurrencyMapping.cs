namespace Amica.vNext.Compatibility.Maps
{
    internal class CurrencyMapping : Mapping
    {
        internal CurrencyMapping()
        {
            Fields.Add("Nome", new FieldMapping {PropertyName = "Name"});
            Fields.Add("Sigla", new FieldMapping {PropertyName = "Code"});
        }
    }

}
