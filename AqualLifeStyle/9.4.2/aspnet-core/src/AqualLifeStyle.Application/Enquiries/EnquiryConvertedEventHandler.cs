using System;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.Events.Bus;
using Abp.Events.Bus.Handlers;
using Abp.Runtime.Session;
using AqualLifeStyle.Application.Exceptions;
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
        private readonly IAbpSession _abpSession;

        public EnquiryConvertedEventHandler(
            IFacilitatorRepository facilitatorRepository,
            IAreaLeaderRepository areaLeaderRepository,
            IReferralRepository referralRepository,
            ICustomerRepository customerRepository,
            IMembershipRepository membershipRepository,
            IUnitOfWorkManager unitOfWorkManager,
            IAbpSession abpSession)
        {
            _facilitatorRepository = facilitatorRepository;
            _areaLeaderRepository = areaLeaderRepository;
            _referralRepository = referralRepository;
            _customerRepository = customerRepository;
            _membershipRepository = membershipRepository;
            _unitOfWorkManager = unitOfWorkManager;
            _abpSession = abpSession;
        }

        public async Task HandleEventAsync(EnquiryConvertedEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            var currentUow = _unitOfWorkManager.Current
                ?? throw new InvalidOperationException(
                    $"{nameof(EnquiryConvertedEventHandler)} must run inside an existing unit of work.");
            var tenantId = GetValidatedTenantId(evt);

            // The conversion may be handled outside the tenant's ambient session (e.g. a host-level
            // trigger), so scope the whole attribution to the tenant carried by the event. This makes
            // ABP's MayHaveTenant filter and the explicit TenantId predicates in the repositories agree.
            using (currentUow.SetTenantId(tenantId))
            {
                await AttributeReferralsAsync(evt);
                await LinkCustomerAsync(evt);
            }
        }

        private int GetValidatedTenantId(EnquiryConvertedEvent evt)
        {
            if (!_abpSession.TenantId.HasValue)
            {
                throw new AqualLifeStyleAuthorizationException("Enquiry conversion handling requires a tenant context.");
            }

            if (_abpSession.TenantId.Value != evt.TenantId)
            {
                throw new AqualLifeStyleAuthorizationException("Enquiry conversion event tenant does not match the current tenant context.");
            }

            return _abpSession.TenantId.Value;
        }

        private async Task AttributeReferralsAsync(EnquiryConvertedEvent evt)
        {
            if (!evt.ReferredByFacilitatorId.HasValue)
            {
                return;
            }

            // Referral attribution creates one direct and one indirect row per conversion, so any
            // existing referral for the same source enquiry within the tenant means this event was
            // already processed and must not be applied again.
            var existingReferral = await _referralRepository.GetBySourceEnquiryAsync(evt.EnquiryId);
            if (existingReferral != null)
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
