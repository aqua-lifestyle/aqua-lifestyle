using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Transactions;
using Abp.Domain.Uow;
using Abp.Runtime.Session;
using Abp.UI;
using Abp.Authorization.Users;
using AqualLifeStyle.Application.Admin.Commissions;
using AqualLifeStyle.Application.ProgrammeParticipations;
using AqualLifeStyle.Application.ProgrammeParticipations.Dto;
using AqualLifeStyle.Application.Recruitment;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.Payments;
using AqualLifeStyle.Payments.Yoco;
using AqualLifeStyle.Authorization.Users;
using Castle.MicroKernel.Registration;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Web.Tests.Integration
{
    public sealed partial class AQGreenV2ContinuousFreshNetworkE2ETests
    {
        [IsolatedPostgreSqlFact]
        public async Task LeanMvp_FirstTimeInvitationJoin_ToUnreleasedLedger_WithoutLoansOrOnyx()
        {
            // This proof must never silently pass without executing its PG body.
            IsPostgreSqlRegressionMode().ShouldBeTrue("Run this proof explicitly with disposable PostgreSQL.");
            var version = await AssertFreshPostgreSqlDatabaseAsync();
            IocManager.IocContainer.Register(Component.For<IYocoCheckoutGateway>()
                .Instance(new LeanMvpCheckoutGateway()).IsDefault());
            var joiningGate = (AQGreenV2TestJoiningGate)Resolve<IAQGreenPlacementV2JoiningGate>();
            (await joiningGate.IsEnabledAsync(TenantId)).ShouldBeFalse();
            LoginAsDefaultTenantAdmin();
            var root = await SeedAwaitingPaymentParticipantAsync(
                EntryProgrammeTerms.CreateSingleJoiningPayment("lean-test-root", DateTime.UtcNow.AddDays(-1), 1200m, 600m, 7),
                null, "lean-root", authorisedProspectiveRoot: true);
            await ConfirmJoiningPaymentAsync(root);
            LoginAsHostAdmin();
            using (ApprovalGate.Enable(root.ParticipationId)) await ApproveAsync(root.ParticipationId);
            await AssertActiveApprovalAsync(root, isRoot: true);

            var children = new List<ParticipantFixture>();
            using (joiningGate.Enable(TenantId))
            {
                (await joiningGate.IsEnabledAsync(TenantId + 1)).ShouldBeFalse();
                using (AbpSession.Use(TenantId, root.UserId))
                {
                    var ownInvitation = (await InUnitOfWorkAsync(() => Resolve<IProgrammeInvitationAppService>()
                        .GetMyInvitationsAsync())).Invitations.Single();
                    await Should.ThrowAsync<UserFriendlyException>(() =>
                        StartLeanAsync(
                            new StartEntryParticipationInput { InviteCode = ownInvitation.Code }));
                }
                for (var slot = 1; slot <= 5; slot++)
                {
                    string code;
                    using (AbpSession.Use(TenantId, root.UserId))
                        code = (await InUnitOfWorkAsync(() => Resolve<IProgrammeInvitationAppService>()
                            .GetMyInvitationsAsync())).Invitations.Single().Code;
                    var member = await SeedLeanMemberIdentityAsync(root.AreaId, $"lean-{slot}");
                    using (AbpSession.Use(TenantId, member.UserId))
                    {
                        await Should.ThrowAsync<UserFriendlyException>(() => StartLeanAsync(new StartEntryParticipationInput()));
                        await Should.ThrowAsync<UserFriendlyException>(() => StartLeanAsync(
                            new StartEntryParticipationInput { RecruiterCustomerId = root.CustomerId }));
                        await Should.ThrowAsync<UserFriendlyException>(() => StartLeanAsync(
                            new StartEntryParticipationInput { InviteCode = "ZZZZZZZZZZZZ" }));
                    }
                    // Two simultaneous requests by the same member must serialize
                    // on the existing ordered invitation/customer locks.
                    await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
                    {
                        using (AbpSession.Use(TenantId, member.UserId))
                        using (var uow = Resolve<IUnitOfWorkManager>().Begin(
                                   new UnitOfWorkOptions { Scope = TransactionScopeOption.RequiresNew, IsTransactional = true }))
                        {
                            await Resolve<IClubMemberProgrammeParticipationAppService>().StartEntryAsync(
                                new StartEntryParticipationInput { InviteCode = code });
                            await uow.CompleteAsync();
                        }
                    })));
                    var child = await UsingDbContextAsync(async context =>
                    {
                        var participation = (await context.EntryParticipations.Where(item =>
                            item.CustomerId == member.CustomerId).ToListAsync()).ShouldHaveSingleItem();
                        var attribution = (await context.AQGreenRecruitmentAttributions.Where(item =>
                            item.ParticipantId == participation.Id).ToListAsync()).ShouldHaveSingleItem();
                        attribution.CreditedSponsorParticipantId.ShouldBe(root.ParticipationId);
                        var invitation = await context.ProgrammeInvitations.SingleAsync(item => item.Id == attribution.SourceReferenceId);
                        invitation.ProgrammeParticipationId.ShouldBe(root.ParticipationId);
                        var acceptance = (await context.AQGreenRecruitmentAttributionConfirmations.Where(item =>
                            item.AttributionId == attribution.Id).ToListAsync()).ShouldHaveSingleItem();
                        acceptance.ConfirmedByUserId.ShouldBe(member.UserId);
                        acceptance.EvidenceReferenceId.ShouldBe(invitation.Id);
                        acceptance.ConfirmedAt.ShouldBe(attribution.AttributedAt);
                        (await context.AQGreenNetworkPlacements.CountAsync(item => item.ParticipantId == participation.Id)).ShouldBe(0);
                        return member with { ParticipationId = participation.Id, SponsorParticipationId = root.ParticipationId };
                    });
                    using (AbpSession.Use(TenantId, child.UserId))
                    {
                        await Resolve<IClubMemberProgrammeParticipationAppService>().CreateAQGreenJoiningCheckoutAsync(
                            new CreateAQGreenJoiningCheckoutInput { Schedule = AQGreenJoiningPaymentSchedule.Full });
                        var checkout = await UsingDbContextAsync(context => context.AQGreenJoiningCheckouts.SingleAsync(item =>
                            item.ParticipationId == child.ParticipationId));
                        var processor = Resolve<ProgrammePaymentConfirmationProcessor>();
                        var confirmationAt = DateTime.UtcNow;
                        var wrongAmount = await Should.ThrowAsync<InvalidOperationException>(() =>
                            processor.ProcessAQGreenJoiningCheckoutAsync(
                                checkout.Id, "Test", $"lean-{checkout.Id:N}",
                                checkout.ProviderCheckoutId, 1199m, "ZAR", confirmationAt));
                        wrongAmount.Message.ShouldContain("checkout amount");
                        var confirmed = await processor.ProcessAQGreenJoiningCheckoutAsync(
                            checkout.Id, "Test", $"lean-{checkout.Id:N}", checkout.ProviderCheckoutId, 1200m, "ZAR", confirmationAt);
                        confirmed.AwaitingAdministrativeApproval.ShouldBeTrue();
                        var retry = await processor.ProcessAQGreenJoiningCheckoutAsync(
                            checkout.Id, "Test", $"lean-{checkout.Id:N}", checkout.ProviderCheckoutId, 1200m, "ZAR", confirmationAt);
                        retry.WasAlreadyProcessed.ShouldBeTrue();
                        retry.PaymentId.ShouldBe(confirmed.PaymentId);
                        await AssertAwaitingApprovalAsync(child.ParticipationId);
                    }
                    LoginAsDefaultTenantAdmin();
                    using (ApprovalGate.Enable(child.ParticipationId))
                    {
                        await ApproveAsync(child.ParticipationId);
                        await ApproveAsync(child.ParticipationId);
                    }
                    await AssertActiveApprovalAsync(child, isRoot: false);
                    children.Add(child);
                }
            }
            (await joiningGate.IsEnabledAsync(TenantId)).ShouldBeFalse();
            using (AbpSession.Use(TenantId, root.UserId))
            using (ProgressGate.Enable(root.ParticipationId))
            {
                var progress = await InUnitOfWorkAsync(() => Resolve<IClubMemberProgrammeProgressAppService>().GetMyProgressAsync());
                progress.DirectRecruits.ShouldBe(5);
                progress.QualifiedLevel.ShouldBe(1);
                progress.StructuralProgress.CompletedLevel.ShouldBe(1);
                progress.StructuralProgress.RequiredCount.ShouldBe(25);
            }
            var resolver = Resolve<LatestClosedCommissionWeekResolver>();
            var week = resolver.Resolve(DateTime.UtcNow.AddDays(21));
            var control = resolver.Resolve(DateTime.UtcNow.AddDays(28));
            await SeedCommissionPrerequisitesAsync(Guid.NewGuid().ToString("N")[..10], week, control);
            LoginAsHostAdmin();
            using (SalesReviewGate.Enable(TenantId))
            {
                await ReviewSalesAsync(root.ParticipationId, week, 5, 5, 5, "TEST:synthetic-5-5-5", true);
                foreach (var child in children)
                    await ReviewSalesAsync(child.ParticipationId, week, 5, 5, 5, $"TEST:synthetic-{child.Label}", false);
                await ReviewSalesAsync(root.ParticipationId, control, 5, 5, 4, "TEST:synthetic-5-5-4", true);
                foreach (var child in children)
                    await ReviewSalesAsync(child.ParticipationId, control, 5, 5, 5, $"TEST:control-{child.Label}", false);
            }
            await CalculateAsync(week);
            var repeat = await CalculateAsync(week);
            repeat.WasAlreadyCalculated.ShouldBeTrue();
            repeat.RecordsCreated.ShouldBe(0);
            await CalculateAsync(control);
            var ledgerId = await UsingDbContextAsync(async context =>
            {
                var period = await context.EntryCommissionPeriods.SingleAsync(item => item.PeriodStart == week.PeriodStartUtc);
                var ledger = (await context.EntryWeeklyCommissions.Include(item => item.Components).Where(item =>
                    item.CommissionPeriodId == period.Id && item.EntryParticipationId == root.ParticipationId).ToListAsync()).ShouldHaveSingleItem();
                ledger.StructuralModel.ShouldBe(AQGreenCommissionStructuralModel.PlacementV2);
                ledger.CommissionDecisionRulesVersion.ShouldBe(AQGreenCommissionDecisionRules.CurrentVersion);
                ledger.HighestQualifiedNetworkLevel.ShouldBe(1);
                ledger.HighestCommissionedLevel.ShouldBe(1);
                ledger.TotalAmount.ShouldBe(150m);
                ledger.Components.ShouldHaveSingleItem().Amount.ShouldBe(150m);
                ledger.PayoutStatus.ShouldBe(WeeklyCommissionPayoutStatus.Earned);
                ledger.ReleasedAt.ShouldBeNull();
                ledger.PaidAt.ShouldBeNull();
                var controlPeriod = await context.EntryCommissionPeriods.SingleAsync(item => item.PeriodStart == control.PeriodStartUtc);
                var notEarned = await context.EntryWeeklyCommissions.SingleAsync(item =>
                    item.CommissionPeriodId == controlPeriod.Id && item.EntryParticipationId == root.ParticipationId);
                notEarned.HighestQualifiedNetworkLevel.ShouldBe(1);
                notEarned.HighestCommissionedLevel.ShouldBe(0);
                notEarned.PayoutStatus.ShouldBe(WeeklyCommissionPayoutStatus.NotEarned);
                notEarned.TotalAmount.ShouldBe(0m);
                (await context.OnyxParticipations.CountAsync()).ShouldBe(0);
                (await context.OnyxLoanAgreements.CountAsync()).ShouldBe(0);
                (await context.OnyxGraduationDecisions.CountAsync()).ShouldBe(0);
                (await context.EntryWeeklyCommissions.AnyAsync(item => item.PaidAt != null || item.ReleasedAt != null)).ShouldBeFalse();
                return ledger.Id;
            });
            var replay = await InUnitOfWorkAsync(() => Resolve<IAQGreenV2WeeklyCommissionEvidenceReplayValidator>().ValidateAsync(ledgerId));
            replay.TotalAmount.ShouldBe(150m);
            replay.CommissionedLevel.ShouldBe(1);
            replay.EvidenceNodeCount.ShouldBe(6);
            // An independently specified synthetic overdue commitment exercises
            // the existing financial hold without loans or changing sales rules.
            var heldWeek = resolver.Resolve(DateTime.UtcNow.AddMonths(2).AddDays(14));
            var obligationMonth = DateTime.UtcNow.AddMonths(1);
            await UsingDbContextAsync(async context =>
            {
                var participant = await context.EntryParticipations.SingleAsync(item => item.Id == root.ParticipationId);
                var duePolicy = EntryMonthlyObligationDuePolicy.Create(
                    "TEST:isolated-overdue-hold", 1,
                    EntryMonthlyObligationDuePolicy.JohannesburgMonthStartUtc(
                        obligationMonth.Year, obligationMonth.Month));
                context.EntryMonthlyObligationDuePolicies.Add(duePolicy);
                context.EntryMonthlyObligations.Add(EntryMonthlyObligation.Create(participant,
                    obligationMonth.Year, obligationMonth.Month,
                    duePolicy.ResolveDueAtUtc(obligationMonth.Year, obligationMonth.Month),
                    duePolicy.Version));
            });
            using (SalesReviewGate.Enable(TenantId))
            {
                await ReviewSalesAsync(root.ParticipationId, heldWeek, 5, 5, 5, "TEST:held-week-5-5-5", true);
                foreach (var child in children)
                    await ReviewSalesAsync(child.ParticipationId, heldWeek, 5, 5, 5, $"TEST:held-{child.Label}", false);
            }
            await CalculateAsync(heldWeek);
            await UsingDbContextAsync(async context =>
            {
                var period = await context.EntryCommissionPeriods.SingleAsync(item => item.PeriodStart == heldWeek.PeriodStartUtc);
                var held = await context.EntryWeeklyCommissions.SingleAsync(item =>
                    item.CommissionPeriodId == period.Id && item.EntryParticipationId == root.ParticipationId);
                held.PayoutStatus.ShouldBe(WeeklyCommissionPayoutStatus.Held);
                held.TotalAmount.ShouldBe(150m);
                held.HoldReason.ShouldContain("monthly commitment is overdue");
                held.ReleasedAt.ShouldBeNull();
                held.PaidAt.ShouldBeNull();
            });
            var directory = Environment.GetEnvironmentVariable("REPRO_MARKER_DIR");
            directory.ShouldNotBeNullOrWhiteSpace();
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, "aqgreen-v2-lean-mvp.json"), JsonSerializer.Serialize(new
            {
                PostgreSql = version, Journey = "root/invitation/join/test-checkout/pending/approval/placement/progress/sales/calculation/unreleased-ledger",
                Members = 6, ConcurrentDuplicateJoins = 5, QualifiedLevel = 1, CommissionedLevel = 1,
                SyntheticSales = "5/5/5", SyntheticTestAmount = 150m, ReplayedEvidenceNodes = 6,
                ControlSales = "5/5/4", ControlAmount = 0m, Released = false, Paid = false,
                FinancialHoldStatus = "Held", FinancialHoldAmount = 150m,
                Loans = 0, Onyx = 0, ActualUserObservation = "NOT RUN", RealProviderUsed = false
            }, new JsonSerializerOptions { WriteIndented = true }));
        }

        [IsolatedPostgreSqlFact]
        public async Task LeanMvp_MissingHistoryAndMissingSponsorScope_FailClosedWithoutPartialJoining()
        {
            LoginAsDefaultTenantAdmin();
            var root = await SeedAwaitingPaymentParticipantAsync(
                EntryProgrammeTerms.CreateSingleJoiningPayment("lean-negative-root", DateTime.UtcNow.AddDays(-1), 1200m, 600m, 7),
                null, "lean-negative-root", authorisedProspectiveRoot: true);
            await ConfirmJoiningPaymentAsync(root);
            LoginAsHostAdmin();
            using (ApprovalGate.Enable(root.ParticipationId)) await ApproveAsync(root.ParticipationId);
            string code;
            using (AbpSession.Use(TenantId, root.UserId))
                code = (await InUnitOfWorkAsync(() => Resolve<IProgrammeInvitationAppService>().GetMyInvitationsAsync())).Invitations.Single().Code;
            var member = await SeedLeanMemberIdentityAsync(root.AreaId, "legacy-no-history");
            using (AbpSession.Use(TenantId, member.UserId))
            {
                // Existing V1 joining must remain supported when the seam is off.
                await StartLeanAsync(new StartEntryParticipationInput { InviteCode = code });
                using (((AQGreenV2TestJoiningGate)Resolve<IAQGreenPlacementV2JoiningGate>()).Enable(TenantId))
                    await Should.ThrowAsync<InvalidOperationException>(() => StartLeanAsync(
                        new StartEntryParticipationInput { InviteCode = code }));
            }
            await UsingDbContextAsync(async context =>
            {
                var participant = await context.EntryParticipations.SingleAsync(item => item.CustomerId == member.CustomerId);
                (await context.AQGreenRecruitmentAttributions.CountAsync(item => item.ParticipantId == participant.Id)).ShouldBe(0);
            });

            LoginAsDefaultTenantAdmin();
            var unplaced = await SeedAwaitingPaymentParticipantAsync(
                EntryProgrammeTerms.CreateSingleJoiningPayment("lean-unplaced-root", DateTime.UtcNow.AddDays(-1), 1200m, 600m, 7),
                null, "lean-unplaced-root", authorisedProspectiveRoot: true);
            await ConfirmJoiningPaymentAsync(unplaced);
            // Ordinary disabled-gate V1 approval has no V2 placement/scope.
            await ApproveAsync(unplaced.ParticipationId);
            using (AbpSession.Use(TenantId, unplaced.UserId))
                code = (await InUnitOfWorkAsync(() => Resolve<IProgrammeInvitationAppService>().GetMyInvitationsAsync())).Invitations.Single().Code;
            var another = await SeedLeanMemberIdentityAsync(root.AreaId, "missing-sponsor-scope");
            using (AbpSession.Use(TenantId, another.UserId))
            using (((AQGreenV2TestJoiningGate)Resolve<IAQGreenPlacementV2JoiningGate>()).Enable(TenantId))
                await Should.ThrowAsync<InvalidOperationException>(() =>
                    StartLeanAsync(new StartEntryParticipationInput { InviteCode = code }));
            await UsingDbContextAsync(async context =>
            {
                (await context.EntryParticipations.CountAsync(item => item.CustomerId == another.CustomerId)).ShouldBe(0);
                (await context.AQGreenRecruitmentAttributions.CountAsync(item => item.CreditedSponsorParticipantId == unplaced.ParticipationId)).ShouldBe(0);
            });
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("REPRO_MARKER_DIR"), "aqgreen-lean-negatives.ran"),
                "PostgreSQL: missing historical acceptance rejected; missing sponsor scope rolls joining back.");
        }

        private Task<ProgrammeParticipationDto> StartLeanAsync(StartEntryParticipationInput input) =>
            Resolve<IClubMemberProgrammeParticipationAppService>().StartEntryAsync(input);

        private async Task<ParticipantFixture> SeedLeanMemberIdentityAsync(Guid areaId, string label)
        {
            return await UsingDbContextAsync(async context =>
            {
                var name = $"aqg-{label}-{Guid.NewGuid():N}";
                var user = new User { TenantId = TenantId, UserName = name, EmailAddress = $"{name}@example.test",
                    Name = "Test", Surname = "Member", IsActive = true, IsEmailConfirmed = true };
                user.SetRole(AquaUserRole.Guest);
                user.SetNormalizedNames();
                user.Password = new PasswordHasher<User>(
                    new OptionsWrapper<PasswordHasherOptions>(new PasswordHasherOptions()))
                    .HashPassword(user, User.DefaultPassword);
                context.Users.Add(user);
                await context.SaveChangesAsync();
                var guestRole = await context.Roles.SingleAsync(item => item.TenantId == TenantId && item.Name == "Guest");
                context.UserRoles.Add(new UserRole(TenantId, user.Id, guestRole.Id));
                var customer = Customer.Create(TenantId, user.Id, "Test member", new EmailAddress(user.EmailAddress), user: user);
                customer.AssignInitialArea(await context.Areas.SingleAsync(item => item.Id == areaId), DateTime.UtcNow, "Isolated test identity");
                context.Customers.Add(customer);
                await context.SaveChangesAsync();
                return new ParticipantFixture(user.Id, name, customer.Id, Guid.Empty, null, areaId, label);
            });
        }

        private sealed class LeanMvpCheckoutGateway : IYocoCheckoutGateway
        {
            public Task<YocoCheckout> CreateAsync(CreateYocoCheckout checkout) => Task.FromResult(new YocoCheckout
            {
                Id = $"test_{checkout.ReferenceId:N}", RedirectUrl = "https://payments.example.test/TEST-ONLY"
            });
        }
    }

    internal sealed class IsolatedPostgreSqlFactAttribute : FactAttribute
    {
        public IsolatedPostgreSqlFactAttribute()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("REPRO_PG"), "true", StringComparison.OrdinalIgnoreCase))
                Skip = "Requires explicitly selected disposable PostgreSQL; run the isolated MVP proof separately.";
        }
    }
}
