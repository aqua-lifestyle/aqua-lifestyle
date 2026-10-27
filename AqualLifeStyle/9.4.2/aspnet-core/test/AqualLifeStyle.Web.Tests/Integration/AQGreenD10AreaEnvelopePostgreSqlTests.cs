using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Abp.Authorization.Users;
using Abp.Domain.Uow;
using Abp.Runtime.Session;
using AqualLifeStyle.Application.Admin.Commissions;
using AqualLifeStyle.Application.ProgrammeParticipations;
using AqualLifeStyle.Application.ProgrammeParticipations.Dto;
using AqualLifeStyle.Application.Recruitment;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Areas;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.Domain.Recruitment;
using AqualLifeStyle.EntityFrameworkCore;
using AqualLifeStyle.MultiTenancy;
using AqualLifeStyle.Payments.Yoco;
using AqualLifeStyle.Payments;
using AqualLifeStyle.Tests.WebHost;
using Castle.MicroKernel.Registration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Shouldly;
using Xunit;
using AqualLifeStyle.Web.Tests.Controllers;

namespace AqualLifeStyle.Web.Tests.Integration
{
    public sealed partial class AQGreenPlacementV2ApprovalPostgreSqlTests
    {
        [DiscoveryPostgreSqlFact]
        public async Task D10Area_ProspectiveJoinAndRetryAllowA_OnyxMoveToBDeniesFinancialSelectionWithoutDiscardingEvidence()
        {
            IsPostgreSqlRegressionMode().ShouldBeTrue(); LoginAsDefaultTenantAdmin();
            var fixture = await SeedSponsoredAwaitingApprovalAsync(recordedAt: AQGreenD10SyntheticEvidence.EffectiveAt.AddDays(-3));
            // Select this synthetic capability before the D10 authority pins its
            // configuration; do not mutate the commissioned configuration later.
            var (_, runtime, configuration, evidence) = await ConfigureD10Async(financialOperationsEnabled: true);
            Resolve<IConfiguration>().ShouldBeSameAs(configuration);
            var gateway = ConfigureAreaJoiningGate(runtime);
            var member = await CreateD10InviteeAsync(fixture.AreaId);
            var code = await AQGreenInvitationCodeAsync(fixture.SponsorParticipantId);
            using (AbpSession.Use(1, member.UserId))
                await Resolve<IClubMemberProgrammeParticipationAppService>().StartEntryAsync(new StartEntryParticipationInput { InviteCode = code });
            await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(() => StartD10InviteeAsync(member.UserId, code))));
            var participantId = await UsingDbContextAsync(async context =>
            {
                var p = (await context.EntryParticipations.Where(p => p.CustomerId == member.CustomerId).ToListAsync()).ShouldHaveSingleItem();
                p.StartedAt.ShouldBeGreaterThanOrEqualTo(evidence.Decision.V2EffectiveAtUtc);
                (await context.AQGreenRecruitmentAttributions.CountAsync(a => a.ParticipantId == p.Id)).ShouldBe(1);
                (await context.Customers.SingleAsync(c => c.Id == member.CustomerId)).AreaId.ShouldBe(fixture.AreaId);
                return p.Id;
            });
            using (AbpSession.Use(1, member.UserId))
            {
                await Resolve<IClubMemberProgrammeParticipationAppService>().CreateAQGreenJoiningCheckoutAsync(
                    new CreateAQGreenJoiningCheckoutInput { Schedule = AQGreenJoiningPaymentSchedule.Full });
                var checkout = await UsingDbContextAsync(context => context.AQGreenJoiningCheckouts.SingleAsync(c => c.CustomerId == member.CustomerId));
                await InD10TransactionAsync(() => Resolve<ProgrammePaymentConfirmationProcessor>().ProcessAQGreenJoiningCheckoutAsync(
                    checkout.Id, "Synthetic", "d10-aqgreen-" + checkout.Id.ToString("N"), checkout.ProviderCheckoutId,
                    checkout.Amount, checkout.Currency, DateTime.UtcNow));
            }
            await ApproveAsync(participantId);
            (await new AQGreenD10CommissionSelector(runtime).SelectAsync(1, DateTime.UtcNow)).ShouldBe(AQGreenCommissionStructuralModel.PlacementV2);
            var areaB = await CreateD10OutsideAreaAsync();
            var onyxCode = await CreateD10OnyxInvitationAsync(areaB, fixture.AdministratorUserId);
            // Execute the existing Onyx checkout/confirmation path with an in-process synthetic gateway.
            using (AbpSession.Use(1, member.UserId))
            {
                await Resolve<IClubMemberProgrammeParticipationAppService>().CreateDirectOnyxCheckoutAsync(
                    new CreateDirectOnyxCheckoutInput { InviteCode = onyxCode });
                var intent = await UsingDbContextAsync(context => context.DirectOnyxCheckoutIntents.SingleAsync(i => i.CustomerId == member.CustomerId));
                await InD10TransactionAsync(() => Resolve<ProgrammePaymentConfirmationProcessor>().ProcessDirectOnyxCheckoutAsync(
                    intent.Id, "Synthetic", "d10-onyx-" + intent.Id.ToString("N"), intent.ProviderCheckoutId,
                    intent.Amount, intent.Currency, DateTime.UtcNow));
            }
            await UsingDbContextAsync(async context =>
            {
                (await context.Customers.SingleAsync(c => c.Id == member.CustomerId)).AreaId.ShouldBe(areaB);
                (await context.CustomerAreaAssignments.SingleAsync(a => a.CustomerId == member.CustomerId && !a.EffectiveTo.HasValue)).AreaId.ShouldBe(areaB);
                var policy = EntryMonthlyObligationDuePolicy.Create("synthetic-d10-obligation", 1,
                    EntryMonthlyObligationDuePolicy.JohannesburgMonthStartUtc(2026, 11));
                context.EntryMonthlyObligationDuePolicies.Add(policy);
                context.EntryMonthlyObligations.Add(EntryMonthlyObligation.Create(
                    await context.EntryParticipations.SingleAsync(p => p.Id == participantId), 2026, 11,
                    policy.ResolveDueAtUtc(2026, 11), policy.Version));
            });
            var before = await D10AreaBusinessStateAsync(member.CustomerId);
            var calls = gateway.Calls;
            for (var retry = 0; retry < 2; retry++)
                (await Should.ThrowAsync<InvalidOperationException>(() => new AQGreenD10CommissionSelector(runtime).SelectAsync(1, DateTime.UtcNow)))
                    .Message.ShouldBe("D10_LIVE_POPULATION_AREA_OR_IDENTITY_UNVERIFIABLE");
            var week = new LatestClosedCommissionWeekResolver().Resolve(DateTime.UtcNow.AddDays(14));
            await UsingDbContextAsync(context =>
            {
                context.AreaActivationStateRecords.Add(AreaActivationStateRecord.Record(Guid.NewGuid(), 1, true,
                    evidence.Decision.V2EffectiveAtUtc.AddDays(-7), DateTime.UtcNow, null, "Synthetic D10 financial prerequisite", AreaActivationStateRecordKind.ObservedBaseline));
                context.EntryCommissionTermsVersions.Add(EntryCommissionTermsVersion.Create("d10-area-" + Guid.NewGuid().ToString("N")[..8], week.PeriodStartUtc, 150m, 250m, 1250m));
                return Task.CompletedTask;
            });
            (await Should.ThrowAsync<InvalidOperationException>(() => InD10TransactionAsync(() => Resolve<IWeeklyCommissionCalculator>()
                .CalculateEntryAsync(1, week, DateTime.UtcNow.AddDays(14))))).Message.ShouldBe("D10_LIVE_POPULATION_AREA_OR_IDENTITY_UNVERIFIABLE");
            using (AbpSession.Use(1, member.UserId))
                await Should.ThrowAsync<InvalidOperationException>(() => Resolve<IClubMemberProgrammeParticipationAppService>().CreateAQGreenJoiningCheckoutAsync(
                    new CreateAQGreenJoiningCheckoutInput { Schedule = AQGreenJoiningPaymentSchedule.Full }));
            gateway.Calls.ShouldBe(calls); (await D10AreaBusinessStateAsync(member.CustomerId)).ShouldBe(before);
            (await new AQGreenD10CommissionSelector(runtime).SelectAsync(1, evidence.Decision.V2EffectiveAtUtc.AddTicks(-1)))
                .ShouldBe(AQGreenCommissionStructuralModel.LegacyV1);
        }

        [DiscoveryPostgreSqlFact]
        public async Task D10Area_ConcurrentSponsorMoveIsResolvedUnderLock_AndFinalDestinationDeniesJoinAndCheckoutWithoutWrites()
        {
            IsPostgreSqlRegressionMode().ShouldBeTrue(); LoginAsDefaultTenantAdmin();
            var fixture = await SeedSponsoredAwaitingApprovalAsync(recordedAt: AQGreenD10SyntheticEvidence.EffectiveAt.AddDays(-3));
            var (_, runtime, _, _) = await ConfigureD10Async();
            var selected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var gate = new D10AreaJoiningGate(runtime, () => selected.TrySetResult(true));
            var gateway = ConfigureAreaJoiningGate(runtime, gate);
            var member = await CreateD10InviteeAsync(fixture.AreaId); var areaB = await CreateD10OutsideAreaAsync();
            var code = await AQGreenInvitationCodeAsync(fixture.SponsorParticipantId);
            var before = await D10AreaBusinessStateAsync(member.CustomerId);
            await using var connection = new NpgsqlConnection(Environment.GetEnvironmentVariable("REPRO_PG_CONNECTION"));
            await connection.OpenAsync(); await using var transaction = await connection.BeginTransactionAsync();
            var sponsorId = await UsingDbContextAsync(async context => (await context.EntryParticipations.SingleAsync(p => p.Id == fixture.SponsorParticipantId)).CustomerId);
            await using (var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended(@resource, 0))", connection, transaction))
            { command.Parameters.AddWithValue("resource", "customer-area-transition:" + sponsorId); await command.ExecuteNonQueryAsync(); }
            var joining = Task.Run(() => StartD10InviteeAsync(member.UserId, code));
            await selected.Task.WaitAsync(TimeSpan.FromSeconds(20)); joining.IsCompleted.ShouldBeFalse();
            // The same domain movement and lock resource used by Onyx; commit after the first D10 check.
            await using (var context = new AqualLifeStyleDbContext(new DbContextOptionsBuilder<AqualLifeStyleDbContext>().UseNpgsql(connection).Options))
            {
                await context.Database.UseTransactionAsync(transaction);
                var sponsor = await context.Customers.Include(c => c.AreaAssignments).SingleAsync(c => c.Id == sponsorId);
                sponsor.MoveToArea(await context.Areas.SingleAsync(a => a.Id == areaB), DateTime.UtcNow, "Synthetic concurrent Onyx-compatible move");
                context.CustomerAreaAssignments.Add(sponsor.AreaAssignments.Single(a => a.IsCurrent));
                await context.SaveChangesAsync();
            }
            await transaction.CommitAsync();
            (await Should.ThrowAsync<InvalidOperationException>(() => joining)).Message.ShouldBe("D10_JOINING_DESTINATION_OUTSIDE_AUTHORIZED_SCOPE");
            gate.DestinationChecks.ShouldBe(1);
            await Should.ThrowAsync<InvalidOperationException>(() => StartD10InviteeAsync(member.UserId, code));
            using (AbpSession.Use(1, member.UserId))
                await Should.ThrowAsync<InvalidOperationException>(() => Resolve<IClubMemberProgrammeParticipationAppService>().CreateAQGreenJoiningCheckoutAsync(
                    new CreateAQGreenJoiningCheckoutInput { Schedule = AQGreenJoiningPaymentSchedule.Full }));
            (await D10AreaBusinessStateAsync(member.CustomerId)).ShouldBe(before); gateway.Calls.ShouldBe(0);
            await UsingDbContextAsync(async context => (await context.Customers.SingleAsync(c => c.Id == member.CustomerId)).AreaId.ShouldBe(fixture.AreaId));
        }

        [DiscoveryPostgreSqlFact]
        public async Task D10Area_CheckoutWaitsForAreaMove_AndReadsCommittedScopeBeforeAnyObligationOrProviderCall()
        {
            IsPostgreSqlRegressionMode().ShouldBeTrue(); LoginAsDefaultTenantAdmin();
            var fixture = await SeedSponsoredAwaitingApprovalAsync(recordedAt: AQGreenD10SyntheticEvidence.EffectiveAt.AddDays(-3));
            var (_, runtime, _, _) = await ConfigureD10Async(); var gateway = ConfigureAreaJoiningGate(runtime);
            var member = await CreateD10InviteeAsync(fixture.AreaId); var code = await AQGreenInvitationCodeAsync(fixture.SponsorParticipantId);
            await StartD10InviteeAsync(member.UserId, code); var areaB = await CreateD10OutsideAreaAsync();
            var before = await D10AreaBusinessStateAsync(member.CustomerId);
            await using var connection = new NpgsqlConnection(Environment.GetEnvironmentVariable("REPRO_PG_CONNECTION"));
            await connection.OpenAsync(); await using var transaction = await connection.BeginTransactionAsync();
            await using (var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended(@resource, 0))", connection, transaction))
            { command.Parameters.AddWithValue("resource", "customer-area-transition:" + member.CustomerId); await command.ExecuteNonQueryAsync(); }
            var checkout = Task.Run(async () =>
            { using (AbpSession.Use(1, member.UserId)) await Resolve<IClubMemberProgrammeParticipationAppService>().CreateAQGreenJoiningCheckoutAsync(
                new CreateAQGreenJoiningCheckoutInput { Schedule = AQGreenJoiningPaymentSchedule.Full }); });
            var waiting = false;
            for (var attempt = 0; attempt < 200 && !waiting; attempt++)
            {
                await using var probe = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE datname = current_database() AND wait_event = 'advisory')", connection, transaction);
                waiting = (bool)await probe.ExecuteScalarAsync();
                if (!waiting) await Task.Delay(25);
            }
            waiting.ShouldBeTrue(); checkout.IsCompleted.ShouldBeFalse();
            await using (var context = new AqualLifeStyleDbContext(new DbContextOptionsBuilder<AqualLifeStyleDbContext>().UseNpgsql(connection).Options))
            {
                await context.Database.UseTransactionAsync(transaction);
                var customer = await context.Customers.Include(c => c.AreaAssignments).SingleAsync(c => c.Id == member.CustomerId);
                customer.MoveToArea(await context.Areas.SingleAsync(a => a.Id == areaB), DateTime.UtcNow, "Synthetic concurrent Onyx-compatible move");
                context.CustomerAreaAssignments.Add(customer.AreaAssignments.Single(a => a.IsCurrent)); await context.SaveChangesAsync();
            }
            await transaction.CommitAsync();
            (await Should.ThrowAsync<InvalidOperationException>(() => checkout)).Message.ShouldBe("D10_AREA_OUTSIDE_AUTHORIZED_SCOPE");
            (await D10AreaBusinessStateAsync(member.CustomerId)).ShouldBe(before); gateway.Calls.ShouldBe(0);
        }

        [DiscoveryPostgreSqlTheory]
        [InlineData(IsolationLevel.RepeatableRead)]
        [InlineData(IsolationLevel.Serializable)]
        public async Task D10Area_RepeatableSnapshotCannotAuthorizeResolvedDestinationOrFinancialSelection(IsolationLevel isolation)
        {
            IsPostgreSqlRegressionMode().ShouldBeTrue(); LoginAsDefaultTenantAdmin();
            var fixture = await SeedSponsoredAwaitingApprovalAsync(recordedAt: AQGreenD10SyntheticEvidence.EffectiveAt.AddDays(-3));
            var (_, runtime, _, _) = await ConfigureD10Async(); var gateway = ConfigureAreaJoiningGate(runtime);
            var member = await CreateD10InviteeAsync(fixture.AreaId); var code = await AQGreenInvitationCodeAsync(fixture.SponsorParticipantId);
            var before = await D10AreaBusinessStateAsync(member.CustomerId);
            using (AbpSession.Use(1, member.UserId))
            {
                using var uow = Resolve<IUnitOfWorkManager>().Begin(new UnitOfWorkOptions { Scope = TransactionScopeOption.RequiresNew,
                    IsTransactional = true, IsolationLevel = isolation });
                (await Should.ThrowAsync<InvalidOperationException>(() => Resolve<IClubMemberProgrammeParticipationAppService>()
                    .StartEntryAsync(new StartEntryParticipationInput { InviteCode = code }))).Message.ShouldBe("D10_JOINING_REQUIRES_CURRENT_READ_TRANSACTION");
            }
            using (var uow = Resolve<IUnitOfWorkManager>().Begin(new UnitOfWorkOptions { Scope = TransactionScopeOption.RequiresNew,
                IsTransactional = true, IsolationLevel = isolation }))
                (await Should.ThrowAsync<InvalidOperationException>(() => new AQGreenD10CommissionSelector(runtime).SelectAsync(1, DateTime.UtcNow)))
                    .Message.ShouldBe("D10_FINANCIAL_REQUIRES_CURRENT_READ_TRANSACTION");
            gateway.Calls.ShouldBe(0); (await D10AreaBusinessStateAsync(member.CustomerId)).ShouldBe(before);
        }

        [DiscoveryPostgreSqlTheory]
        [InlineData("missing")]
        [InlineData("missing-assignment")]
        [InlineData("conflicting")]
        [InlineData("deleted-customer")]
        public async Task D10Area_UnverifiableProspectiveCurrentAssignmentFailsClosed(string failure)
        {
            IsPostgreSqlRegressionMode().ShouldBeTrue(); LoginAsDefaultTenantAdmin();
            var fixture = await SeedSponsoredAwaitingApprovalAsync(recordedAt: AQGreenD10SyntheticEvidence.EffectiveAt.AddDays(-3));
            var (source, runtime, _, evidence) = await ConfigureD10Async(); ConfigureAreaJoiningGate(runtime);
            var member = await CreateD10InviteeAsync(fixture.AreaId);
            await StartD10InviteeAsync(member.UserId, await AQGreenInvitationCodeAsync(fixture.SponsorParticipantId));
            var other = await CreateD10OutsideAreaAsync();
            if (failure == "conflicting")
            {
                evidence.Decision.AuthorizedAreaIds = evidence.Decision.AuthorizedAreaIds.Append(other).ToArray();
                evidence.Authority.DecisionDigest = AQGreenB6CanonicalEvidence.Digest(evidence.Decision);
                source.Current = evidence.Bind(); // Both Areas authorized; the current projection/history still disagree.
            }
            await UsingDbContextAsync(async context =>
            {
                var customer = await context.Customers.SingleAsync(c => c.Id == member.CustomerId);
                if (failure == "deleted-customer") customer.IsDeleted = true;
                else if (failure == "missing-assignment")
                    (await context.CustomerAreaAssignments.SingleAsync(a => a.CustomerId == member.CustomerId && !a.EffectiveTo.HasValue)).IsDeleted = true;
                else context.Entry(customer).Property(c => c.AreaId).CurrentValue = failure == "missing" ? null : other;
            });
            try
            {
                var before = await D10AreaBusinessStateAsync(member.CustomerId);
                (await Should.ThrowAsync<InvalidOperationException>(() => new AQGreenD10CommissionSelector(runtime).SelectAsync(1, DateTime.UtcNow)))
                    .Message.ShouldBe("D10_LIVE_POPULATION_AREA_OR_IDENTITY_UNVERIFIABLE");
                (await D10AreaBusinessStateAsync(member.CustomerId)).ShouldBe(before);
            }
            finally
            {
                await UsingDbContextAsync(async context =>
                { var c = await context.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == member.CustomerId); c.IsDeleted = false;
                    context.Entry(c).Property(c => c.AreaId).CurrentValue = fixture.AreaId;
                    (await context.CustomerAreaAssignments.IgnoreQueryFilters().SingleAsync(a => a.CustomerId == member.CustomerId && !a.EffectiveTo.HasValue)).IsDeleted = false; });
            }
        }

        private async Task<(long UserId, int CustomerId)> CreateD10InviteeAsync(Guid areaId) => await UsingDbContextAsync(async context =>
        {
            var member = await CreateCustomerAsync(context, await context.Areas.SingleAsync(a => a.Id == areaId), AquaUserRole.Guest, DateTime.UtcNow, "d10-area-invitee");
            var role = await context.Roles.SingleAsync(r => r.TenantId == 1 && r.Name == "Guest");
            context.UserRoles.Add(new UserRole(1, member.User.Id, role.Id)); await context.SaveChangesAsync();
            return (member.User.Id, member.Customer.Id);
        });
        private Task<Guid> CreateD10OutsideAreaAsync() => UsingDbContextAsync(async context =>
        { var area = Area.Create(1, "D10" + Guid.NewGuid().ToString("N")[..8], "Synthetic unauthorized D10 Area"); context.Areas.Add(area); await context.SaveChangesAsync(); return area.Id; });
        private Task<string> AQGreenInvitationCodeAsync(Guid participantId) => UsingDbContextAsync(async context =>
            (await context.ProgrammeInvitations.SingleAsync(i => i.ProgrammeParticipationId == participantId && i.ProgrammeKey == "AQGREEN")).Code);
        private async Task<string> CreateD10OnyxInvitationAsync(Guid areaId, long administratorId) => await UsingDbContextAsync(async context =>
        {
            var member = await CreateCustomerAsync(context, await context.Areas.SingleAsync(a => a.Id == areaId), AquaUserRole.Member, DateTime.UtcNow.AddMinutes(-5), "d10-onyx-sponsor");
            var terms = Resolve<ICurrentProgrammeTermsProvider>().GetDirectOnyxTerms();
            var membership = await context.Memberships.FirstAsync(m => m.MembershipType == MembershipType.Onyx && (m.TenantId == null || m.TenantId == 1));
            var participation = OnyxParticipation.StartDirectIndependently(1, member.Customer.Id, membership.Id, terms, DateTime.UtcNow.AddMinutes(-5));
            var payment = MemberPayment.CreatePending(1, member.Customer.Id, MemberPaymentPurpose.OnyxDirectEntry, terms.DirectEntryAmount,
                "Synthetic", "d10-onyx-sponsor-" + Guid.NewGuid().ToString("N"), DateTime.UtcNow.AddMinutes(-4), "ZAR");
            payment.Confirm(DateTime.UtcNow.AddMinutes(-4)); participation.ApplyConfirmedDirectEntryPayment(payment);
            participation.ApproveByAdministrator(administratorId, DateTime.UtcNow.AddMinutes(-3));
            context.MemberPayments.Add(payment); context.OnyxParticipations.Add(participation);
            var invitation = ProgrammeInvitation.Create(1, RecruitmentProgrammeKeys.Onyx, participation.Id);
            context.ProgrammeInvitations.Add(invitation); await context.SaveChangesAsync(); return invitation.Code;
        });
        private D10AreaCheckoutGateway ConfigureAreaJoiningGate(AQGreenD10Runtime runtime, IAQGreenPlacementV2JoiningGate gate = null)
        {
            var gateway = new D10AreaCheckoutGateway(); var name = Guid.NewGuid().ToString("N");
            IocManager.IocContainer.Register(Component.For<IAQGreenPlacementV2JoiningGate>().Instance(gate ?? new AQGreenD10JoiningGate(runtime)).Named("d10-area-gate-" + name).IsDefault(),
                Component.For<IYocoCheckoutGateway>().Instance(gateway).Named("d10-area-gateway-" + name).IsDefault());
            return gateway;
        }
        private async Task StartD10InviteeAsync(long userId, string code)
        { using (AbpSession.Use(1, userId)) await InD10TransactionAsync(() => Resolve<IClubMemberProgrammeParticipationAppService>()
                .StartEntryAsync(new StartEntryParticipationInput { InviteCode = code })); }
        private async Task<T> InD10TransactionAsync<T>(Func<Task<T>> action)
        { using var uow = Resolve<IUnitOfWorkManager>().Begin(new UnitOfWorkOptions { Scope = TransactionScopeOption.RequiresNew,
                IsTransactional = true, IsolationLevel = IsolationLevel.ReadCommitted });
            var result = await action(); await uow.CompleteAsync(); return result; }
        private Task<string> D10AreaBusinessStateAsync(int customerId) => UsingDbContextAsync(async context =>
        {
            var participation = await context.EntryParticipations.SingleOrDefaultAsync(p => p.CustomerId == customerId);
            var obligations = await context.EntryMonthlyObligations.OrderBy(o => o.Id).ToListAsync();
            return string.Join("|", participation?.Id, participation?.JoiningPaymentSchedule,
                await context.EntryCommissionPeriods.CountAsync(), await context.EntryWeeklyCommissions.CountAsync(),
                await context.EntryCommissionComponents.CountAsync(), await context.AQGreenJoiningCheckouts.CountAsync(c => c.CustomerId == customerId),
                await context.MemberPayments.CountAsync(), string.Join(";", obligations.Select(o => $"{o.Id}:{o.Status}:{o.AmountDue}:{o.PaymentId}")));
        });
        private sealed class D10AreaJoiningGate : IAQGreenPlacementV2JoiningGate
        {
            private readonly AQGreenD10JoiningGate _gate; private readonly Action _selected; public int DestinationChecks;
            public D10AreaJoiningGate(AQGreenD10Runtime runtime, Action selected) { _gate = new(runtime); _selected = selected; }
            public async Task<bool> IsEnabledAsync(int tenantId) { var selected = await _gate.IsEnabledAsync(tenantId); if (selected) _selected(); return selected; }
            public async Task ValidateDestinationAsync(int tenantId, Guid destinationAreaId)
            { Interlocked.Increment(ref DestinationChecks); await _gate.ValidateDestinationAsync(tenantId, destinationAreaId); }
        }
        private sealed class D10AreaCheckoutGateway : IYocoCheckoutGateway
        {
            private int _calls; public int Calls => _calls;
            public Task<YocoCheckout> CreateAsync(CreateYocoCheckout checkout)
            { Interlocked.Increment(ref _calls); return Task.FromResult(new YocoCheckout { Id = "synthetic_" + checkout.ReferenceId.ToString("N"),
                RedirectUrl = "https://payments.example.test/SYNTHETIC-D10" }); }
        }
    }
}
