namespace Amica.vNext.Compatibility.Maps
{
    internal class CompanyMapping : Mapping
    {
        internal CompanyMapping()
        {
            Fields.Add("Nome", new FieldMapping {PropertyName = "Name"});
        }
    }

}
