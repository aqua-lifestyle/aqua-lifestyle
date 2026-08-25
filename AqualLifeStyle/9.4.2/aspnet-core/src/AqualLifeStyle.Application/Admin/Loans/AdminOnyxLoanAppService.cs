using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Application.Services.Dto;
using Abp.Auditing;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Runtime.Session;
using AqualLifeStyle.Application.Loans.Dto;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.Areas;
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
        private readonly IRepository<Area, Guid> _areaRepository;
        private readonly IRepository<AreaAdminAssignment, Guid> _areaAdminAssignmentRepository;

        public AdminOnyxLoanAppService(
            ICustomerRepository customerRepository,
            IRepository<OnyxLoanAgreement, Guid> loanRepository,
            IRepository<Area, Guid> areaRepository,
            IRepository<AreaAdminAssignment, Guid> areaAdminAssignmentRepository)
        {
            _customerRepository = customerRepository;
            _loanRepository = loanRepository;
            _areaRepository = areaRepository;
            _areaAdminAssignmentRepository = areaAdminAssignmentRepository;
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
                var areaScope = await GetAuthorizedAreaScopeAsync();
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

        private sealed class OnyxLoanAgreementQueryRow
        {
            public OnyxLoanAgreement Agreement { get; set; }
            public Customer Customer { get; set; }
        }
    }
}
