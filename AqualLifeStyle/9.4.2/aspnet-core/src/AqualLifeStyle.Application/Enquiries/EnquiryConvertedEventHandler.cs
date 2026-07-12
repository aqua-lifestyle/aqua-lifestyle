using System;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.Events.Bus;
using Abp.Events.Bus.Handlers;
using Castle.Core.Logging;
using AqualLifeStyle.Application.Exceptions;
using AqualLifeStyle.Application.Memberships;
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
    /// Runs in its own tenant-scoped unit of work after the conversion commits.
    /// </summary>
    public class EnquiryConvertedEventHandler : IAsyncEventHandler<EnquiryConvertedEvent>, ITransientDependency
    {
        private readonly IFacilitatorRepository _facilitatorRepository;
        private readonly IAreaLeaderRepository _areaLeaderRepository;
        private readonly IReferralRepository _referralRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly IActiveMembershipCache _activeMembershipCache;
        private readonly IUnitOfWorkManager _unitOfWorkManager;

        public ILogger Logger { get; set; }

        public EnquiryConvertedEventHandler(
            IFacilitatorRepository facilitatorRepository,
            IAreaLeaderRepository areaLeaderRepository,
            IReferralRepository referralRepository,
            ICustomerRepository customerRepository,
            IActiveMembershipCache activeMembershipCache,
            IUnitOfWorkManager unitOfWorkManager)
        {
            _facilitatorRepository = facilitatorRepository;
            _areaLeaderRepository = areaLeaderRepository;
            _referralRepository = referralRepository;
            _customerRepository = customerRepository;
            _activeMembershipCache = activeMembershipCache;
            _unitOfWorkManager = unitOfWorkManager;
            Logger = NullLogger.Instance;
        }

        public async Task HandleEventAsync(EnquiryConvertedEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            var tenantId = GetValidatedTenantId(evt);
            Logger.Info(
                $"Received enquiry conversion event: enquiryId={evt.EnquiryId}, customerId={evt.CustomerId}, tenantId={tenantId}, referredByFacilitatorId={evt.ReferredByFacilitatorId}.");

            try
            {
                using (var uow = _unitOfWorkManager.Begin())
                {
                    // The conversion event runs after the originating UoW commits, so it needs its own
                    // tenant-scoped UoW for any follow-up reads and writes.
                    using (_unitOfWorkManager.Current.SetTenantId(tenantId))
                    {
                        await AttributeReferralsAsync(evt);
                        await LinkCustomerAsync(evt);
                    }

                    await uow.CompleteAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(
                    $"Failed to process enquiry conversion event: enquiryId={evt.EnquiryId}, customerId={evt.CustomerId}, tenantId={tenantId}, referredByFacilitatorId={evt.ReferredByFacilitatorId}.",
                    ex);
                throw;
            }
        }

        private int GetValidatedTenantId(EnquiryConvertedEvent evt)
        {
            if (!evt.TenantId.HasValue || evt.TenantId.Value <= 0)
            {
                throw new AqualLifeStyleAuthorizationException("Enquiry conversion handling requires a valid tenant context.");
            }

            return evt.TenantId.Value;
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
            var existingReferral = await _referralRepository.GetBySourceEnquiryAsync(evt.EnquiryId, evt.TenantId);
            if (existingReferral != null)
            {
                Logger.Info(
                    $"Skipping referral attribution for enquiry {evt.EnquiryId} in tenant {evt.TenantId}: attribution already exists.");
                return;
            }

            var facilitator = await _facilitatorRepository.GetWithAreaLeaderAsync(evt.ReferredByFacilitatorId.Value);
            var areaLeader = facilitator?.AreaLeader;

            if (facilitator != null && areaLeader == null)
            {
                Logger.Warn(
                    $"Skipping referral attribution for enquiry {evt.EnquiryId} in tenant {evt.TenantId}: facilitator {facilitator.Id} references missing area leader {facilitator.AreaLeaderId}.");
                return;
            }

            if (facilitator != null && areaLeader != null)
            {
                var attribution = new ReferralAttributionService(new CommissionCalculator())
                    .Attribute(evt, facilitator, areaLeader);

                await _referralRepository.InsertAsync(attribution.DirectReferral);
                await _referralRepository.InsertAsync(attribution.IndirectReferral);
                await _facilitatorRepository.UpdateAsync(facilitator);
                await _areaLeaderRepository.UpdateAsync(areaLeader);
                Logger.Info(
                    $"Applied referral attribution for enquiry {evt.EnquiryId} in tenant {evt.TenantId}: facilitatorId={facilitator.Id}, areaLeaderId={areaLeader.Id}, directReferralCustomerId={attribution.DirectReferral.ReferredCustomerId}, indirectReferralCustomerId={attribution.IndirectReferral.ReferredCustomerId}.");
            }
        }

        private async Task LinkCustomerAsync(EnquiryConvertedEvent evt)
        {
            var customer = await _customerRepository.FirstOrDefaultAsync(c => c.Id == evt.CustomerId && c.TenantId == evt.TenantId);
            if (customer == null || customer.MembershipId.HasValue)
            {
                return;
            }

            var membershipId = await _activeMembershipCache.GetFirstActiveMembershipIdAsync(evt.TenantId);
            if (!membershipId.HasValue)
            {
                return;
            }

            await _customerRepository.AssignMembershipIfUnassignedAsync(customer.Id, evt.TenantId, membershipId.Value);
            Logger.Info(
                $"Linked converted enquiry customer to active membership: enquiryId={evt.EnquiryId}, customerId={customer.Id}, tenantId={evt.TenantId}, membershipId={membershipId.Value}.");
        }
    }
}
