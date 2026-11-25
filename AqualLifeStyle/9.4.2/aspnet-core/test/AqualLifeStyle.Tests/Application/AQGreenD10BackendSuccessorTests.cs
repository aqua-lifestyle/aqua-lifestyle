using System;
using System.Linq;
using AqualLifeStyle.Application.ProgrammeParticipations;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Tests.WebHost;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class AQGreenD10BackendSuccessorTests
    {
        private static AQGreenD10SyntheticEvidence Evidence()
        {
            var boundary = new AQGreenReconciliationSourceBoundary
            {
                SourceDatabaseIdentityEvidence = AQGreenD10SyntheticEvidence.Hash("synthetic-production-database"),
                SourceSchemaVersion = AQGreenD10BackendSuccessor.IndependentRootSchemaSha256,
                SourceAssemblyDigest = AQGreenD10SyntheticEvidence.Hash("current-ef-assembly"),
                ExpectedMigrations = ExpectedMigrations(),
                AppliedMigrations = ExpectedMigrations()
            };
            return AQGreenD10SyntheticEvidence.Create(AQGreenD10SyntheticEvidence.Configuration(), boundary: boundary);
        }

        private static string[] ExpectedMigrations()
        {
            var prior = Enumerable.Range(1, 54).Select(i => $"202601010000{i:00}_PriorMigration{i:00}");
            return prior.Append(AQGreenD10BackendSuccessor.PreviousMigration)
                .Append(AQGreenD10BackendSuccessor.IndependentRootMigration).ToArray();
        }

        private static AQGreenReconciliationSourceBoundary Boundary() => new()
        {
            SourceDatabaseIdentityEvidence = AQGreenD10SyntheticEvidence.Hash("synthetic-production-database"),
            SourceSchemaVersion = AQGreenD10BackendSuccessor.IndependentRootSchemaSha256,
            SourceAssemblyDigest = AQGreenD10SyntheticEvidence.Hash("current-ef-assembly"),
            ExpectedMigrations = ExpectedMigrations(),
            AppliedMigrations = ExpectedMigrations()
        };

        private static IConfiguration Configuration(string revision = null) => new ConfigurationBuilder()
            .AddInMemoryCollection(new[] { new System.Collections.Generic.KeyValuePair<string, string>(
                "RENDER_GIT_COMMIT", revision ?? new string('a', 40)) }).Build();

        private static void Reject(AQGreenD10Decision decision, AQGreenReconciliationSourceBoundary boundary,
            IConfiguration configuration, string code) =>
            Should.Throw<InvalidOperationException>(() => AQGreenD10BackendSuccessor.Accept(decision, boundary, configuration))
                .Message.ShouldBe(code);

        [Fact]
        public void ExactProductionSchemaAndCompleteMigration56HistoryAreAcceptedWithoutRuntimePayload()
        {
            var evidence = Evidence();
            AQGreenD10BackendSuccessor.Accept(evidence.Decision, Boundary(), Configuration()).ShouldBeTrue();
        }

        [Fact]
        public void ChangedSchemaDigestIsRejectedEvenWhenMigrationHistoryClaimsSuccess()
        {
            var evidence = Evidence();
            var boundary = Boundary();
            boundary.SourceSchemaVersion = AQGreenD10SyntheticEvidence.Hash("unreviewed-schema");
            Reject(evidence.Decision, boundary, Configuration(), "D10_BACKEND_SUCCESSOR_SCHEMA_MISMATCH");
        }

        [Fact]
        public void WrongDatabaseIdentityIsRejected()
        {
            var evidence = Evidence();
            var boundary = Boundary();
            boundary.SourceDatabaseIdentityEvidence = AQGreenD10SyntheticEvidence.Hash("other-database");
            Reject(evidence.Decision, boundary, Configuration(), "D10_BACKEND_SUCCESSOR_DATABASE_IDENTITY_MISMATCH");
        }

        [Fact]
        public void PendingOrUnexpectedMigrationsAreRejected()
        {
            var evidence = Evidence();
            var pending = Boundary();
            pending.AppliedMigrations = pending.AppliedMigrations.Take(55).ToArray();
            Reject(evidence.Decision, pending, Configuration(), "D10_BACKEND_SUCCESSOR_MIGRATION_HISTORY_MISMATCH");

            var unexpected = Boundary();
            unexpected.ExpectedMigrations = unexpected.ExpectedMigrations.Append("20990101000000_Unreviewed").ToArray();
            unexpected.AppliedMigrations = unexpected.ExpectedMigrations.ToArray();
            Reject(evidence.Decision, unexpected, Configuration(), "D10_BACKEND_SUCCESSOR_MIGRATION_HISTORY_MISMATCH");
        }

        [Fact]
        public void MalformedRenderRevisionIsRejected()
        {
            var evidence = Evidence();
            Reject(evidence.Decision, Boundary(), Configuration("not-a-render-commit"),
                "D10_BACKEND_SUCCESSOR_DEPLOYMENT_IDENTITY_MISSING");
        }
    }
}
