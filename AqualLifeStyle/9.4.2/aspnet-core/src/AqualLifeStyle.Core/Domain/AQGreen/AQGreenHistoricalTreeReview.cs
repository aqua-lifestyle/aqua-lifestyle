using System;
using Abp.Domain.Entities;

namespace AqualLifeStyle.Domain.AQGreen
{
    public sealed class AQGreenHistoricalTreeReview : Entity<Guid>, IMustHaveTenant
    {
        public int TenantId { get; set; }
        public Guid ScopeId { get; private set; }
        public string EvidenceReference { get; private set; }
        public int ExpectedMembers { get; private set; }
        public long OpenedBy { get; private set; }
        public DateTime OpenedAt { get; private set; }
        public long? ClearedBy { get; private set; }
        public DateTime? ClearedAt { get; private set; }
        public string RosterDigest { get; private set; }
        public long? PlacementCountAtOpen { get; private set; }
        public DateTime? ProtectedAt { get; private set; }
        public long? CancelledBy { get; private set; }
        public DateTime? CancelledAt { get; private set; }
        public string CancellationReason { get; private set; }
        public string CancellationEvidenceReference { get; private set; }

        private AQGreenHistoricalTreeReview() { }

        public AQGreenHistoricalTreeReview(int tenantId, Guid scopeId, string evidenceReference, int expectedMembers,
            long actor, DateTime now, string rosterDigest, long? placementCountAtOpen)
        {
            Id = Guid.NewGuid();
            TenantId = tenantId;
            ScopeId = scopeId;
            EvidenceReference = evidenceReference;
            ExpectedMembers = expectedMembers;
            OpenedBy = actor;
            OpenedAt = now;
            RosterDigest = rosterDigest;
            PlacementCountAtOpen = placementCountAtOpen;
        }

        public void BindRoster(string digest)
        {
            if (ClearedAt.HasValue || CancelledAt.HasValue || RosterDigest != null || string.IsNullOrWhiteSpace(digest))
                throw new InvalidOperationException("Invalid historical register binding.");
            RosterDigest = digest;
        }

        public void Protect(DateTime now)
        {
            if (ClearedAt.HasValue || CancelledAt.HasValue || now < OpenedAt)
                throw new InvalidOperationException("Invalid historical hold protection.");
            ProtectedAt ??= now;
        }

        public void Clear(long actor, DateTime now)
        {
            if (ClearedAt.HasValue || CancelledAt.HasValue || RosterDigest == null || !ProtectedAt.HasValue || now < ProtectedAt.Value)
                throw new InvalidOperationException("Invalid historical clearance.");
            ClearedBy = actor;
            ClearedAt = now;
        }

        public void Cancel(long actor, DateTime now, string reason, string evidenceReference)
        {
            if (ClearedAt.HasValue || CancelledAt.HasValue || ProtectedAt.HasValue || !PlacementCountAtOpen.HasValue ||
                now < OpenedAt || actor <= 0 || string.IsNullOrWhiteSpace(reason) || string.IsNullOrWhiteSpace(evidenceReference))
                throw new InvalidOperationException("Invalid historical cancellation.");
            CancelledBy = actor;
            CancelledAt = now;
            CancellationReason = reason;
            CancellationEvidenceReference = evidenceReference;
        }
    }

    public sealed class AQGreenHistoricalMemberReference : Entity<Guid>, IMustHaveTenant
    {
        public int TenantId { get; set; }
        public Guid ReviewId { get; private set; }
        public Guid ParticipantId { get; private set; }
        public string OriginalReference { get; private set; }
        public string ReferenceKey { get; private set; }

        private AQGreenHistoricalMemberReference() { }

        public AQGreenHistoricalMemberReference(AQGreenHistoricalTreeReview review, Guid participantId, string reference)
        {
            Id = Guid.NewGuid();
            TenantId = review.TenantId;
            ReviewId = review.Id;
            ParticipantId = participantId;
            OriginalReference = reference;
            ReferenceKey = reference.ToUpperInvariant();
        }
    }
}
