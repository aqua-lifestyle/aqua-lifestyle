using System.Linq;
using AutoMapper;
using AqualLifeStyle.Domain.Enquiries;
using AqualLifeStyle.Domain.Enums;

namespace AqualLifeStyle.Application.Enquiries.Dto
{
    public class EnquiryMapProfile : Profile
    {
        public EnquiryMapProfile()
        {
            CreateMap<Enquiry, EnquiryDto>()
                .ForMember(destination => destination.Status, options => options.MapFrom(source => (int)source.Status))
                .ForMember(destination => destination.CreatedAt, options => options.MapFrom(source => source.CreatedAt.ToString("u")))
                .ForMember(destination => destination.IsClosed, options => options.MapFrom(source => source.Status == EnquiryStatus.Closed))
                .ForMember(destination => destination.IsPending, options => options.MapFrom(source => source.Status == EnquiryStatus.Pending))
                .ForMember(destination => destination.ConvertedAt, options => options.MapFrom(source => source.ConvertedAt.HasValue ? source.ConvertedAt.Value.ToString("u") : null))
                .ForMember(destination => destination.FollowUpCount, options => options.MapFrom(source => source.GetFollowUpCount()))
                .ForMember(destination => destination.IsSalesReady, options => options.MapFrom(source => source.IsSalesReady()))
                .ForMember(destination => destination.FollowUps, options => options.MapFrom(source => source.FollowUps.OrderByDescending(followUp => followUp.FollowUpDate)));

            CreateMap<EnquiryFollowUp, EnquiryFollowUpDto>()
                .ForMember(destination => destination.Outcome, options => options.MapFrom(source => (int)source.Outcome))
                .ForMember(destination => destination.OutcomeText, options => options.MapFrom(source => source.Outcome.ToString()));
        }
    }
}
