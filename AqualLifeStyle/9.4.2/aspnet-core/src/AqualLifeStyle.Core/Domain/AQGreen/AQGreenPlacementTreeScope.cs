using System;
using Abp.Domain.Entities;

namespace AqualLifeStyle.Domain.AQGreen
{
    /// <summary>
    /// Stable identity of one root-specific AQGreen placement tree within a Tenant.
    /// The root participant is the unique root-shaped placement in this scope.
    /// </summary>
    public sealed class AQGreenPlacementTreeScope
        : AggregateRoot<Guid>, IMustHaveTenant
    {
        public int TenantId { get; private set; }

        int IMustHaveTenant.TenantId
        {
            get => TenantId;
            set => TenantId = value;
        }

        private AQGreenPlacementTreeScope()
        {
        }

        private AQGreenPlacementTreeScope(int tenantId)
        {
            if (tenantId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tenantId));
            }

            Id = Guid.NewGuid();
            TenantId = tenantId;
        }

        public static AQGreenPlacementTreeScope Create(int tenantId) =>
            new(tenantId);
    }
}
