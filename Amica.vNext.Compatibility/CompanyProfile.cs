using Amica.Data;
using Amica.vNext.Objects;
using AutoMapper;

namespace Amica.vNext.Compatibility
{
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