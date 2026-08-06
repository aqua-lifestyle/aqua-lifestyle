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

            var url = $"{root}{path}?tenantId={tenantId}&userId={userId}";
            if (!string.IsNullOrWhiteSpace(areaName))
                url += "&area=" + Uri.EscapeDataString(areaName);
            var safeRedirect = SafeClientRedirect(redirectPath);
            if (safeRedirect != null)
                url += "&redirect=" + Uri.EscapeDataString(safeRedirect);
            return url + "#token=" + Uri.EscapeDataString(token);
        }

        public string BuildInternalAccountInvitation(string invitationCode, string token)
        {
            if (string.IsNullOrWhiteSpace(invitationCode))
                throw new ArgumentException("The invitation code is required.", nameof(invitationCode));
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("The invitation token is required.", nameof(token));
            var root = _configuration["App:ClientRootAddress"]?.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(root))
                throw new InvalidOperationException("The client application address is not configured.");
            return $"{root}/reset-password?invitation={Uri.EscapeDataString(invitationCode)}" +
                   $"#token={Uri.EscapeDataString(token)}";
        }

        public string BuildSignIn(string areaName)
        {
            if (string.IsNullOrWhiteSpace(areaName))
                throw new ArgumentException("The Area sign-in name is required.", nameof(areaName));
            var root = _configuration["App:ClientRootAddress"]?.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(root))
                throw new InvalidOperationException("The client application address is not configured.");
            return $"{root}/login?area={Uri.EscapeDataString(areaName.Trim())}";
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
