using System;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using Abp.Domain.Uow;
using AqualLifeStyle.Application.Admin.Commissions;
using AqualLifeStyle.Application.Admin.ProgrammeParticipations;
using AqualLifeStyle.Application.ProgrammeParticipations;
using AqualLifeStyle.Configuration;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Tests.WebHost;
using Castle.MicroKernel.Registration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;
using AqualLifeStyle.Web.Tests.Controllers;

namespace AqualLifeStyle.Web.Tests.Integration
{
    public sealed partial class AQGreenPlacementV2ApprovalPostgreSqlTests
    {
        [DiscoveryPostgreSqlFact]
        public async Task D10Cutover_AuthorisedPopulationUsesAtomicV2Approval_AndPreservesHistoricalFinancialSelection()
        {
            IsPostgreSqlRegressionMode().ShouldBeTrue(); LoginAsDefaultTenantAdmin();
            var fixture = await SeedSponsoredAwaitingApprovalAsync(recordedAt: AQGreenD10SyntheticEvidence.EffectiveAt.AddDays(-3));
            var (source, runtime, config, evidence) = await ConfigureD10Async();
            var before = await D10BusinessCountsAsync(fixture);
            await runtime.VerifyStartupAsync(); (await D10BusinessCountsAsync(fixture)).ShouldBe(before);
            var financial = new AQGreenD10CommissionSelector(runtime);
            (await financial.SelectAsync(1, evidence.Decision.V2EffectiveAtUtc.AddTicks(-1))).ShouldBe(AQGreenCommissionStructuralModel.LegacyV1);
            (await financial.SelectAsync(1, evidence.Decision.V2EffectiveAtUtc)).ShouldBe(AQGreenCommissionStructuralModel.PlacementV2);
            var legacyTenantId = await UsingDbContextAsync(async context =>
            { var tenant = new AqualLifeStyle.MultiTenancy.Tenant("d10legacy" + Guid.NewGuid().ToString("N"), "Synthetic uncommissioned Tenant");
                context.Tenants.Add(tenant); await context.SaveChangesAsync(); return tenant.Id; });
            try { AbpSession.TenantId = legacyTenantId;
                (await financial.SelectAsync(legacyTenantId, DateTime.UtcNow)).ShouldBe(AQGreenCommissionStructuralModel.LegacyV1); }
            finally { AbpSession.TenantId = 1; }
            // Valid startup never approves or calculates by itself.
            await AssertNoApprovalSideEffectsAsync(fixture.ParticipantId, fixture.UserId);
            await ApproveAsync(fixture.ParticipantId); await ApproveAsync(fixture.ParticipantId);
            await UsingDbContextAsync(async context =>
            {
                (await context.EntryParticipations.SingleAsync(p => p.Id == fixture.ParticipantId)).Status.ShouldBe(
                    EntryParticipationStatus.Active);
                (await context.AQGreenNetworkPlacements.CountAsync(p => p.ParticipantId == fixture.ParticipantId)).ShouldBe(1);
                (await context.EntryParticipationApprovalDecisions.CountAsync(p => EF.Property<Guid>(p, "EntryParticipationId") == fixture.ParticipantId)).ShouldBe(1);
                (await context.TransactionalEmailOutboxMessages.CountAsync(p => p.IdempotencyKey == $"Entry:{fixture.ParticipantId}:approved")).ShouldBe(1);
            });
            (await new AQGreenD10ProgressGate(runtime).IsEnabledAsync(1, fixture.ParticipantId)).ShouldBeTrue();
            await Should.ThrowAsync<InvalidOperationException>(() => new AQGreenD10GraduationSelector(runtime).SelectAsync(1, fixture.ParticipantId));
            await Should.ThrowAsync<InvalidOperationException>(() => runtime.EnsureFinancialWorkersAsync());
            await AssertApprovalUserLockAvailableAsync(fixture.UserId);
        }

