using System.Text.RegularExpressions;

namespace AqualLifeStyle.Web.Host.Email
{
    public sealed class BirdOptions
    {
        private static readonly Regex ApiKeyPattern = new Regex(
            @"^bk_(?<region>[a-z]{2}[0-9]+)_.+$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public bool Enabled { get; set; }
        public string ApiKey { get; set; }
        public string FromEmail { get; set; }
        public string FromName { get; set; } = "Aqua Lifestyle Club";
        public string ReplyToEmail { get; set; }

        internal static bool TryResolveRegion(string apiKey, out string region)
        {
            var match = ApiKeyPattern.Match(apiKey ?? string.Empty);
            region = match.Success ? match.Groups["region"].Value : null;
            return match.Success;
        }
    }
}
