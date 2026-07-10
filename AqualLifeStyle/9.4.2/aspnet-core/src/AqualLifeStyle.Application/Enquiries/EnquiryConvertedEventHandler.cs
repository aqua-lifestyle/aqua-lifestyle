using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Repositories;
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

        public EnquiryConvertedEventHandler(
            IFacilitatorRepository facilitatorRepository,
            IAreaLeaderRepository areaLeaderRepository,
            IReferralRepository referralRepository,
            ICustomerRepository customerRepository,
            IMembershipRepository membershipRepository)
        {
            _facilitatorRepository = facilitatorRepository;
            _areaLeaderRepository = areaLeaderRepository;
            _referralRepository = referralRepository;
            _customerRepository = customerRepository;
            _membershipRepository = membershipRepository;
        }

        public async Task HandleEventAsync(EnquiryConvertedEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            if (evt.ReferredByFacilitatorId.HasValue)
            {
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

            await LinkCustomerAsync(evt);
        }

        private async Task LinkCustomerAsync(EnquiryConvertedEvent evt)
        {
            var customer = await _customerRepository.FirstOrDefaultAsync(c => c.Id == evt.CustomerId);
            if (customer == null || customer.MembershipId.HasValue)
            {
                return;
            }

            var membership = await _membershipRepository.FirstOrDefaultAsync(m => m.IsActive);
            if (membership == null)
            {
                return;
            }

            customer.ChangeMembership(membership.Id);
            await _customerRepository.UpdateAsync(customer);
        }
    }
}
