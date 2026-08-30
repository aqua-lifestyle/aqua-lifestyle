using System;
using System.Threading.Tasks;
using AqualLifeStyle.Application.Admin.ProgrammeParticipations;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.EntityFrameworkCore;
using Npgsql;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public sealed class AQGreenGraduationInfrastructureTests
    {
        [Fact]
        public async Task ProductionSelector_RemainsDormantLegacyV1()
        {
            var selector = new LegacyV1AQGreenGraduationStructuralModelSelector();

            (await selector.SelectAsync(1, Guid.NewGuid()))
                .ShouldBe(AQGreenGraduationStructuralModel.LegacyV1);
        }

        [Fact]
        public void PostgreSqlFailureClassifier_RetriesOnlySerializationAndKnownGraduationKeys()
        {
            var classifier = new OnyxGraduationTransactionFailureClassifier();

            classifier.Classify(
                    PostgreSql(PostgresErrorCodes.SerializationFailure),
                    commitWasAttempted: false)
                .Kind.ShouldBe(
                    OnyxGraduationTransactionFailureKind.SerializationFailure);
            classifier.Classify(
                    PostgreSql(
                        PostgresErrorCodes.UniqueViolation,
                        "IX_OnyxGraduationDecisions_LoanAgreementId"),
                    commitWasAttempted: false)
                .Kind.ShouldBe(
                    OnyxGraduationTransactionFailureKind.KnownGraduationUniqueCollision);
            classifier.Classify(
                    PostgreSql(
                        PostgresErrorCodes.UniqueViolation,
                        "IX_UnrelatedBusinessFact"),
                    commitWasAttempted: false)
                .Kind.ShouldBe(
                    OnyxGraduationTransactionFailureKind.UnknownUniqueViolation);
        }

        [Fact]
        public void PostgreSqlFailureClassifier_RequiresCommitPhaseForAmbiguousConnectionFailure()
        {
            var classifier = new OnyxGraduationTransactionFailureClassifier();
            var connectionFailure = new NpgsqlException("Acknowledgement lost.");

            classifier.Classify(connectionFailure, commitWasAttempted: false)
                .Kind.ShouldBe(OnyxGraduationTransactionFailureKind.NotRetryable);
            classifier.Classify(connectionFailure, commitWasAttempted: true)
                .Kind.ShouldBe(OnyxGraduationTransactionFailureKind.CommitOutcomeUnknown);
        }

        private static PostgresException PostgreSql(
            string sqlState,
            string constraintName = null) =>
            new(
                "test database failure",
                "ERROR",
                "ERROR",
                sqlState,
                detail: null,
                hint: null,
                position: 0,
                internalPosition: 0,
                internalQuery: null,
                where: null,
                schemaName: "public",
                tableName: "test",
                columnName: null,
                dataTypeName: null,
                constraintName,
                file: "test.c",
                line: "1",
                routine: "test");
    }
}
