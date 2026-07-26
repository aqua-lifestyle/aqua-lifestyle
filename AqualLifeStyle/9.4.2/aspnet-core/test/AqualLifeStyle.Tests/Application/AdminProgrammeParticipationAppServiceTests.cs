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
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.Payments;
using Microsoft.EntityFrameworkCore;
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
            participation.ClubMemberNumber.ShouldStartWith("CLB-");
            participation.AreaName.ShouldBe("Default");
            participation.ProgrammeName.ShouldBe("AQGreen");
            participation.Status.ShouldBe("Awaiting activation payment");
            participation.NextPaymentAmount.ShouldBe(600m);
            participation.NextPaymentDescription.ShouldBe("Activation payment");
            participation.JoinedIndependently.ShouldBeTrue();
            participation.ConfirmedPayments.Count.ShouldBe(1);
            participation.ConfirmedPayments[0].Description.ShouldBe(
                "AQGreen registration payment");
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
        public async Task HostReviewerWithoutAllAreasPermission_CannotRequestAreaParticipations()
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
                    TenantId = 1,
                    Programme = AdminProgrammeType.Entry
                }));
        }

        [Fact]
        public async Task AdministratorCorrection_PreservesHistoryRejectsCyclesAndIsIdempotent()
        {
            var network = await CreateActiveAQGreenNetworkAsync();

            var selfPlacement = await Should.ThrowAsync<Abp.UI.UserFriendlyException>(() =>
                _service.CorrectRecruiterAsync(new CorrectProgrammeRecruiterInput
                {
                    Programme = AdminProgrammeType.Entry,
                    ClubMemberNumber = network.TargetNumber,
                    NewRecruiterClubMemberNumber = network.TargetNumber,
                    Reason = "Testing self-placement protection"
                }));
            selfPlacement.Details.ShouldContain("themselves");

            var crossProgramme = await Should.ThrowAsync<Abp.UI.UserFriendlyException>(() =>
                _service.CorrectRecruiterAsync(new CorrectProgrammeRecruiterInput
                {
                    Programme = AdminProgrammeType.Onyx,
                    ClubMemberNumber = network.TargetNumber,
                    NewRecruiterClubMemberNumber = network.RecruiterNumber,
                    Reason = "Testing programme isolation"
                }));
            crossProgramme.Details.ShouldContain("Onyx participation was not found");

            await _service.CorrectRecruiterAsync(new CorrectProgrammeRecruiterInput
            {
                Programme = AdminProgrammeType.Entry,
                ClubMemberNumber = network.TargetNumber,
                NewRecruiterClubMemberNumber = null,
                Reason = "Correcting placement to independent"
            });
            var restoreInput = new CorrectProgrammeRecruiterInput
            {
                Programme = AdminProgrammeType.Entry,
                ClubMemberNumber = network.TargetNumber,
                NewRecruiterClubMemberNumber = network.RecruiterNumber,
                Reason = "Restoring verified recruiter placement"
            };
            await _service.CorrectRecruiterAsync(restoreInput);
            await _service.CorrectRecruiterAsync(restoreInput);

            var cycle = await Should.ThrowAsync<Abp.UI.UserFriendlyException>(() =>
                _service.CorrectRecruiterAsync(new CorrectProgrammeRecruiterInput
                {
                    Programme = AdminProgrammeType.Entry,
                    ClubMemberNumber = network.RecruiterNumber,
                    NewRecruiterClubMemberNumber = network.DescendantNumber,
                    Reason = "Testing cycle protection"
                }));
            cycle.Details.ShouldContain("cycle");

            await UsingDbContextAsync(1, async context =>
            {
                var target = await context.EntryParticipations
                    .Include(item => item.RecruiterCorrections)
                    .SingleAsync(item => item.CustomerId == network.TargetCustomerId);
                target.RecruiterCustomerId.ShouldBe(network.RecruiterCustomerId);
                target.RecruiterCorrections.Count.ShouldBe(2);
                target.RecruiterCorrections.All(item =>
                    item.AdministratorUserId > 0 &&
                    !string.IsNullOrWhiteSpace(item.Reason)).ShouldBeTrue();
            });
        }

        [Fact]
        public async Task OnyxCorrection_PreservesHistoryRejectsCyclesAndIsIdempotent()
        {
            var network = await CreateActiveOnyxNetworkAsync();

            await _service.CorrectRecruiterAsync(new CorrectProgrammeRecruiterInput
            {
                Programme = AdminProgrammeType.Onyx,
                ClubMemberNumber = network.TargetNumber,
                NewRecruiterClubMemberNumber = null,
                Reason = "Correcting Onyx placement to independent"
            });
            var restoreInput = new CorrectProgrammeRecruiterInput
            {
                Programme = AdminProgrammeType.Onyx,
                ClubMemberNumber = network.TargetNumber,
                NewRecruiterClubMemberNumber = network.RecruiterNumber,
                Reason = "Restoring verified Onyx recruiter placement"
            };
            await _service.CorrectRecruiterAsync(restoreInput);
            await _service.CorrectRecruiterAsync(restoreInput);

            var cycle = await Should.ThrowAsync<Abp.UI.UserFriendlyException>(() =>
                _service.CorrectRecruiterAsync(new CorrectProgrammeRecruiterInput
                {
                    Programme = AdminProgrammeType.Onyx,
                    ClubMemberNumber = network.RecruiterNumber,
                    NewRecruiterClubMemberNumber = network.DescendantNumber,
                    Reason = "Testing Onyx cycle protection"
                }));
            cycle.Details.ShouldContain("cycle");

            await UsingDbContextAsync(1, async context =>
            {
                var target = await context.OnyxParticipations
                    .Include(item => item.RecruiterCorrections)
                    .SingleAsync(item => item.CustomerId == network.TargetCustomerId);
                target.RecruiterCustomerId.ShouldBe(network.RecruiterCustomerId);
                target.RecruiterCorrections.Count.ShouldBe(2);
                target.RecruiterCorrections.All(item =>
                    item.AdministratorUserId > 0 &&
                    !string.IsNullOrWhiteSpace(item.Reason)).ShouldBeTrue();
            });
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

        private async Task<RecruitmentNetworkFixture> CreateActiveAQGreenNetworkAsync()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var recruiterUserId = await CreateTestUserAsync(1, $"recruiter-{suffix}", $"recruiter-{suffix}@example.com");
            var targetUserId = await CreateTestUserAsync(1, $"target-{suffix}", $"target-{suffix}@example.com");
            var descendantUserId = await CreateTestUserAsync(1, $"descendant-{suffix}", $"descendant-{suffix}@example.com");
            return await UsingDbContextAsync(1, async context =>
            {
                var recruiterCustomer = Customer.Create(1, recruiterUserId, "Verified Recruiter", new EmailAddress($"recruiter-customer-{suffix}@example.com"));
                var targetCustomer = Customer.Create(1, targetUserId, "Placed Member", new EmailAddress($"target-customer-{suffix}@example.com"));
                var descendantCustomer = Customer.Create(1, descendantUserId, "Network Descendant", new EmailAddress($"descendant-customer-{suffix}@example.com"));
                context.Customers.AddRange(recruiterCustomer, targetCustomer, descendantCustomer);
                await context.SaveChangesAsync();

                var terms = Resolve<ICurrentProgrammeTermsProvider>().GetEntryTerms();
                var recruiter = EntryParticipation.StartIndependently(1, recruiterCustomer.Id, terms, EffectiveFrom);
                var payments = new System.Collections.Generic.List<MemberPayment>();
                payments.AddRange(Activate(recruiter, recruiterCustomer.Id, $"recruiter-{suffix}"));
                var target = EntryParticipation.StartUnderRecruiter(1, targetCustomer.Id, recruiter, terms, EffectiveFrom);
                payments.AddRange(Activate(target, targetCustomer.Id, $"target-{suffix}"));
                var descendant = EntryParticipation.StartUnderRecruiter(1, descendantCustomer.Id, target, terms, EffectiveFrom);
                payments.AddRange(Activate(descendant, descendantCustomer.Id, $"descendant-{suffix}"));
                context.MemberPayments.AddRange(payments);
                context.EntryParticipations.AddRange(recruiter, target, descendant);
                await context.SaveChangesAsync();

                return new RecruitmentNetworkFixture
                {
                    RecruiterCustomerId = recruiterCustomer.Id,
                    RecruiterNumber = recruiterCustomer.ClubMemberNumber,
                    TargetCustomerId = targetCustomer.Id,
                    TargetNumber = targetCustomer.ClubMemberNumber,
                    DescendantNumber = descendantCustomer.ClubMemberNumber
                };
            });
        }

        private async Task<RecruitmentNetworkFixture> CreateActiveOnyxNetworkAsync()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var recruiterUserId = await CreateTestUserAsync(1, $"onyx-recruiter-{suffix}", $"onyx-recruiter-{suffix}@example.com");
            var targetUserId = await CreateTestUserAsync(1, $"onyx-target-{suffix}", $"onyx-target-{suffix}@example.com");
            var descendantUserId = await CreateTestUserAsync(1, $"onyx-descendant-{suffix}", $"onyx-descendant-{suffix}@example.com");
            return await UsingDbContextAsync(1, async context =>
            {
                var recruiterCustomer = Customer.Create(1, recruiterUserId, "Verified Onyx Recruiter", new EmailAddress($"onyx-recruiter-customer-{suffix}@example.com"));
                var targetCustomer = Customer.Create(1, targetUserId, "Placed Onyx Member", new EmailAddress($"onyx-target-customer-{suffix}@example.com"));
                var descendantCustomer = Customer.Create(1, descendantUserId, "Onyx Network Descendant", new EmailAddress($"onyx-descendant-customer-{suffix}@example.com"));
                var membership = Membership.Create(1, $"Onyx-{suffix}", "Onyx correction test", MembershipType.Onyx);
                context.Customers.AddRange(recruiterCustomer, targetCustomer, descendantCustomer);
                context.Memberships.Add(membership);
                await context.SaveChangesAsync();

                var terms = Resolve<ICurrentProgrammeTermsProvider>().GetDirectOnyxTerms();
                var recruiter = OnyxParticipation.StartDirectIndependently(1, recruiterCustomer.Id, membership.Id, terms, EffectiveFrom);
                var payments = new System.Collections.Generic.List<MemberPayment>();
                payments.Add(Activate(recruiter, recruiterCustomer.Id, $"onyx-recruiter-{suffix}"));
                var target = OnyxParticipation.StartDirectUnderRecruiter(1, targetCustomer.Id, recruiter, membership.Id, terms, EffectiveFrom);
                payments.Add(Activate(target, targetCustomer.Id, $"onyx-target-{suffix}"));
                var descendant = OnyxParticipation.StartDirectUnderRecruiter(1, descendantCustomer.Id, target, membership.Id, terms, EffectiveFrom);
                payments.Add(Activate(descendant, descendantCustomer.Id, $"onyx-descendant-{suffix}"));
                context.MemberPayments.AddRange(payments);
                context.OnyxParticipations.AddRange(recruiter, target, descendant);
                await context.SaveChangesAsync();

                return new RecruitmentNetworkFixture
                {
                    RecruiterCustomerId = recruiterCustomer.Id,
                    RecruiterNumber = recruiterCustomer.ClubMemberNumber,
                    TargetCustomerId = targetCustomer.Id,
                    TargetNumber = targetCustomer.ClubMemberNumber,
                    DescendantNumber = descendantCustomer.ClubMemberNumber
                };
            });
        }

        private static MemberPayment[] Activate(
            EntryParticipation participation,
            int customerId,
            string reference)
        {
            var registration = MemberPayment.CreatePending(1, customerId, MemberPaymentPurpose.EntryRegistration, 600m, "Test", $"{reference}-registration", EffectiveFrom);
            registration.Confirm(EffectiveFrom.AddMinutes(1));
            participation.ApplyConfirmedActivationPayment(registration);
            var activation = MemberPayment.CreatePending(1, customerId, MemberPaymentPurpose.EntryActivation, 600m, "Test", $"{reference}-activation", EffectiveFrom);
            activation.Confirm(EffectiveFrom.AddMinutes(2));
            participation.ApplyConfirmedActivationPayment(activation);
            return new[] { registration, activation };
        }

        private static MemberPayment Activate(
            OnyxParticipation participation,
            int customerId,
            string reference)
        {
            var payment = MemberPayment.CreatePending(
                1,
                customerId,
                MemberPaymentPurpose.OnyxDirectEntry,
                6120m,
                "Test",
                reference,
                EffectiveFrom);
            payment.Confirm(EffectiveFrom.AddMinutes(1));
            participation.ApplyConfirmedDirectEntryPayment(payment);
            return payment;
        }

        private sealed class RecruitmentNetworkFixture
        {
            public int RecruiterCustomerId { get; init; }
            public string RecruiterNumber { get; init; }
            public int TargetCustomerId { get; init; }
            public string TargetNumber { get; init; }
            public string DescendantNumber { get; init; }
        }
    }
}
