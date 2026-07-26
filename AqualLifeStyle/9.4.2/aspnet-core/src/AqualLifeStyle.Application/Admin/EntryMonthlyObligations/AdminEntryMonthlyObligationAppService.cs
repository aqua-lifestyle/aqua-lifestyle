using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Application.Services.Dto;
using Abp.Auditing;
using Abp.Authorization;
using Abp.Domain.Repositories;
using AqualLifeStyle.Application.EntryMonthlyObligations.Dto;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.Admin.EntryMonthlyObligations
{
    [Audited]
    public class AdminEntryMonthlyObligationAppService
        : AdminAppServiceBase, IAdminEntryMonthlyObligationAppService
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly IRepository<EntryMonthlyObligation, Guid>
            _obligationRepository;

        public AdminEntryMonthlyObligationAppService(
            ICustomerRepository customerRepository,
            IRepository<EntryMonthlyObligation, Guid> obligationRepository)
        {
            _customerRepository = customerRepository;
            _obligationRepository = obligationRepository;
        }

        [AbpAuthorize(AquaPermissions.Admin.EntryMonthlyObligations.View)]
        public async Task<PagedResultDto<EntryMonthlyObligationDto>>
            GetAllAsync(AdminEntryMonthlyObligationListInput input)
        {
            input ??= new AdminEntryMonthlyObligationListInput();
            ValidateRequestedTenant(input.TenantId, "AQGreen commitment");
            if (!AbpSession.TenantId.HasValue &&
                !await PermissionChecker.IsGrantedAsync(
                    AquaPermissions.Admin.AllTenants))
            {
                throw new AbpAuthorizationException(
                    "Host-wide AQGreen commitment access requires permission to view all Areas.");
            }

            using (DisableAllTenantDataFiltersForHost())
            {
                var query =
                    from obligation in _obligationRepository.GetAll()
                    join customer in _customerRepository.GetAll()
                        on obligation.CustomerId equals customer.Id
                    select new QueryRow
                    {
                        Obligation = obligation,
                        Customer = customer
                    };
                if (AbpSession.TenantId.HasValue)
                {
                    var tenantId = AbpSession.TenantId.Value;
                    query = query.Where(row =>
                        row.Obligation.TenantId == tenantId);
                }
                else if (input.TenantId.HasValue)
                {
                    var tenantId = input.TenantId.Value;
                    query = query.Where(row =>
                        row.Obligation.TenantId == tenantId);
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
                    .OrderByDescending(row => row.Obligation.PeriodYear)
                    .ThenByDescending(row => row.Obligation.PeriodMonth)
                    .Skip(input.SkipCount)
                    .Take(input.MaxResultCount)
                    .ToListAsync();
                return new PagedResultDto<EntryMonthlyObligationDto>(
                    total,
                    rows.Select(row =>
                            EntryMonthlyObligationDtoMapper.Map(
                                row.Obligation,
                                row.Customer.Name,
                                row.Customer.Email.Value))
                        .ToList());
            }
        }

        private sealed class QueryRow
        {
            public EntryMonthlyObligation Obligation { get; set; }
            public Customer Customer { get; set; }
        }
    }
}
