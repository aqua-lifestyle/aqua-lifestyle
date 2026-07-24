using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Application.Services.Dto;
using Abp.Auditing;
using Abp.Authorization;
using Abp.Domain.Repositories;
using AqualLifeStyle.Application.Loans.Dto;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.Admin.Loans
{
    [Audited]
    public class AdminOnyxLoanAppService
        : AdminAppServiceBase, IAdminOnyxLoanAppService
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly IRepository<OnyxLoanAgreement, Guid> _loanRepository;

        public AdminOnyxLoanAppService(
            ICustomerRepository customerRepository,
            IRepository<OnyxLoanAgreement, Guid> loanRepository)
        {
            _customerRepository = customerRepository;
            _loanRepository = loanRepository;
        }

        [AbpAuthorize(AquaPermissions.Admin.Loans.View)]
        public async Task<PagedResultDto<OnyxLoanAgreementDto>> GetAllAsync(
            AdminOnyxLoanAgreementListInput input)
        {
            input ??= new AdminOnyxLoanAgreementListInput();
            ValidateRequestedTenant(input.TenantId, "Loan");
            if (!AbpSession.TenantId.HasValue &&
                !await PermissionChecker.IsGrantedAsync(
                    AquaPermissions.Admin.AllTenants))
            {
                throw new AbpAuthorizationException(
                    "Host-wide loan access requires permission to view all Areas.");
            }

            using (DisableAllTenantDataFiltersForHost())
            {
                var query =
                    from agreement in _loanRepository.GetAllIncluding(
                        item => item.WeeklyRequirements,
                        item => item.Repayments)
                    join customer in _customerRepository.GetAll()
                        on agreement.CustomerId equals customer.Id
                    select new OnyxLoanAgreementQueryRow
                    {
                        Agreement = agreement,
                        Customer = customer
                    };

                if (AbpSession.TenantId.HasValue)
                {
                    var tenantId = AbpSession.TenantId.Value;
                    query = query.Where(row =>
                        row.Agreement.TenantId == tenantId);
                }
                else if (input.TenantId.HasValue)
                {
                    var tenantId = input.TenantId.Value;
                    query = query.Where(row =>
                        row.Agreement.TenantId == tenantId);
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
                    .OrderByDescending(row => row.Agreement.OfferedAt)
                    .Skip(input.SkipCount)
                    .Take(input.MaxResultCount)
                    .ToListAsync();

                return new PagedResultDto<OnyxLoanAgreementDto>(
                    total,
                    rows.Select(row => OnyxLoanAgreementDtoMapper.Map(
                            row.Agreement,
                            row.Customer.Name,
                            row.Customer.Email.Value))
                        .ToList());
            }
        }

        private sealed class OnyxLoanAgreementQueryRow
        {
            public OnyxLoanAgreement Agreement { get; set; }
            public Customer Customer { get; set; }
        }
    }
}
