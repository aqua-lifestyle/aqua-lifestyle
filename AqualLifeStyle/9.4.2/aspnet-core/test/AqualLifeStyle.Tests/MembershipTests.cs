using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using Xunit;

namespace AqualLifeStyle.Tests
{
    public class MembershipTests
    {
        [Fact]
        public void Create_DefaultsToActiveStandardMembership()
        {
            var membership = Membership.Create(1, "Aqua Plus", "Premium access");

            Assert.True(membership.IsActive);
            Assert.Equal(MembershipType.Jasper, membership.MembershipType);
            Assert.Equal("Aqua Plus", membership.Name);
        }

        [Fact]
        public void ChangeMembershipType_UpdatesTier()
        {
            var membership = Membership.Create(1, "Aqua Plus", "Premium access", MembershipType.Jasper);

            membership.ChangeType(MembershipType.Onyx);

            Assert.Equal(MembershipType.Onyx, membership.MembershipType);
        }

        [Fact]
        public void ActivateAndDeactivate_ChangeLifecycleState()
        {
            var membership = Membership.Create(1, "Aqua Plus", "Premium access");

            membership.Deactivate();
            Assert.False(membership.IsActive);

            membership.Activate();
            Assert.True(membership.IsActive);
        }

        [Fact]
        public void EnsureCanBeAssignedToCustomer_ThrowsWhenInactive()
        {
            var membership = Membership.Create(1, "Aqua Plus", "Premium access");
            membership.Deactivate();

            Assert.Throws<System.InvalidOperationException>(() => membership.EnsureCanBeAssignedToCustomer());
        }

        [Fact]
        public void EnsureCanBeAssignedToCustomer_RejectsOnyxProgrammeConfiguration()
        {
            var membership = Membership.Create(
                null,
                "Onyx",
                "Onyx programme configuration",
                MembershipType.Onyx);

            var exception = Assert.Throws<System.InvalidOperationException>(
                () => membership.EnsureCanBeAssignedToCustomer());

            Assert.Contains("programme participation", exception.Message);
        }
    }
}
