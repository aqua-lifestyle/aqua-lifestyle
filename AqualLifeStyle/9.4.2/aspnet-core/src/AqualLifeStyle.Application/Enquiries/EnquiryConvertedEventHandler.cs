using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.Events.Bus;
using Abp.Events.Bus.Handlers;
using AqualLifeStyle.Domain.AreaNetwork;
using AqualLifeStyle.Domain.AreaLeaders;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enquiries;
using AqualLifeStyle.Domain.Facilitators;
using AqualLifeStyle.Domain.Memberships;

namespace AqualLifeStyle.Application.Enquiries
{
    /// <summary>
    /// Reacts to an enquiry conversion: links the converted customer to an active membership tier
    /// (idempotent) and, when the lead was sourced by a facilitator, attributes the direct and
    /// indirect referrals to the network via <see cref="ReferralAttributionService"/>.
    /// Runs synchronously inside the converting enquiry's unit of work.
    /// </summary>
    public class EnquiryConvertedEventHandler : IAsyncEventHandler<EnquiryConvertedEvent>, ITransientDependency
    {
        private readonly IFacilitatorRepository _facilitatorRepository;
        private readonly IAreaLeaderRepository _areaLeaderRepository;
        private readonly IReferralRepository _referralRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly IMembershipRepository _membershipRepository;
        private readonly IUnitOfWorkManager _unitOfWorkManager;

        public EnquiryConvertedEventHandler(
            IFacilitatorRepository facilitatorRepository,
            IAreaLeaderRepository areaLeaderRepository,
            IReferralRepository referralRepository,
            ICustomerRepository customerRepository,
            IMembershipRepository membershipRepository,
            IUnitOfWorkManager unitOfWorkManager)
        {
            _facilitatorRepository = facilitatorRepository;
            _areaLeaderRepository = areaLeaderRepository;
            _referralRepository = referralRepository;
            _customerRepository = customerRepository;
            _membershipRepository = membershipRepository;
            _unitOfWorkManager = unitOfWorkManager;
        }

        public async Task HandleEventAsync(EnquiryConvertedEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            // The conversion may be handled outside the tenant's ambient session (e.g. a host-level
            // trigger), so scope the whole attribution to the tenant carried by the event. This makes
            // ABP's MayHaveTenant filter and the explicit TenantId predicates in the repositories agree.
            using (var uow = _unitOfWorkManager.Begin())
            {
                using (_unitOfWorkManager.Current.SetTenantId(evt.TenantId))
                {
                    await AttributeReferralsAsync(evt);
                    await LinkCustomerAsync(evt);
                }

                await uow.CompleteAsync();
            }
        }

        private async Task AttributeReferralsAsync(EnquiryConvertedEvent evt)
        {
            if (!evt.ReferredByFacilitatorId.HasValue)
            {
                return;
            }

            var facilitator = await _facilitatorRepository.FirstOrDefaultAsync(f => f.Id == evt.ReferredByFacilitatorId.Value);
            var areaLeader = facilitator == null
                ? null
                : await _areaLeaderRepository.FirstOrDefaultAsync(a => a.Id == facilitator.AreaLeaderId);

            if (facilitator != null && areaLeader != null)
            {
                var attribution = new ReferralAttributionService(new CommissionCalculator())
                    .Attribute(evt, facilitator, areaLeader);

                await _referralRepository.InsertAsync(attribution.DirectReferral);
                await _referralRepository.InsertAsync(attribution.IndirectReferral);
                await _facilitatorRepository.UpdateAsync(facilitator);
                await _areaLeaderRepository.UpdateAsync(areaLeader);
            }
        }

        private async Task LinkCustomerAsync(EnquiryConvertedEvent evt)
        {
            var customer = await _customerRepository.FirstOrDefaultAsync(c => c.Id == evt.CustomerId && c.TenantId == evt.TenantId);
            if (customer == null || customer.MembershipId.HasValue)
            {
                return;
            }

            var membership = await _membershipRepository.GetFirstActiveAsync(evt.TenantId);
            if (membership == null)
            {
                return;
            }

            await _customerRepository.AssignMembershipIfUnassignedAsync(customer.Id, evt.TenantId, membership.Id);
        }
    }
}
