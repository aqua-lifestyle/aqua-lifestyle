using AutoMapper;
using AqualLifeStyle.Domain.Customers;

namespace AqualLifeStyle.Application.Customers.Dto
{
    public class CustomerMapProfile : Profile
    {
        public CustomerMapProfile()
        {
            CreateMap<Customer, CustomerDto>()
                .ForMember(destination => destination.Email, options => options.MapFrom(source => source.Email == null ? null : source.Email.Value))
                .ForMember(destination => destination.AreaName, options => options.MapFrom(source => source.Area == null ? null : source.Area.Name));
        }
    }
}
