using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Authorization;
using AqualLifeStyle.Application.Admin.Loans;
using AqualLifeStyle.Application.Loans;
using AqualLifeStyle.Application.Loans.Dto;
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
    public class OnyxLoanAppServiceTests : AqualLifeStyleTestBase
    {
        private static readonly DateTime EffectiveFrom =
            new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        private static readonly EntryProgrammeTerms EntryTerms =
            EntryProgrammeTerms.Create(
                "2026-07",
                EffectiveFrom,
                600m,
                600m,
                600m,
                7);

        private static readonly OnyxLoanTerms LoanTerms =
            OnyxLoanTerms.Create(
                "2026-07",
                EffectiveFrom,
                6120m,
                30m,
                3,
                4,
                200m);

        private readonly IClubMemberOnyxLoanAppService _memberService;
        private readonly IAdminOnyxLoanAppService _adminService;

        public OnyxLoanAppServiceTests()
        {
            _memberService = Resolve<IClubMemberOnyxLoanAppService>();
            _adminService = Resolve<IAdminOnyxLoanAppService>();
        }

        [Fact]
        public async Task MemberAndAdministrator_ReadPersistedLoanLedger()
        {
            var details = await CreateActiveLoanAsync();

            SetCurrentUser(details.UserId, 1);
            var memberResult = await _memberService.GetMyAgreementsAsync();

            memberResult.Items.Count.ShouldBe(1);
            var memberLoan = memberResult.Items[0];
            memberLoan.CustomerId.ShouldBe(details.CustomerId);
            memberLoan.Status.ShouldBe("Active");
            memberLoan.PrincipalAmount.ShouldBe(6120m);
            memberLoan.TotalPayableAmount.ShouldBe(7956m);
            memberLoan.RepaidAmount.ShouldBe(200m);
            memberLoan.OutstandingAmount.ShouldBe(7756m);
            memberLoan.WeeklyRequirements.Count.ShouldBe(4);
            memberLoan.WeeklyRequirements[0].Status.ShouldBe("Paid");
            memberLoan.Repayments.Count.ShouldBe(1);

            LoginAsHostAdmin();
            var adminResult = await _adminService.GetAllAsync(
                new AdminOnyxLoanAgreementListInput
                {
                    TenantId = 1,
                    Keyword = details.Email,
                    MaxResultCount = 20
                });

            adminResult.TotalCount.ShouldBe(1);
            adminResult.Items[0].CustomerName.ShouldBe("Loan Club Member");
            adminResult.Items[0].Email.ShouldBe(details.Email);
            adminResult.Items[0].Repayments.Count.ShouldBe(1);
        }

        [Fact]
        public async Task MemberWithoutLoan_ReceivesAnEmptyList()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var email = $"no-loan-{suffix}@example.com";
            var userId = await CreateTestUserAsync(
                1,
                $"no-loan-{suffix}",
                email);
            await UsingDbContextAsync(1, async context =>
            {
                context.Customers.Add(Customer.Create(
                    1,
                    userId,
                    "Club Member Without Loan",
                    new EmailAddress(email)));
                await context.SaveChangesAsync();
            });
            SetCurrentUser(userId, 1);

            var result = await _memberService.GetMyAgreementsAsync();

            result.Items.ShouldBeEmpty();
        }

        [Fact]
        public async Task TenantAdministrator_CannotRequestAnotherAreasLoans()
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                _adminService.GetAllAsync(
                    new AdminOnyxLoanAgreementListInput
                    {
                        TenantId = 2,
                        MaxResultCount = 20
                    }));
        }

        [Fact]
        public async Task HostReviewerWithoutAllAreasPermission_CannotRequestAreaLoans()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userName = $"host-loan-reviewer-{suffix}";
            var userId = await CreateTestUserAsync(
                null,
                userName,
                $"{userName}@example.com");
            await UsingDbContextAsync(null, async context =>
            {
                var role = new Role(
                    null,
                    $"LoanReviewer-{suffix}",
                    $"Loan Reviewer {suffix}");
                context.Roles.Add(role);
                await context.SaveChangesAsync();

                context.UserRoles.RemoveRange(
                    context.UserRoles.Where(item => item.UserId == userId));
                context.UserRoles.Add(new UserRole(null, userId, role.Id));
                context.Permissions.Add(new RolePermissionSetting
                {
                    TenantId = null,
                    Name = AquaPermissions.Admin.Loans.View,
                    IsGranted = true,
                    RoleId = role.Id
                });
                await context.SaveChangesAsync();
            });
            LoginAsHost(userName);

            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                _adminService.GetAllAsync(
                    new AdminOnyxLoanAgreementListInput
                    {
                        TenantId = 1,
                        MaxResultCount = 20
                    }));
        }

        private async Task<LoanDetails> CreateActiveLoanAsync()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var email = $"loan-{suffix}@example.com";
            var userId = await CreateTestUserAsync(
                1,
                $"loan-{suffix}",
                email);

            return await UsingDbContextAsync(1, async context =>
            {
                var customer = Customer.Create(
                    1,
                    userId,
                    "Loan Club Member",
                    new EmailAddress(email));
                context.Customers.Add(customer);
                await context.SaveChangesAsync();

                var root = EntryParticipation.StartIndependently(
                    1,
                    customer.Id,
                    EntryTerms,
                    EffectiveFrom);
                var rootPayments = Activate(
                    root,
                    $"root-registration-{suffix}",
                    $"root-activation-{suffix}");
                var network = BuildLevelTwoNetwork(root, suffix);
                var agreement =
                    OnyxLoanAgreement.OfferToEligibleEntryParticipant(
                        root,
                        network,
                        new EntryNetworkQualificationEvaluator(),
                        LoanTerms,
                        EffectiveFrom.AddDays(1));
                agreement.AcceptByMember(
                    userId,
                    "I accept the Onyx loan terms.",
                    EffectiveFrom.AddDays(2));
                agreement.ApproveByAdministrator(
                    1,
                    EffectiveFrom.AddDays(3));
                var repayment = CreateConfirmedPayment(
                    customer.Id,
                    MemberPaymentPurpose.OnyxLoanRepayment,
                    200m,
                    $"repayment-{suffix}",
                    EffectiveFrom.AddDays(4));
                agreement.ApplyConfirmedRepayment(repayment, 1);

                context.MemberPayments.AddRange(
                    rootPayments.Registration,
                    rootPayments.Activation,
                    repayment);
                context.EntryParticipations.Add(root);
                context.OnyxLoanAgreements.Add(agreement);
                await context.SaveChangesAsync();

                return new LoanDetails(userId, customer.Id, email);
            });
        }

        private static List<EntryParticipation> BuildLevelTwoNetwork(
            EntryParticipation root,
            string suffix)
        {
            var network = new List<EntryParticipation> { root };
            var firstLevel = new List<EntryParticipation>();
            var nextCustomerId = 30000;

            for (var index = 0;
                 index < EntryNetworkQualificationEvaluator.BranchSize;
                 index++)
            {
                var recruit = EntryParticipation.StartUnderRecruiter(
                    1,
                    nextCustomerId++,
                    root,
                    EntryTerms,
                    EffectiveFrom);
                Activate(
                    recruit,
                    $"l1-registration-{index}-{suffix}",
                    $"l1-activation-{index}-{suffix}");
                network.Add(recruit);
                firstLevel.Add(recruit);
            }

            foreach (var recruiter in firstLevel)
            {
                for (var index = 0;
                     index < EntryNetworkQualificationEvaluator.BranchSize;
                     index++)
                {
                    var recruit = EntryParticipation.StartUnderRecruiter(
                        1,
                        nextCustomerId++,
                        recruiter,
                        EntryTerms,
                        EffectiveFrom);
                    Activate(
                        recruit,
                        $"l2-registration-{nextCustomerId}-{suffix}",
                        $"l2-activation-{nextCustomerId}-{suffix}");
                    network.Add(recruit);
                }
            }

            return network;
        }

        private static (MemberPayment Registration, MemberPayment Activation)
            Activate(
                EntryParticipation participation,
                string registrationReference,
                string activationReference)
        {
            var registration = CreateConfirmedPayment(
                participation.CustomerId,
                MemberPaymentPurpose.EntryRegistration,
                600m,
                registrationReference,
                EffectiveFrom);
            participation.ApplyConfirmedActivationPayment(registration);
            var activation = CreateConfirmedPayment(
                participation.CustomerId,
                MemberPaymentPurpose.EntryActivation,
                600m,
                activationReference,
                EffectiveFrom.AddHours(1));
            participation.ApplyConfirmedActivationPayment(activation);
            return (registration, activation);
        }

        private static MemberPayment CreateConfirmedPayment(
            int customerId,
            MemberPaymentPurpose purpose,
            decimal amount,
            string reference,
            DateTime confirmedAt)
        {
            var payment = MemberPayment.CreatePending(
                1,
                customerId,
                purpose,
                amount,
                "Test",
                reference,
                confirmedAt.AddMinutes(-1));
            payment.Confirm(confirmedAt);
            return payment;
        }

        private sealed class LoanDetails
        {
            public long UserId { get; }
            public int CustomerId { get; }
            public string Email { get; }

            public LoanDetails(long userId, int customerId, string email)
            {
                UserId = userId;
                CustomerId = customerId;
                Email = email;
            }
        }
    }
}
