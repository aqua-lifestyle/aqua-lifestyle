using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace AqualLifeStyle.EntityFrameworkCore
{
    public sealed record AQGreenBackfillTargetIdentity(
        string Database, string User, string ServerAddress, int ServerPort,
        string SourceSnapshotSha256);

    public sealed record AQGreenBackfillResult(
        bool DryRun, bool AlreadyApplied, int ScopeInserts, int PlacementInserts,
        int Conflicts = 0, int UnexpectedExistingRows = 0, int Updates = 0, int Deletes = 0,
        int AttributionInserts = 0, int ConfirmationInserts = 0);

    /// <summary>
    /// Explicit operator-only transaction owner; intentionally not registered in
    /// application DI or exposed by an HTTP endpoint. Does not migrate schemas.
    /// </summary>
    public sealed class AQGreenCustomer11BackfillWriter
    {
        private readonly AqualLifeStyleDbContext _context;
        public AQGreenCustomer11BackfillWriter(AqualLifeStyleDbContext context) => _context = context;

        public async Task<AQGreenBackfillResult> ExecuteAsync(
            AQGreenCustomer11BackfillPlan plan, AQGreenBackfillTargetIdentity target,
            bool dryRun, CancellationToken cancellationToken = default)
        {
            if (plan == null || target == null) throw new ArgumentNullException(nameof(plan));
            Require(_context.Database.IsNpgsql(), "PostgreSQL is required.");
            Require(_context.Database.CurrentTransaction == null && !_context.ChangeTracker.Entries().Any(),
                "Writer requires a fresh context and owns the sole transaction.");
            Require(target.SourceSnapshotSha256 == plan.SourceSnapshotSha256,
                "Operator-verified source snapshot identity differs from accepted input.");
            var configuredHost = new NpgsqlConnectionStringBuilder(_context.Database.GetConnectionString()).Host;
            Require(!string.IsNullOrWhiteSpace(configuredHost) &&
                    !configuredHost.Contains(plan.SourceServiceId, StringComparison.OrdinalIgnoreCase),
                "The source production Render endpoint is prohibited.");
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            try
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "SET LOCAL lock_timeout = '10s'; SET LOCAL statement_timeout = '30s'; SET LOCAL search_path = public, pg_catalog;",
                    cancellationToken);
                var connection = (NpgsqlConnection)_context.Database.GetDbConnection();
                var pgTransaction = (NpgsqlTransaction)transaction.GetDbTransaction();
                await using (var identity = new NpgsqlCommand(
                    "SELECT current_database(), current_user, host(inet_server_addr()), inet_server_port(), current_setting('session_replication_role')", connection, pgTransaction))
                await using (var reader = await identity.ExecuteReaderAsync(cancellationToken))
                {
                    Require(await reader.ReadAsync(cancellationToken) &&
                            reader.GetString(0) == target.Database && reader.GetString(1) == target.User &&
                            !reader.IsDBNull(2) && reader.GetString(2) == target.ServerAddress && reader.GetInt32(3) == target.ServerPort &&
                            reader.GetString(4) == "origin", "Target database/user/server identity or trigger execution mode differs.");
                }
                // Same transaction-scoped key and PostgreSQL hash used by live placement allocation.
                await _context.Database.ExecuteSqlRawAsync(
                    "SELECT pg_advisory_xact_lock(hashtextextended({0}, 0))",
                    new object[] { AQGreenPlacementTreeLock.Resource(plan.ScopeId) }, cancellationToken);
                // Freeze source facts, including correction/approval phantoms; serialize topology DML
                // even from writers that do not cooperate with the normal advisory lock.
                await _context.Database.ExecuteSqlRawAsync("""
                    LOCK TABLE public."EntryParticipations", public."EntryRecruiterCorrections",
                      public."EntryParticipationApprovalDecisions", public."MemberPayments",
                      public."AQGreenJoiningCheckouts", public."YocoWebhookReceipts",
                      public."Customers", public."AbpUsers", public."AbpTenants" IN SHARE MODE;
                    LOCK TABLE public."AQGreenPlacementTreeScopes", public."AQGreenNetworkPlacements"
                      IN SHARE ROW EXCLUSIVE MODE;
                    """, cancellationToken);
                await AQGreenBackfillSchemaGuard.VerifyAsync(_context, cancellationToken);
                await VerifySourceAsync(plan, cancellationToken);
                var ids = plan.Members.Select(m => m.ParticipantId).ToArray();
                var scope = await _context.AQGreenPlacementTreeScopes.IgnoreQueryFilters().AsNoTracking()
                    .SingleOrDefaultAsync(s => s.Id == plan.ScopeId, cancellationToken);
                var placements = await _context.AQGreenNetworkPlacements.IgnoreQueryFilters().AsNoTracking()
                    .Where(p => p.PlacementTreeScopeId == plan.ScopeId || ids.Contains(p.ParticipantId))
                    .ToListAsync(cancellationToken);
                var alreadyApplied = scope != null || placements.Count != 0;
                if (alreadyApplied)
                {
                    Require(scope != null && scope.TenantId == plan.TenantId && placements.Count == 6,
                        "Existing scope/placement delta is partial, foreign or unexpected; no repair is authorised.");
                    foreach (var member in plan.Members)
                    {
                        var existing = placements.SingleOrDefault(p => p.ParticipantId == member.ParticipantId);
                        Require(existing != null && existing.TenantId == plan.TenantId && existing.PlacementTreeScopeId == plan.ScopeId &&
                                existing.PlacementParentParticipantId == member.ParentId && existing.PlacementSlot == member.Slot &&
                                existing.CanonicalPath == member.CanonicalPath && existing.PlacedAt == plan.EffectiveAtUtc &&
                                existing.RulesVersion == AQGreenPlacementRules.CurrentVersion,
                            "Existing placement conflicts with accepted topology; overwrite is forbidden.");
                    }
                    await transaction.RollbackAsync(cancellationToken);
                    return new AQGreenBackfillResult(dryRun, true, 0, 0);
                }
                if (dryRun)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new AQGreenBackfillResult(true, false, 1, 6);
                }

                var newScope = AQGreenPlacementTreeScope.Create(plan.TenantId);
                newScope.Id = plan.ScopeId;
                _context.AQGreenPlacementTreeScopes.Add(newScope);
                await _context.SaveChangesAsync(cancellationToken);
                AQGreenNetworkPlacement root = null;
                foreach (var member in plan.Members)
                {
                    var placement = member.ParentId == null
                        ? AQGreenNetworkPlacement.CreateRoot(newScope, member.ParticipantId, plan.EffectiveAtUtc, AQGreenPlacementRules.CurrentVersion)
                        : AQGreenNetworkPlacement.CreateChild(root, member.ParticipantId, member.Slot.Value, plan.EffectiveAtUtc, AQGreenPlacementRules.CurrentVersion);
                    placement.Id = plan.PlacementId(member.ParticipantId);
                    Require(placement.CanonicalPath == member.CanonicalPath && (member.ParentId == null || placement.PlacementParentParticipantId == member.ParentId),
                        "Domain-generated accepted row differs; no topology search/fallback is authorised.");
                    _context.AQGreenNetworkPlacements.Add(placement);
                    await _context.SaveChangesAsync(cancellationToken);
                    if (member.ParentId == null) root = placement;
                }
                Require(await _context.AQGreenNetworkPlacements.CountAsync(p => p.PlacementTreeScopeId == plan.ScopeId, cancellationToken) == 6,
                    "Written delta differs from accepted six placements.");
                await transaction.CommitAsync(cancellationToken); // deferred exactly-one-root invariant fires here
                _context.ChangeTracker.Clear();
                return new AQGreenBackfillResult(false, false, 1, 6);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                _context.ChangeTracker.Clear();
                throw;
            }
        }

        private async Task VerifySourceAsync(AQGreenCustomer11BackfillPlan plan, CancellationToken ct)
        {
            var ids = plan.Members.Select(m => m.ParticipantId).ToArray();
            var customers = plan.Members.Select(m => m.CustomerId).ToArray();
            var sources = await _context.EntryParticipations.IgnoreQueryFilters().AsNoTracking()
                .Where(p => ids.Contains(p.Id)).ToListAsync(ct);
            Require(sources.Count == 6, "Expected participant missing or substituted.");
            Require(!await _context.EntryParticipations.IgnoreQueryFilters().AnyAsync(p =>
                p.TenantId == plan.TenantId && p.Status == EntryParticipationStatus.Active && !p.IsDeleted &&
                p.RecruiterCustomerId.HasValue && customers.Contains(p.RecruiterCustomerId.Value) && !ids.Contains(p.Id), ct),
                "Accepted component population changed: unexpected Active descendant.");
            Require(!await _context.EntryRecruiterCorrections.AnyAsync(c => ids.Contains(EF.Property<Guid>(c, "EntryParticipationId")), ct),
                "Recruiter correction conflicts with accepted no-correction source.");
            var tenant = await _context.Tenants.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(t => t.Id == plan.TenantId, ct);
            Require(tenant != null && tenant.IsActive && !tenant.IsDeleted, "Tenant ownership/lifecycle differs.");
            foreach (var member in plan.Members)
            {
                var source = sources.Single(p => p.Id == member.ParticipantId);
                Require(source.TenantId == plan.TenantId && source.CustomerId == member.CustomerId &&
                        source.RecruiterCustomerId == member.RecruiterCustomerId && source.Status == EntryParticipationStatus.Active && !source.IsDeleted &&
                        source.StartedAt == member.StartedAtUtc && source.ActivatedAt == member.ActivatedAtUtc &&
                        source.JoiningPaymentAmount == member.JoiningAmount && source.Currency == member.Currency && source.JoiningPaymentId == member.CapturedJoiningPaymentId &&
                        source.JoiningPaymentSchedule == AQGreenJoiningPaymentSchedule.Full &&
                        source.RegistrationPaymentId == null && source.ActivationPaymentId == null,
                    "Tenant/programme, participant, recruiter, lifecycle or snapshot source facts differ.");
                var customer = await _context.Customers.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(c => c.Id == member.CustomerId, ct);
                Require(customer != null && customer.TenantId == plan.TenantId && customer.IsActive && !customer.IsDeleted && customer.UserId == member.CapturedUserId,
                    "Customer ownership/lifecycle differs.");
                var user = await _context.Users.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(u => u.Id == customer.UserId, ct);
                Require(user != null && user.TenantId == plan.TenantId && user.IsActive && !user.IsDeleted && user.LockoutEndDateUtc == null,
                    "Retained user ownership/security state differs.");
                var payment = await _context.MemberPayments.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(p => p.Id == source.JoiningPaymentId, ct);
                Require(payment != null && payment.TenantId == plan.TenantId && payment.CustomerId == member.CustomerId && !payment.IsDeleted &&
                        payment.Purpose == MemberPaymentPurpose.AQGreenJoining && payment.Status == MemberPaymentStatus.Confirmed &&
                        payment.Amount == member.JoiningAmount && payment.Currency == member.Currency && payment.Provider == "YOCO" &&
                        !string.IsNullOrWhiteSpace(payment.ExternalReference) &&
                        payment.ConfirmedAt == member.PaymentConfirmedAtUtc, "Retained joining-payment linkage/finality differs.");
                var checkouts = await _context.AQGreenJoiningCheckouts.IgnoreQueryFilters().AsNoTracking()
                    .Where(c => c.ParticipationId == member.ParticipantId).ToListAsync(ct);
                Require(checkouts.Count == 1, "Retained joining-checkout count differs.");
                var checkout = checkouts.Single();
                Require(checkout.Id == member.CapturedCheckoutId && checkout.TenantId == plan.TenantId && checkout.CustomerId == member.CustomerId && !checkout.IsDeleted &&
                        checkout.Status == HostedPaymentCheckoutStatus.Completed && checkout.PaymentId == payment.Id &&
                        checkout.Amount == member.JoiningAmount && checkout.Currency == member.Currency && checkout.TerminatedAt == null &&
                        checkout.TerminatedByAdministratorUserId == null && string.IsNullOrWhiteSpace(checkout.TerminalEvidence) &&
                        !string.IsNullOrWhiteSpace(checkout.ProviderCheckoutId) &&
                        checkout.Schedule == AQGreenJoiningPaymentSchedule.Full && checkout.Stage == AQGreenJoiningPaymentStage.Full &&
                        checkout.CompletedAt == member.PaymentConfirmedAtUtc, "Retained joining-checkout evidence differs.");
                var receipts = await _context.YocoWebhookReceipts.IgnoreQueryFilters().AsNoTracking()
                    .Where(r => r.CheckoutReferenceId == checkout.Id).ToListAsync(ct);
                Require(receipts.Count == 1 && receipts[0].Id == member.CapturedReceiptId && receipts[0].ProcessedAt == member.CapturedReceiptProcessedAtUtc &&
                        receipts[0].TenantId == plan.TenantId && receipts[0].Programme == YocoCheckoutProgramme.AQGreen &&
                        receipts[0].ProviderCheckoutId == checkout.ProviderCheckoutId && receipts[0].PaymentId == payment.ExternalReference &&
                        !string.IsNullOrWhiteSpace(receipts[0].EventId) && receipts[0].PayloadHash != null &&
                        receipts[0].PayloadHash.Length == 64 && receipts[0].PayloadHash.All(c=>(c>='0' && c<='9') || (c>='a' && c<='f')),
                    "Retained joining-receipt binding differs.");
                var decisions = await _context.EntryParticipationApprovalDecisions.AsNoTracking()
                    .Where(d => EF.Property<Guid>(d, "EntryParticipationId") == member.ParticipantId).ToListAsync(ct);
                if (member.RequiresRetainedPositiveApproval)
                {
                    Require(decisions.Count == 1 && decisions[0].Id == member.CapturedApprovalDecisionId && decisions[0].Approved &&
                            decisions[0].AdministratorUserId == member.CapturedApprovingActorId &&
                            decisions[0].DecidedAt == member.ActivatedAtUtc && decisions[0].Reason == null,
                        "Retained actual approval value/time differs.");
                    var actor = await _context.Users.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(u => u.Id == decisions[0].AdministratorUserId, ct);
                    Require(actor != null && !actor.IsDeleted && actor.CreationTime == member.CapturedActorCreationTimeUtc && actor.CreationTime <= decisions[0].DecidedAt,
                        "Retained approving-actor reference absent or inconsistent.");
                }
                else Require(decisions.Count == 0, "Root UNKNOWN approval evidence changed; reconciliation is required.");
            }
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