        [DiscoveryPostgreSqlFact]
        public async Task D10Cutover_LateApprovalFailureRollsBackEveryEffect_ThenRetriesUnderSameFrozenAuthority()
        {
            IsPostgreSqlRegressionMode().ShouldBeTrue(); LoginAsDefaultTenantAdmin();
            var fixture = await SeedSponsoredAwaitingApprovalAsync(recordedAt: AQGreenD10SyntheticEvidence.EffectiveAt.AddDays(-3));
            await ConfigureD10Async();
            await using (await InstallFailureTriggerAsync("TransactionalEmailOutboxMessages", "INSERT", fixture))
                await Should.ThrowAsync<Exception>(() => ApproveAsync(fixture.ParticipantId));
            await AssertNoApprovalSideEffectsAsync(fixture.ParticipantId, fixture.UserId);
            await AssertApprovalUserLockAvailableAsync(fixture.UserId);
            await ApproveAsync(fixture.ParticipantId); await ApproveAsync(fixture.ParticipantId);
            await UsingDbContextAsync(async context =>
                (await context.AQGreenNetworkPlacements.CountAsync(p => p.ParticipantId == fixture.ParticipantId)).ShouldBe(1));
        }

        [DiscoveryPostgreSqlFact]
        public async Task D10Cutover_MissingAuthorityWrongScopeDriftAndRecoveryCannotFallBackToV1()
        {
            IsPostgreSqlRegressionMode().ShouldBeTrue(); LoginAsDefaultTenantAdmin();
            var fixture = await SeedSponsoredAwaitingApprovalAsync(recordedAt: AQGreenD10SyntheticEvidence.EffectiveAt.AddDays(-3));
            var (source, runtime, config, e) = await ConfigureD10Async(); var before = await D10BusinessCountsAsync(fixture);
            await Should.ThrowAsync<InvalidOperationException>(() => runtime.ForParticipantAsync(2, fixture.ParticipantId, true));
            source.Current = null;
            (await Should.ThrowAsync<InvalidOperationException>(() => runtime.VerifyStartupAsync())).Message.ShouldBe("D10_AUTHORITY_MISSING_FOR_EXISTING_V2_STATE");
            await Should.ThrowAsync<InvalidOperationException>(() => runtime.EnsureFinancialWorkersAsync());
            await Should.ThrowAsync<InvalidOperationException>(() => ApproveAsync(fixture.ParticipantId));
            source.Current = e.Bind();
            e.Decision.ReconciledPlacements[0].PlacementId = Guid.NewGuid();
            e.Decision.ReconciledPlacementDigest = AQGreenD10Commissioning.PlacementDigest(e.Decision.ReconciledPlacements);
            e.Authority.ReconciledPlacementDigest = e.Decision.ReconciledPlacementDigest;
            e.Authority.DecisionDigest = AQGreenB6CanonicalEvidence.Digest(e.Decision); source.Current = e.Bind();
            (await Should.ThrowAsync<InvalidOperationException>(() => ApproveAsync(fixture.ParticipantId))).Message.ShouldBe("D10_APPLIED_RECONCILIATION_CHANGED");
            config[ApplicationRecoveryMode.EnabledSetting] = "true";
            await Should.ThrowAsync<InvalidOperationException>(() => runtime.ForParticipantAsync(1, fixture.ParticipantId, true));
            config[ApplicationRecoveryMode.EnabledSetting] = "false";
            await Should.ThrowAsync<InvalidOperationException>(() => runtime.ForParticipantAsync(1, fixture.ParticipantId, true));
            (await D10BusinessCountsAsync(fixture)).ShouldBe(before);
            await AssertNoApprovalSideEffectsAsync(fixture.ParticipantId, fixture.UserId);
        }

