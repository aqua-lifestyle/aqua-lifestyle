using System;
using Microsoft.Extensions.Configuration;

namespace AqualLifeStyle.Configuration
{
    // Explicit pinned JSON transport through the existing private process
    // configuration boundary. No arbitrary environment lookup, file fallback,
    // authority grant, secret output, or persistent runtime-file staging.
    public static class PrivateOperatorJson
    {
        public const string Scheme = "environment:";
        public static bool TryRead(IConfiguration configuration, string selector, string expectedKey, int maxBytes, out byte[] bytes)
        {
            bytes = null;
            if (selector == null || !selector.StartsWith(Scheme, StringComparison.Ordinal)) return false;
            var encoded = configuration[expectedKey];
            if (selector != Scheme + expectedKey || string.IsNullOrWhiteSpace(encoded) ||
                encoded.Length > 4L * ((maxBytes + 2L) / 3L))
                throw new InvalidOperationException("OPERATOR_ENVIRONMENT_EVIDENCE_INVALID");
            try { bytes = Convert.FromBase64String(encoded); }
            catch (FormatException) { throw new InvalidOperationException("OPERATOR_ENVIRONMENT_EVIDENCE_INVALID"); }
            if (bytes.Length == 0 || bytes.Length > maxBytes) throw new InvalidOperationException("OPERATOR_ENVIRONMENT_EVIDENCE_INVALID");
            return true;
        }
    }
}
