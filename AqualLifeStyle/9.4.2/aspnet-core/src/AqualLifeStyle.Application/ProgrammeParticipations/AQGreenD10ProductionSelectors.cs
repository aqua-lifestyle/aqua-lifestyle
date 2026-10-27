using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.Runtime.Session;
using AqualLifeStyle.Configuration;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using AqualLifeStyle.Domain.Memberships;
using AqualLifeStyle.Domain.Enums;
using System.Transactions;
using System.IO;
using AqualLifeStyle.Domain.Areas;
using AqualLifeStyle.Domain.Payments;

namespace AqualLifeStyle.Application.ProgrammeParticipations
{
    /// <summary>Read-only D10 selection. Existing approval/allocator/ledger transactions own all writes.</summary>
    public sealed class AQGreenD10Runtime : ITransientDependency
    {
        private readonly IAQGreenD10CommissioningSource _source;
        private readonly IConfiguration _configuration;
        private readonly IUnitOfWorkManager _uows;
        private readonly IRepository<EntryParticipation, Guid> _participations;
        private readonly ICustomerRepository _customers;
        private readonly IRepository<AQGreenNetworkPlacement, Guid> _placements;
        private readonly IAbpSession _session;
        private readonly IRepository<Membership, int> _memberships;
        private readonly IAQGreenReconciliationEvidenceSnapshot _snapshot;
        private readonly IRepository<EntryCommissionPeriod, Guid> _periods;
        private readonly IRepository<EntryWeeklyCommission, Guid> _commissions;
        private readonly IRepository<Area, Guid> _areas;
        private readonly IRepository<CustomerAreaAssignment, Guid> _areaAssignments;
        private readonly IHostedPaymentCheckoutLock _areaLocks;
        public AQGreenD10Runtime(IAQGreenD10CommissioningSource source, IConfiguration configuration,
            IUnitOfWorkManager uows, IRepository<EntryParticipation, Guid> participations,
            ICustomerRepository customers, IRepository<AQGreenNetworkPlacement, Guid> placements, IAbpSession session,
            IRepository<Membership, int> memberships, IAQGreenReconciliationEvidenceSnapshot snapshot,
            IRepository<EntryCommissionPeriod, Guid> periods, IRepository<EntryWeeklyCommission, Guid> commissions,
            IRepository<Area, Guid> areas, IRepository<CustomerAreaAssignment, Guid> areaAssignments,
            IHostedPaymentCheckoutLock areaLocks)
        { _source = source; _configuration = configuration; _uows = uows; _participations = participations;
            _customers = customers; _placements = placements; _session = session; _memberships = memberships; _snapshot = snapshot;
            _periods = periods; _commissions = commissions; _areas = areas; _areaAssignments = areaAssignments; _areaLocks = areaLocks; }

        public async Task VerifyStartupAsync()
        {
            var authority = _source.Read();
            if (authority == null)
            {
                using var uow = _uows.Begin();
                using (_uows.Current.DisableFilter(AbpDataFilters.MustHaveTenant, AbpDataFilters.MayHaveTenant))
                    AQGreenD10Commissioning.Require(!await _placements.GetAll().AnyAsync(), "D10_AUTHORITY_MISSING_FOR_EXISTING_V2_STATE");
                await uow.CompleteAsync(); return;
            }
            Check(authority);
            await VerifyPopulationAsync(authority.Decision, cleanStart: authority.CleanStartEvidence);
            using var check = _uows.Begin();
            using (_uows.Current.DisableFilter(AbpDataFilters.MustHaveTenant, AbpDataFilters.MayHaveTenant))
                AQGreenD10Commissioning.Require(!await _placements.GetAll().AnyAsync(p => p.TenantId != authority.Decision.TenantId),
                    "D10_AUTHORITY_MISSING_FOR_OTHER_TENANT_V2_STATE");
            await check.CompleteAsync();
        }

        private void Check(AQGreenD10Commissioning authority)
        {
            ApplicationRecoveryMode.DenyBusinessOperation(_configuration);
            authority.EnsureCurrent(DateTime.UtcNow); authority.EnsureConfiguration(_configuration);
        }