        [DiscoveryPostgreSqlFact]
        public async Task D10Cutover_PreEffectiveFenceAndUnreconciledLegacyOnlyMemberDenyWithoutMutation()
        {
            IsPostgreSqlRegressionMode().ShouldBeTrue(); LoginAsDefaultTenantAdmin();
            var fixture = await SeedSponsoredAwaitingApprovalAsync(recordedAt: AQGreenD10SyntheticEvidence.EffectiveAt.AddDays(-3));
            var (source, runtime, config, e) = await ConfigureD10Async(); var before = await D10BusinessCountsAsync(fixture);
            e.Decision.V2EffectiveAtUtc = e.Decision.V2EffectiveAtUtc.AddDays(7); e.Authority.V2EffectiveAtUtc = e.Decision.V2EffectiveAtUtc;
            e.Authority.ExpiresAtUtc = DateTime.UtcNow.AddDays(14); e.Authority.DecisionDigest = AQGreenB6CanonicalEvidence.Digest(e.Decision);
            source.Current = e.Bind();
            (await Should.ThrowAsync<InvalidOperationException>(() => ApproveAsync(fixture.ParticipantId))).Message.ShouldBe("D10_CUTOVER_WRITE_FENCE");
            (await D10BusinessCountsAsync(fixture)).ShouldBe(before);
            // Reuse the current known synthetic membership catalog. A legacy assignment cannot be omitted.
            await ConfigureD10Async();
            var legacyCustomerId = await UsingDbContextAsync(async context =>
            {
                var area = await context.Areas.SingleAsync(a => a.Id == fixture.AreaId);
                var legacy = await CreateCustomerAsync(context, area, AqualLifeStyle.Domain.Enums.AquaUserRole.Guest, DateTime.UtcNow, "d10-legacy-only");
                var membership = await context.Memberships.FirstAsync(m => m.MembershipType == AqualLifeStyle.Domain.Enums.MembershipType.AQGreen &&
                    (m.TenantId == null || m.TenantId == 1));
                legacy.Customer.ChangeMembership(membership.Id); await context.SaveChangesAsync(); return legacy.Customer.Id;
            });
            (await Should.ThrowAsync<InvalidOperationException>(() => ApproveAsync(fixture.ParticipantId))).Message
                .ShouldBe("D10_UNRECONCILED_LEGACY_ASSIGNMENT_OR_CONFLICT");
            await AssertNoApprovalSideEffectsAsync(fixture.ParticipantId, fixture.UserId);
            await UsingDbContextAsync(async context => (await context.Customers.SingleAsync(c => c.Id == legacyCustomerId)).ChangeMembership(null));
        }

        private async Task<(D10SyntheticSource Source, AQGreenD10Runtime Runtime, IConfigurationRoot Configuration, AQGreenD10SyntheticEvidence Evidence)> ConfigureD10Async(bool financialOperationsEnabled = false)
        {
            var configuration = AQGreenD10SyntheticEvidence.Configuration(); AQGreenReconciliationSourceBoundary boundary;
            if (financialOperationsEnabled) configuration["App:FinancialOperations:Enabled"] = "true";
            var uows = IocManager.Resolve<IUnitOfWorkManager>();
            using (var uow = uows.Begin(new UnitOfWorkOptions { Scope = TransactionScopeOption.RequiresNew, IsTransactional = true,
                IsolationLevel = IsolationLevel.RepeatableRead }))
            { boundary = await IocManager.Resolve<IAQGreenReconciliationEvidenceSnapshot>().CaptureAsync(); await uow.CompleteAsync(); }
            var evidence = await UsingDbContextAsync(async context =>
            {
                var placements = await context.AQGreenNetworkPlacements.Where(p => p.TenantId == 1).ToListAsync();
                var attributions = await context.AQGreenRecruitmentAttributions.Where(p => p.TenantId == 1).ToListAsync();
                var rows = await (from p in context.EntryParticipations join c in context.Customers on p.CustomerId equals c.Id
                    where p.TenantId == 1 && c.TenantId == 1 select new { p.Id, p.CustomerId, c.AreaId }).ToListAsync();
                var members = rows.Select(p =>
                {
                    p.AreaId.ShouldNotBeNull();
                    var placed = placements.SingleOrDefault(x => x.ParticipantId == p.Id);
                    var sponsor = attributions.SingleOrDefault(x => x.ParticipantId == p.Id)?.CreditedSponsorParticipantId;
                    var scope = placed?.PlacementTreeScopeId ?? placements.Single(x => x.ParticipantId == sponsor).PlacementTreeScopeId;
                    return new AQGreenD10MemberIdentity { CustomerId = p.CustomerId, ParticipantId = p.Id, AreaId = p.AreaId.Value,
                        ComponentDigest = AQGreenD10SyntheticEvidence.Hash("synthetic-component:" + scope), ParticipantSnapshotKey = Guid.NewGuid().ToString("D") };
                }).ToList();
                return AQGreenD10SyntheticEvidence.Create(configuration, members, placements.Where(p => p.PlacedAt <= AQGreenD10SyntheticEvidence.EffectiveAt).Select(p => new AQGreenD10PlacementEvidence
                    { PlacementId = p.Id, ParticipantId = p.ParticipantId, PlacementTreeScopeId = p.PlacementTreeScopeId,
                        ParentParticipantId = p.PlacementParentParticipantId, Slot = p.PlacementSlot, CanonicalPath = p.CanonicalPath,
                        PlacedAtUtc = p.PlacedAt, RulesVersion = p.RulesVersion }).ToList(), boundary);
            });
            var source = new D10SyntheticSource { Current = evidence.Bind() };
            var registration = Guid.NewGuid().ToString("N");
            IocManager.IocContainer.Register(Component.For<IConfiguration>().Instance(configuration).Named("d10-config-" + registration).IsDefault(),
                Component.For<IAQGreenD10CommissioningSource>().Instance(source).Named("d10-source-" + registration).IsDefault(),
                Component.For<IAQGreenPlacementV2ApprovalGate>().ImplementedBy<AQGreenD10ApprovalGate>().Named("d10-approval-" + registration).LifestyleTransient().IsDefault(),
                Component.For<IAQGreenPlacementV2ProgressGate>().ImplementedBy<AQGreenD10ProgressGate>().Named("d10-progress-" + registration).LifestyleTransient().IsDefault(),
                Component.For<IAQGreenCommissionStructuralModelSelector>().ImplementedBy<AQGreenD10CommissionSelector>().Named("d10-financial-" + registration).LifestyleTransient().IsDefault());
            return (source, IocManager.Resolve<AQGreenD10Runtime>(), configuration, evidence);
        }

