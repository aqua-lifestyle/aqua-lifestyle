using System;

namespace AqualLifeStyle.Domain.Onyx
{
    public enum OnyxGraduationTransactionFailureKind
    {
        NotRetryable = 0,
        SerializationFailure = 1,
        KnownGraduationUniqueCollision = 2,
        UnknownUniqueViolation = 3,
        CommitOutcomeUnknown = 4
    }

    public sealed class OnyxGraduationTransactionFailure
    {
        public OnyxGraduationTransactionFailure(
            OnyxGraduationTransactionFailureKind kind,
            string databaseConstraintName = null)
        {
            Kind = kind;
            DatabaseConstraintName = databaseConstraintName;
        }

        public OnyxGraduationTransactionFailureKind Kind { get; }
        public string DatabaseConstraintName { get; }
    }

    public interface IOnyxGraduationTransactionFailureClassifier
    {
        OnyxGraduationTransactionFailure Classify(
            Exception exception,
            bool commitWasAttempted);
    }
}
