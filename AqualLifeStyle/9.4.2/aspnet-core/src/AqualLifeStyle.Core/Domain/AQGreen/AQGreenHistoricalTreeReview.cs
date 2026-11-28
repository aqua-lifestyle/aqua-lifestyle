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

        private AQGreenHistoricalTreeReview() { }

        public AQGreenHistoricalTreeReview(int tenantId, Guid scopeId, string evidenceReference, int expectedMembers, long actor, DateTime now)
        {
            Id = Guid.NewGuid();
            TenantId = tenantId;
            ScopeId = scopeId;
            EvidenceReference = evidenceReference;
            ExpectedMembers = expectedMembers;
            OpenedBy = actor;
            OpenedAt = now;
        }

        public void Clear(long actor, DateTime now)
        {
            if (ClearedAt.HasValue || now < OpenedAt) throw new InvalidOperationException("Invalid historical clearance.");
            ClearedBy = actor;
            ClearedAt = now;
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
