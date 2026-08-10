using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Authorization;
using AqualLifeStyle.Application.Admin.EntryMonthlyObligations;
using AqualLifeStyle.Application.EntryMonthlyObligations;
using AqualLifeStyle.Application.EntryMonthlyObligations.Dto;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Authorization.Roles;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using Shouldly;
using Xunit;
using RolePermissionSetting = Abp.Authorization.Roles.RolePermissionSetting;
using UserRole = Abp.Authorization.Users.UserRole;

namespace AqualLifeStyle.Tests.Application
{
    public class EntryMonthlyObligationAppServiceTests
        : AqualLifeStyleTestBase
    {
        private static readonly DateTime EffectiveFrom =
            new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        private readonly IClubMemberEntryMonthlyObligationAppService
            _memberService;
        private readonly IAdminEntryMonthlyObligationAppService _adminService;

        public EntryMonthlyObligationAppServiceTests()
        {
            _memberService =
                Resolve<IClubMemberEntryMonthlyObligationAppService>();
            _adminService =
                Resolve<IAdminEntryMonthlyObligationAppService>();
        }

        [Fact]
        public async Task MemberAndAdministrator_ReadPersistedCommitment()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var email = $"commitment-{suffix}@example.com";
            var userId = await CreateTestUserAsync(
                1,
                $"commitment-{suffix}",
                email);
            var customerId = await UsingDbContextAsync(1, async context =>
            {
                var customer = Customer.Create(
                    1,
                    userId,
                    "Entry Club Member",
                    new EmailAddress(email));
                context.Customers.Add(customer);
                await context.SaveChangesAsync();
                var terms = EntryProgrammeTerms.Create(
                    "2026-07",
                    EffectiveFrom,
                    600m,
                    600m,
                    600m,
                    7);
                var participation = EntryParticipation.StartIndependently(
                    1,
                    customer.Id,
                    terms,
                    EffectiveFrom);
                var registration = ConfirmedPayment(
                    customer.Id,
                    MemberPaymentPurpose.EntryRegistration,
                    "registration-" + suffix);
                participation.ApplyConfirmedActivationPayment(registration);
                var activation = ConfirmedPayment(
                    customer.Id,
                    MemberPaymentPurpose.EntryActivation,
                    "activation-" + suffix);
                participation.ApplyConfirmedActivationPayment(activation);
                participation.ApproveByAdministrator(1L, EffectiveFrom.AddMinutes(3));
                context.EntryMonthlyObligationDuePolicies.Add(
                    EntryMonthlyObligationDuePolicy.Create(
                        "due-policy-v1",
                        10,
                        EntryMonthlyObligationDuePolicy.JohannesburgMonthStartUtc(2026, 8)));
                var obligation = EntryMonthlyObligation.Create(
                    participation,
                    2026,
                    8,
                    new DateTime(
                        2026, 8, 10, 0, 0, 0, DateTimeKind.Utc),
                    "due-policy-v1");
                obligation.AssessStatus(
                    new DateTime(
                        2026, 8, 18, 0, 0, 0, DateTimeKind.Utc));
                context.MemberPayments.AddRange(registration, activation);
                context.EntryParticipations.Add(participation);
                context.EntryMonthlyObligations.Add(obligation);
                await context.SaveChangesAsync();
                return customer.Id;
            });

            SetCurrentUser(userId, 1);
            var memberResult = await _memberService.GetMyObligationsAsync();
            memberResult.Count.ShouldBe(1);
            memberResult[0].Status.ShouldBe("Overdue");
            memberResult[0].AmountDue.ShouldBe(600m);
            memberResult[0].IsOwnPayoutEligible.ShouldBeFalse();

            LoginAsHostAdmin();
            var adminResult = await _adminService.GetAllAsync(
                new AdminEntryMonthlyObligationListInput
                {
                    TenantId = 1,
                    Keyword = email,
                    MaxResultCount = 20
                });
            adminResult.TotalCount.ShouldBe(1);
            adminResult.Items[0].CustomerId.ShouldBe(customerId);
            adminResult.Items[0].CustomerName.ShouldBe("Entry Club Member");
        }

        [Fact]
        public async Task TenantAdministrator_CannotRequestAnotherArea()
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                _adminService.GetAllAsync(
                    new AdminEntryMonthlyObligationListInput
                    {
                        TenantId = 2,
                        MaxResultCount = 20
                    }));
        }


        [Fact]
        public async Task HostReviewerWithoutAllAreas_CannotRequestAreaCommitments()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userName = $"host-commitment-reviewer-{suffix}";
            var userId = await CreateTestUserAsync(
                null,
                userName,
                $"{userName}@example.com");
            await UsingDbContextAsync(null, async context =>
            {
                var role = new Role(
                    null,
                    $"CommitmentReviewer-{suffix}",
                    $"Commitment Reviewer {suffix}");
                context.Roles.Add(role);
                await context.SaveChangesAsync();
                context.UserRoles.RemoveRange(
                    context.UserRoles.Where(item => item.UserId == userId));
                context.UserRoles.Add(new UserRole(null, userId, role.Id));
                context.Permissions.Add(new RolePermissionSetting
                {
                    TenantId = null,
                    Name =
                        AquaPermissions.Admin.EntryMonthlyObligations.View,
                    IsGranted = true,
                    RoleId = role.Id
                });
                await context.SaveChangesAsync();
            });
            LoginAsHost(userName);

            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                _adminService.GetAllAsync(
                    new AdminEntryMonthlyObligationListInput
                    {
                        TenantId = 1,
                        MaxResultCount = 20
                    }));
        }

        private static MemberPayment ConfirmedPayment(
            int customerId,
            MemberPaymentPurpose purpose,
            string reference)
        {
            var payment = MemberPayment.CreatePending(
                1,
                customerId,
                purpose,
                600m,
                "Test",
                reference,
                EffectiveFrom);
            payment.Confirm(EffectiveFrom.AddHours(1));
            return payment;
        }
    }
}
