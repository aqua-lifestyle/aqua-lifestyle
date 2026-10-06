using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Reflection;
using System.Linq;
using System.Threading.Tasks;
using Abp.Authorization;
using Abp.Authorization.Users;
using Abp.Domain.Uow;
using AqualLifeStyle.Application.Admin.Commissions;
using AqualLifeStyle.Application.Admin.Commissions.Dto;
using AqualLifeStyle.Application.Admin.ProgrammeParticipations;
using AqualLifeStyle.Application.Admin.ProgrammeParticipations.Dto;
using AqualLifeStyle.Application.ProgrammeParticipations;
using AqualLifeStyle.Application.ProgrammeParticipations.Dto;
using AqualLifeStyle.Application.Recruitment;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Web.Tests.Integration
{
    public sealed class PilotPostgreSqlFactAttribute : FactAttribute
    {
        public PilotPostgreSqlFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("REPRO_PG") != "true" ||
                Environment.GetEnvironmentVariable("REPRO_PILOT") != "true")
                Skip = "Requires the explicit isolated-pilot disposable PostgreSQL test environment.";
        }
    }

    // No participation, attribution, placement, review, or ledger is directly seeded.
    // Only prospective identities are fixtures. All business transitions use the real services.
    [Collection("AQGreen B3.2 PostgreSQL application path")]
    public class AQGreenIsolatedPilotPostgreSqlTests : AqualLifeStyleWebTestBase
    {
        [PilotPostgreSqlFact]
        public async Task RealProspectiveFlow_PreservesAttribution_PlacesFiveWide_AndProducesTestLedger()
        {
            Resolve<IAQGreenPilotMode>().IsEnabled.ShouldBeTrue();
            var root = await IdentityAsync("root");
            LoginAsHostAdmin();
            root.ParticipantId = await Resolve<IAQGreenPilotAppService>().AuthorizeProspectiveRootAsync(
                new PilotRootAdmissionInput { TenantId = 1, CustomerId = root.CustomerId, Reason = "Disposable TEST-MONEY pilot root" });
            var exactReplay = await Resolve<IAQGreenPilotAppService>().AuthorizeProspectiveRootAsync(
                new PilotRootAdmissionInput { TenantId = 1, CustomerId = root.CustomerId, Reason = "Disposable TEST-MONEY pilot root" });
            exactReplay.ShouldBe(root.ParticipantId);
            await PayAndApproveAsync(root, root: true);
            As(root);
            var invitation = (await Resolve<IProgrammeInvitationAppService>().GetMyInvitationsAsync())
                .Invitations.Single(item => item.ProgrammeKey == "AQGREEN");
            var children = new List<Identity>();
            for (var index = 0; index < 6; index++)
            {
                var member = await IdentityAsync("member" + index);
                As(member);
                await Resolve<IClubMemberProgrammeParticipationAppService>().StartEntryAsync(
                    new StartEntryParticipationInput { InviteCode = invitation.Code });
                member.ParticipantId = await UsingDbContextAsync(context => context.EntryParticipations
                    .Where(item => item.CustomerId == member.CustomerId).Select(item => item.Id).SingleAsync());
                await Resolve<IClubMemberProgrammeParticipationAppService>().StartEntryAsync(
                    new StartEntryParticipationInput { InviteCode = invitation.Code });
                await PayAndApproveAsync(member);
                children.Add(member);
            }
            await UsingDbContextAsync(async context =>
            {
                context.Database.IsNpgsql().ShouldBeTrue();
                var placements = await context.AQGreenNetworkPlacements.Where(item =>
                    children.Select(child => child.ParticipantId).Contains(item.ParticipantId))
                    .OrderBy(item => item.PlacedAt).ToListAsync();
                placements.Select(item => item.CanonicalPath).ShouldBe(new[] { "1", "2", "3", "4", "5", "11" });
                var attributions = await context.AQGreenRecruitmentAttributions.Where(item =>
                    children.Select(child => child.ParticipantId).Contains(item.ParticipantId)).ToListAsync();
                attributions.Count.ShouldBe(6);
                attributions.ShouldAllBe(item => item.CreditedSponsorParticipantId == root.ParticipantId);
                placements.Last().PlacementParentParticipantId.ShouldNotBe(root.ParticipantId);
                (await context.AQGreenRecruitmentAttributionConfirmations.CountAsync(item =>
                    attributions.Select(attribution => attribution.Id).Contains(item.AttributionId))).ShouldBe(6);
                foreach (var member in children.Prepend(root))
                {
                    (await context.EntryParticipationApprovalDecisions.CountAsync(item =>
                        EF.Property<Guid>(item, "EntryParticipationId") == member.ParticipantId)).ShouldBe(1);
                    (await context.TransactionalEmailOutboxMessages.CountAsync(item =>
                        item.IdempotencyKey == $"Entry:{member.ParticipantId}:approved")).ShouldBe(1);
                }
                (await context.AQGreenProspectiveRootAdmissions.CountAsync(item => item.ParticipantId == root.ParticipantId)).ShouldBe(1);
            });
            As(root);
            var progress = await Resolve<IClubMemberProgrammeProgressAppService>().GetMyProgressAsync();
            progress.QualifiedLevel.ShouldBe(1);

            // Development clock advances only the test review/calculation inputs; no facts are backdated.
            var asOf = DateTime.UtcNow.AddDays(14);
            var week = Resolve<LatestClosedCommissionWeekResolver>().Resolve(asOf);
            LoginAsHostAdmin();
            await Resolve<IAdminCommissionBootstrapAppService>().BootstrapInitialCommissionTermsAsync(
                new BootstrapInitialCommissionTermsInput { DryRun = false });
            await Resolve<AqualLifeStyle.Application.Admin.Tenants.IAdminTenantAppService>().ObserveActivationStateAsync(
                new AqualLifeStyle.Application.Admin.Tenants.Dto.ObserveTenantActivationStateInput { Id = 1, Justification = "TEST-ONLY prospective baseline" });
            using (((AQGreenV2ContinuousSalesClock)Resolve<IAQGreenWeeklySalesEligibilityClock>()).Set(asOf))
            {
                var reviewService = Resolve<IAdminAQGreenWeeklySalesEligibilityAppService>();
                var held = await reviewService.BeginReviewAsync(new BeginAQGreenWeeklySalesReviewInput
                { TenantId = 1, ParticipantId = root.ParticipantId, CommissionWeekStartUtc = week.PeriodStartUtc });
                held.ReviewStatus.ShouldBe(AQGreenWeeklySalesReviewStatus.HeldForEvidence);
                held.ThresholdResult.ShouldBeNull();
                await Should.ThrowAsync<AQGreenWeeklySalesEligibilityUnavailableException>(() => InUow(async () =>
                    await Resolve<IWeeklyCommissionCalculator>().CalculateEntryAsync(1, week, asOf)));
                await UsingDbContextAsync(async context =>
                    (await context.EntryWeeklyCommissions.CountAsync(item => item.EntryParticipationId == root.ParticipantId)).ShouldBe(0));
                var confirmed = await reviewService.ConfirmAsync(new ConfirmAQGreenWeeklySalesEligibilityInput
                { TenantId = 1, ParticipantId = root.ParticipantId, CommissionWeekStartUtc = week.PeriodStartUtc,
                  SprayQuantity = 5, OneLitreQuantity = 5, FiveLitreQuantity = 5,
                  EvidenceReferences = new List<string> { "TEST-ONLY:pilot-weekly-sales" } });
                confirmed.ReviewStatus.ShouldBe(AQGreenWeeklySalesReviewStatus.Confirmed);
                confirmed.ThresholdResult.ShouldBe(AQGreenWeeklySalesThresholdResult.Met);
            }
            await InUow(async () =>
            {
                var calculator = Resolve<IWeeklyCommissionCalculator>();
                var result = await calculator.CalculateEntryAsync(1, week, asOf);
                result.WasAlreadyCalculated.ShouldBeFalse();
                (await calculator.CalculateEntryAsync(1, week, asOf)).WasAlreadyCalculated.ShouldBeTrue();
            });
            await UsingDbContextAsync(async context =>
            {
                var ledger = await context.EntryWeeklyCommissions.Include(item => item.Components)
                    .SingleAsync(item => item.EntryParticipationId == root.ParticipantId && item.CalculatedAt == asOf);
                ledger.IsTestMoney.ShouldBeTrue();
                ledger.TotalAmount.ShouldBe(150m);
                ledger.StructuralModel.ShouldBe(AQGreenCommissionStructuralModel.PlacementV2);
                ledger.RulesVersion.ShouldNotBeNullOrWhiteSpace();
                ledger.PayoutStatus.ShouldBe(WeeklyCommissionPayoutStatus.Earned);
                Should.Throw<InvalidOperationException>(() => ledger.ReleaseEligiblePayout(asOf));
                await Should.ThrowAsync<Npgsql.PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE \"EntryWeeklyCommissions\" SET \"IsTestMoney\" = false WHERE \"Id\" = {ledger.Id}"));
                await Should.ThrowAsync<Npgsql.PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE \"EntryWeeklyCommissions\" SET \"PayoutStatus\" = 3 WHERE \"Id\" = {ledger.Id}"));
            });
            await UsingDbContextAsync(async context =>
            {
                await Should.ThrowAsync<Npgsql.PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE \"AQGreenProspectiveRootAdmissions\" SET \"Reason\" = 'tampered' WHERE \"ParticipantId\" = {root.ParticipantId}"));
            });
        }

        [PilotPostgreSqlFact]
        public async Task UnauthorizedRoot_InviteMissing_AndExcludedServiceCallsFailClosed()
        {
            var member = await IdentityAsync("negative");
            As(member);
            await Should.ThrowAsync<AbpAuthorizationException>(() => Resolve<IAQGreenPilotAppService>()
                .AuthorizeProspectiveRootAsync(new PilotRootAdmissionInput { TenantId = 1, CustomerId = member.CustomerId, Reason = "Unauthorized" }));
            await Should.ThrowAsync<Exception>(() => Resolve<IClubMemberProgrammeParticipationAppService>()
                .StartEntryAsync(new StartEntryParticipationInput()));
            await UsingDbContextAsync(async context =>
                (await context.EntryParticipations.CountAsync(item => item.CustomerId == member.CustomerId)).ShouldBe(0));
            await Should.ThrowAsync<AbpAuthorizationException>(() => Resolve<IClubMemberProgrammeParticipationAppService>()
                .CreateDirectOnyxCheckoutAsync(new CreateDirectOnyxCheckoutInput()));
            LoginAsHostAdmin();
            await Should.ThrowAsync<AbpAuthorizationException>(() => Resolve<AdminProgrammeParticipationAppService>()
                .GraduateAQGreenToOnyxAsync(null));
            await Should.ThrowAsync<AbpAuthorizationException>(() => Resolve<AdminCommissionAppService>().ReleaseAsync(null));
            await Should.ThrowAsync<AbpAuthorizationException>(() => Resolve<AdminCommissionAppService>().RecordPaymentAsync(null));
        }

        [PilotPostgreSqlFact]
        public async Task ConcurrentProspectiveRootAdmissionIsOneImmutableFact()
        {
            var root = await IdentityAsync("concurrent-root");
            LoginAsHostAdmin();
            var input = new PilotRootAdmissionInput { TenantId = 1, CustomerId = root.CustomerId, Reason = "TEST-ONLY concurrent root admission" };
            var results = await Task.WhenAll(
                Resolve<IAQGreenPilotAppService>().AuthorizeProspectiveRootAsync(input),
                Resolve<IAQGreenPilotAppService>().AuthorizeProspectiveRootAsync(input));
            results.Distinct().Count().ShouldBe(1);
            await UsingDbContextAsync(async context =>
            {
                (await context.EntryParticipations.CountAsync(item => item.CustomerId == root.CustomerId)).ShouldBe(1);
                (await context.AQGreenProspectiveRootAdmissions.CountAsync(item => item.ParticipantId == results[0])).ShouldBe(1);
                (await context.AQGreenRecruitmentAttributions.CountAsync(item => item.ParticipantId == results[0])).ShouldBe(1);
            });
            await Should.ThrowAsync<InvalidOperationException>(() => Resolve<IAQGreenPilotAppService>()
                .AuthorizeProspectiveRootAsync(new PilotRootAdmissionInput { TenantId = 1, CustomerId = root.CustomerId, Reason = "Conflicting replay" }));
        }

        [PilotPostgreSqlFact]
        public async Task ConcurrentApprovalsAllocateCanonicalSlotsAndNeverDuplicate()
        {
            var root = await IdentityAsync("approval-race-root");
            LoginAsHostAdmin();
            root.ParticipantId = await Resolve<IAQGreenPilotAppService>().AuthorizeProspectiveRootAsync(
                new PilotRootAdmissionInput { TenantId = 1, CustomerId = root.CustomerId, Reason = "TEST-ONLY approval race root" });
            await PayAndApproveAsync(root, root: true);
            As(root);
            var invitation = (await Resolve<IProgrammeInvitationAppService>().GetMyInvitationsAsync()).Invitations.Single();
            var members = new List<Identity>();
            for (var index = 0; index < 2; index++)
            {
                var member = await IdentityAsync("approval-race-member"); As(member);
                await Resolve<IClubMemberProgrammeParticipationAppService>().StartEntryAsync(new StartEntryParticipationInput { InviteCode = invitation.Code });
                member.ParticipantId = await UsingDbContextAsync(context => context.EntryParticipations.Where(item => item.CustomerId == member.CustomerId).Select(item => item.Id).SingleAsync());
                await PayAndApproveAsync(member, approve: false); members.Add(member);
            }
            LoginAsDefaultTenantAdmin();
            await Task.WhenAll(members.SelectMany(member => Enumerable.Range(0, 2).Select(_ =>
                Resolve<IAdminProgrammeParticipationAppService>().ApproveProgrammeParticipationAsync(
                    new ApproveProgrammeParticipationInput { Programme = AdminProgrammeType.Entry, ParticipationId = member.ParticipantId }))));
            await UsingDbContextAsync(async context =>
            {
                var ids = members.Select(item => item.ParticipantId).ToArray();
                (await context.AQGreenNetworkPlacements.Where(item => ids.Contains(item.ParticipantId)).OrderBy(item => item.PlacementSlot).Select(item => item.CanonicalPath).ToListAsync()).ShouldBe(new[] { "1", "2" });
                foreach (var member in members)
                {
                    (await context.EntryParticipations.SingleAsync(item => item.Id == member.ParticipantId)).Status.ShouldBe(EntryParticipationStatus.Active);
                    (await context.EntryParticipationApprovalDecisions.CountAsync(item => EF.Property<Guid>(item, "EntryParticipationId") == member.ParticipantId)).ShouldBe(1);
                    (await context.TransactionalEmailOutboxMessages.CountAsync(item => item.IdempotencyKey == $"Entry:{member.ParticipantId}:approved")).ShouldBe(1);
                }
            });
        }

        [PilotPostgreSqlFact]
        public async Task FailedRootAdmissionRollsBackAndRetryStartsCleanly()
        {
            var root = await IdentityAsync("fault-root"); LoginAsHostAdmin();
            var suffix = Guid.NewGuid().ToString("N");
            await using var connection = new Npgsql.NpgsqlConnection(Environment.GetEnvironmentVariable("REPRO_PG_CONNECTION"));
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                CREATE FUNCTION public."pilot_fail_{suffix}"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'TEST-ONLY root admission interruption'; END $$;
                CREATE TRIGGER "pilot_fail_{suffix}" BEFORE INSERT ON public."AQGreenProspectiveRootAdmissions"
                FOR EACH ROW WHEN (NEW."Reason" = 'TEST-ONLY fault root') EXECUTE FUNCTION public."pilot_fail_{suffix}"();
                """;
            await command.ExecuteNonQueryAsync();
            var input = new PilotRootAdmissionInput { TenantId = 1, CustomerId = root.CustomerId, Reason = "TEST-ONLY fault root" };
            try
            {
                await Should.ThrowAsync<Exception>(() => Resolve<IAQGreenPilotAppService>().AuthorizeProspectiveRootAsync(input));
                await UsingDbContextAsync(async context =>
                    (await context.EntryParticipations.CountAsync(item => item.CustomerId == root.CustomerId)).ShouldBe(0));
            }
            finally
            {
                command.CommandText = $"DROP TRIGGER \"pilot_fail_{suffix}\" ON public.\"AQGreenProspectiveRootAdmissions\"; DROP FUNCTION public.\"pilot_fail_{suffix}\"();";
                await command.ExecuteNonQueryAsync();
            }
            root.ParticipantId = await Resolve<IAQGreenPilotAppService>().AuthorizeProspectiveRootAsync(input);
            await UsingDbContextAsync(async context =>
                (await context.AQGreenProspectiveRootAdmissions.CountAsync(item => item.ParticipantId == root.ParticipantId)).ShouldBe(1));
        }

        [PilotPostgreSqlFact]
        public async Task WorkersAndExternalFinancialServicesDenyDirectInvocation()
        {
            foreach (var type in new[] {
                typeof(AqualLifeStyle.Web.Host.Commissions.WeeklyCommissionCalculationWorker),
                typeof(AqualLifeStyle.Web.Host.Email.TransactionalEmailOutboxWorker),
                typeof(AqualLifeStyle.Web.Host.Payments.Yoco.YocoPaymentOperationsMonitor),
                typeof(AqualLifeStyle.Web.Host.ProgrammeEngine.EntryMonthlyObligationWorker) })
            {
                // Web tests omit the Host module; register the actual worker without starting its timer.
                if (!IocManager.IsRegistered(type)) IocManager.Register(type, type, Abp.Dependency.DependencyLifeStyle.Singleton);
                var worker = IocManager.Resolve(type);
                ((IAQGreenPilotMode)type.GetProperty("PilotMode").GetValue(worker)).IsEnabled.ShouldBeTrue();
                var task = (Task)type.GetMethod("DoWorkAsync", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(worker, null);
                await Should.ThrowAsync<AbpAuthorizationException>(() => task);
            }
            await Should.ThrowAsync<AbpAuthorizationException>(() => Resolve<IWeeklyCommissionCalculator>().CalculateOnyxAsync(1, null, DateTime.UtcNow));
            await Should.ThrowAsync<AbpAuthorizationException>(() => Resolve<AqualLifeStyle.Payments.Yoco.YocoPaymentNotificationProcessor>().ProcessAsync(null));
            await Should.ThrowAsync<AbpAuthorizationException>(() => Resolve<AqualLifeStyle.Application.EntryMonthlyObligations.EntryMonthlyObligationScheduler>()
                .EnsureObligationsForPeriodAsync(2026, 10, DateTime.UtcNow, "TEST-ONLY"));
        }

        [PilotPostgreSqlFact]
        public async Task ProspectiveBrowserIdentitiesHaveNoBusinessState()
        {
            var path = Environment.GetEnvironmentVariable("PILOT_BROWSER_FIXTURE_OUTPUT");
            if (string.IsNullOrWhiteSpace(path)) return; // Optional identity handoff; not a workflow proof.
            var root = await IdentityAsync("browser-root");
            var member = await IdentityAsync("browser-member");
            await UsingDbContextAsync(async context =>
            {
                (await context.EntryParticipations.CountAsync(item => item.CustomerId == root.CustomerId || item.CustomerId == member.CustomerId)).ShouldBe(0);
            });
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new
            { TestOnly = true, Tenant = "Default", TenantId = 1, Root = root, Member = member },
                new JsonSerializerOptions { IncludeFields = true }));
        }

        private async Task PayAndApproveAsync(Identity member, bool root = false, bool approve = true)
        {
            As(member);
            var checkout = await Resolve<IClubMemberProgrammeParticipationAppService>().CreateAQGreenJoiningCheckoutAsync(
                new CreateAQGreenJoiningCheckoutInput { Schedule = AQGreenJoiningPaymentSchedule.Full });
            await Resolve<IAQGreenPilotAppService>().ConfirmTestPaymentAsync(new PilotPaymentInput { CheckoutId = checkout.CheckoutId });
            await Resolve<IAQGreenPilotAppService>().ConfirmTestPaymentAsync(new PilotPaymentInput { CheckoutId = checkout.CheckoutId });
            await UsingDbContextAsync(async context =>
            {
                var participation = await context.EntryParticipations.SingleAsync(item => item.Id == member.ParticipantId);
                participation.Status.ShouldBe(EntryParticipationStatus.PaymentConfirmedAwaitingApproval);
                (await context.AQGreenNetworkPlacements.CountAsync(item => item.ParticipantId == member.ParticipantId)).ShouldBe(0);
            });
            if (!approve) return;
            if (root) LoginAsHostAdmin(); else LoginAsDefaultTenantAdmin();
            var approval = Resolve<AdminProgrammeParticipationAppService>();
            var input = new ApproveProgrammeParticipationInput { Programme = AdminProgrammeType.Entry, ParticipationId = member.ParticipantId };
            await approval.ApproveProgrammeParticipationAsync(input);
            await approval.ApproveProgrammeParticipationAsync(input);
        }
        private void As(Identity member) { AbpSession.TenantId = 1; AbpSession.UserId = member.UserId; }
        private async Task InUow(Func<Task> action)
        {
            using var uow = Resolve<IUnitOfWorkManager>().Begin();
            await action(); await uow.CompleteAsync();
        }
        private async Task<Identity> IdentityAsync(string label)
        {
            LoginAsDefaultTenantAdmin();
            return await UsingDbContextAsync(async context =>
            {
                var area = await (from assignment in context.AreaAdminAssignments
                    join candidate in context.Areas on assignment.AreaId equals candidate.Id
                    where assignment.TenantId == 1 && assignment.UserId == AbpSession.UserId && !assignment.RevokedAt.HasValue
                    select candidate).FirstAsync();
                var guest = await context.Roles.SingleAsync(item => item.TenantId == 1 && item.Name == "Guest");
                var suffix = Guid.NewGuid().ToString("N");
                var user = new User { TenantId = 1, UserName = "pilot-" + suffix,
                    EmailAddress = "pilot-" + suffix + "@example.test", Name = "TEST", Surname = label,
                    IsEmailConfirmed = true, IsActive = true };
                user.SetRole(AquaUserRole.Guest); user.SetNormalizedNames();
                user.Password = new PasswordHasher<User>(new OptionsWrapper<PasswordHasherOptions>(new PasswordHasherOptions()))
                    .HashPassword(user, User.DefaultPassword);
                context.Users.Add(user); await context.SaveChangesAsync();
                context.UserRoles.Add(new UserRole(1, user.Id, guest.Id));
                var customer = Customer.Create(1, user.Id, "TEST " + label, new EmailAddress(user.EmailAddress), user: user);
                customer.AssignInitialArea(area, DateTime.UtcNow, "Disposable pilot identity fixture");
                context.Customers.Add(customer); await context.SaveChangesAsync();
                return new Identity { UserId = user.Id, CustomerId = customer.Id, UserName = user.UserName };
            });
        }
        private class Identity { public long UserId; public int CustomerId; public Guid ParticipantId; public string UserName; }
    }
}
