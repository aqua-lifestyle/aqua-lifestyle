using AutoMapper;
using AqualLifeStyle.Domain.Customers;

namespace AqualLifeStyle.Application.Admin.Customers.Dto
{
    public class AdminCustomerMapProfile : Profile
    {
        public AdminCustomerMapProfile()
        {
            CreateMap<Customer, AdminCustomerDto>()
                .ForMember(dto => dto.TenantId, options => options.MapFrom(customer => customer.TenantId ?? 0))
                .ForMember(dto => dto.FirstName, options => options.MapFrom(customer => customer.User.Name))
                .ForMember(dto => dto.LastName, options => options.MapFrom(customer => customer.User.Surname))
                .ForMember(dto => dto.Email, options => options.MapFrom(customer => customer.Email.Value));
        }
    }
}
