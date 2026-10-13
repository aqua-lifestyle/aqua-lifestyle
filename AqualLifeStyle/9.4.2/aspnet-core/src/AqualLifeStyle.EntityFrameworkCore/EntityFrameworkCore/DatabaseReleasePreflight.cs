using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.EntityFrameworkCore
{
    /// <summary>Read-only release/schema boundary. API startup never applies migrations.</summary>
    public static class DatabaseReleasePreflight
    {
        public static void Verify(AqualLifeStyleDbContext context)
        {
            var expected = context.Database.GetMigrations().ToArray();
            var applied = context.Database.GetAppliedMigrations().ToArray();
            if (expected.Length == 0 || !expected.SequenceEqual(applied, StringComparer.Ordinal))
                throw new InvalidOperationException(
                    "Database release precondition failed: migration history must exactly match this API release. " +
                    "Stop recovery; do not migrate or repair from API startup.");
        }
    }
}