        public async Task EnsureFinancialWorkersAsync()
        {
            var authority = _source.Read();
            if (authority == null)
            {
                await VerifyStartupAsync(); // Uncommissioned V2 state cannot be processed by a legacy worker either.
                return;
            }
            Check(authority);
            AQGreenD10Commissioning.Require(authority.FinancialWorkersCommissioned && DateTime.UtcNow >= authority.Decision.V2EffectiveAtUtc,
                "D10_FINANCIAL_WORKER_AUTHORITY_MISSING_OR_NOT_EFFECTIVE");
            await VerifyStartupAsync(); // Verify before either AQGreen or Onyx financial work can start.
        }

        public async Task<bool> ForTenantAsync(int tenantId, bool write, DateTime? financialCutoff = null)
        {
            ApplicationRecoveryMode.DenyBusinessOperation(_configuration);
            AQGreenD10Commissioning.Require(tenantId > 0, "D10_TENANT_REQUIRED");
            AQGreenD10Commissioning.Require(!_session.TenantId.HasValue || _session.TenantId == tenantId, "D10_SESSION_TENANT_MISMATCH");
            var a = _source.Read();
            if (a == null || a.Decision.TenantId != tenantId)
            {
                using var legacy = _uows.Begin();
                using (_uows.Current.DisableFilter(AbpDataFilters.MustHaveTenant, AbpDataFilters.MayHaveTenant))
                    AQGreenD10Commissioning.Require(!await _placements.GetAll().AnyAsync(p => p.TenantId == tenantId),
                        "D10_AUTHORITY_MISSING_FOR_EXISTING_V2_STATE");
                await legacy.CompleteAsync(); return false;
            }
            Check(a); var d = a.Decision;
            if (financialCutoff.HasValue)
            {
                AQGreenD10Commissioning.Require(financialCutoff != default(DateTime) && financialCutoff.Value.Kind == DateTimeKind.Utc,
                    "D10_FINANCIAL_CUTOFF_INVALID");
                if (financialCutoff < d.V2EffectiveAtUtc) return false; // Explicit V1 historical boundary, never failure fallback.
            }
            else if (DateTime.UtcNow < d.V2EffectiveAtUtc)
            {
                AQGreenD10Commissioning.Require(!write, "D10_CUTOVER_WRITE_FENCE"); return false;
            }
            if (financialCutoff.HasValue && financialCutoff >= d.V2EffectiveAtUtc)
                AQGreenD10Commissioning.Require(DateTime.UtcNow >= d.V2EffectiveAtUtc, "D10_CUTOVER_NOT_EFFECTIVE");
            await VerifyPopulationAsync(d, stabilizeAreas: financialCutoff.HasValue, cleanStart: a.CleanStartEvidence); return true;
        }

        public async Task<bool> ForParticipantAsync(int? tenantId, Guid participantId, bool write, bool graduation = false)
        {
            ApplicationRecoveryMode.DenyBusinessOperation(_configuration);
            AQGreenD10Commissioning.Require(participantId != Guid.Empty && (!tenantId.HasValue || tenantId > 0), "D10_PARTICIPANT_SCOPE_REQUIRED");
            AQGreenD10Commissioning.Require(!_session.TenantId.HasValue || tenantId == _session.TenantId, "D10_SESSION_TENANT_MISMATCH");
            using var uow = _uows.Begin();
            using (_uows.Current.DisableFilter(AbpDataFilters.MustHaveTenant, AbpDataFilters.MayHaveTenant))
            {
                var p = await _participations.GetAll().AsNoTracking().SingleOrDefaultAsync(x => x.Id == participantId &&
                    (!tenantId.HasValue || x.TenantId == tenantId.Value));
                AQGreenD10Commissioning.Require(p != null, "D10_PARTICIPANT_SCOPE_UNAVAILABLE");
                if (!await ForTenantAsync(p.TenantId, write)) { await uow.CompleteAsync(); return false; }
                var d = _source.Read().Decision;
                var c = await _customers.GetAll().AsNoTracking().SingleOrDefaultAsync(x => x.Id == p.CustomerId && x.TenantId == p.TenantId);
                AQGreenD10Commissioning.Require(c?.AreaId != null && d.AuthorizedAreaIds.Contains(c.AreaId.Value), "D10_AREA_OUTSIDE_AUTHORIZED_SCOPE");
                var historical = d.HistoricalMembers.SingleOrDefault(m => m.CustomerId == c.Id);
                AQGreenD10Commissioning.Require(historical != null ? historical.ParticipantId == p.Id :
                    d.AllowProspectiveAdmissions == true && p.StartedAt >= d.V2EffectiveAtUtc, "D10_PARTICIPANT_OUTSIDE_ACCEPTED_POPULATION");
                if (graduation) AQGreenD10Commissioning.Require(d.EnableGraduation == true, "D10_GRADUATION_NOT_COMMISSIONED");
                await uow.CompleteAsync(); return true;
            }
        }

