using System.Linq;
using System.Threading.Tasks;
using AqualLifeStyle.Application.Admin.Tenants;
using AqualLifeStyle.Application.Admin.Tenants.Dto;
using AqualLifeStyle.Domain.AreaLeaders;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;
using Abp.Authorization.Users;
using Abp.Application.Services.Dto;
using Abp.UI;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.MultiTenancy;

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
                var activationState = await context.AreaActivationStateRecords
                    .SingleAsync(record => record.TenantId == tenant.Id);
                activationState.IsActive.ShouldBeTrue();
                activationState.Kind.ShouldBe(AreaActivationStateRecordKind.Provisioned);
                activationState.Justification.ShouldBe("Initial administrator invitation test");
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
            deactivated.HasActivationHistory.ShouldBeTrue();
            deactivated.ActivationHistoryBeginsAt.ShouldNotBeNull();

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
                var activationState = await context.AreaActivationStateRecords
                    .SingleAsync(record => record.TenantId == tenant.Id);
                activationState.IsActive.ShouldBeFalse();
                activationState.Kind.ShouldBe(AreaActivationStateRecordKind.Changed);
            });
        }

        [Fact]
        public async Task ObserveActivationState_RecordsOneProspectiveBaseline()
        {
            var observed = await _tenantAdministration.ObserveActivationStateAsync(
                new ObserveTenantActivationStateInput
                {
                    Id = 1,
                    Justification = "Observed current Area state before financial rollout"
                });
            var repeated = await _tenantAdministration.ObserveActivationStateAsync(
                new ObserveTenantActivationStateInput
                {
                    Id = 1,
                    Justification = "Repeated baseline observation request"
                });

            observed.HasActivationHistory.ShouldBeTrue();
            observed.ActivationHistoryBeginsAt.ShouldNotBeNull();
            repeated.ActivationHistoryBeginsAt.ShouldBe(
                observed.ActivationHistoryBeginsAt);

            await UsingDbContextAsync(null, async context =>
            {
                var record = await context.AreaActivationStateRecords.SingleAsync(
                    item => item.TenantId == 1);
                record.Kind.ShouldBe(AreaActivationStateRecordKind.ObservedBaseline);
                record.IsActive.ShouldBeTrue();
                record.Justification.ShouldBe(
                    "Observed current Area state before financial rollout");
            });
        }

        [Fact]
        public async Task LegacyTenantDeletion_IsRejectedToPreserveFinancialHistory()
        {
            var legacyService = Resolve<ITenantAppService>();

            var exception = await Should.ThrowAsync<UserFriendlyException>(
                () => legacyService.DeleteAsync(new EntityDto<int> { Id = 1 }));

            exception.Message.ShouldContain("Deactivate the Area instead");
            await UsingDbContextAsync(null, async context =>
            {
                (await context.Tenants.IgnoreQueryFilters().CountAsync(
                    tenant => tenant.Id == 1)).ShouldBe(1);
            });
        }
    }
}
