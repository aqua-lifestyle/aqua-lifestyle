using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Application.Services.Dto;
using Abp.Auditing;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Timing;
using AqualLifeStyle.Application.Savings.Dto;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Savings;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.Admin.Savings
{
    [Audited]
    public class AdminSavingsAppService
        : AdminAppServiceBase, IAdminSavingsAppService
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly IRepository<SavingsAccount, Guid> _savingsRepository;

        public AdminSavingsAppService(
            ICustomerRepository customerRepository,
            IRepository<SavingsAccount, Guid> savingsRepository)
        {
            _customerRepository = customerRepository;
            _savingsRepository = savingsRepository;
        }

        [AbpAuthorize(AquaPermissions.Admin.Savings.View)]
        public async Task<PagedResultDto<SavingsAccountDto>> GetAllAsync(
            AdminSavingsAccountListInput input)
        {
            input ??= new AdminSavingsAccountListInput();
            ValidateRequestedTenant(input.TenantId, "Savings");
            if (!AbpSession.TenantId.HasValue &&
                !input.TenantId.HasValue &&
                !await PermissionChecker.IsGrantedAsync(
                    AquaPermissions.Admin.AllTenants))
            {
                throw new AbpAuthorizationException(
                    "Host-wide savings access requires permission to view all Areas.");
            }

            using (DisableAllTenantDataFiltersForHost())
            {
                var query =
                    from account in _savingsRepository.GetAllIncluding(
                        item => item.Contributions)
                    join customer in _customerRepository.GetAll()
                        on account.CustomerId equals customer.Id
                    select new SavingsAccountQueryRow
                    {
                        Account = account,
                        Customer = customer
                    };
                if (AbpSession.TenantId.HasValue)
                {
                    var tenantId = AbpSession.TenantId.Value;
                    query = query.Where(row =>
                        row.Account.TenantId == tenantId);
                }
                else if (input.TenantId.HasValue)
                {
                    var tenantId = input.TenantId.Value;
                    query = query.Where(row =>
                        row.Account.TenantId == tenantId);
                }

                if (!string.IsNullOrWhiteSpace(input.Keyword))
                {
                    var keyword = input.Keyword.Trim().ToLower();
                    query = query.Where(row =>
                        row.Customer.Name.ToLower().Contains(keyword) ||
                        row.Customer.Email.Value.ToLower().Contains(keyword));
                }

                var total = await query.CountAsync();
                var rows = await query
                    .OrderByDescending(row => row.Account.OpenedAt)
                    .Skip(input.SkipCount)
                    .Take(input.MaxResultCount)
                    .ToListAsync();
                var asOf = Clock.Now.ToUniversalTime();

                return new PagedResultDto<SavingsAccountDto>(
                    total,
                    rows.Select(row => SavingsAccountDtoMapper.Map(
                            row.Account,
                            row.Customer.Name,
                            row.Customer.Email.Value,
                            asOf))
                        .ToList());
            }
        }

        private sealed class SavingsAccountQueryRow
        {
            public SavingsAccount Account { get; set; }
            public Customer Customer { get; set; }
        }
    }
}
