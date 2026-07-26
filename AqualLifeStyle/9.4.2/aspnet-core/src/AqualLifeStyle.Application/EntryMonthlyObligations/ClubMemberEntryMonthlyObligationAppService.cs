using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Auditing;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Runtime.Session;
using Abp.UI;
using AqualLifeStyle.Application.EntryMonthlyObligations.Dto;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.EntryMonthlyObligations
{
    [Audited]
    public class ClubMemberEntryMonthlyObligationAppService
        : AqualLifeStyleAppServiceBase,
            IClubMemberEntryMonthlyObligationAppService
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly IRepository<EntryMonthlyObligation, Guid>
            _obligationRepository;

        public ClubMemberEntryMonthlyObligationAppService(
            ICustomerRepository customerRepository,
            IRepository<EntryMonthlyObligation, Guid> obligationRepository)
        {
            _customerRepository = customerRepository;
            _obligationRepository = obligationRepository;
        }

        [AbpAuthorize(AquaPermissions.EntryMonthlyObligations.ViewSelf)]
        public async Task<IReadOnlyList<EntryMonthlyObligationDto>>
            GetMyObligationsAsync()
        {
            var tenantId = GetRequiredTenantId(
                "Your AQGreen monthly commitments are unavailable.");
            var customer = await _customerRepository.FirstOrDefaultAsync(
                item =>
                    item.TenantId == tenantId &&
                    item.UserId == AbpSession.GetUserId());
            if (customer == null || !customer.IsActive)
            {
                throw new UserFriendlyException(
                    "Your AQGreen monthly commitments are unavailable.",
                    "An active Club Member account is required.");
            }

            var obligations = await _obligationRepository.GetAll()
                .Where(item => item.CustomerId == customer.Id)
                .OrderByDescending(item => item.PeriodYear)
                .ThenByDescending(item => item.PeriodMonth)
                .ToListAsync();

            return obligations.Select(item =>
                    EntryMonthlyObligationDtoMapper.Map(
                        item,
                        customer.Name,
                        customer.Email.Value))
                .ToList();
        }
    }
}
