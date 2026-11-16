using System;
using Abp.Domain.Entities;

namespace AqualLifeStyle.Domain.AQGreen
{
    // A member's pending request, not root authorization. The existing Area
    // approval transaction creates the separate root admission after payment.
    public sealed class AQGreenIndependentRootRequest : AggregateRoot<Guid>, IMustHaveTenant
    {
        public int TenantId { get; private set; }
        public Guid ParticipantId { get; private set; }
        public long RequestedByUserId { get; private set; }
        public DateTime RequestedAt { get; private set; }
        int IMustHaveTenant.TenantId { get => TenantId; set => TenantId = value; }
        private AQGreenIndependentRootRequest() { }

        public static AQGreenIndependentRootRequest Record(int tenantId, Guid participantId,
            long requestedByUserId, DateTime requestedAt)
        {
            if (tenantId <= 0 || participantId == Guid.Empty || requestedByUserId <= 0 ||
                requestedAt == default || requestedAt.Kind != DateTimeKind.Utc)
                throw new ArgumentException("An exact member, participant and UTC request time are required.");
            return new AQGreenIndependentRootRequest { Id = Guid.NewGuid(), TenantId = tenantId,
                ParticipantId = participantId, RequestedByUserId = requestedByUserId, RequestedAt = requestedAt };
        }
    }
}
