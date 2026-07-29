using System;
using Abp.Dependency;
using Microsoft.Extensions.Configuration;

namespace AqualLifeStyle.Authorization.Accounts
{
    public sealed class AccountEmailLinkBuilder : ITransientDependency
    {
        private readonly IConfiguration _configuration;

        public AccountEmailLinkBuilder(IConfiguration configuration)
            => _configuration = configuration;

        public string Build(
            string path,
            int tenantId,
            long userId,
            string token,
            string areaName = null,
            string redirectPath = null)
        {
            var root = _configuration["App:ClientRootAddress"]?.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(root))
                throw new InvalidOperationException("The client application address is not configured.");

            var url = $"{root}{path}?tenantId={tenantId}&userId={userId}&token={Uri.EscapeDataString(token)}";
            if (!string.IsNullOrWhiteSpace(areaName))
                url += "&area=" + Uri.EscapeDataString(areaName);
            var safeRedirect = SafeClientRedirect(redirectPath);
            if (safeRedirect != null)
                url += "&redirect=" + Uri.EscapeDataString(safeRedirect);
            return url;
        }

        private static string SafeClientRedirect(string value)
        {
            var candidate = value?.Trim();
            return !string.IsNullOrWhiteSpace(candidate) &&
                   candidate.StartsWith("/", StringComparison.Ordinal) &&
                   !candidate.StartsWith("//", StringComparison.Ordinal) &&
                   candidate.IndexOf('\\') < 0 &&
                   Uri.TryCreate(candidate, UriKind.Relative, out _)
                ? candidate
                : null;
        }
    }
}
