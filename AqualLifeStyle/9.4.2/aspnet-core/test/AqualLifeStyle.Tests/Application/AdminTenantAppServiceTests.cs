using System.Linq;
using System.Threading.Tasks;
using AqualLifeStyle.Application.Admin.Tenants;
using AqualLifeStyle.Application.Admin.Tenants.Dto;
using AqualLifeStyle.Domain.AreaLeaders;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;
using Abp.Authorization.Users;
using AqualLifeStyle.Authorization.Users;

namespace AqualLifeStyle.Tests.Application
{
    public class AdminTenantAppServiceTests : AqualLifeStyleTestBase
    {
        private readonly IAdminTenantAppService _tenantAdministration;

        public AdminTenantAppServiceTests()
        {
            LoginAsHostAdmin();
            _tenantAdministration = Resolve<IAdminTenantAppService>();
        }

        [Fact]
        public async Task Create_ProvisionsInitialAdministratorThroughInvitationWorkflow()
        {
            var suffix = System.Guid.NewGuid().ToString("N").Substring(0, 8);
            var email = $"area-admin-{suffix}@example.com";
            var tenant = await _tenantAdministration.CreateAsync(new CreateAdminTenantInput
            {
                TenancyName = $"Area{suffix}",
                Name = $"Area {suffix}",
                AdminEmailAddress = email,
                IsActive = true,
                Justification = "Initial administrator invitation test"
            });

            await UsingDbContextAsync(null, async context =>
            {
                var administrator = await context.Users.IgnoreQueryFilters().SingleAsync(user =>
                    user.TenantId == tenant.Id && user.UserName == AbpUserBase.AdminUserName);
                administrator.IsActive.ShouldBeFalse();
                administrator.IsEmailConfirmed.ShouldBeFalse();
                administrator.RequiresPasswordReset().ShouldBeTrue();
                (await context.InternalAccountInvitations.IgnoreQueryFilters().CountAsync(invitation =>
                    invitation.TenantId == tenant.Id && invitation.UserId == administrator.Id)).ShouldBe(1);
                (await context.TransactionalEmailOutboxMessages.IgnoreQueryFilters().CountAsync(message =>
                    message.TenantId == tenant.Id && message.NotificationType == "InternalAccountInvitation" &&
                    message.Recipient == email)).ShouldBe(1);
            });
        }

        [Fact]
        public async Task TenantLifecycle_EditsActivationAndAssignsApprovedTenantLeader()
        {
            var leaderId = await UsingDbContextAsync(1, async context =>
            {
                var customerId = await context.Customers.Where(customer => customer.TenantId == 1 &&
                    !context.AreaLeaders.Any(leader => leader.CustomerId == customer.Id)).Select(customer => customer.Id).FirstAsync();
                var leader = AreaLeader.Apply(1, customerId, LicenseType.EntreLevel);
                leader.ApproveApplication();
                context.AreaLeaders.Add(leader);
                await context.SaveChangesAsync();
                return leader.Id;
            });

            var edited = await _tenantAdministration.EditAsync(new EditAdminTenantInput
            {
                Id = 1,
                Name = "Default tenant updated",
                TenancyName = "Default",
                Justification = "Corrected the tenant display name"
            });
            edited.Name.ShouldBe("Default tenant updated");

            var deactivated = await _tenantAdministration.SetActivationAsync(new SetTenantActivationInput
            {
                Id = 1,
                IsActive = false,
                Justification = "Tenant requested a temporary pause"
            });
            deactivated.IsActive.ShouldBeFalse();

            var assigned = await _tenantAdministration.AssignAreaLeaderAsync(new AssignTenantAreaLeaderInput
            {
                Id = 1,
                AreaLeaderId = leaderId,
                Justification = "Approved leader accepted tenant responsibility"
            });
            assigned.AreaLeaderId.ShouldBe(leaderId);
            assigned.AreaLeaderName.ShouldNotBeNullOrWhiteSpace();

            await UsingDbContextAsync(null, async context =>
            {
                var tenant = await context.Tenants.SingleAsync(item => item.Id == 1);
                tenant.Name.ShouldBe("Default tenant updated");
                tenant.IsActive.ShouldBeFalse();
                tenant.AreaLeaderId.ShouldBe(leaderId);
            });
        }
    }
}
