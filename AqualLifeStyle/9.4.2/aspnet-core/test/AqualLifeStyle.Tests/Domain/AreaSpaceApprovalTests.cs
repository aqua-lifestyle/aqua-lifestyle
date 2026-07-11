using System;
using Abp.Timing;
using AqualLifeStyle.Domain.AreaLeaders;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Domain
{
    public class AreaSpaceApprovalTests
    {
        private static AreaSpace NewSpace(int interested = 0) =>
            AreaSpace.Apply(tenantId: 1, areaLeaderId: 5,
                new Address("1 Main St", "Cape Town", "WC", "8001"), "20 by 40", interested);

        [Fact]
        public void Apply_StartsInAppliedState()
        {
            NewSpace().Status.ShouldBe(AreaSpaceStatus.Applied);
        }

        [Fact]
        public void Approve_BeforeReview_Throws()
        {
            Should.Throw<InvalidOperationException>(() => NewSpace(AreaSpaceApprovalRules.MinInterestedMembers).Approve());
        }

        [Fact]
        public void Approve_WithoutEnoughPresentations_Throws()
        {
            var space = NewSpace(AreaSpaceApprovalRules.MinInterestedMembers);
            space.StartReview();
            Should.Throw<InvalidOperationException>(() => space.Approve());
        }

        [Fact]
        public void Approve_WithoutEnoughStartupOrders_Throws()
        {
            var space = NewSpace(AreaSpaceApprovalRules.MinInterestedMembers);
            space.StartReview();
            for (var i = 0; i < AreaSpaceApprovalRules.RequiredPresentations; i++) space.RecordPresentation();
            Should.Throw<InvalidOperationException>(() => space.Approve());
        }

        [Fact]
        public void Approve_Before42hWindow_Throws()
        {
            var space = NewSpace(AreaSpaceApprovalRules.MinInterestedMembers);
            space.StartReview();
            for (var i = 0; i < AreaSpaceApprovalRules.RequiredPresentations; i++) space.RecordPresentation();
            for (var i = 0; i < AreaSpaceApprovalRules.RequiredStartupOrders; i++) space.RecordStartupOrder();

            // Right after the review starts, the 42h window is unmet.
            Should.Throw<InvalidOperationException>(() => space.Approve());
        }

        [Fact]
        public void Approve_WithAllGuardsMet_After42h_Approves()
        {
            var space = NewSpace(AreaSpaceApprovalRules.MinInterestedMembers);
            space.Id = 123;
            space.StartReview();
            for (var i = 0; i < AreaSpaceApprovalRules.RequiredPresentations; i++) space.RecordPresentation();
            for (var i = 0; i < AreaSpaceApprovalRules.RequiredStartupOrders; i++) space.RecordStartupOrder();

            var afterWindow = Clock.Now.AddHours(AreaSpaceApprovalRules.ReviewWindowHours + 1);
            space.Approve(afterWindow);

            space.Status.ShouldBe(AreaSpaceStatus.Approved);
        }

        [Fact]
        public void Approve_WithoutPersistedId_Succeeds()
        {
            var space = NewSpace(AreaSpaceApprovalRules.MinInterestedMembers);
            space.StartReview();
            for (var i = 0; i < AreaSpaceApprovalRules.RequiredPresentations; i++) space.RecordPresentation();
            for (var i = 0; i < AreaSpaceApprovalRules.RequiredStartupOrders; i++) space.RecordStartupOrder();

            var afterWindow = Clock.Now.AddHours(AreaSpaceApprovalRules.ReviewWindowHours + 1);

            space.Approve(afterWindow);
            space.Status.ShouldBe(AreaSpaceStatus.Approved);
        }

        [Fact]
        public void Suspend_OnlyFromApproved_ThrowsOtherwise()
        {
            var space = NewSpace();
            Should.Throw<InvalidOperationException>(() => space.Suspend());
        }
    }
}
