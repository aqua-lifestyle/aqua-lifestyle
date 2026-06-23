using System;
using Abp.Domain.Entities.Auditing;
using Abp.Domain.Entities;
using Abp.Timing;

namespace AqualLifeStyle.Domain.AreaLeaders
{
    /// <summary>
    /// Approval rules for an area-space application. Centralized so guards are not magic numbers
    /// and can be mirrored in seed/tests (source: <c>docs/BusinessDocs/workflows.md</c> §6).
    /// </summary>
    public static class AreaSpaceApprovalRules
    {
        public const int MinInterestedMembers = 20;
        public const int RequiredPresentations = 4;
        public const int RequiredStartupOrders = 20;
        public const int ReviewWindowHours = 42;
        public const int MaxAreaLeaders = 300;
    }

    /// <summary>
    /// An area-space application and its lifecycle: Applied → UnderReview → Approved / Suspended.
    /// Approval is Fail-Fast: every guard must hold or the call throws.
    /// </summary>
    public class AreaSpace : FullAuditedAggregateRoot<int>, IMustHaveTenant
    {
        public int TenantId { get; set; }

        public int AreaLeaderId { get; private set; }
        public string AddressLine { get; private set; }
        public string Capacity { get; private set; }
        public int InterestedMembers { get; private set; }
        public AreaSpaceStatus Status { get; private set; }
        public DateTime? ReviewStartedAt { get; private set; }
        public int PresentationsCompleted { get; private set; }
        public int StartupOrdersCompleted { get; private set; }
        public DateTime? ApprovedAt { get; private set; }

        protected AreaSpace()
        {
        }

        private AreaSpace(int tenantId, int areaLeaderId, Address address, string capacity, int interestedMembers)
        {
            if (tenantId <= 0) throw new ArgumentException("TenantId must be valid.", nameof(tenantId));
            if (areaLeaderId <= 0) throw new ArgumentException("AreaLeaderId must be valid.", nameof(areaLeaderId));
            if (address == null) throw new ArgumentNullException(nameof(address));
            if (string.IsNullOrWhiteSpace(capacity)) throw new ArgumentException("Capacity is required.", nameof(capacity));
            if (interestedMembers < 0) throw new ArgumentException("Interested members cannot be negative.", nameof(interestedMembers));

            TenantId = tenantId;
            AreaLeaderId = areaLeaderId;
            AddressLine = address.ToString();
            Capacity = capacity.Trim();
            InterestedMembers = interestedMembers;
            Status = AreaSpaceStatus.Applied;
        }

        public static AreaSpace Apply(int tenantId, int areaLeaderId, Address address, string capacity, int interestedMembers)
            => new AreaSpace(tenantId, areaLeaderId, address, capacity, interestedMembers);

        public void StartReview()
        {
            if (Status != AreaSpaceStatus.Applied)
            {
                throw new InvalidOperationException("Review can only start from the Applied state.");
            }

            Status = AreaSpaceStatus.UnderReview;
            ReviewStartedAt = Clock.Now;
        }

        public void RecordPresentation()
        {
            if (Status != AreaSpaceStatus.UnderReview)
            {
                throw new InvalidOperationException("Presentations can only be recorded while under review.");
            }

            PresentationsCompleted++;
        }

        public void RecordStartupOrder()
        {
            if (Status != AreaSpaceStatus.UnderReview)
            {
                throw new InvalidOperationException("Startup orders can only be recorded while under review.");
            }

            StartupOrdersCompleted++;
        }

        /// <summary>
        /// Approve the area space. Fail-Fast: 20+ interested members, 4 presentations,
        /// 20 startup orders, and the 42-hour review window must all be satisfied.
        /// Follow-up events should be published after persistence by the application layer.
        /// </summary>
        public void Approve(DateTime? atUtc = null)
        {
            if (Status == AreaSpaceStatus.Approved)
            {
                return;
            }

            if (Status != AreaSpaceStatus.UnderReview)
            {
                throw new InvalidOperationException("Only a space under review can be approved.");
            }

            if (InterestedMembers < AreaSpaceApprovalRules.MinInterestedMembers)
            {
                throw new InvalidOperationException(
                    $"Need at least {AreaSpaceApprovalRules.MinInterestedMembers} interested members (have {InterestedMembers}).");
            }

            if (PresentationsCompleted < AreaSpaceApprovalRules.RequiredPresentations)
            {
                throw new InvalidOperationException(
                    $"Need at least {AreaSpaceApprovalRules.RequiredPresentations} presentations (have {PresentationsCompleted}).");
            }

            if (StartupOrdersCompleted < AreaSpaceApprovalRules.RequiredStartupOrders)
            {
                throw new InvalidOperationException(
                    $"Need at least {AreaSpaceApprovalRules.RequiredStartupOrders} startup orders (have {StartupOrdersCompleted}).");
            }

            if (!ReviewStartedAt.HasValue)
            {
                throw new InvalidOperationException("Review must be started before approval.");
            }

            var now = atUtc ?? Clock.Now;
            var elapsed = now - ReviewStartedAt.Value;
            if (elapsed < TimeSpan.FromHours(AreaSpaceApprovalRules.ReviewWindowHours))
            {
                throw new InvalidOperationException(
                    $"Area space must be under review for at least {AreaSpaceApprovalRules.ReviewWindowHours} hours.");
            }

            Status = AreaSpaceStatus.Approved;
            ApprovedAt = now;
        }

        public void Suspend()
        {
            if (Status != AreaSpaceStatus.Approved)
            {
                throw new InvalidOperationException("Only an approved space can be suspended.");
            }

            Status = AreaSpaceStatus.Suspended;
        }
    }
}
