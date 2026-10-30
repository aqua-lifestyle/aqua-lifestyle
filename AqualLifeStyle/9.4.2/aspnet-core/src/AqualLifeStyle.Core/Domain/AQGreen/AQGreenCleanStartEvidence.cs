using System;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AqualLifeStyle.Domain.AQGreen
{
    // Operator evidence is independently authenticated and pinned outside Git, just
    // like historical D10 inputs. A self-hash does not authenticate classification.
    public sealed class AQGreenCleanStartEvidence
    {
        public const string OwnerAttestedTestOnly = "OWNER_ATTESTED_TEST_ONLY_V1";
        public const string ZeroPaymentExposure = "ZERO_PAYMENT_EXPOSURE_V1";
        public const string ProspectiveBoundary = "OWNER_AUTHORIZED_PROSPECTIVE_UTC_V1";
        public string ContractVersion { get; set; }
        // V2 is explicit and bound to the native retirement receipt/authority.
        // Omitted fields preserve the original V1 canonical evidence encoding.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string BoundaryPolicy { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string BoundaryAuthorizationDigest { get; set; }
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
        // Business-owner classification, never an assertion of verified provider mode.
        public string PaymentAdmissionMode { get; set; }
        public string OwnerTestOnlyAttestationDigest { get; set; }
        public string ProviderModeVerification { get; set; }
        public bool? ProtectedFinancialRecordsPreserved { get; set; }
        public string HistoricalFinancialRecordsDigest { get; set; }
        public AQGreenCleanStartState State { get; set; }

        public bool AdmitsPreservedState(AQGreenCleanStartState state)
        {
            if (state == null) return false;
            if (PaymentAdmissionMode == null || PaymentAdmissionMode == ZeroPaymentExposure)
                return OwnerTestOnlyAttestationDigest == null && ProviderModeVerification == null &&
                    ProtectedFinancialRecordsPreserved == null && HistoricalFinancialRecordsDigest == null &&
                    state.HasNoProtectedExposure() && (state.PaymentAdmissionMode == null || state.PaymentAdmissionMode == ZeroPaymentExposure);
            return PaymentAdmissionMode == OwnerAttestedTestOnly &&
                ValidDigest(OwnerTestOnlyAttestationDigest) && ProviderModeVerification == "NOT_INDEPENDENTLY_VERIFIED" &&
                ProtectedFinancialRecordsPreserved == true && ValidDigest(HistoricalFinancialRecordsDigest) &&
                state.HasNoNonPaymentProtectedExposure() && state.ProtectedPaymentCount.HasValue && state.ProtectedPaymentCount >= 0 &&
                state.PaymentAdmissionMode == OwnerAttestedTestOnly &&
                state.OwnerTestOnlyAttestationDigest == OwnerTestOnlyAttestationDigest &&
                state.ResetProtectedPaymentCount == state.ProtectedPaymentCount &&
                ValidDigest(state.PaymentEvidenceDigest) && state.PaymentEvidenceDigest == state.ResetPaymentEvidenceDigest &&
                HistoricalFinancialRecordsDigest == state.ResetTenantFinancialRecordsDigest;
        }

        private static bool ValidDigest(string value) => value != null && value != new string('0',64) &&
            Regex.IsMatch(value,"\\A[0-9a-f]{64}\\z",RegexOptions.CultureInvariant);
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
        public string PaymentEvidenceDigest { get; set; }
        public string TenantFinancialRecordsDigest { get; set; }
        public string AuditHistoryDigest { get; set; }
        public string ParticipationFactsDigest { get; set; }
        public string PaymentAdmissionMode { get; set; }
        public string OwnerTestOnlyAttestationDigest { get; set; }
        public string ResetPaymentEvidenceDigest { get; set; }
        public string ResetTenantFinancialRecordsDigest { get; set; }
        public long? ResetProtectedPaymentCount { get; set; }
        public DateTimeOffset? ResetAtUtc { get; set; }
        public DateTimeOffset? BoundaryUtc { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string BoundaryPolicy { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string BoundaryAuthorizationDigest { get; set; }

        public bool HasNoProtectedExposure() => new[] { UnclassifiedMembershipCount,
            ProtectedPaymentCount, RealCommissionCount, LoanCount, GraduationCount,
            MonthlyObligationCount, PayoutCount, OrphanTopologyCount, InvalidParticipationCount }
            .All(count => count.HasValue && count.Value == 0);

        public bool HasNoNonPaymentProtectedExposure() => new[] { UnclassifiedMembershipCount,
            RealCommissionCount, LoanCount, GraduationCount, MonthlyObligationCount,
            PayoutCount, OrphanTopologyCount, InvalidParticipationCount }
            .All(count => count.HasValue && count.Value == 0);
    }
}
