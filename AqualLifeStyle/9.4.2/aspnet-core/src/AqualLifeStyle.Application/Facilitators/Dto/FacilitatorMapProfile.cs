using AutoMapper;
using AqualLifeStyle.Domain.Facilitators;

namespace AqualLifeStyle.Application.Facilitators.Dto
{
    public class FacilitatorMapProfile : Profile
    {
        public FacilitatorMapProfile()
        {
            CreateMap<Facilitator, FacilitatorDto>()
                .ForMember(destination => destination.Rank, options => options.MapFrom(source => (int)source.Rank));
        }
    }
}
