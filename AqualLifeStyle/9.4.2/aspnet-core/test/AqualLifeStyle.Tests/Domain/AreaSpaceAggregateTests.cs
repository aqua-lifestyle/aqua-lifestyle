using System;
using System.Linq;
using Abp.Timing;
using AqualLifeStyle.Domain.AreaLeaders;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Domain
{
    public class AreaSpaceAggregateTests
    {
        private static readonly Address ValidAddress = new("1 Main St", "Cape Town", "WC", "8001");

        private static AreaSpace NewSpace(int interestedMembers = AreaSpaceApprovalRules.MinInterestedMembers) =>
            AreaSpace.Apply(tenantId: 1, areaLeaderId: 5, ValidAddress, " 20 by 40 ", interestedMembers);

        [Fact]
        public void Apply_SetsExpectedDefaultsAndNormalizesFields()
        {
            var space = NewSpace();

            space.TenantId.ShouldBe(1);
            space.AreaLeaderId.ShouldBe(5);
            space.AddressLine.ShouldBe("1 Main St, Cape Town, WC, 8001");
            space.Capacity.ShouldBe("20 by 40");
            space.InterestedMembers.ShouldBe(AreaSpaceApprovalRules.MinInterestedMembers);
            space.Status.ShouldBe(AreaSpaceStatus.Applied);
            space.ReviewStartedAt.ShouldBeNull();
            space.PresentationsCompleted.ShouldBe(0);
            space.StartupOrdersCompleted.ShouldBe(0);
            space.ApprovedAt.ShouldBeNull();
        }

        [Fact]
        public void Apply_WithInvalidArguments_Throws()
        {
            Should.Throw<ArgumentException>(() => AreaSpace.Apply(0, 5, ValidAddress, "20 by 40", 20));
            Should.Throw<ArgumentException>(() => AreaSpace.Apply(1, 0, ValidAddress, "20 by 40", 20));
            Should.Throw<ArgumentNullException>(() => AreaSpace.Apply(1, 5, null, "20 by 40", 20));
            Should.Throw<ArgumentException>(() => AreaSpace.Apply(1, 5, ValidAddress, " ", 20));
            Should.Throw<ArgumentException>(() => AreaSpace.Apply(1, 5, ValidAddress, "20 by 40", -1));
        }

        [Fact]
        public void StartReview_FromApplied_SetsUnderReviewAndTimestamp()
        {
            var before = Clock.Now;
            var space = NewSpace();

            space.StartReview();

            space.Status.ShouldBe(AreaSpaceStatus.UnderReview);
            space.ReviewStartedAt.ShouldNotBeNull();
            space.ReviewStartedAt.Value.ShouldBeGreaterThanOrEqualTo(before);
        }

        [Fact]
        public void StartReview_FromNonAppliedState_Throws()
        {
            var space = NewSpace();
            space.StartReview();

            Should.Throw<InvalidOperationException>(() => space.StartReview());
        }

        [Fact]
        public void RecordPresentation_OnlyAllowedUnderReview()
        {
            var space = NewSpace();

            Should.Throw<InvalidOperationException>(() => space.RecordPresentation());

            space.StartReview();
            space.RecordPresentation();

            space.PresentationsCompleted.ShouldBe(1);
        }

        [Fact]
        public void RecordStartupOrder_OnlyAllowedUnderReview()
        {
            var space = NewSpace();

            Should.Throw<InvalidOperationException>(() => space.RecordStartupOrder());

            space.StartReview();
            space.RecordStartupOrder();

            space.StartupOrdersCompleted.ShouldBe(1);
        }

        [Fact]
        public void Approve_RequiresUnderReviewState()
        {
            var space = NewSpace();

            Should.Throw<InvalidOperationException>(() => space.Approve());
        }

        [Fact]
        public void Approve_ThrowsWhenInterestedMembersBelowMinimum()
        {
            var space = NewSpace(AreaSpaceApprovalRules.MinInterestedMembers - 1);
            space.StartReview();
            RecordPresentations(space, AreaSpaceApprovalRules.RequiredPresentations);
            RecordStartupOrders(space, AreaSpaceApprovalRules.RequiredStartupOrders);

            var afterWindow = Clock.Now.AddHours(AreaSpaceApprovalRules.ReviewWindowHours + 1);

            Should.Throw<InvalidOperationException>(() => space.Approve(afterWindow))
                .Message.ShouldContain("Need at least 20 interested members");
        }

        [Fact]
        public void Approve_ThrowsWhenPresentationsBelowMinimum()
        {
            var space = NewSpace();
            space.StartReview();
            RecordPresentations(space, AreaSpaceApprovalRules.RequiredPresentations - 1);
            RecordStartupOrders(space, AreaSpaceApprovalRules.RequiredStartupOrders);

            var afterWindow = Clock.Now.AddHours(AreaSpaceApprovalRules.ReviewWindowHours + 1);

            Should.Throw<InvalidOperationException>(() => space.Approve(afterWindow))
                .Message.ShouldContain("Need at least 4 presentations");
        }

        [Fact]
        public void Approve_ThrowsWhenStartupOrdersBelowMinimum()
        {
            var space = NewSpace();
            space.StartReview();
            RecordPresentations(space, AreaSpaceApprovalRules.RequiredPresentations);
            RecordStartupOrders(space, AreaSpaceApprovalRules.RequiredStartupOrders - 1);

            var afterWindow = Clock.Now.AddHours(AreaSpaceApprovalRules.ReviewWindowHours + 1);

            Should.Throw<InvalidOperationException>(() => space.Approve(afterWindow))
                .Message.ShouldContain("Need at least 20 startup orders");
        }

        [Fact]
        public void Approve_ThrowsBeforeReviewWindowElapsed()
        {
            var space = NewPersistedReviewReadySpace();

            var beforeWindow = space.ReviewStartedAt.Value.AddHours(AreaSpaceApprovalRules.ReviewWindowHours).AddMinutes(-1);

            Should.Throw<InvalidOperationException>(() => space.Approve(beforeWindow))
                .Message.ShouldContain("at least 42 hours");
        }

        [Fact]
        public void Approve_ThrowsWhenAggregateIsNotPersisted()
        {
            var space = NewSpace();
            space.StartReview();
            RecordPresentations(space, AreaSpaceApprovalRules.RequiredPresentations);
            RecordStartupOrders(space, AreaSpaceApprovalRules.RequiredStartupOrders);

            var afterWindow = Clock.Now.AddHours(AreaSpaceApprovalRules.ReviewWindowHours + 1);

            Should.Throw<InvalidOperationException>(() => space.Approve(afterWindow))
                .Message.ShouldBe("Area space must be persisted before approval can raise events.");
        }

        [Fact]
        public void Approve_WithAllRequirementsMet_SetsApprovedStateAndEvent()
        {
            var space = NewPersistedReviewReadySpace();
            var approvedAt = space.ReviewStartedAt.Value.AddHours(AreaSpaceApprovalRules.ReviewWindowHours + 1);

            space.Approve(approvedAt);

            space.Status.ShouldBe(AreaSpaceStatus.Approved);
            space.ApprovedAt.ShouldBe(approvedAt);
            space.DomainEvents.Count.ShouldBe(1);

            var evt = space.DomainEvents.Single().ShouldBeOfType<AreaSpaceApprovedEvent>();
            evt.TenantId.ShouldBe(1);
            evt.AreaSpaceId.ShouldBe(space.Id);
            evt.AreaLeaderId.ShouldBe(5);
        }

        [Fact]
        public void Approve_WhenAlreadyApproved_IsIdempotent()
        {
            var space = NewPersistedReviewReadySpace();
            var firstApprovedAt = space.ReviewStartedAt.Value.AddHours(AreaSpaceApprovalRules.ReviewWindowHours + 1);
            var secondApprovedAt = firstApprovedAt.AddHours(10);

            space.Approve(firstApprovedAt);
            space.Approve(secondApprovedAt);

            space.ApprovedAt.ShouldBe(firstApprovedAt);
            space.DomainEvents.Count.ShouldBe(1);
        }

        [Fact]
        public void ApprovedOrSuspendedSpace_RejectsFurtherStartupOrders()
        {
            var approvedSpace = NewPersistedApprovedSpace();
            Should.Throw<InvalidOperationException>(() => approvedSpace.RecordStartupOrder());

            approvedSpace.Suspend();
            Should.Throw<InvalidOperationException>(() => approvedSpace.RecordStartupOrder());
        }

        [Fact]
        public void Suspend_OnlyAllowedFromApprovedState()
        {
            var appliedSpace = NewSpace();
            Should.Throw<InvalidOperationException>(() => appliedSpace.Suspend());

            var approvedSpace = NewPersistedApprovedSpace();
            approvedSpace.Suspend();

            approvedSpace.Status.ShouldBe(AreaSpaceStatus.Suspended);
        }

        private static AreaSpace NewPersistedReviewReadySpace()
        {
            var space = NewSpace();
            space.Id = 123;
            space.StartReview();
            RecordPresentations(space, AreaSpaceApprovalRules.RequiredPresentations);
            RecordStartupOrders(space, AreaSpaceApprovalRules.RequiredStartupOrders);
            return space;
        }

        private static AreaSpace NewPersistedApprovedSpace()
        {
            var space = NewPersistedReviewReadySpace();
            space.Approve(space.ReviewStartedAt.Value.AddHours(AreaSpaceApprovalRules.ReviewWindowHours + 1));
            return space;
        }

        private static void RecordPresentations(AreaSpace space, int count)
        {
            Enumerable.Range(0, count).ToList().ForEach(_ => space.RecordPresentation());
        }

        private static void RecordStartupOrders(AreaSpace space, int count)
        {
            Enumerable.Range(0, count).ToList().ForEach(_ => space.RecordStartupOrder());
        }
    }
}
