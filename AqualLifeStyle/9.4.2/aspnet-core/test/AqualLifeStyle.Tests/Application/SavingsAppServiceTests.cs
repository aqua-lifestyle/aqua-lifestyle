using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Authorization;
using AqualLifeStyle.Application.Admin.Savings;
using AqualLifeStyle.Application.Savings;
using AqualLifeStyle.Application.Savings.Dto;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Authorization.Roles;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.Domain.Savings;
using Shouldly;
using Xunit;
using RolePermissionSetting = Abp.Authorization.Roles.RolePermissionSetting;
using UserRole = Abp.Authorization.Users.UserRole;

namespace AqualLifeStyle.Tests.Application
{
    public class SavingsAppServiceTests : AqualLifeStyleTestBase
    {
        private readonly IClubMemberSavingsAppService _memberService;
        private readonly IAdminSavingsAppService _adminService;

        public SavingsAppServiceTests()
        {
            _memberService = Resolve<IClubMemberSavingsAppService>();
            _adminService = Resolve<IAdminSavingsAppService>();
        }

        [Fact]
        public async Task MemberAndAdministrator_ReadPersistedSavingsLedger()
        {
            var details = await CreateSavingsAccountAsync();

            SetCurrentUser(details.UserId, 1);
            var memberResult = await _memberService.GetMyAccountAsync();

            memberResult.Account.ShouldNotBeNull();
            memberResult.Account.CustomerId.ShouldBe(details.CustomerId);
            memberResult.Account.Status.ShouldBe("Active");
            memberResult.Account.PrincipalBalance.ShouldBe(500m);
            memberResult.Account.ProjectedInterestAmount.ShouldBe(100m);
            memberResult.Account.ProjectedMaturityAmount.ShouldBe(600m);
            memberResult.Account.Contributions.Count.ShouldBe(1);
            memberResult.Account.Contributions[0]
                .InterestRatePercent.ShouldBe(20m);

            LoginAsHostAdmin();
            var adminResult = await _adminService.GetAllAsync(
                new AdminSavingsAccountListInput
                {
                    TenantId = 1,
                    Keyword = details.Email,
                    MaxResultCount = 20
                });

            adminResult.TotalCount.ShouldBe(1);
            adminResult.Items[0].CustomerName.ShouldBe("Savings Club Member");
            adminResult.Items[0].Email.ShouldBe(details.Email);
            adminResult.Items[0].Contributions.Count.ShouldBe(1);
        }

        [Fact]
        public async Task MemberWithoutSavingsAccount_ReceivesAnEmptyAccountResult()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var email = $"no-savings-{suffix}@example.com";
            var userId = await CreateTestUserAsync(
                1,
                $"no-savings-{suffix}",
                email);
            await UsingDbContextAsync(1, async context =>
            {
                context.Customers.Add(Customer.Create(
                    1,
                    userId,
                    "Club Member Without Savings",
                    new EmailAddress(email)));
                await context.SaveChangesAsync();
            });
            SetCurrentUser(userId, 1);

            var result = await _memberService.GetMyAccountAsync();

            result.Account.ShouldBeNull();
        }

        [Fact]
        public async Task TenantAdministrator_CannotRequestAnotherAreasSavings()
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                _adminService.GetAllAsync(new AdminSavingsAccountListInput
                {
                    TenantId = 2,
                    MaxResultCount = 20
                }));
        }

        [Fact]
        public async Task HostReviewerWithoutAllAreasPermission_CannotRequestAllSavings()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userName = $"host-savings-reviewer-{suffix}";
            var userId = await CreateTestUserAsync(
                null,
                userName,
                $"{userName}@example.com");
            await UsingDbContextAsync(null, async context =>
            {
                var role = new Role(
                    null,
                    $"SavingsReviewer-{suffix}",
                    $"Savings Reviewer {suffix}");
                context.Roles.Add(role);
                await context.SaveChangesAsync();

                context.UserRoles.RemoveRange(
                    context.UserRoles.Where(userRole =>
                        userRole.UserId == userId));
                context.UserRoles.Add(new UserRole(
                    null,
                    userId,
                    role.Id));
                context.Permissions.Add(new RolePermissionSetting
                {
                    TenantId = null,
                    Name = AquaPermissions.Admin.Savings.View,
                    IsGranted = true,
                    RoleId = role.Id
                });
                await context.SaveChangesAsync();
            });
            LoginAsHost(userName);

            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                _adminService.GetAllAsync(new AdminSavingsAccountListInput
                {
                    MaxResultCount = 20
                }));
        }

        private async Task<SavingsAccountDetails>
            CreateSavingsAccountAsync()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var email = $"savings-{suffix}@example.com";
            var userId = await CreateTestUserAsync(
                1,
                $"savings-{suffix}",
                email);
            return await UsingDbContextAsync(1, async context =>
            {
                var customer = Customer.Create(
                    1,
                    userId,
                    "Savings Club Member",
                    new EmailAddress(email));
                context.Customers.Add(customer);
                await context.SaveChangesAsync();

                var openedAt =
                    new DateTime(2026, 7, 5, 10, 0, 0, DateTimeKind.Utc);
                var terms = SavingsAccountTerms.Create(
                    "2026-07",
                    new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                    12,
                    100m,
                    20m,
                    1,
                    15);
                var account = SavingsAccount.Open(
                    1,
                    customer.Id,
                    openedAt,
                    terms);
                var payment = MemberPayment.CreatePending(
                    1,
                    customer.Id,
                    MemberPaymentPurpose.SavingsContribution,
                    500m,
                    "Test",
                    $"savings-contribution-{suffix}",
                    openedAt);
                payment.Confirm(openedAt.AddDays(5));
                account.ApplyConfirmedContribution(payment);
                context.MemberPayments.Add(payment);
                context.SavingsAccounts.Add(account);

                return new SavingsAccountDetails(
                    userId,
                    customer.Id,
                    email);
            });
        }

        private sealed class SavingsAccountDetails
        {
            public long UserId { get; }
            public int CustomerId { get; }
            public string Email { get; }

            public SavingsAccountDetails(
                long userId,
                int customerId,
                string email)
            {
                UserId = userId;
                CustomerId = customerId;
                Email = email;
            }
        }
    }
}
