using AutoMapper;
using AqualLifeStyle.Domain.AreaLeaders;

namespace AqualLifeStyle.Application.AreaLeaders.Dto
{
    public class AreaLeaderMapProfile : Profile
    {
        public AreaLeaderMapProfile()
        {
            CreateMap<AreaLeader, AreaLeaderDto>()
                .ForMember(destination => destination.LicenseType, options => options.MapFrom(source => (int)source.LicenseType))
                .ForMember(destination => destination.Rank, options => options.MapFrom(source => (int)source.Rank));

            CreateMap<AreaSpace, AreaSpaceDto>()
                .ForMember(destination => destination.AddressLine, options => options.MapFrom(source => source.AddressLine))
                .ForMember(destination => destination.Status, options => options.MapFrom(source => (int)source.Status));
        }
    }
}
