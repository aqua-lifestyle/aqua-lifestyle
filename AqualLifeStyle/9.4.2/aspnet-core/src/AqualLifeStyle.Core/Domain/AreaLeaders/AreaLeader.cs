using System;
using Abp.Domain.Entities.Auditing;
using Abp.Domain.Entities;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Facilitators;

namespace AqualLifeStyle.Domain.AreaLeaders
{
    /// <summary>
    /// An area leader operates an area space and leads facilitators. Rank advances with cumulative
    /// order target; indirect referrals (from their facilitators' leads) accrue commission.
    /// </summary>
    public class AreaLeader : FullAuditedAggregateRoot<int>, IMustHaveTenant
    {
        public int TenantId { get; set; }

        public int CustomerId { get; private set; }
        public LicenseType LicenseType { get; private set; }
        public decimal LicenseFee { get; private set; }
        public AreaLeaderRank Rank { get; private set; }
        public int? AreaSpaceId { get; private set; }
        public decimal MonthlySubscription { get; private set; }
        public int DirectReferrals { get; private set; }
        public int IndirectReferrals { get; private set; }
        public int OrderTarget { get; private set; }

        protected AreaLeader()
        {
        }

        private AreaLeader(int tenantId, int customerId, LicenseType licenseType)
        {
            if (tenantId <= 0) throw new ArgumentException("TenantId must be valid.", nameof(tenantId));
            if (customerId <= 0) throw new ArgumentException("CustomerId must be valid.", nameof(customerId));

            TenantId = tenantId;
            CustomerId = customerId;
            LicenseType = licenseType;
            LicenseFee = LicenseFeeFor(licenseType);
            Rank = AreaLeaderRank.Ruby;
            DirectReferrals = 0;
            IndirectReferrals = 0;
            OrderTarget = 0;
            MonthlySubscription = 0m;
        }

        public static AreaLeader Apply(int tenantId, int customerId, LicenseType licenseType)
            => new AreaLeader(tenantId, customerId, licenseType);

        public void LinkAreaSpace(int areaSpaceId)
        {
            if (areaSpaceId <= 0) throw new ArgumentException("AreaSpaceId must be valid.", nameof(areaSpaceId));
            AreaSpaceId = areaSpaceId;
        }

        /// <summary>Register a facilitator under this leader (increments direct-referral count).</summary>
        public void RecordFacilitator()
        {
            if (IsDeleted) throw new InvalidOperationException("Cannot record a facilitator for a deleted area leader.");
            DirectReferrals++;
        }

        /// <summary>Increment the cumulative order target (drives rank progression).</summary>
        public void RecordStartupOrder()
        {
            if (IsDeleted) throw new InvalidOperationException("Cannot record an order for a deleted area leader.");
            OrderTarget++;
        }

        /// <summary>Increment the indirect-referral count (driven by facilitators' converted leads).</summary>
        public void RecordIndirectReferral()
        {
            if (IsDeleted) throw new InvalidOperationException("Cannot record a referral for a deleted area leader.");
            IndirectReferrals++;
        }

        /// <summary>Promote to the rank attained for the current order target.</summary>
        public void PromoteToCurrentRank(RankProgressionPolicy policy)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            Rank = policy.EvaluateAreaLeaderRank(OrderTarget);
        }

        private static decimal LicenseFeeFor(LicenseType licenseType) => licenseType switch
        {
            LicenseType.EntreLevel => 750m,
            LicenseType.AreaIndependentLeader => 2500m,
            _ => throw new ArgumentOutOfRangeException(nameof(licenseType))
        };
    }
}
