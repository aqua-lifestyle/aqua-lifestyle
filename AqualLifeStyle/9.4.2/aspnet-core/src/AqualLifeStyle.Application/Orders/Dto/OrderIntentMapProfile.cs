using AutoMapper;
using AqualLifeStyle.Domain.Orders;

namespace AqualLifeStyle.Application.Orders.Dto
{
    public class OrderIntentMapProfile : Profile
    {
        public OrderIntentMapProfile()
        {
            CreateMap<OrderIntent, OrderIntentDto>()
                .ForMember(destination => destination.Status, options => options.MapFrom(source => (int)source.Status))
                .ForMember(destination => destination.StatusText, options => options.MapFrom(source => source.Status.ToString()));
        }
    }
}
