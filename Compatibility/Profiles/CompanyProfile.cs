using Amica.Data;
using Amica.vNext.Models;
using AutoMapper;

namespace Amica.vNext.Compatibility.Profiles
{
    /// <summary>
    /// Maps a configDataSet.AziendeRow to a Amica.vNext.Objects.Company object.
    /// </summary>
    internal class CompanyProfile : Profile
    {
        protected override void Configure()
        {
            base.Configure();

            CreateMap<configDataSet.AziendeRow, Company>()
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Nome));
        }
        public override string ProfileName { get { return GetType().Name; } } }
}