        [DiscoveryPostgreSqlFact]
        public async Task D10Cutover_PostBoundaryV1LedgerBlocksCommissioningAndRemainsUnchanged()
        {
            IsPostgreSqlRegressionMode().ShouldBeTrue(); LoginAsDefaultTenantAdmin();
            var fixture = await SeedSponsoredAwaitingApprovalAsync(recordedAt: AQGreenD10SyntheticEvidence.EffectiveAt.AddDays(-3));
            var (source, runtime, config, e) = await ConfigureD10Async();
            var ledgerId = await UsingDbContextAsync(async context =>
            {
                var participant = await context.EntryParticipations.SingleAsync(p => p.Id == fixture.SponsorParticipantId);
                var terms = EntryCommissionTerms.Create("synthetic-d10-ledger", e.Decision.V2EffectiveAtUtc.AddDays(-7), 200m, 1000m, 7000m, "ZAR");
                var week = new LatestClosedCommissionWeekResolver().Resolve(DateTime.UtcNow.AddDays(7));
                var period = EntryCommissionPeriod.CreateClosedPeriod(1, week.PeriodStartUtc, week.PeriodEndUtc,
                    week.TimeZoneId, DateTime.UtcNow.AddDays(7), terms);
                context.EntryCommissionPeriods.Add(period);
                var method = typeof(EntryWeeklyCommission).GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                    .Single(m => m.Name == "RecordCalculation" && m.GetParameters().Length == 5);
                var row = (EntryWeeklyCommission)method.Invoke(null, new object[] { participant, period, terms, EntryNetworkLevel.None, null });
                context.EntryWeeklyCommissions.Add(row); await context.SaveChangesAsync(); return row.Id;
            });
            (await Should.ThrowAsync<InvalidOperationException>(() => runtime.VerifyStartupAsync())).Message
                .ShouldBe("D10_INCOMPATIBLE_POST_BOUNDARY_FINANCIAL_HISTORY");
            await UsingDbContextAsync(async context =>
            {
                var row = await context.EntryWeeklyCommissions.SingleAsync(c => c.Id == ledgerId);
                row.StructuralModel.ShouldBe(AQGreenCommissionStructuralModel.LegacyV1); row.TotalAmount.ShouldBe(0m);
                // Remove only this fixture's synthetic row after proving that processing left it unchanged.
                context.EntryWeeklyCommissions.Remove(row);
                context.EntryCommissionPeriods.Remove(await context.EntryCommissionPeriods.SingleAsync(p => p.Id == row.CommissionPeriodId));
            });
        }

        private async Task<string> D10BusinessCountsAsync(ApprovalFixture fixture) => await UsingDbContextAsync(async context =>
            string.Join(":", (await context.EntryParticipations.SingleAsync(p => p.Id == fixture.ParticipantId)).Status,
                await context.AQGreenNetworkPlacements.CountAsync(p => p.ParticipantId == fixture.ParticipantId),
                await context.MemberPayments.CountAsync(), await context.EntryParticipationApprovalDecisions.CountAsync(),
                await context.TransactionalEmailOutboxMessages.CountAsync()));

        private sealed class D10SyntheticSource : IAQGreenD10CommissioningSource
        { public AQGreenD10Commissioning Current; public AQGreenD10Commissioning Read() => Current; }
    }
}
