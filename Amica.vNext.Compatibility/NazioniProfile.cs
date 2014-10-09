using Amica.Data;
using AutoMapper;

namespace Amica.vNext.Objects
{
    internal class NazioniProfile : Profile
    {
        protected override void Configure()
        {
            base.Configure();

            CreateMap<companyDataSet.NazioniRow, Country>()
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Nome))
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id));


        }
        public override string ProfileName { get { return GetType().Name; } } }
}
