using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Authorization;
using Abp.Runtime.Session;
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
using AqualLifeStyle.Email;
using AqualLifeStyle.MultiTenancy;
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

        private static readonly EntryProgrammeTerms LegacySplitPaymentTerms =
            EntryProgrammeTerms.Create(
                "entry-2026-07",
                EffectiveFrom,
                registrationPaymentAmount: 600m,
                activationPaymentAmount: 600m,
                monthlyCommitmentAmount: 600m,
                gracePeriodDays: 7);

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
        public async Task Administrator_CanReviewMonthlyObligationCheckoutReconciliationQueue()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userId = await CreateTestUserAsync(
                1,
                $"monthly-reconciliation-{suffix}",
                $"monthly-reconciliation-{suffix}@example.com");
            var checkoutIds = await UsingDbContextAsync(1, async context =>
            {
                var customer = Customer.Create(
                    1,
                    userId,
                    $"Monthly Reconciliation {suffix}",
                    new EmailAddress($"monthly-reconciliation-customer-{suffix}@example.com"));
                context.Customers.Add(customer);
                await context.SaveChangesAsync();

                var participation = EntryParticipation.StartIndependently(
                    1,
                    customer.Id,
                    LegacySplitPaymentTerms,
                    EffectiveFrom);
                context.EntryParticipations.Add(participation);
                var activationPayments = Activate(
                    participation,
                    customer.Id,
                    $"monthly-reconciliation-{suffix}");
                context.MemberPayments.AddRange(activationPayments);
                await context.SaveChangesAsync();

                var allocatedObligation = EntryMonthlyObligation.Create(
                    participation,
                    2026,
                    7,
                    new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),
                    "monthly-policy-v1");
                var unallocatedObligation = EntryMonthlyObligation.Create(
                    participation,
                    2026,
                    8,
                    new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc),
                    "monthly-policy-v1");
                context.EntryMonthlyObligationDuePolicies.Add(
                    EntryMonthlyObligationDuePolicy.Create(
                        "monthly-policy-v1",
                        10,
                        EntryMonthlyObligationDuePolicy
                            .JohannesburgMonthStartUtc(2026, 7)));
                var payment = MemberPayment.CreatePending(
                    1,
                    customer.Id,
                    MemberPaymentPurpose.EntryMonthlyCommitment,
                    600m,
                    "Test",
                    $"monthly-payment-{suffix}",
                    EffectiveFrom);
                payment.Confirm(EffectiveFrom.AddMinutes(1));
                var unallocatedPayment = MemberPayment.CreatePending(
                    1,
                    customer.Id,
                    MemberPaymentPurpose.EntryMonthlyCommitment,
                    600m,
                    "Test",
                    $"monthly-payment-unallocated-{suffix}",
                    EffectiveFrom);
                unallocatedPayment.Confirm(EffectiveFrom.AddMinutes(1));

                var allocatedCheckout = AQGreenMonthlyObligationCheckout.Create(
                    allocatedObligation,
                    EffectiveFrom);
                allocatedCheckout.RecordCheckout(
                    $"ch_monthly_allocated_{suffix}",
                    $"https://payments.example.test/ch_monthly_allocated_{suffix}",
                    EffectiveFrom.AddMinutes(1));
                var unallocatedCheckout = AQGreenMonthlyObligationCheckout.Create(
                    unallocatedObligation,
                    EffectiveFrom);
                unallocatedCheckout.RecordCheckout(
                    $"ch_monthly_unallocated_{suffix}",
                    $"https://payments.example.test/ch_monthly_unallocated_{suffix}",
                    EffectiveFrom.AddMinutes(1));

                allocatedObligation.ApplyConfirmedPayment(payment);
                allocatedCheckout.CompleteAllocation(payment.Id, EffectiveFrom.AddMinutes(2));
                unallocatedCheckout.RequireReconciliation(
                    unallocatedPayment.Id,
                    EffectiveFrom.AddMinutes(2),
                    $"The monthly obligation for {suffix} could not be settled.");

                context.MemberPayments.Add(payment);
                context.MemberPayments.Add(unallocatedPayment);
                context.EntryMonthlyObligations.AddRange(
                    allocatedObligation,
                    unallocatedObligation);
                context.AQGreenMonthlyObligationCheckouts.AddRange(
                    allocatedCheckout,
                    unallocatedCheckout);
                await context.SaveChangesAsync();
                return new[] { allocatedCheckout.Id, unallocatedCheckout.Id };
            });
            LoginAsHostAdmin();

            var result = await _service.GetMonthlyObligationCheckoutReconciliationAsync(
                new MonthlyObligationCheckoutReconciliationListInput
                {
                    PeriodMonth = 7,
                    MaxResultCount = 50
                });

            result.TotalCount.ShouldBe(1);
            var allocated = result.Items.Single();
            allocated.CheckoutId.ShouldBe(checkoutIds[0]);
            allocated.TenantId.ShouldBe(1);
            allocated.AreaName.ShouldBe("Default");
            allocated.ClubMemberNumber.ShouldStartWith("CLB-");
            allocated.CustomerName.ShouldBe($"Monthly Reconciliation {suffix}");
            allocated.PeriodYear.ShouldBe(2026);
            allocated.PeriodMonth.ShouldBe(7);
            allocated.Amount.ShouldBe(600m);
            allocated.Currency.ShouldBe("ZAR");
            allocated.Status.ShouldBe(HostedPaymentCheckoutStatus.Completed);
            allocated.ProviderCheckoutId.ShouldBe(
                $"ch_monthly_allocated_{suffix}");
            allocated.AllocationStatus.ShouldBe(
                AQGreenMonthlyPaymentAllocationStatus.Allocated);
            allocated.AllocationEvidence.ShouldBeNull();
            allocated.CompletedAt.ShouldNotBeNull();
            allocated.IsPaymentAllocated.ShouldBeTrue();
            allocated.RecordedObligationStatus.ShouldBe(
                EntryMonthlyObligationStatus.Paid);
            allocated.PaymentId.ShouldNotBeNull();
            allocated.ProviderPaymentReference.ShouldBe(
                $"monthly-payment-{suffix}");

            var all = await _service.GetMonthlyObligationCheckoutReconciliationAsync(
                new MonthlyObligationCheckoutReconciliationListInput
                {
                    MaxResultCount = 50
                });
            all.TotalCount.ShouldBe(2);
            var unallocated = all.Items.Single(item => item.CheckoutId == checkoutIds[1]);
            unallocated.AllocationStatus.ShouldBe(
                AQGreenMonthlyPaymentAllocationStatus.ReconciliationRequired);
            unallocated.AllocationEvidence.ShouldContain(suffix);
            unallocated.IsPaymentAllocated.ShouldBeFalse();
            unallocated.RecordedObligationStatus.ShouldBe(
                EntryMonthlyObligationStatus.Due);
            unallocated.ProviderPaymentReference.ShouldBe(
                $"monthly-payment-unallocated-{suffix}");
        }

        [Fact]
        public async Task TenantAdministrator_CannotRequestAnotherAreasMonthlyObligationReconciliation()
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                _service.GetMonthlyObligationCheckoutReconciliationAsync(
                    new MonthlyObligationCheckoutReconciliationListInput
                    {
                        TenantId = 2
                    }));
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
        public async Task HostCorrectorWithoutAllAreasPermission_CannotLookupAreaMembers()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userName = $"host-programme-corrector-{suffix}";
            var userId = await CreateTestUserAsync(
                null,
                userName,
                $"{userName}@example.com");
            await UsingDbContextAsync(null, async context =>
            {
                var role = new Role(
                    null,
                    $"ProgrammeCorrector-{suffix}",
                    $"Programme Corrector {suffix}");
                context.Roles.Add(role);
                await context.SaveChangesAsync();

                context.UserRoles.RemoveRange(
                    context.UserRoles.Where(userRole => userRole.UserId == userId));
                context.UserRoles.Add(new UserRole(null, userId, role.Id));
                context.Permissions.Add(new RolePermissionSetting
                {
                    TenantId = null,
                    Name = AquaPermissions.Admin.ProgrammeParticipations.CorrectRecruiter,
                    IsGranted = true,
                    RoleId = role.Id
                });
                await context.SaveChangesAsync();
            });
            LoginAsHost(userName);

            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                _service.CorrectRecruiterAsync(new CorrectProgrammeRecruiterInput
                {
                    Programme = AdminProgrammeType.Entry,
                    ClubMemberNumber = "CLB-NOTFOUND01",
                    Reason = "Verifying cross-Area authorization"
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

        [Fact]
        public async Task HostAdministrator_CanCorrectAQGreenRecruiterAcrossAreas()
        {
            var fixture = await CreateCrossAreaAQGreenPlacementAsync();
            LoginAsHostAdmin();

            await _service.CorrectRecruiterAsync(new CorrectProgrammeRecruiterInput
            {
                Programme = AdminProgrammeType.Entry,
                ClubMemberNumber = fixture.TargetNumber,
                NewRecruiterClubMemberNumber = fixture.RecruiterNumber,
                Reason = "Correcting a verified cross-Area placement"
            });

            await UsingDbContextAsync(1, async context =>
            {
                var participation = await context.EntryParticipations
                    .Include(item => item.RecruiterCorrections)
                    .SingleAsync(item => item.CustomerId == fixture.TargetCustomerId);
                participation.RecruiterCustomerId.ShouldBe(fixture.RecruiterCustomerId);
                participation.RecruiterCorrections.Count.ShouldBe(1);
            });
        }

        [Fact]
        public async Task TenantAdministrator_CannotCorrectRecruiterAcrossAreas()
        {
            var fixture = await CreateCrossAreaAQGreenPlacementAsync();

            var exception = await Should.ThrowAsync<Abp.UI.UserFriendlyException>(() =>
                _service.CorrectRecruiterAsync(new CorrectProgrammeRecruiterInput
                {
                    Programme = AdminProgrammeType.Entry,
                    ClubMemberNumber = fixture.TargetNumber,
                    NewRecruiterClubMemberNumber = fixture.RecruiterNumber,
                    Reason = "Attempting an unauthorised cross-Area placement"
                }));
            exception.Details.ShouldContain("management authority");

            await UsingDbContextAsync(1, async context =>
            {
                var participation = await context.EntryParticipations
                    .Include(item => item.RecruiterCorrections)
                    .SingleAsync(item => item.CustomerId == fixture.TargetCustomerId);
                participation.RecruiterCustomerId.ShouldBeNull();
                participation.RecruiterCorrections.ShouldBeEmpty();
            });
        }

        [Fact]
        public async Task HostAdministrator_CanCorrectOnyxRecruiterAcrossAreas()
        {
            var fixture = await CreateCrossAreaOnyxPlacementAsync();
            LoginAsHostAdmin();

            await _service.CorrectRecruiterAsync(new CorrectProgrammeRecruiterInput
            {
                Programme = AdminProgrammeType.Onyx,
                ClubMemberNumber = fixture.TargetNumber,
                NewRecruiterClubMemberNumber = fixture.RecruiterNumber,
                Reason = "Correcting a verified cross-Area Onyx placement"
            });

            await UsingDbContextAsync(1, async context =>
            {
                var participation = await context.OnyxParticipations
                    .Include(item => item.RecruiterCorrections)
                    .SingleAsync(item => item.CustomerId == fixture.TargetCustomerId);
                participation.RecruiterCustomerId.ShouldBe(fixture.RecruiterCustomerId);
                participation.RecruiterCorrections.Count.ShouldBe(1);
            });
        }

        [Fact]
        public async Task Administrator_CanInspectAndIdempotentlyTerminateLockedAQGreenCheckout()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var checkoutId = await CreateLockedAQGreenCheckoutAsync(suffix);

            var locked = await _service.GetAQGreenJoiningCheckoutsAsync(
                new AQGreenJoiningCheckoutListInput
                {
                    Keyword = suffix,
                    MaxResultCount = 20
                });

            locked.TotalCount.ShouldBe(1);
            var evidence = locked.Items.Single();
            evidence.CheckoutId.ShouldBe(checkoutId);
            evidence.Amount.ShouldBe(600m);
            evidence.PaymentId.ShouldBeNull();
            evidence.ProviderCheckoutId.ShouldContain(suffix);
            evidence.LockReason.ShouldContain("authoritative provider confirmation");

            var input = new TerminateAQGreenJoiningCheckoutInput
            {
                CheckoutId = checkoutId,
                Evidence = "Authorised Yoco support review confirmed the checkout is no longer payable"
            };
            await _service.TerminateAQGreenJoiningCheckoutAsync(input);
            await _service.TerminateAQGreenJoiningCheckoutAsync(input);

            await UsingDbContextAsync(1, async context =>
            {
                var checkout = await context.AQGreenJoiningCheckouts.SingleAsync(
                    item => item.Id == checkoutId);
                checkout.Status.ShouldBe(
                    HostedPaymentCheckoutStatus.AdministrativelyTerminated);
                checkout.TerminatedByAdministratorUserId.ShouldNotBeNull();
                checkout.TerminatedAt.ShouldNotBeNull();
                checkout.TerminalEvidence.ShouldBe(input.Evidence);
                (await context.MemberPayments.AnyAsync()).ShouldBeFalse();
            });
        }

        [Fact]
        public async Task Administrator_CanApproveAwaitingAQGreenParticipation()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var fixture = await CreateAwaitingApprovalEntryAsync(suffix);

            var before = await _service.GetPendingApprovalSummaryAsync(
                new PendingProgrammeApprovalSummaryInput());
            before.AQGreenCount.ShouldBe(1);
            before.TotalCount.ShouldBe(1);

            var queue = await _service.GetAllAsync(
                new AdminProgrammeParticipationListInput
                {
                    Programme = AdminProgrammeType.Entry,
                    AwaitingApprovalOnly = true,
                    MaxResultCount = 20
                });
            queue.Items.Single().ParticipationId.ShouldBe(fixture.ParticipationId);

            await _service.ApproveProgrammeParticipationAsync(
                new ApproveProgrammeParticipationInput
                {
                    Programme = AdminProgrammeType.Entry,
                    ParticipationId = fixture.ParticipationId
                });
            await _service.ApproveProgrammeParticipationAsync(
                new ApproveProgrammeParticipationInput
                {
                    Programme = AdminProgrammeType.Entry,
                    ParticipationId = fixture.ParticipationId
                });

            var after = await _service.GetPendingApprovalSummaryAsync(
                new PendingProgrammeApprovalSummaryInput());
            after.TotalCount.ShouldBe(0);

            await UsingDbContextAsync(1, async context =>
            {
                var participation = await context.EntryParticipations
                    .SingleAsync(item => item.Id == fixture.ParticipationId);
                participation.Status.ShouldBe(EntryParticipationStatus.Active);
                participation.ActivatedAt.ShouldNotBeNull();

                var decision = await context.EntryParticipationApprovalDecisions
                    .SingleAsync(item =>
                        EF.Property<Guid>(item, "EntryParticipationId") ==
                        fixture.ParticipationId);
                decision.Approved.ShouldBeTrue();
                decision.Reason.ShouldBeNull();
                decision.AdministratorUserId.ShouldBe(AbpSession.GetUserId());

                var user = await context.Users.SingleAsync(candidate => candidate.Id == fixture.UserId);
                user.Role.ShouldBe(AquaUserRole.Member);

                (await context.TransactionalEmailOutboxMessages.CountAsync(message =>
                    message.NotificationType == "ParticipationDecision" &&
                    message.IdempotencyKey ==
                        $"Entry:{fixture.ParticipationId}:approved")).ShouldBe(1);
                (await context.EntryParticipationApprovalDecisions.CountAsync(item =>
                    EF.Property<Guid>(item, "EntryParticipationId") ==
                    fixture.ParticipationId)).ShouldBe(1);
            });
        }

        [Fact]
        public async Task Administrator_CanRejectAwaitingOnyxParticipationWithReason()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var fixture = await CreateAwaitingApprovalOnyxAsync(suffix);
            (await _service.GetPendingApprovalSummaryAsync(
                new PendingProgrammeApprovalSummaryInput())).OnyxCount.ShouldBe(1);

            await _service.RejectProgrammeParticipationAsync(
                new RejectProgrammeParticipationInput
                {
                    Programme = AdminProgrammeType.Onyx,
                    ParticipationId = fixture.ParticipationId,
                    Reason = "Signed joining form does not match the payer"
                });
            await _service.RejectProgrammeParticipationAsync(
                new RejectProgrammeParticipationInput
                {
                    Programme = AdminProgrammeType.Onyx,
                    ParticipationId = fixture.ParticipationId,
                    Reason = "Signed joining form does not match the payer"
                });

            (await _service.GetPendingApprovalSummaryAsync(
                new PendingProgrammeApprovalSummaryInput())).TotalCount.ShouldBe(0);

            await UsingDbContextAsync(1, async context =>
            {
                var participation = await context.OnyxParticipations
                    .SingleAsync(item => item.Id == fixture.ParticipationId);
                participation.Status.ShouldBe(OnyxParticipationStatus.Rejected);
                participation.ActivatedAt.ShouldBeNull();

                var decision = await context.OnyxParticipationApprovalDecisions
                    .SingleAsync(item =>
                        EF.Property<Guid>(item, "OnyxParticipationId") ==
                        fixture.ParticipationId);
                decision.Approved.ShouldBeFalse();
                decision.Reason.ShouldBe("Signed joining form does not match the payer");

                var user = await context.Users.SingleAsync(candidate => candidate.Id == fixture.UserId);
                user.Role.ShouldBe(AquaUserRole.Guest);

                var outcome = await context.TransactionalEmailOutboxMessages.SingleAsync(message =>
                    message.NotificationType == "ParticipationDecision" &&
                    message.IdempotencyKey ==
                        $"Onyx:{fixture.ParticipationId}:declined");
                Resolve<ITransactionalEmailBodyProtector>()
                    .Unprotect(outcome.TextBody)
                    .ShouldContain("Signed joining form does not match the payer");
                (await context.OnyxParticipationApprovalDecisions.CountAsync(item =>
                    EF.Property<Guid>(item, "OnyxParticipationId") ==
                    fixture.ParticipationId)).ShouldBe(1);
            });
        }

        [Fact]
        public async Task TenantAdministrator_CannotApproveAnotherAreasParticipationByIdentifier()
        {
            await EnsureCrossAreaTenantAsync();
            var fixture = await CreateAwaitingApprovalEntryAsync(
                Guid.NewGuid().ToString("N"),
                tenantId: 2);

            var exception = await Should.ThrowAsync<Abp.UI.UserFriendlyException>(() =>
                _service.ApproveProgrammeParticipationAsync(
                    new ApproveProgrammeParticipationInput
                    {
                        Programme = AdminProgrammeType.Entry,
                        ParticipationId = fixture.ParticipationId
                    }));
            exception.Details.ShouldContain("not found in your Area");

            await UsingDbContextAsync(2, async context =>
            {
                var participation = await context.EntryParticipations.SingleAsync(
                    item => item.Id == fixture.ParticipationId);
                participation.Status.ShouldBe(
                    EntryParticipationStatus.PaymentConfirmedAwaitingApproval);
                (await context.EntryParticipationApprovalDecisions.AnyAsync(item =>
                    EF.Property<Guid>(item, "EntryParticipationId") ==
                    fixture.ParticipationId)).ShouldBeFalse();
            });
        }

        [Fact]
        public async Task Administrator_CannotRejectAnAlreadyActiveParticipation()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userId = await CreateTestUserAsync(
                1,
                $"approval-active-{suffix}",
                $"approval-active-{suffix}@example.com");
            var participationId = await UsingDbContextAsync(1, async context =>
            {
                var customer = Customer.Create(
                    1,
                    userId,
                    $"Approval Active {suffix}",
                    new EmailAddress($"approval-active-customer-{suffix}@example.com"));
                var membership = Membership.Create(
                    1,
                    $"Onyx-active-{suffix}",
                    "Onyx active test",
                    MembershipType.Onyx);
                context.Customers.Add(customer);
                context.Memberships.Add(membership);
                await context.SaveChangesAsync();

                var onyx = OnyxParticipation.StartDirectIndependently(
                    1,
                    customer.Id,
                    membership.Id,
                    OnyxPlanTerms.Create("2026-07", EffectiveFrom, 6120m),
                    EffectiveFrom);
                context.OnyxParticipations.Add(onyx);
                await context.SaveChangesAsync();

                var payment = MemberPayment.CreatePending(
                    1,
                    customer.Id,
                    MemberPaymentPurpose.OnyxDirectEntry,
                    6120m,
                    "Test",
                    $"onyx-active-{suffix}",
                    EffectiveFrom);
                payment.Confirm(EffectiveFrom.AddMinutes(1));
                onyx.ApplyConfirmedDirectEntryPayment(payment);
                onyx.ApproveByAdministrator(1L, EffectiveFrom.AddMinutes(2));
                context.MemberPayments.Add(payment);
                await context.SaveChangesAsync();
                return onyx.Id;
            });

            await Should.ThrowAsync<InvalidOperationException>(() =>
                _service.RejectProgrammeParticipationAsync(
                    new RejectProgrammeParticipationInput
                    {
                        Programme = AdminProgrammeType.Onyx,
                        ParticipationId = participationId,
                        Reason = "Unexpected decline"
                    }));
        }

        [Fact]
        public async Task HostReviewerWithoutAllAreasPermission_CannotApproveAnotherAreasParticipation()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userName = $"host-approval-reviewer-{suffix}";
            var userId = await CreateTestUserAsync(
                null,
                userName,
                $"{userName}@example.com");
            await UsingDbContextAsync(null, async context =>
            {
                var role = new Role(
                    null,
                    $"ApprovalReviewer-{suffix}",
                    $"Approval Reviewer {suffix}");
                context.Roles.Add(role);
                await context.SaveChangesAsync();
                context.UserRoles.RemoveRange(
                    context.UserRoles.Where(userRole => userRole.UserId == userId));
                context.UserRoles.Add(new UserRole(null, userId, role.Id));
                context.Permissions.AddRange(new[]
                {
                    new RolePermissionSetting
                    {
                        TenantId = null,
                        Name = AquaPermissions.Admin.ProgrammeParticipations.Approve,
                        IsGranted = true,
                        RoleId = role.Id
                    }
                });
                await context.SaveChangesAsync();
            });

            var fixture = await CreateAwaitingApprovalEntryAsync(suffix);

            AbpSession.TenantId = null;
            AbpSession.UserId = userId;

            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                _service.ApproveProgrammeParticipationAsync(
                    new ApproveProgrammeParticipationInput
                    {
                        Programme = AdminProgrammeType.Entry,
                        ParticipationId = fixture.ParticipationId
                    }));
        }

        [Fact]
        public async Task TenantReviewerWithoutApprovalPermission_CannotApproveParticipation()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var fixture = await CreateAwaitingApprovalEntryAsync(suffix);
            var userName = $"tenant-approval-reviewer-{suffix}";
            var reviewerId = await CreateTestUserAsync(
                1,
                userName,
                $"{userName}@example.com");
            await UsingDbContextAsync(1, async context =>
            {
                var role = new Role(
                    1,
                    $"ApprovalViewer-{suffix}",
                    $"Approval Viewer {suffix}");
                context.Roles.Add(role);
                await context.SaveChangesAsync();
                context.UserRoles.RemoveRange(context.UserRoles.Where(userRole =>
                    userRole.UserId == reviewerId));
                context.UserRoles.Add(new UserRole(1, reviewerId, role.Id));
                context.Permissions.Add(new RolePermissionSetting
                {
                    TenantId = 1,
                    Name = AquaPermissions.Admin.ProgrammeParticipations.View,
                    IsGranted = true,
                    RoleId = role.Id
                });
                await context.SaveChangesAsync();
            });

            AbpSession.TenantId = 1;
            AbpSession.UserId = reviewerId;

            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                _service.ApproveProgrammeParticipationAsync(
                    new ApproveProgrammeParticipationInput
                    {
                        Programme = AdminProgrammeType.Entry,
                        ParticipationId = fixture.ParticipationId
                    }));
        }

        private async Task<Guid> CreateLockedAQGreenCheckoutAsync(string suffix)
        {
            var userId = await CreateTestUserAsync(
                1,
                $"checkout-recovery-{suffix}",
                $"checkout-recovery-{suffix}@example.com");
            return await UsingDbContextAsync(1, async context =>
            {
                var customer = Customer.Create(
                    1,
                    userId,
                    $"Checkout Recovery {suffix}",
                    new EmailAddress($"checkout-recovery-customer-{suffix}@example.com"));
                context.Customers.Add(customer);
                await context.SaveChangesAsync();
                var terms = EntryProgrammeTerms.CreateFlexibleJoiningPayment(
                    $"checkout-recovery-{suffix}",
                    EffectiveFrom,
                    1200m,
                    600m,
                    600m,
                    7);
                var participation = EntryParticipation.StartIndependently(
                    1,
                    customer.Id,
                    terms,
                    EffectiveFrom);
                participation.SelectJoiningPaymentSchedule(
                    AQGreenJoiningPaymentSchedule.TwoInstallments);
                var checkout = AQGreenJoiningCheckout.Create(
                    1,
                    participation.Id,
                    customer.Id,
                    AQGreenJoiningPaymentSchedule.TwoInstallments,
                    AQGreenJoiningPaymentStage.FirstInstallment,
                    600m,
                    "ZAR",
                    EffectiveFrom);
                checkout.RecordCheckout(
                    $"ch_recovery_{suffix}",
                    $"https://payments.example.test/ch_recovery_{suffix}",
                    EffectiveFrom.AddMinutes(1));
                context.EntryParticipations.Add(participation);
                context.AQGreenJoiningCheckouts.Add(checkout);
                await context.SaveChangesAsync();
                return checkout.Id;
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
                    LegacySplitPaymentTerms,
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

                var terms = LegacySplitPaymentTerms;
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

        private async Task<RecruitmentNetworkFixture> CreateCrossAreaAQGreenPlacementAsync()
        {
            var suffix = Guid.NewGuid().ToString("N");
            await EnsureCrossAreaTenantAsync();
            var targetUserId = await CreateTestUserAsync(
                1,
                $"cross-area-target-{suffix}",
                $"cross-area-target-{suffix}@example.com");
            var recruiterUserId = await CreateTestUserAsync(
                2,
                $"cross-area-recruiter-{suffix}",
                $"cross-area-recruiter-{suffix}@example.com");

            Customer targetCustomer = null;
            await UsingDbContextAsync(1, async context =>
            {
                targetCustomer = Customer.Create(
                    1,
                    targetUserId,
                    "Cross Area Target",
                    new EmailAddress($"cross-area-target-customer-{suffix}@example.com"));
                context.Customers.Add(targetCustomer);
                await context.SaveChangesAsync();
                context.EntryParticipations.Add(EntryParticipation.StartIndependently(
                    1,
                    targetCustomer.Id,
                    LegacySplitPaymentTerms,
                    EffectiveFrom));
                await context.SaveChangesAsync();
            });

            Customer recruiterCustomer = null;
            await UsingDbContextAsync(2, async context =>
            {
                recruiterCustomer = Customer.Create(
                    2,
                    recruiterUserId,
                    "Cross Area Recruiter",
                    new EmailAddress($"cross-area-recruiter-customer-{suffix}@example.com"));
                context.Customers.Add(recruiterCustomer);
                await context.SaveChangesAsync();
                var recruiter = EntryParticipation.StartIndependently(
                    2,
                    recruiterCustomer.Id,
                    LegacySplitPaymentTerms,
                    EffectiveFrom);
                context.MemberPayments.AddRange(Activate(
                    recruiter,
                    recruiterCustomer.Id,
                    $"cross-area-recruiter-{suffix}",
                    2));
                context.EntryParticipations.Add(recruiter);
                await context.SaveChangesAsync();
            });

            return new RecruitmentNetworkFixture
            {
                RecruiterCustomerId = recruiterCustomer.Id,
                RecruiterNumber = recruiterCustomer.ClubMemberNumber,
                TargetCustomerId = targetCustomer.Id,
                TargetNumber = targetCustomer.ClubMemberNumber
            };
        }

        private async Task<RecruitmentNetworkFixture> CreateCrossAreaOnyxPlacementAsync()
        {
            var suffix = Guid.NewGuid().ToString("N");
            await EnsureCrossAreaTenantAsync();
            var targetUserId = await CreateTestUserAsync(
                1,
                $"cross-area-onyx-target-{suffix}",
                $"cross-area-onyx-target-{suffix}@example.com");
            var recruiterUserId = await CreateTestUserAsync(
                2,
                $"cross-area-onyx-recruiter-{suffix}",
                $"cross-area-onyx-recruiter-{suffix}@example.com");
            var terms = Resolve<ICurrentProgrammeTermsProvider>().GetDirectOnyxTerms();

            Customer targetCustomer = null;
            await UsingDbContextAsync(1, async context =>
            {
                targetCustomer = Customer.Create(
                    1,
                    targetUserId,
                    "Cross Area Onyx Target",
                    new EmailAddress($"cross-area-onyx-target-customer-{suffix}@example.com"));
                var membership = Membership.Create(
                    1,
                    $"Cross-Area-Onyx-Target-{suffix}",
                    "Cross-Area Onyx correction test",
                    MembershipType.Onyx);
                context.Customers.Add(targetCustomer);
                context.Memberships.Add(membership);
                await context.SaveChangesAsync();
                var target = OnyxParticipation.StartDirectIndependently(
                    1,
                    targetCustomer.Id,
                    membership.Id,
                    terms,
                    EffectiveFrom);
                context.MemberPayments.Add(Activate(
                    target,
                    targetCustomer.Id,
                    $"cross-area-onyx-target-{suffix}"));
                context.OnyxParticipations.Add(target);
                await context.SaveChangesAsync();
            });

            Customer recruiterCustomer = null;
            await UsingDbContextAsync(2, async context =>
            {
                recruiterCustomer = Customer.Create(
                    2,
                    recruiterUserId,
                    "Cross Area Onyx Recruiter",
                    new EmailAddress($"cross-area-onyx-recruiter-customer-{suffix}@example.com"));
                var membership = Membership.Create(
                    2,
                    $"Cross-Area-Onyx-Recruiter-{suffix}",
                    "Cross-Area Onyx correction test",
                    MembershipType.Onyx);
                context.Customers.Add(recruiterCustomer);
                context.Memberships.Add(membership);
                await context.SaveChangesAsync();
                var recruiter = OnyxParticipation.StartDirectIndependently(
                    2,
                    recruiterCustomer.Id,
                    membership.Id,
                    terms,
                    EffectiveFrom);
                context.MemberPayments.Add(Activate(
                    recruiter,
                    recruiterCustomer.Id,
                    $"cross-area-onyx-recruiter-{suffix}",
                    2));
                context.OnyxParticipations.Add(recruiter);
                await context.SaveChangesAsync();
            });

            return new RecruitmentNetworkFixture
            {
                RecruiterCustomerId = recruiterCustomer.Id,
                RecruiterNumber = recruiterCustomer.ClubMemberNumber,
                TargetCustomerId = targetCustomer.Id,
                TargetNumber = targetCustomer.ClubMemberNumber
            };
        }

        private async Task EnsureCrossAreaTenantAsync()
        {
            await UsingDbContextAsync(null, async context =>
            {
                if (await context.Tenants.AnyAsync(tenant => tenant.Id == 2)) return;
                context.Tenants.Add(new Tenant("CrossArea", "Cross Area"));
                await context.SaveChangesAsync();
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
            string reference,
            int tenantId = 1)
        {
            var registration = MemberPayment.CreatePending(tenantId, customerId, MemberPaymentPurpose.EntryRegistration, 600m, "Test", $"{reference}-registration", EffectiveFrom);
            registration.Confirm(EffectiveFrom.AddMinutes(1));
            participation.ApplyConfirmedActivationPayment(registration);
            var activation = MemberPayment.CreatePending(tenantId, customerId, MemberPaymentPurpose.EntryActivation, 600m, "Test", $"{reference}-activation", EffectiveFrom);
            activation.Confirm(EffectiveFrom.AddMinutes(2));
            participation.ApplyConfirmedActivationPayment(activation);
            participation.ApproveByAdministrator(1L, EffectiveFrom.AddMinutes(3));
            return new[] { registration, activation };
        }

        private static MemberPayment Activate(
            OnyxParticipation participation,
            int customerId,
            string reference,
            int tenantId = 1)
        {
            var payment = MemberPayment.CreatePending(
                tenantId,
                customerId,
                MemberPaymentPurpose.OnyxDirectEntry,
                6120m,
                "Test",
                reference,
                EffectiveFrom);
            payment.Confirm(EffectiveFrom.AddMinutes(1));
            participation.ApplyConfirmedDirectEntryPayment(payment);
            participation.ApproveByAdministrator(1L, EffectiveFrom.AddMinutes(2));
            return payment;
        }

        private async Task<ApprovalFixture> CreateAwaitingApprovalEntryAsync(
            string suffix,
            int tenantId = 1)
        {
            var userId = await CreateTestUserAsync(
                tenantId,
                $"approval-entry-{suffix}",
                $"approval-entry-{suffix}@example.com");
            return await UsingDbContextAsync(tenantId, async context =>
            {
                var customer = Customer.Create(
                    tenantId,
                    userId,
                    $"Approval Entry {suffix}",
                    new EmailAddress($"approval-entry-customer-{suffix}@example.com"));
                context.Customers.Add(customer);
                await context.SaveChangesAsync();

                var participation = EntryParticipation.StartIndependently(
                    tenantId,
                    customer.Id,
                    LegacySplitPaymentTerms,
                    EffectiveFrom);
                context.EntryParticipations.Add(participation);
                await context.SaveChangesAsync();

                var registration = MemberPayment.CreatePending(
                    tenantId,
                    customer.Id,
                    MemberPaymentPurpose.EntryRegistration,
                    600m,
                    "Test",
                    $"approval-entry-registration-{suffix}",
                    EffectiveFrom);
                registration.Confirm(EffectiveFrom.AddMinutes(1));
                participation.ApplyConfirmedActivationPayment(registration);
                var activation = MemberPayment.CreatePending(
                    tenantId,
                    customer.Id,
                    MemberPaymentPurpose.EntryActivation,
                    600m,
                    "Test",
                    $"approval-entry-activation-{suffix}",
                    EffectiveFrom);
                activation.Confirm(EffectiveFrom.AddMinutes(2));
                participation.ApplyConfirmedActivationPayment(activation);
                context.MemberPayments.AddRange(registration, activation);
                await context.SaveChangesAsync();

                return new ApprovalFixture
                {
                    UserId = userId,
                    ParticipationId = participation.Id
                };
            });
        }

        private async Task<ApprovalFixture> CreateAwaitingApprovalOnyxAsync(string suffix)
        {
            var userId = await CreateTestUserAsync(
                1,
                $"approval-onyx-{suffix}",
                $"approval-onyx-{suffix}@example.com");
            return await UsingDbContextAsync(1, async context =>
            {
                var customer = Customer.Create(
                    1,
                    userId,
                    $"Approval Onyx {suffix}",
                    new EmailAddress($"approval-onyx-customer-{suffix}@example.com"));
                var membership = Membership.Create(
                    1,
                    $"Onyx-approval-{suffix}",
                    "Onyx approval test",
                    MembershipType.Onyx);
                context.Customers.Add(customer);
                context.Memberships.Add(membership);
                await context.SaveChangesAsync();

                var onyx = OnyxParticipation.StartDirectIndependently(
                    1,
                    customer.Id,
                    membership.Id,
                    OnyxPlanTerms.Create("2026-07", EffectiveFrom, 6120m),
                    EffectiveFrom);
                context.OnyxParticipations.Add(onyx);
                await context.SaveChangesAsync();

                var payment = MemberPayment.CreatePending(
                    1,
                    customer.Id,
                    MemberPaymentPurpose.OnyxDirectEntry,
                    6120m,
                    "Test",
                    $"onyx-approval-{suffix}",
                    EffectiveFrom);
                payment.Confirm(EffectiveFrom.AddMinutes(1));
                onyx.ApplyConfirmedDirectEntryPayment(payment);
                context.MemberPayments.Add(payment);
                await context.SaveChangesAsync();

                return new ApprovalFixture
                {
                    UserId = userId,
                    ParticipationId = onyx.Id
                };
            });
        }

        private sealed class RecruitmentNetworkFixture
        {
            public int RecruiterCustomerId { get; init; }
            public string RecruiterNumber { get; init; }
            public int TargetCustomerId { get; init; }
            public string TargetNumber { get; init; }
            public string DescendantNumber { get; init; }
        }

        private sealed class ApprovalFixture
        {
            public long UserId { get; init; }
            public Guid ParticipationId { get; init; }
        }
    }
}
