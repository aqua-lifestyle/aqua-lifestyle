using System;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;

namespace AqualLifeStyle.Domain.Areas
{
    public class AreaAdminAssignment : FullAuditedAggregateRoot<Guid>, IMustHaveTenant
    {
        public int TenantId { get; set; }
        public Guid AreaId { get; private set; }
        public long UserId { get; private set; }
        public DateTime EffectiveFrom { get; private set; }
        public DateTime? RevokedAt { get; private set; }
        public bool IsActive => !RevokedAt.HasValue;

        protected AreaAdminAssignment()
        {
        }

        public static AreaAdminAssignment Assign(
            Area area,
            long userId,
            int userTenantId,
            DateTime effectiveFrom)
        {
            if (area == null) throw new ArgumentNullException(nameof(area));
            if (!area.IsActive) throw new InvalidOperationException("Administrators cannot be assigned to an inactive Area.");
            if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));
            if (userTenantId <= 0 || userTenantId != area.TenantId)
                throw new InvalidOperationException("The administrator and Area must belong to the same Tenant.");
            if (effectiveFrom == default) throw new ArgumentException("An effective time is required.", nameof(effectiveFrom));

            return new AreaAdminAssignment
            {
                Id = Guid.NewGuid(),
                TenantId = area.TenantId,
                AreaId = area.Id,
                UserId = userId,
                EffectiveFrom = effectiveFrom
            };
        }

        public void Revoke(DateTime revokedAt)
        {
            if (RevokedAt.HasValue) return;
            if (revokedAt < EffectiveFrom)
                throw new ArgumentException("Revocation cannot precede the assignment.", nameof(revokedAt));
            RevokedAt = revokedAt;
        }
    }
}
