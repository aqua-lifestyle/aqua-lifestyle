using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Auditing;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Runtime.Session;
using Abp.Timing;
using Abp.UI;
using AqualLifeStyle.Application.Savings.Dto;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Savings;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.Savings
{
    [Audited]
    public class ClubMemberSavingsAppService
        : AqualLifeStyleAppServiceBase, IClubMemberSavingsAppService
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly IRepository<SavingsAccount, Guid> _savingsRepository;

        public ClubMemberSavingsAppService(
            ICustomerRepository customerRepository,
            IRepository<SavingsAccount, Guid> savingsRepository)
        {
            _customerRepository = customerRepository;
            _savingsRepository = savingsRepository;
        }

        [AbpAuthorize(AquaPermissions.Savings.ViewSelf)]
        public async Task<MySavingsAccountDto> GetMyAccountAsync()
        {
            var tenantId = GetRequiredTenantId(
                "Your savings account is unavailable.");
            var customer = await _customerRepository.FirstOrDefaultAsync(
                item =>
                    item.TenantId == tenantId &&
                    item.UserId == AbpSession.GetUserId());
            if (customer == null || !customer.IsActive)
            {
                throw new UserFriendlyException(
                    "Your savings account is unavailable.",
                    "An active Club Member account is required to view savings.");
            }

            var account = await _savingsRepository
                .GetAllIncluding(item => item.Contributions)
                .Where(item => item.CustomerId == customer.Id)
                .OrderByDescending(item => item.OpenedAt)
                .FirstOrDefaultAsync();

            return new MySavingsAccountDto
            {
                Account = account == null
                    ? null
                    : SavingsAccountDtoMapper.Map(
                        account,
                        customer.Name,
                        customer.Email.Value,
                        Clock.Now.ToUniversalTime())
            };
        }
    }
}
