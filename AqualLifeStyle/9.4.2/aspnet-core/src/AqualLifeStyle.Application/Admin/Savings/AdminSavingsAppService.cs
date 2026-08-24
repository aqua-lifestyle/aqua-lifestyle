using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Application.Services.Dto;
using Abp.Auditing;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Runtime.Session;
using Abp.Timing;
using AqualLifeStyle.Application.Savings.Dto;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.Areas;
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
        private readonly IRepository<Area, Guid> _areaRepository;
        private readonly IRepository<AreaAdminAssignment, Guid> _areaAdminAssignmentRepository;

        public AdminSavingsAppService(
            ICustomerRepository customerRepository,
            IRepository<SavingsAccount, Guid> savingsRepository,
            IRepository<Area, Guid> areaRepository,
            IRepository<AreaAdminAssignment, Guid> areaAdminAssignmentRepository)
        {
            _customerRepository = customerRepository;
            _savingsRepository = savingsRepository;
            _areaRepository = areaRepository;
            _areaAdminAssignmentRepository = areaAdminAssignmentRepository;
        }

        [AbpAuthorize(AquaPermissions.Admin.Savings.View)]
        public async Task<PagedResultDto<SavingsAccountDto>> GetAllAsync(
            AdminSavingsAccountListInput input)
        {
            input ??= new AdminSavingsAccountListInput();
            ValidateRequestedTenant(input.TenantId, "Savings");
            if (!AbpSession.TenantId.HasValue &&
                !await PermissionChecker.IsGrantedAsync(
                    AquaPermissions.Admin.AllTenants))
            {
                throw new AbpAuthorizationException(
                    "Host-wide savings access requires permission to view all Areas.");
            }

            using (DisableAllTenantDataFiltersForHost())
            {
                var areaScope = await GetAuthorizedAreaScopeAsync();
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
                if (areaScope != null)
                {
                    query = query.Where(row =>
                        row.Customer.AreaId.HasValue &&
                        areaScope.Contains(row.Customer.AreaId.Value));
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

        private async Task<Guid[]> GetAuthorizedAreaScopeAsync()
        {
            if (!AbpSession.TenantId.HasValue) return null;

            var tenantId = AbpSession.TenantId.Value;
            var userId = AbpSession.GetUserId();
            return await (
                    from assignment in _areaAdminAssignmentRepository.GetAll()
                    join area in _areaRepository.GetAll()
                        on new { assignment.TenantId, assignment.AreaId }
                        equals new { area.TenantId, AreaId = area.Id }
                    where assignment.TenantId == tenantId &&
                          assignment.UserId == userId &&
                          !assignment.RevokedAt.HasValue &&
                          area.IsActive
                    select assignment.AreaId)
                .Distinct()
                .ToArrayAsync();
        }

        private sealed class SavingsAccountQueryRow
        {
            public SavingsAccount Account { get; set; }
            public Customer Customer { get; set; }
        }
    }
}
