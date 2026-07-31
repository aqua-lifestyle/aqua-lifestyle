using System;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Repositories;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Onyx;

namespace AqualLifeStyle.Application.Admin.Customers
{
    public interface ICustomerFallbackRoleResolver
    {
        Task<AquaUserRole> ResolveAsync(Customer customer);
    }

    public class CustomerFallbackRoleResolver : ICustomerFallbackRoleResolver, ITransientDependency
    {
        private readonly IRepository<EntryParticipation, Guid> _aqGreenParticipationRepository;
        private readonly IRepository<OnyxParticipation, Guid> _onyxParticipationRepository;

        public CustomerFallbackRoleResolver(
            IRepository<EntryParticipation, Guid> aqGreenParticipationRepository,
            IRepository<OnyxParticipation, Guid> onyxParticipationRepository)
        {
            _aqGreenParticipationRepository = aqGreenParticipationRepository;
            _onyxParticipationRepository = onyxParticipationRepository;
        }

        public async Task<AquaUserRole> ResolveAsync(Customer customer)
        {
            if (customer == null) throw new ArgumentNullException(nameof(customer));
            if (customer.MembershipId.HasValue) return AquaUserRole.Member;

            var hasActiveAQGreenParticipation = await _aqGreenParticipationRepository.FirstOrDefaultAsync(
                participation => participation.CustomerId == customer.Id &&
                                 participation.Status == EntryParticipationStatus.Active) != null;
            if (hasActiveAQGreenParticipation) return AquaUserRole.Member;

            var hasActiveOnyxParticipation = await _onyxParticipationRepository.FirstOrDefaultAsync(
                participation => participation.CustomerId == customer.Id &&
                                 participation.Status == OnyxParticipationStatus.Active) != null;
            return hasActiveOnyxParticipation ? AquaUserRole.Member : AquaUserRole.Guest;
        }
    }
}
