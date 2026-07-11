using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Events.Bus.Entities;
using Abp.Events.Bus.Handlers;
using AqualLifeStyle.Domain.Memberships;

namespace AqualLifeStyle.Application.Memberships
{
    public class ActiveMembershipCacheInvalidationHandler :
        IAsyncEventHandler<EntityCreatedEventData<Membership>>,
        IAsyncEventHandler<EntityUpdatedEventData<Membership>>,
        IAsyncEventHandler<EntityDeletedEventData<Membership>>,
        ITransientDependency
    {
        private readonly IActiveMembershipCache _activeMembershipCache;

        public ActiveMembershipCacheInvalidationHandler(IActiveMembershipCache activeMembershipCache)
        {
            _activeMembershipCache = activeMembershipCache;
        }

        public Task HandleEventAsync(EntityCreatedEventData<Membership> eventData)
        {
            Invalidate(eventData?.Entity);
            return Task.CompletedTask;
        }

        public Task HandleEventAsync(EntityUpdatedEventData<Membership> eventData)
        {
            Invalidate(eventData?.Entity);
            return Task.CompletedTask;
        }

        public Task HandleEventAsync(EntityDeletedEventData<Membership> eventData)
        {
            Invalidate(eventData?.Entity);
            return Task.CompletedTask;
        }

        private void Invalidate(Membership membership)
        {
            if (membership == null)
            {
                return;
            }

            _activeMembershipCache.Remove(membership.TenantId);
        }
    }
}
