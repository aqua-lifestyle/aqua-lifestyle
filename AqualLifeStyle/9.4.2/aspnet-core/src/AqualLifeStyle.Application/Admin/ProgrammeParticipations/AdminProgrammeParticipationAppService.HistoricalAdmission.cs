using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using Abp.Auditing;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.Runtime.Session;
using AqualLifeStyle.Application.Admin.ProgrammeParticipations.Dto;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.Admin.ProgrammeParticipations
{
    public partial class AdminProgrammeParticipationAppService
    {
        public IRepository<AQGreenHistoricalTreeReview, Guid> HistoricalTreeReviews { get; set; }
        public IRepository<AQGreenHistoricalMemberReference, Guid> HistoricalMemberReferences { get; set; }
        public IRepository<EntryCommissionPeriod, Guid> HistoricalCommissionPeriods { get; set; }

        [AbpAuthorize(AquaPermissions.Admin.ProgrammeParticipations.Approve,
            AquaPermissions.Admin.ProgrammeParticipations.ExportLegacyReconciliationEvidence,
            AquaPermissions.Admin.AllTenants, RequireAllPermissions = true)]
        [UnitOfWork(IsDisabled = true)]
        public async Task<Guid> OpenAQGreenHistoricalReviewAsync(OpenAQGreenHistoricalReviewInput input)
        {
            RequireHistoricalHost(input?.TenantId ?? 0, input?.ScopeId ?? Guid.Empty, input?.EvidenceReference);
            if (input.ExpectedMembers < 1 || input.ExpectedMembers > 1000)
                throw Failed("Historical review", "Supply the verified roster member count (1–1000).");

            using var uow = _unitOfWorkManager.Begin(new UnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.ReadCommitted });
            using (DisableAllTenantDataFiltersForHost())
            {
                await _placementTreeLock.AcquireAsync(input.ScopeId);
                if (!await _networkPlacementRepository.GetAll().AnyAsync(p => p.TenantId == input.TenantId &&
                    p.PlacementTreeScopeId == input.ScopeId && !p.PlacementParentParticipantId.HasValue))
                    throw Failed("Historical review", "Select an established tree in the requested Tenant.");
                var review = await HistoricalTreeReviews.FirstOrDefaultAsync(r => r.TenantId == input.TenantId && r.ScopeId == input.ScopeId);
                if (review == null)
                {
                    review = new AQGreenHistoricalTreeReview(input.TenantId, input.ScopeId, input.EvidenceReference.Trim(),
                        input.ExpectedMembers, AbpSession.GetUserId(), await _placementClock.GetUtcNowAsync());
                    await HistoricalTreeReviews.InsertAsync(review);
                }
                else if (review.EvidenceReference != input.EvidenceReference.Trim() || review.ExpectedMembers != input.ExpectedMembers)
                    throw Failed("Historical review", "A different review already owns this tree.");
                else if (review.ClearedAt.HasValue)
                    throw Failed("Historical review", "This review is already cleared; no new hold was applied.");
                await uow.CompleteAsync();
                return review.Id;
            }
        }

        [AbpAuthorize(AquaPermissions.Admin.ProgrammeParticipations.Approve,
            AquaPermissions.Admin.ProgrammeParticipations.ExportLegacyReconciliationEvidence,
            AquaPermissions.Admin.AllTenants, RequireAllPermissions = true)]
        [DisableAuditing]
        [UnitOfWork(IsDisabled = true)]
        public async Task CompleteAQGreenHistoricalReviewAsync(CompleteAQGreenHistoricalReviewInput input)
        {
            RequireHistoricalHost(input?.TenantId ?? 0, input?.ScopeId ?? Guid.Empty, input?.EvidenceReference);
            var rows = input.Members;
            if (rows == null || rows.Length < 1 || rows.Length > 1000 || rows.Any(r => r == null ||
                r.ParticipantId == Guid.Empty || r.CustomerId <= 0 || string.IsNullOrWhiteSpace(r.OriginalReference) ||
                r.OriginalReference.Trim().Length > 128 || r.StartedAtUtc.Kind != DateTimeKind.Utc ||
                r.ActivatedAtUtc.Kind != DateTimeKind.Utc || r.PlacedAtUtc.Kind != DateTimeKind.Utc ||
                r.ActivatedAtUtc < r.StartedAtUtc || r.PlacedAtUtc < r.StartedAtUtc ||
                (r.ParentParticipantId.HasValue ? r.Slot is < 1 or > 5 or null : r.Slot.HasValue)) ||
                rows.Select(r => r.OriginalReference.Trim().ToUpperInvariant()).Distinct().Count() != rows.Length ||
                rows.Select(r => r.ParticipantId).Distinct().Count() != rows.Length ||
                rows.Select(r => r.CustomerId).Distinct().Count() != rows.Length)
                throw Failed("Historical review", "Resolve incomplete dates, positions and duplicate identities before approval.");

            using var uow = _unitOfWorkManager.Begin(new UnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.ReadCommitted });
            using (DisableAllTenantDataFiltersForHost())
            {
                // Area transition locks precede the tree lock, as in ordinary approval.
                await _hostedPaymentCheckoutLock.AcquireCustomerAreaTransitionsAsync(rows.Select(r => r.CustomerId).OrderBy(i => i).ToArray());
                await _placementTreeLock.AcquireAsync(input.ScopeId);
                var review = await HistoricalTreeReviews.FirstOrDefaultAsync(r => r.TenantId == input.TenantId && r.ScopeId == input.ScopeId);
                if (review == null || review.EvidenceReference != input.EvidenceReference.Trim() || review.ExpectedMembers != rows.Length)
                    throw Failed("Historical review", "Approve the complete verified roster for the open tree review.");
                var now = await _placementClock.GetUtcNowAsync();
                var ids = rows.Select(r => r.ParticipantId).ToArray();
                var keys = rows.Select(r => r.OriginalReference.Trim().ToUpperInvariant()).ToArray();
                var references = await HistoricalMemberReferences.GetAll().Where(r => r.TenantId == input.TenantId &&
                    (ids.Contains(r.ParticipantId) || keys.Contains(r.ReferenceKey))).ToListAsync();
                var placements = await _networkPlacementRepository.GetAll().AsNoTracking().Where(p => p.TenantId == input.TenantId &&
                    (p.PlacementTreeScopeId == input.ScopeId || ids.Contains(p.ParticipantId))).ToListAsync();
                var byParticipant = placements.ToDictionary(p => p.ParticipantId);
                var occupied = placements.Where(p => p.PlacementTreeScopeId == input.ScopeId && p.PlacementParentParticipantId.HasValue)
                    .Select(p => (p.PlacementParentParticipantId.Value, p.PlacementSlot.Value)).ToHashSet();
                var pending = new List<AQGreenHistoricalPlacementInput>();
                foreach (var row in rows)
                {
                    if (row.ActivatedAtUtc > now || row.PlacedAtUtc > now)
                        throw Failed("Historical review", "A historical date is in the future.");
                    var reference = row.OriginalReference.Trim();
                    if (review.ClearedAt.HasValue)
                    {
                        var exact = references.Any(r => r.ReviewId == review.Id && r.ParticipantId == row.ParticipantId && r.OriginalReference == reference);
                        if (!exact || !byParticipant.ContainsKey(row.ParticipantId))
                            throw Failed("Historical review", "This request conflicts with the recorded clearance.");
                    }
                    else if (references.Any(r => r.ParticipantId == row.ParticipantId || r.ReferenceKey == reference.ToUpperInvariant()))
                        throw Failed("Historical review", "The original reference or participation is already reconciled.");

                    var entry = await _entryParticipationRepository.FirstOrDefaultAsync(p => p.Id == row.ParticipantId && p.TenantId == input.TenantId);
                    var customer = await _customerRepository.FirstOrDefaultAsync(c => c.TenantId == input.TenantId && c.Id == row.CustomerId);
                    if (customer == null || entry == null || entry.CustomerId != row.CustomerId ||
                        entry.Status != EntryParticipationStatus.Active || entry.StartedAt != row.StartedAtUtc || entry.ActivatedAt != row.ActivatedAtUtc)
                        throw Failed("Historical review", "The supplied identity, active membership or historical dates do not match the existing record.");
                    var stabilizedArea = await _approvalAuthorityStabilizer.StabilizeAsync(input.TenantId, customer.Id, null);
                    if (customer.AreaId != stabilizedArea)
                        throw Failed("Historical review", "The Area assignment changed. Reload before proceeding.");
                    await EnsureCanAdministerAreaAsync(customer);
                    await ValidateAQGreenJoiningPaymentAsync(entry);
                    if (byParticipant.TryGetValue(row.ParticipantId, out var placement))
                    {
                        if (placement.PlacementTreeScopeId != input.ScopeId || placement.PlacementParentParticipantId != row.ParentParticipantId ||
                            placement.PlacementSlot != row.Slot || placement.PlacedAt != row.PlacedAtUtc)
                            throw Failed("Historical review", "The verified position conflicts with an existing placement.");
                    }
                    else
                    {
                        if (!row.ParentParticipantId.HasValue || !occupied.Add((row.ParentParticipantId.Value, row.Slot.Value)))
                            throw Failed("Historical review", "The requested historical root or slot is not available.");
                        pending.Add(row);
                    }
                }

                if (!review.ClearedAt.HasValue)
                {
                    if (pending.Count > 0)
                    {
                        // A prospective/CLEAN_START authorization cannot amend a
                        // commissioned historical population or previously calculated period.
                        var cutover = D10Source.Read();
                        if (cutover?.Decision.TenantId == input.TenantId)
                            throw Failed("Historical review", "New historical placements require a separately approved D10 reconciliation for this commissioned Tenant.");
                        var earliest = pending.Min(r => r.PlacedAtUtc);
                        using (CurrentUnitOfWork.DisableFilter(AbpDataFilters.SoftDelete))
                            if (await HistoricalCommissionPeriods.GetAll().AnyAsync(p => p.TenantId == input.TenantId && p.PeriodEnd >= earliest))
                                throw Failed("Historical review", "A previously calculated commission period overlaps these placements. Preserve the financial history and resolve the conflict separately.");
                    }
                    while (pending.Count > 0)
                    {
                        var ready = pending.Where(r => byParticipant.TryGetValue(r.ParentParticipantId.Value, out var p) &&
                            p.PlacementTreeScopeId == input.ScopeId).ToArray();
                        if (ready.Length == 0) throw Failed("Historical review", "A parent is missing, cyclic, or in another tree.");
                        foreach (var row in ready)
                        {
                            var parent = byParticipant[row.ParentParticipantId.Value];
                            if (row.PlacedAtUtc < parent.PlacedAt)
                                throw Failed("Historical review", "The recorded child placement predates the parent.");
                            var placement = AQGreenNetworkPlacement.CreateChild(parent, row.ParticipantId, row.Slot.Value,
                                row.PlacedAtUtc, AQGreenPlacementRules.CurrentVersion);
                            await _networkPlacementRepository.InsertAsync(placement);
                            byParticipant.Add(row.ParticipantId, placement);
                            pending.Remove(row);
                        }
                        // Insert ancestors before dependent rows under the same transaction.
                        await CurrentUnitOfWork.SaveChangesAsync();
                    }
                    foreach (var row in rows)
                        await HistoricalMemberReferences.InsertAsync(new AQGreenHistoricalMemberReference(review, row.ParticipantId, row.OriginalReference.Trim()));
                    await CurrentUnitOfWork.SaveChangesAsync();
                    review.Clear(AbpSession.GetUserId(), now);
                }
                await uow.CompleteAsync();
            }
        }

        private void RequireHistoricalHost(int tenantId, Guid scopeId, string evidence)
        {
            if (AbpSession.TenantId.HasValue)
                throw new AbpAuthorizationException("Tree-wide historical approval requires host authority.");
            if (tenantId <= 0 || scopeId == Guid.Empty || string.IsNullOrWhiteSpace(evidence) || evidence.Trim().Length > 128)
                throw Failed("Historical review", "Supply a Tenant, tree and reviewed evidence reference.");
        }
    }
}
