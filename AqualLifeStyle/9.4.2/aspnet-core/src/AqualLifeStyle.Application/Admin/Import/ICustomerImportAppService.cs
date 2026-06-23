using System.Threading.Tasks;
using Abp.Application.Services;
using AqualLifeStyle.Application.Admin.Import.Dto;

namespace AqualLifeStyle.Application.Admin.Import
{
    public interface ICustomerImportAppService : IApplicationService
    {
        Task<CustomerImportPreviewDto> PreviewAsync(PreviewCustomerImportInput input);
        Task<CustomerImportResultDto> ImportAsync(ConfirmCustomerImportInput input);
    }
}
