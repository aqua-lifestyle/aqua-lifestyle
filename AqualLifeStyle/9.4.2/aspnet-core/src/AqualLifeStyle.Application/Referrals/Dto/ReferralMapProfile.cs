using AutoMapper;
using AqualLifeStyle.Domain.Facilitators;

namespace AqualLifeStyle.Application.Referrals.Dto
{
    public class ReferralMapProfile : Profile
    {
        public ReferralMapProfile()
        {
            CreateMap<Referral, ReferralDto>()
                .ForMember(destination => destination.Type, options => options.MapFrom(source => (int)source.Type));
        }
    }
}
