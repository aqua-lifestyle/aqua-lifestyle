using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Auditing;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Runtime.Session;
using Abp.UI;
using AqualLifeStyle.Application.Loans.Dto;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.Loans
{
    [Audited]
    public class ClubMemberOnyxLoanAppService
        : AqualLifeStyleAppServiceBase, IClubMemberOnyxLoanAppService
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly IRepository<OnyxLoanAgreement, Guid> _loanRepository;

        public ClubMemberOnyxLoanAppService(
            ICustomerRepository customerRepository,
            IRepository<OnyxLoanAgreement, Guid> loanRepository)
        {
            _customerRepository = customerRepository;
            _loanRepository = loanRepository;
        }

        [AbpAuthorize(AquaPermissions.Loans.ViewSelf)]
        public async Task<MyOnyxLoanAgreementsDto> GetMyAgreementsAsync()
        {
            var tenantId = GetRequiredTenantId(
                "Your loan agreements are unavailable.");
            var customer = await _customerRepository.FirstOrDefaultAsync(
                item =>
                    item.TenantId == tenantId &&
                    item.UserId == AbpSession.GetUserId());
            if (customer == null || !customer.IsActive)
            {
                throw new UserFriendlyException(
                    "Your loan agreements are unavailable.",
                    "An active Club Member account is required to view loans.");
            }

            var agreements = await _loanRepository
                .GetAllIncluding(
                    item => item.WeeklyRequirements,
                    item => item.Repayments)
                .Where(item => item.CustomerId == customer.Id)
                .OrderByDescending(item => item.OfferedAt)
                .ToListAsync();

            return new MyOnyxLoanAgreementsDto
            {
                Items = agreements
                    .Select(item => OnyxLoanAgreementDtoMapper.Map(
                        item,
                        customer.Name,
                        customer.Email.Value))
                    .ToList()
            };
        }
    }
}
