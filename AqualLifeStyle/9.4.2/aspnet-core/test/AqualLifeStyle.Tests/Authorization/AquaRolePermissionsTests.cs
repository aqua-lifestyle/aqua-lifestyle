using System.Linq;
using Abp.Authorization;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.Enums;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Authorization
{
    public class AquaRolePermissionsTests
    {
        [Fact]
        public void SystemAdmin_HasEveryPermission() => AquaRolePermissions.GetFor(AquaUserRole.SystemAdmin).ShouldBe(AquaPermissions.GetAll(), ignoreOrder: true);

        [Fact]
        public void NestedAdminProgrammePermissions_AreIncludedForSystemAdmin()
        {
            AquaPermissions.GetAll().ShouldContain(
                AquaPermissions.Admin.ProgrammeParticipations.Default);
            AquaPermissions.GetAll().ShouldContain(
                AquaPermissions.Admin.ProgrammeParticipations.View);
            AquaRolePermissions.GetFor(AquaUserRole.SystemAdmin).ShouldContain(
                AquaPermissions.Admin.ProgrammeParticipations.Default);
            AquaRolePermissions.GetFor(AquaUserRole.SystemAdmin).ShouldContain(
                AquaPermissions.Admin.ProgrammeParticipations.View);
        }

        [Fact]
        public void NestedAdminCommissionPermissions_AreIncludedForSystemAdmin()
        {
            AquaPermissions.GetAll().ShouldContain(
                AquaPermissions.Admin.Commissions.Default);
            AquaPermissions.GetAll().ShouldContain(
                AquaPermissions.Admin.Commissions.View);
            AquaPermissions.GetAll().ShouldContain(
                AquaPermissions.Admin.Commissions.Calculate);
            AquaPermissions.GetAll().ShouldContain(
                AquaPermissions.Admin.Commissions.Release);
            AquaPermissions.GetAll().ShouldContain(
                AquaPermissions.Admin.Commissions.RecordPayment);
            AquaRolePermissions.GetFor(AquaUserRole.SystemAdmin).ShouldContain(
                AquaPermissions.Admin.Commissions.Calculate);
        }

        [Fact]
        public void NestedAdminSavingsPermissions_AreIncludedForSystemAdmin()
        {
            AquaPermissions.GetAll().ShouldContain(
                AquaPermissions.Admin.Savings.Default);
            AquaPermissions.GetAll().ShouldContain(
                AquaPermissions.Admin.Savings.View);
            AquaRolePermissions.GetFor(AquaUserRole.SystemAdmin).ShouldContain(
                AquaPermissions.Admin.Savings.View);
        }

        [Fact]
        public void NestedAdminLoanPermissions_AreIncludedForSystemAdmin()
        {
            AquaPermissions.GetAll().ShouldContain(
                AquaPermissions.Admin.Loans.Default);
            AquaPermissions.GetAll().ShouldContain(
                AquaPermissions.Admin.Loans.View);
            AquaRolePermissions.GetFor(AquaUserRole.SystemAdmin).ShouldContain(
                AquaPermissions.Admin.Loans.View);
        }

        [Fact]
        public void Guest_HasOnlySelfServicePermissions()
        {
            AquaRolePermissions.GetFor(AquaUserRole.Guest).ShouldBe(new[]
            {
                AquaPermissions.Members.ViewSelf,
                AquaPermissions.Memberships.ViewSelf,
                AquaPermissions.Memberships.Upgrade,
                AquaPermissions.ProgrammeParticipations.ViewSelf,
                AquaPermissions.ProgrammeParticipations.Join,
                AquaPermissions.Orders.Place
            }, ignoreOrder: true);
        }

        [Theory]
        [InlineData(AquaUserRole.AreaLeader, AquaPermissions.AreaSpaces.Manage)]
        [InlineData(AquaUserRole.AreaLeader, AquaPermissions.Orders.Process)]
        [InlineData(AquaUserRole.AreaLeader, AquaPermissions.Facilitators.Promote)]
        [InlineData(AquaUserRole.Facilitator, AquaPermissions.Facilitators.Refer)]
        [InlineData(AquaUserRole.Facilitator, AquaPermissions.Referrals.Create)]
        [InlineData(AquaUserRole.Facilitator, AquaPermissions.Enquiries.Create)]
        [InlineData(AquaUserRole.Member, AquaPermissions.Members.ViewSelf)]
        [InlineData(AquaUserRole.Member, AquaPermissions.Orders.Place)]
        [InlineData(AquaUserRole.Member, AquaPermissions.Savings.Deposit)]
        [InlineData(AquaUserRole.Member, AquaPermissions.Savings.Withdraw)]
        [InlineData(AquaUserRole.Member, AquaPermissions.Loans.ViewSelf)]
        public void Role_HasExpectedPermission(AquaUserRole role, string permission) => AquaRolePermissions.GetFor(role).ShouldContain(permission);

        [Theory]
        [InlineData(AquaUserRole.AreaLeader, AquaPermissions.Admin.AllTenants)]
        [InlineData(AquaUserRole.AreaLeader, AquaPermissions.Savings.Withdraw)]
        [InlineData(AquaUserRole.Facilitator, AquaPermissions.AreaSpaces.Approve)]
        [InlineData(AquaUserRole.Facilitator, AquaPermissions.Orders.Approve)]
        [InlineData(AquaUserRole.Member, AquaPermissions.Members.Delete)]
        [InlineData(AquaUserRole.Member, AquaPermissions.Referrals.Confirm)]
        [InlineData(AquaUserRole.Guest, AquaPermissions.Enquiries.Create)]
        public void Role_DoesNotLeakPermission(AquaUserRole role, string permission) => AquaRolePermissions.GetFor(role).ShouldNotContain(permission);

        [Fact]
        public void Provider_RegistersEveryAquaPermission()
        {
            var names = PermissionFinder.GetAllPermissions(new AqualLifeStyleAuthorizationProvider()).Select(permission => permission.Name).ToArray();
            foreach (var permission in AquaPermissions.GetAll()) names.ShouldContain(permission);
        }

        [Theory]
        [InlineData(AquaPermissions.Members.ViewSelf, AquaPermissions.Members.Default)]
        [InlineData(AquaPermissions.AreaSpaces.Approve, AquaPermissions.AreaSpaces.Default)]
        [InlineData(AquaPermissions.Orders.Place, AquaPermissions.Orders.Default)]
        [InlineData(AquaPermissions.Savings.Withdraw, AquaPermissions.Savings.Default)]
        [InlineData(AquaPermissions.Enquiries.Resolve, AquaPermissions.Enquiries.Default)]
        [InlineData(AquaPermissions.Referrals.Confirm, AquaPermissions.Referrals.Default)]
        [InlineData(AquaPermissions.ProgrammeParticipations.Join, AquaPermissions.ProgrammeParticipations.Default)]
        [InlineData(AquaPermissions.Admin.ProgrammeParticipations.View, AquaPermissions.Admin.ProgrammeParticipations.Default)]
        [InlineData(AquaPermissions.Admin.Commissions.View, AquaPermissions.Admin.Commissions.Default)]
        [InlineData(AquaPermissions.Admin.Commissions.Calculate, AquaPermissions.Admin.Commissions.Default)]
        [InlineData(AquaPermissions.Admin.Commissions.Release, AquaPermissions.Admin.Commissions.Default)]
        [InlineData(AquaPermissions.Admin.Commissions.RecordPayment, AquaPermissions.Admin.Commissions.Default)]
        [InlineData(AquaPermissions.Admin.Savings.View, AquaPermissions.Admin.Savings.Default)]
        [InlineData(AquaPermissions.Loans.ViewSelf, AquaPermissions.Loans.Default)]
        [InlineData(AquaPermissions.Admin.Loans.View, AquaPermissions.Admin.Loans.Default)]
        public void Provider_RegistersExpectedParent(string childName, string parentName)
        {
            var permissions = PermissionFinder.GetAllPermissions(new AqualLifeStyleAuthorizationProvider());
            permissions.Single(permission => permission.Name == childName).Parent.Name.ShouldBe(parentName);
        }
    }
}
