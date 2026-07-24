using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Authorization;
using AqualLifeStyle.Application.Admin.ProgrammeParticipations;
using AqualLifeStyle.Application.Admin.ProgrammeParticipations.Dto;
using AqualLifeStyle.Application.ProgrammeParticipations;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Authorization.Roles;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.Payments;
using Shouldly;
using Xunit;
using RolePermissionSetting = Abp.Authorization.Roles.RolePermissionSetting;
using UserRole = Abp.Authorization.Users.UserRole;

namespace AqualLifeStyle.Tests.Application
{
    public class AdminProgrammeParticipationAppServiceTests
        : AqualLifeStyleTestBase
    {
        private static readonly DateTime EffectiveFrom =
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        private readonly IAdminProgrammeParticipationAppService _service;

        public AdminProgrammeParticipationAppServiceTests()
        {
            _service = Resolve<IAdminProgrammeParticipationAppService>();
        }

        [Fact]
        public async Task Administrator_CanReconcileEntryStateAndConfirmedPayments()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var customerId = await CreateEntryParticipantAsync(suffix);
            await Resolve<ProgrammePaymentConfirmationProcessor>().ProcessAsync(
                new ConfirmedProgrammePayment(
                    tenantId: 1,
                    customerId,
                    MemberPaymentPurpose.EntryRegistration,
                    amount: 600m,
                    currency: "ZAR",
                    provider: "yoco",
                    externalReference: $"entry-registration-{suffix}",
                    initiatedAt: EffectiveFrom,
                    confirmedAt: EffectiveFrom.AddMinutes(1)));

            var result = await _service.GetAllAsync(
                new AdminProgrammeParticipationListInput
                {
                    Keyword = suffix,
                    Programme = AdminProgrammeType.Entry,
                    MaxResultCount = 20
                });

            result.TotalCount.ShouldBe(1);
            var participation = result.Items.Single();
            participation.CustomerId.ShouldBe(customerId);
            participation.ProgrammeName.ShouldBe("Entry");
            participation.Status.ShouldBe("Awaiting activation payment");
            participation.NextPaymentAmount.ShouldBe(600m);
            participation.NextPaymentDescription.ShouldBe("Activation payment");
            participation.JoinedIndependently.ShouldBeTrue();
            participation.ConfirmedPayments.Count.ShouldBe(1);
            participation.ConfirmedPayments[0].Description.ShouldBe(
                "Entry registration payment");
            participation.ConfirmedPayments[0].Provider.ShouldBe("YOCO");
            participation.ConfirmedPayments[0].ProviderReference.ShouldBe(
                $"entry-registration-{suffix}");
        }

        [Fact]
        public async Task TenantAdministrator_CannotRequestAnotherAreasParticipations()
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                _service.GetAllAsync(new AdminProgrammeParticipationListInput
                {
                    TenantId = 2,
                    Programme = AdminProgrammeType.Entry
                }));
        }

        [Fact]
        public async Task HostReviewerWithoutAllAreasPermission_CannotRequestAllParticipations()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userName = $"host-programme-reviewer-{suffix}";
            var userId = await CreateTestUserAsync(
                null,
                userName,
                $"{userName}@example.com");
            await UsingDbContextAsync(null, async context =>
            {
                var role = new Role(
                    null,
                    $"ProgrammeReviewer-{suffix}",
                    $"Programme Reviewer {suffix}");
                context.Roles.Add(role);
                await context.SaveChangesAsync();

                context.UserRoles.RemoveRange(
                    context.UserRoles.Where(userRole => userRole.UserId == userId));
                context.UserRoles.Add(new UserRole(null, userId, role.Id));
                context.Permissions.Add(new RolePermissionSetting
                {
                    TenantId = null,
                    Name = AquaPermissions.Admin.ProgrammeParticipations.View,
                    IsGranted = true,
                    RoleId = role.Id
                });
                await context.SaveChangesAsync();
            });
            LoginAsHost(userName);

            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                _service.GetAllAsync(new AdminProgrammeParticipationListInput
                {
                    Programme = AdminProgrammeType.Entry
                }));
        }

        private async Task<int> CreateEntryParticipantAsync(string suffix)
        {
            var userId = await CreateTestUserAsync(
                1,
                $"admin-reconciliation-{suffix}",
                $"admin-reconciliation-{suffix}@example.com");
            return await UsingDbContextAsync(1, async context =>
            {
                var customer = Customer.Create(
                    1,
                    userId,
                    $"Reconciliation {suffix}",
                    new EmailAddress($"reconciliation-{suffix}@example.com"));
                context.Customers.Add(customer);
                await context.SaveChangesAsync();

                context.EntryParticipations.Add(EntryParticipation.StartIndependently(
                    1,
                    customer.Id,
                    Resolve<ICurrentProgrammeTermsProvider>().GetEntryTerms(),
                    EffectiveFrom));
                await context.SaveChangesAsync();
                return customer.Id;
            });
        }
    }
}
