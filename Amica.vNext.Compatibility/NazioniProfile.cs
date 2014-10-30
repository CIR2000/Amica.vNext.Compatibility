using Amica.Data;
using Amica.vNext.Objects;
using AutoMapper;

namespace Amica.vNext.Compatibility
{
    internal class NazioniProfile : Profile
    {
        protected override void Configure()
        {
            base.Configure();

            CreateMap<companyDataSet.NazioniRow, Country>()
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Nome));
        }
        public override string ProfileName { get { return GetType().Name; } } }
}
