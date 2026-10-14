using System;
using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace AqualLifeStyle.Configuration
{
    // Operator-approved activation boundary; older messages remain unchanged and unsent.
    public static class TransactionalEmailDeliveryBoundary
    {
        public const string Setting = "Bird:DeliveryNotBeforeUtc";

        public static DateTime Require(IConfiguration configuration)
        {
            var value = configuration[Setting];
            if (string.IsNullOrWhiteSpace(value) || !value.EndsWith("Z", StringComparison.Ordinal) ||
                !DateTime.TryParseExact(value,
                    new[] { "O", "yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'" },
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var cutoff) || cutoff > DateTime.UtcNow)
                throw new InvalidOperationException("Bird__DeliveryNotBeforeUtc requires an approved, non-future UTC timestamp ending in Z.");
            return cutoff;
        }
    }
}