        public async Task<bool> JoiningAsync(int tenantId)
        {
            if (!await ForTenantAsync(tenantId, true)) return false;
            var d = _source.Read().Decision;
            AQGreenD10Commissioning.Require(d.AllowProspectiveAdmissions == true && _session.TenantId == tenantId && _session.UserId.HasValue,
                "D10_PROSPECTIVE_JOINING_NOT_COMMISSIONED");
            // Checkout's serializable snapshot may precede an Area-lock wait.
            // Read the committed invitee assignment in a fresh transaction while
            // the caller retains that lock; no joining writes exist at this point.
            using var uow = _uows.Begin(new UnitOfWorkOptions { Scope = TransactionScopeOption.RequiresNew,
                IsTransactional = true, IsolationLevel = IsolationLevel.ReadCommitted });
            var c = await _customers.GetAll().AsNoTracking().SingleOrDefaultAsync(c => c.TenantId == tenantId && c.UserId == _session.UserId);
            AQGreenD10Commissioning.Require(c?.AreaId != null && d.AuthorizedAreaIds.Contains(c.AreaId.Value), "D10_AREA_OUTSIDE_AUTHORIZED_SCOPE");
            var assignments = await _areaAssignments.GetAll().IgnoreQueryFilters().AsNoTracking()
                .Where(a => a.CustomerId == c.Id && !a.IsDeleted && !a.EffectiveTo.HasValue)
                .Select(a => new { a.TenantId, a.AreaId, a.EffectiveFrom }).ToListAsync();
            AQGreenD10Commissioning.Require(assignments.Count == 1 && assignments[0].TenantId == tenantId &&
                assignments[0].AreaId == c.AreaId && assignments[0].EffectiveFrom <= DateTime.UtcNow &&
                await _areas.GetAll().AsNoTracking().AnyAsync(a => a.Id == c.AreaId && a.TenantId == tenantId && a.IsActive),
                "D10_CURRENT_JOINING_AREA_UNVERIFIABLE");
            await uow.CompleteAsync(); return true;
        }

        public async Task ValidateJoiningDestinationAsync(int tenantId, Guid destinationAreaId)
        {
            ApplicationRecoveryMode.DenyBusinessOperation(_configuration);
            await RequireCurrentReadTransactionAsync("D10_JOINING_REQUIRES_CURRENT_READ_TRANSACTION");
            var authority = _source.Read();
            AQGreenD10Commissioning.Require(authority != null && authority.Decision.TenantId == tenantId,
                "D10_JOINING_AUTHORITY_CHANGED");
            Check(authority);
            AQGreenD10Commissioning.Require(destinationAreaId != Guid.Empty &&
                authority.Decision.AuthorizedAreaIds.Contains(destinationAreaId), "D10_JOINING_DESTINATION_OUTSIDE_AUTHORIZED_SCOPE");
            // The invitation resolver owns ordered sponsor/invitee Area-transition locks.
            // Recheck commissioning inside that transaction, before any joining writes.
            AQGreenD10Commissioning.Require(await JoiningAsync(tenantId), "D10_JOINING_AUTHORITY_CHANGED");
            using var uow = _uows.Begin();
            AQGreenD10Commissioning.Require(await _areas.GetAll().AsNoTracking().AnyAsync(a =>
                a.Id == destinationAreaId && a.TenantId == tenantId && a.IsActive), "D10_JOINING_DESTINATION_UNVERIFIABLE");
            await uow.CompleteAsync();
        }

