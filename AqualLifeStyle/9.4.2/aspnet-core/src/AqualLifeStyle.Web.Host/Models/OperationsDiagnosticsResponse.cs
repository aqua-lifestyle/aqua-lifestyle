namespace AqualLifeStyle.Web.Host.Models
{
    /// <summary>
    /// Protected, sanitised deployment and migration evidence for authorised operators.
    /// </summary>
    public sealed class OperationsDiagnosticsResponse
    {
        public string ApplicationVersion { get; set; }
        public string BuildId { get; set; }
        public string ImageId { get; set; }
        public string Environment { get; set; }
        public string EnvironmentId { get; set; }
        public string PaymentContractVersion { get; set; }
        public string DatabaseProvider { get; set; }
        public string DatabaseFingerprint { get; set; }
        public string LatestAppliedMigration { get; set; }
        public string RequiredPaymentMigration { get; set; }
        public bool IsRequiredPaymentMigrationApplied { get; set; }
        public bool AreAllKnownMigrationsApplied { get; set; }
    }
}
