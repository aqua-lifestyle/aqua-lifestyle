using System;
using System.Collections.Generic;
using Abp.Dependency;
using AqualLifeStyle.Domain.Onyx;
using Npgsql;

namespace AqualLifeStyle.EntityFrameworkCore
{
    public sealed class OnyxGraduationTransactionFailureClassifier
        : IOnyxGraduationTransactionFailureClassifier, ISingletonDependency
    {
        private static readonly HashSet<string> KnownGraduationConstraints =
            new(StringComparer.Ordinal)
            {
                "IX_OnyxGraduationDecisions_EntryParticipationId",
                "IX_OnyxGraduationDecisions_LoanAgreementId",
                "IX_OnyxGraduationDecisions_OnyxParticipationId",
                "IX_OnyxParticipations_TenantId_CustomerId"
            };

        public OnyxGraduationTransactionFailure Classify(
            Exception exception,
            bool commitWasAttempted)
        {
            var postgres = Find<PostgresException>(exception);
            if (postgres != null)
            {
                if (postgres.SqlState == PostgresErrorCodes.SerializationFailure)
                    return new OnyxGraduationTransactionFailure(
                        OnyxGraduationTransactionFailureKind.SerializationFailure);
                if (postgres.SqlState == PostgresErrorCodes.UniqueViolation)
                    return new OnyxGraduationTransactionFailure(
                        KnownGraduationConstraints.Contains(postgres.ConstraintName)
                            ? OnyxGraduationTransactionFailureKind.KnownGraduationUniqueCollision
                            : OnyxGraduationTransactionFailureKind.UnknownUniqueViolation,
                        postgres.ConstraintName);
                return new OnyxGraduationTransactionFailure(
                    OnyxGraduationTransactionFailureKind.NotRetryable,
                    postgres.ConstraintName);
            }

            if (commitWasAttempted && Find<NpgsqlException>(exception) != null)
                return new OnyxGraduationTransactionFailure(
                    OnyxGraduationTransactionFailureKind.CommitOutcomeUnknown);

            return new OnyxGraduationTransactionFailure(
                OnyxGraduationTransactionFailureKind.NotRetryable);
        }

        private static T Find<T>(Exception exception) where T : Exception
        {
            for (var current = exception; current != null; current = current.InnerException)
            {
                if (current is T match) return match;
            }

            return null;
        }
    }
}