        private async Task VerifyPopulationAsync(AQGreenD10Decision d, bool stabilizeAreas = false,
            AQGreenCleanStartEvidence cleanStart = null)
        {
            // Reuse the existing PostgreSQL source-boundary reader in its own read-only snapshot;
            // never mark an approval/calculation transaction read-only or observe its uncommitted writes.
            using (var sourceUow = _uows.Begin(new UnitOfWorkOptions { Scope = TransactionScopeOption.RequiresNew,
                IsTransactional = true, IsolationLevel = IsolationLevel.RepeatableRead }))
            {
                var boundary = await _snapshot.CaptureAsync();
                AQGreenD10Commissioning.Require(boundary.SourceDatabaseIdentityEvidence == d.RequiredDatabaseIdentityDigest &&
                    boundary.SourceSchemaVersion == d.RequiredSchemaDigest && boundary.SourceAssemblyDigest == d.RequiredAssemblyDigest &&
                    RuntimeAssemblyDigest(boundary.SourceAssemblyDigest) == d.RequiredRuntimeAssemblyDigest,
                    "D10_DATABASE_SCHEMA_OR_RELEASE_MISMATCH");
                if (d.AdmissionMode == "CLEAN_START")
                {
                    AQGreenD10Commissioning.Require(cleanStart != null, "D10_CLEAN_START_AUTHORITY_INVALID");
                    var state = await _snapshot.CaptureCleanStartAsync(d.TenantId, d.V2EffectiveAtUtc, d.CleanStartResetId);
                    AQGreenD10Commissioning.Require(state != null && state.HasNoProtectedExposure() &&
                        state.PopulationCount == cleanStart.State.PopulationCount && state.ArchivedCount == state.PopulationCount &&
                        state.ArchiveDigest == cleanStart.State.ArchiveDigest && state.PlacementCount == 0 &&
                        state.LegacyMembershipCount == 0 && state.ResetReceiptDigest == cleanStart.State.ResetReceiptDigest,
                        "D10_CLEAN_START_BASELINE_CHANGED");
                }
                await sourceUow.CompleteAsync();
            }
            using var uow = _uows.Begin();
            using (_uows.Current.DisableFilter(AbpDataFilters.MustHaveTenant, AbpDataFilters.MayHaveTenant))
            {
                var ids = d.HistoricalMembers.Select(m => m.ParticipantId).ToArray();
                var live = _participations.GetAll().IgnoreQueryFilters().AsNoTracking().Where(p =>
                    p.TenantId == d.TenantId && (!p.IsDeleted || p.Status == EntryParticipationStatus.Active) &&
                    (cleanStart == null || !p.IsDeleted));
                if (stabilizeAreas)
                {
                    await RequireCurrentReadTransactionAsync("D10_FINANCIAL_REQUIRES_CURRENT_READ_TRANSACTION");
                    var customerIdsToLock = await live.Select(p => p.CustomerId).Distinct().ToArrayAsync();
                    if (customerIdsToLock.Length > 0) await _areaLocks.AcquireCustomerAreaTransitionsAsync(customerIdsToLock);
                }
                var currentAreaAssignments = _areaAssignments.GetAll().IgnoreQueryFilters().AsNoTracking();
                var now = DateTime.UtcNow;
                var invalidLiveArea = await (from p in live
                    join c in _customers.GetAll().IgnoreQueryFilters().AsNoTracking()
                        on new { p.CustomerId, TenantId = (int?)p.TenantId } equals new { CustomerId = c.Id, c.TenantId } into customers
                    from c in customers.DefaultIfEmpty()
                    join area in _areas.GetAll().IgnoreQueryFilters().AsNoTracking()
                        on new { AreaId = c.AreaId, TenantId = (int?)p.TenantId } equals new { AreaId = (Guid?)area.Id, TenantId = (int?)area.TenantId } into areas
                    from area in areas.DefaultIfEmpty()
                    where p.IsDeleted || c == null || c.IsDeleted || !c.AreaId.HasValue ||
                        !d.AuthorizedAreaIds.Contains(c.AreaId.Value) || area == null || area.IsDeleted || !area.IsActive ||
                        currentAreaAssignments.Count(a => a.CustomerId == p.CustomerId && !a.IsDeleted && !a.EffectiveTo.HasValue) != 1 ||
                        !currentAreaAssignments.Any(a => a.CustomerId == p.CustomerId && a.TenantId == d.TenantId &&
                            !a.IsDeleted && !a.EffectiveTo.HasValue && a.AreaId == c.AreaId && a.EffectiveFrom <= now) ||
                        (!ids.Contains(p.Id) && (d.AllowProspectiveAdmissions != true || p.StartedAt < d.V2EffectiveAtUtc))
                    select p.Id).AnyAsync();
                AQGreenD10Commissioning.Require(!invalidLiveArea, "D10_LIVE_POPULATION_AREA_OR_IDENTITY_UNVERIFIABLE");
                var members = await (from p in _participations.GetAll().IgnoreQueryFilters().AsNoTracking()
                    join c in _customers.GetAll().IgnoreQueryFilters().AsNoTracking() on new { p.CustomerId, TenantId = (int?)p.TenantId }
                        equals new { CustomerId = c.Id, c.TenantId }
                    where p.TenantId == d.TenantId && ids.Contains(p.Id)
                    select new { p.Id, p.CustomerId, c.AreaId, CustomerDeleted = c.IsDeleted, ParticipantDeleted = p.IsDeleted }).ToListAsync();
                AQGreenD10Commissioning.Require(members.Count == ids.Length && members.All(p => !p.CustomerDeleted && !p.ParticipantDeleted &&
                    p.AreaId.HasValue && d.AuthorizedAreaIds.Contains(p.AreaId.Value) &&
                    d.HistoricalMembers.Any(m => m.ParticipantId == p.Id && m.CustomerId == p.CustomerId)),
                    "D10_RECONCILED_MEMBER_IDENTITY_CHANGED");
                var observed = await _placements.GetAll().AsNoTracking().Where(p => p.TenantId == d.TenantId && ids.Contains(p.ParticipantId))
                    .Select(p => new AQGreenD10PlacementEvidence { PlacementId = p.Id, ParticipantId = p.ParticipantId,
                        PlacementTreeScopeId = p.PlacementTreeScopeId, ParentParticipantId = p.PlacementParentParticipantId,
                        Slot = p.PlacementSlot, CanonicalPath = p.CanonicalPath, PlacedAtUtc = p.PlacedAt, RulesVersion = p.RulesVersion }).ToListAsync();
                // Historical pending participants may acquire their first placement through the existing V2 approval transaction.
                var baseline = observed.Where(p => d.ReconciledPlacements.Any(e => e.ParticipantId == p.ParticipantId)).ToList();
                AQGreenD10Commissioning.Require(AQGreenD10Commissioning.PlacementDigest(baseline) == d.ReconciledPlacementDigest,
                    "D10_APPLIED_RECONCILIATION_CHANGED");
                AQGreenD10Commissioning.Require(!observed.Any(p => p.PlacedAtUtc < d.V2EffectiveAtUtc &&
                    !d.ReconciledPlacements.Any(e => e.ParticipantId == p.ParticipantId)), "D10_OMITTED_HISTORICAL_PLACEMENT");
                var unknownHistorical = await _participations.GetAll().IgnoreQueryFilters().AsNoTracking().AnyAsync(p =>
                    p.TenantId == d.TenantId && p.StartedAt < d.V2EffectiveAtUtc && !ids.Contains(p.Id) &&
                    (cleanStart == null || !p.IsDeleted));
                AQGreenD10Commissioning.Require(!unknownHistorical, "D10_OMITTED_HISTORICAL_PARTICIPATION");
                var customerIds = d.HistoricalMembers.Select(m => m.CustomerId).ToArray();
                var unresolvedLegacy = await (from c in _customers.GetAll().IgnoreQueryFilters().AsNoTracking()
                    join m in _memberships.GetAll().IgnoreQueryFilters().AsNoTracking() on c.MembershipId equals (int?)m.Id into assignments
                    from m in assignments.DefaultIfEmpty()
                    where c.TenantId == d.TenantId && !c.IsDeleted && c.MembershipId.HasValue &&
                        (m == null || m.TenantId.HasValue && m.TenantId != c.TenantId ||
                            m.MembershipType == MembershipType.AQGreen && !customerIds.Contains(c.Id))
                    select c.Id).AnyAsync();
                AQGreenD10Commissioning.Require(!unresolvedLegacy, "D10_UNRECONCILED_LEGACY_ASSIGNMENT_OR_CONFLICT");
                var incompatibleLedger = await (from period in _periods.GetAll().IgnoreQueryFilters().AsNoTracking()
                    join row in _commissions.GetAll().IgnoreQueryFilters().AsNoTracking() on period.Id equals row.CommissionPeriodId
                    where period.TenantId == d.TenantId && period.PeriodEnd >= d.V2EffectiveAtUtc &&
                        (row.TenantId != d.TenantId || row.StructuralModel != AQGreenCommissionStructuralModel.PlacementV2 ||
                            row.CommissionDecisionRulesVersion != AQGreenCommissionDecisionRules.CurrentVersion)
                    select row.Id).AnyAsync();
                AQGreenD10Commissioning.Require(!incompatibleLedger, "D10_INCOMPATIBLE_POST_BOUNDARY_FINANCIAL_HISTORY");
                await uow.CompleteAsync();
            }
        }

