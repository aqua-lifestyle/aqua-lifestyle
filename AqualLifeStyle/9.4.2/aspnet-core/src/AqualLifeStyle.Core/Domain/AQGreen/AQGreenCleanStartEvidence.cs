using System;
using System.Linq;

namespace AqualLifeStyle.Domain.AQGreen
{
    // Operator evidence is independently authenticated and pinned outside Git, just
    // like historical D10 inputs. A self-hash does not authenticate classification.
    public sealed class AQGreenCleanStartEvidence
    {
        public string ContractVersion { get; set; }
        public string EvidenceClassification { get; set; }
        public int TenantId { get; set; }
        public string ResetId { get; set; }
        public DateTime ResetAtUtc { get; set; }
        public string ClassificationDigest { get; set; }
        public string IndependentAcceptanceDigest { get; set; }
        public string OwnerResetAuthorizationDigest { get; set; }
        public string ResetSqlDigest { get; set; }
        public string BeforeStateDigest { get; set; }
        public string DatabaseIdentityDigest { get; set; }
        public string SchemaDigest { get; set; }
        public bool? NoGenuineMembershipsInResetScope { get; set; }
        public bool? AutomaticPayoutsDisabled { get; set; }
        public AQGreenCleanStartState State { get; set; }
    }

    // Nullable counts prevent omitted evidence becoming an apparently safe zero.
    public sealed class AQGreenCleanStartState
    {
        public long? PopulationCount { get; set; }
        public long? ArchivedCount { get; set; }
        public long? PlacementCount { get; set; }
        public long? LegacyMembershipCount { get; set; }
        public long? UnclassifiedMembershipCount { get; set; }
        public long? ProtectedPaymentCount { get; set; }
        public long? RealCommissionCount { get; set; }
        public long? LoanCount { get; set; }
        public long? GraduationCount { get; set; }
        public long? MonthlyObligationCount { get; set; }
        public long? PayoutCount { get; set; }
        public long? OrphanTopologyCount { get; set; }
        public long? InvalidParticipationCount { get; set; }
        public string SourceDigest { get; set; }
        public string ArchiveDigest { get; set; }
        public string ResetReceiptDigest { get; set; }
        public DateTimeOffset? ResetAtUtc { get; set; }
        public DateTimeOffset? BoundaryUtc { get; set; }

        public bool HasNoProtectedExposure() => new[] { UnclassifiedMembershipCount,
            ProtectedPaymentCount, RealCommissionCount, LoanCount, GraduationCount,
            MonthlyObligationCount, PayoutCount, OrphanTopologyCount, InvalidParticipationCount }
            .All(count => count.HasValue && count.Value == 0);
    }
}
