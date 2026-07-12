using System.Collections.Generic;
using System.Threading.Tasks;
using AqualLifeStyle.Application.Facilitators.Dto;
using AqualLifeStyle.Domain.AreaLeaders;
using AqualLifeStyle.Domain.Facilitators;

namespace AqualLifeStyle.Application.Facilitators
{
    public interface IFacilitatorAppService : Abp.Application.Services.IApplicationService
    {
        Task<FacilitatorDto> RegisterAsync(RegisterFacilitatorDto input);

        Task<IReadOnlyList<FacilitatorDto>> GetAllAsync();

        Task<FacilitatorDto> GetAsync(int id);

        Task<FacilitatorDto> GetByCustomerAsync(int customerId);
    }
}
