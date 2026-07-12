using System;
using System.Linq;
using System.Reflection;
using AqualLifeStyle.Domain.Facilitators;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Domain
{
    public class ReferralAggregateTests
    {
        [Fact]
        public void CreateDirect_SetsExpectedState()
        {
            var convertedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var referral = Referral.CreateDirect(
                tenantId: 1,
                referrerFacilitatorId: 7,
                referredCustomerId: 100,
                sourceEnquiryId: 200,
                awardAmount: 50m,
                convertedAt: convertedAt);

            referral.TenantId.ShouldBe(1);
            referral.ReferrerFacilitatorId.ShouldBe(7);
            referral.ReferrerAreaLeaderId.ShouldBeNull();
            referral.ReferredCustomerId.ShouldBe(100);
            referral.SourceEnquiryId.ShouldBe(200);
            referral.Type.ShouldBe(ReferralType.Direct);
            referral.AwardAmount.ShouldBe(50m);
            referral.AwardIssued.ShouldBeFalse();
            referral.ConfirmedAt.ShouldBeNull();
            referral.ConvertedAt.ShouldBe(convertedAt);
        }

        [Fact]
        public void CreateIndirect_SetsExpectedState()
        {
            var convertedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);

            var referral = Referral.CreateIndirect(
                tenantId: 1,
                referrerAreaLeaderId: 11,
                referredCustomerId: 100,
                sourceEnquiryId: 200,
                awardAmount: 25m,
                convertedAt: convertedAt);

            referral.ReferrerFacilitatorId.ShouldBeNull();
            referral.ReferrerAreaLeaderId.ShouldBe(11);
            referral.Type.ShouldBe(ReferralType.Indirect);
            referral.ConvertedAt.ShouldBe(convertedAt);
        }

        [Fact]
        public void Create_WithDefaultConversionDate_UsesCurrentUtcTime()
        {
            var before = DateTime.UtcNow;

            var referral = Referral.CreateDirect(1, 7, 100, 200, 50m, default);

            referral.ConvertedAt.ShouldBeGreaterThanOrEqualTo(before);
            referral.ConvertedAt.ShouldBeLessThanOrEqualTo(DateTime.UtcNow);
        }

        [Fact]
        public void Create_WithInvalidArguments_Throws()
        {
            Should.Throw<ArgumentException>(() => Referral.CreateDirect(0, 7, 100, 200, 50m, DateTime.UtcNow));
            Should.Throw<ArgumentException>(() => Referral.CreateDirect(1, 7, 0, 200, 50m, DateTime.UtcNow));
            Should.Throw<ArgumentException>(() => Referral.CreateDirect(1, 7, 100, 0, 50m, DateTime.UtcNow));
            Should.Throw<ArgumentException>(() => Referral.CreateDirect(1, 7, 100, 200, -1m, DateTime.UtcNow));
        }

        [Fact]
        public void Constructor_WhenNoReferrerSpecified_Throws()
        {
            var constructor = typeof(Referral).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types:
                [
                    typeof(int),
                    typeof(int?),
                    typeof(int?),
                    typeof(int),
                    typeof(int),
                    typeof(ReferralType),
                    typeof(decimal),
                    typeof(DateTime)
                ],
                modifiers: null);

            constructor.ShouldNotBeNull();

            var ex = Should.Throw<TargetInvocationException>(() => constructor.Invoke(
                [1, null, null, 100, 200, ReferralType.Direct, 50m, DateTime.UtcNow]));

            ex.InnerException.ShouldBeOfType<ArgumentException>()
                .Message.ShouldContain("must credit a facilitator or an area leader");
        }

        [Fact]
        public void ConfirmAward_WithPositiveAmount_SetsConfirmationAndRaisesEvent()
        {
            var referral = Referral.CreateDirect(1, 7, 100, 200, 50m, DateTime.UtcNow);
            referral.Id = 15;

            referral.ConfirmAward();

            referral.AwardIssued.ShouldBeTrue();
            referral.ConfirmedAt.ShouldNotBeNull();
            referral.DomainEvents.Count.ShouldBe(1);

            var evt = referral.DomainEvents.Single().ShouldBeOfType<ReferralConfirmedEvent>();
            evt.ReferralId.ShouldBe(15);
            evt.ReferrerFacilitatorId.ShouldBe(7);
            evt.ReferrerAreaLeaderId.ShouldBeNull();
            evt.AwardAmount.ShouldBe(50m);
        }

        [Fact]
        public void ConfirmAward_WithZeroAmount_Throws()
        {
            var referral = Referral.CreateDirect(1, 7, 100, 200, 0m, DateTime.UtcNow);

            Should.Throw<InvalidOperationException>(() => referral.ConfirmAward())
                .Message.ShouldBe("Cannot confirm an award of zero.");
        }

        [Fact]
        public void ConfirmAward_WhenAlreadyIssued_IsIdempotent()
        {
            var referral = Referral.CreateIndirect(1, 11, 100, 200, 25m, DateTime.UtcNow);
            referral.ConfirmAward();
            var firstConfirmedAt = referral.ConfirmedAt;

            referral.ConfirmAward();

            referral.ConfirmedAt.ShouldBe(firstConfirmedAt);
            referral.DomainEvents.Count.ShouldBe(1);
        }
    }
}