        private async Task RequireCurrentReadTransactionAsync(string code)
        {
            // PostgreSQL READ UNCOMMITTED has READ COMMITTED statement visibility.
            // A repeatable snapshot established before an advisory-lock wait cannot
            // establish the current Area after the other transaction commits.
            // ABP may omit the requested option when the provider chose its default.
            // Establish actual PostgreSQL visibility rather than assume that default.
            AQGreenD10Commissioning.Require(await _areaLocks.HasCurrentReadTransactionAsync(), code);
        }

        public static string RuntimeAssemblyDigest(string efAssemblyDigest) => AQGreenB6CanonicalEvidence.Tuples(new[]
        {
            new[] { "AqualLifeStyle.Application", AQGreenB6CanonicalEvidence.Hash(File.ReadAllBytes(typeof(AQGreenD10Runtime).Assembly.Location)) },
            new[] { "AqualLifeStyle.Core", AQGreenB6CanonicalEvidence.Hash(File.ReadAllBytes(typeof(AQGreenD10Commissioning).Assembly.Location)) },
            new[] { "AqualLifeStyle.EntityFrameworkCore", efAssemblyDigest }
        });
    }

    public sealed class AQGreenD10JoiningGate : IAQGreenPlacementV2JoiningGate
    { private readonly AQGreenD10Runtime _runtime; public AQGreenD10JoiningGate(AQGreenD10Runtime runtime) => _runtime = runtime;
        public Task<bool> IsEnabledAsync(int tenantId) => _runtime.JoiningAsync(tenantId);
        public Task ValidateDestinationAsync(int tenantId, Guid destinationAreaId) => _runtime.ValidateJoiningDestinationAsync(tenantId, destinationAreaId); }
    public sealed class AQGreenD10ApprovalGate : IAQGreenPlacementV2ApprovalGate
    { private readonly AQGreenD10Runtime _runtime; public AQGreenD10ApprovalGate(AQGreenD10Runtime runtime) => _runtime = runtime;
        public Task<bool> IsEnabledAsync(int? tenantId, Guid participantId) => _runtime.ForParticipantAsync(tenantId, participantId, true); }
    public sealed class AQGreenD10ProgressGate : IAQGreenPlacementV2ProgressGate
    { private readonly AQGreenD10Runtime _runtime; public AQGreenD10ProgressGate(AQGreenD10Runtime runtime) => _runtime = runtime;
        public Task<bool> IsEnabledAsync(int? tenantId, Guid participantId) => _runtime.ForParticipantAsync(tenantId, participantId, false); }
    public sealed class AQGreenD10SalesReviewGate : IAQGreenWeeklySalesReviewGate
    { private readonly AQGreenD10Runtime _runtime; public AQGreenD10SalesReviewGate(AQGreenD10Runtime runtime) => _runtime = runtime;
        public Task<bool> IsEnabledAsync(int tenantId) => _runtime.ForTenantAsync(tenantId, true); }
    public sealed class AQGreenD10CommissionSelector : IAQGreenCommissionStructuralModelSelector
    { private readonly AQGreenD10Runtime _runtime; public AQGreenD10CommissionSelector(AQGreenD10Runtime runtime) => _runtime = runtime;
        public async Task<AQGreenCommissionStructuralModel> SelectAsync(int tenantId, DateTime cutoffUtc) =>
            await _runtime.ForTenantAsync(tenantId, false, cutoffUtc) ? AQGreenCommissionStructuralModel.PlacementV2 : AQGreenCommissionStructuralModel.LegacyV1; }
    public sealed class AQGreenD10GraduationSelector : IAQGreenGraduationStructuralModelSelector
    { private readonly AQGreenD10Runtime _runtime; public AQGreenD10GraduationSelector(AQGreenD10Runtime runtime) => _runtime = runtime;
        public async Task<AQGreenGraduationStructuralModel> SelectAsync(int tenantId, Guid participantId) =>
            await _runtime.ForParticipantAsync(tenantId, participantId, true, true) ? AQGreenGraduationStructuralModel.PlacementV2 : AQGreenGraduationStructuralModel.LegacyV1; }
}
