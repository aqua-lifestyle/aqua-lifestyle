using System;
using System.Linq;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Authorization.Users.Events;
using AqualLifeStyle.Domain.Enums;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Domain
{
    public class UserRoleTests
    {
        [Fact]
        public void NewUser_DefaultRole_IsGuest()
        {
            var user = new User();

            user.Role.ShouldBe(AquaUserRole.Guest);
            user.IsGuest().ShouldBeTrue();
            user.IsSystemAdmin().ShouldBeFalse();
            user.IsAreaLeader().ShouldBeFalse();
            user.IsFacilitator().ShouldBeFalse();
            user.IsMember().ShouldBeFalse();
        }

        [Theory]
        [InlineData(AquaUserRole.SystemAdmin)]
        [InlineData(AquaUserRole.AreaLeader)]
        [InlineData(AquaUserRole.Facilitator)]
        [InlineData(AquaUserRole.Member)]
        public void SetRole_ValidRole_AssignsSuccessfully(AquaUserRole role)
        {
            var user = new User();

            user.SetRole(role);

            user.Role.ShouldBe(role);
            user.DomainEvents.Count.ShouldBe(1);
        }

        [Fact]
        public void SetRole_Guest_AfterNonDefault_RaisesEvent()
        {
            var user = new User();
            user.SetRole(AquaUserRole.Member);
            user.DomainEvents.Clear();

            user.SetRole(AquaUserRole.Guest);

            user.Role.ShouldBe(AquaUserRole.Guest);
            user.DomainEvents.Count.ShouldBe(1);
            var evt = user.DomainEvents.Single().ShouldBeOfType<UserRoleChangedEvent>();
            evt.OldRole.ShouldBe(AquaUserRole.Member);
            evt.NewRole.ShouldBe(AquaUserRole.Guest);
        }

        [Fact]
        public void SetRole_InvalidRole_ThrowsArgumentException()
        {
            var user = new User();

            var ex = Should.Throw<ArgumentException>(() => user.SetRole((AquaUserRole)999));

            ex.ParamName.ShouldBe("role");
            ex.Message.ShouldContain("not a valid user role");
            user.Role.ShouldBe(AquaUserRole.Guest);
            user.DomainEvents.Count.ShouldBe(0);
        }

        [Fact]
        public void SetRole_SameRole_DoesNotRaiseEvent()
        {
            var user = new User();
            user.SetRole(AquaUserRole.Member);
            user.DomainEvents.Clear();

            user.SetRole(AquaUserRole.Member);

            user.Role.ShouldBe(AquaUserRole.Member);
            user.DomainEvents.Count.ShouldBe(0);
        }

        [Fact]
        public void SetRole_DifferentRole_RaisesUserRoleChangedEvent()
        {
            var user = new User { Id = 42 };

            user.SetRole(AquaUserRole.Facilitator);

            user.DomainEvents.Count.ShouldBe(1);
            var evt = user.DomainEvents.Single().ShouldBeOfType<UserRoleChangedEvent>();
            evt.UserId.ShouldBe(42);
            evt.OldRole.ShouldBe(AquaUserRole.Guest);
            evt.NewRole.ShouldBe(AquaUserRole.Facilitator);
        }

        [Fact]
        public void CreateTenantAdminUser_SetsSystemAdminRole()
        {
            var user = User.CreateTenantAdminUser(1, "admin@test.com");

            user.Role.ShouldBe(AquaUserRole.SystemAdmin);
            user.IsSystemAdmin().ShouldBeTrue();
        }

        [Fact]
        public void HelperMethods_ReflectCurrentRole()
        {
            var guest = new User();
            guest.SetRole(AquaUserRole.Guest);
            guest.IsGuest().ShouldBeTrue();
            guest.IsMember().ShouldBeFalse();

            var member = new User();
            member.SetRole(AquaUserRole.Member);
            member.IsMember().ShouldBeTrue();
            member.IsGuest().ShouldBeFalse();

            var facilitator = new User();
            facilitator.SetRole(AquaUserRole.Facilitator);
            facilitator.IsFacilitator().ShouldBeTrue();
            facilitator.IsMember().ShouldBeFalse();

            var leader = new User();
            leader.SetRole(AquaUserRole.AreaLeader);
            leader.IsAreaLeader().ShouldBeTrue();
            leader.IsFacilitator().ShouldBeFalse();

            var admin = new User();
            admin.SetRole(AquaUserRole.SystemAdmin);
            admin.IsSystemAdmin().ShouldBeTrue();
            admin.IsAreaLeader().ShouldBeFalse();
        }
    }
}
