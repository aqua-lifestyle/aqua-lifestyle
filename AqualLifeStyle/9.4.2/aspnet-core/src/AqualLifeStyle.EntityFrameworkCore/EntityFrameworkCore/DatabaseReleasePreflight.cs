using System;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.EntityFrameworkCore
{
    /// <summary>Read-only release/schema boundary. API startup never applies migrations.</summary>
    public static class DatabaseReleasePreflight
    {
        public static void Verify(AqualLifeStyleDbContext context, IConfiguration configuration = null)
        {
            var expected = context.Database.GetMigrations().ToArray();
            var applied = context.Database.GetAppliedMigrations().ToArray();
            // The sole pending-migration exception is the exact additive
            // independent-root table. The candidate is still required to pass
            // D10 startup with the old schema and its qualified assembly pin
            // before it can serve. The original commissioned configuration is
            // immutable; preflight cannot change its financial-operation flag.
            const string migration = "20261115113914_AddAQGreenIndependentRootRequests";
            var staged = configuration?["App:AQGreenV2:D10:BackendSuccessorJsonBase64"];
            var pin = configuration?["App:AQGreenV2:D10:BackendSuccessorSha256"];
            var pendingQualified = expected.Length == 56 && expected[^1] == migration &&
                applied.Length == 55 && expected.Take(55).SequenceEqual(applied, StringComparer.Ordinal) &&
                !string.IsNullOrWhiteSpace(staged) && !string.IsNullOrWhiteSpace(pin) &&
                configuration["App:AutomaticPayouts:Enabled"] == "false" &&
                configuration["App:BackgroundJobs:Enabled"] == "false";
            if (expected.Length == 0 || !expected.SequenceEqual(applied, StringComparer.Ordinal) && !pendingQualified)
                throw new InvalidOperationException(
                    "Database release precondition failed: migration history must exactly match this API release. " +
                    "Stop recovery; do not migrate or repair from API startup.");
        }
    }
}
