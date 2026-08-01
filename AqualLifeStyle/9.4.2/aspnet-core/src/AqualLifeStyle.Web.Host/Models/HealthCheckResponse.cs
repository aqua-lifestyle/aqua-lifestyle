using System;

namespace AqualLifeStyle.Web.Host.Models
{
    /// <summary>
    /// Represents the public health status returned by the host API.
    /// </summary>
    public class HealthCheckResponse
    {
        /// <summary>
        /// Gets or sets the current health status.
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the configured database is reachable.
        /// </summary>
        public bool IsDatabaseReachable { get; set; }

        /// <summary>
        /// Gets or sets the database dependency status.
        /// </summary>
        public string DatabaseStatus { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the Data Protection key table is queryable.
        /// </summary>
        public bool IsDataProtectionKeyStoreReachable { get; set; }

        /// <summary>
        /// Gets or sets the Data Protection key-store dependency status.
        /// </summary>
        public string DataProtectionKeyStoreStatus { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the configured Redis cache is reachable.
        /// </summary>
        public bool IsRedisReachable { get; set; }

        /// <summary>
        /// Gets or sets the Redis dependency status.
        /// </summary>
        public string RedisStatus { get; set; }

        /// <summary>
        /// Gets or sets the application version.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the immutable commit or build identifier supplied by the deployment.
        /// </summary>
        public string BuildId { get; set; }

        /// <summary>
        /// Gets or sets an optional non-sensitive container image identifier.
        /// </summary>
        public string ImageId { get; set; }

        /// <summary>
        /// Gets or sets the payment API contract expected by matching frontends.
        /// </summary>
        public string PaymentContractVersion { get; set; }

        /// <summary>
        /// Gets or sets the explicitly supported API capabilities.
        /// </summary>
        public string[] ContractCapabilities { get; set; }

        /// <summary>
        /// Gets or sets the configured release date for this application version.
        /// </summary>
        public DateTime ReleaseDate { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when the health check was produced.
        /// </summary>
        public DateTime CheckedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the current hosting environment name.
        /// </summary>
        public string Environment { get; set; }

        /// <summary>
        /// Gets or sets the request trace identifier for correlation.
        /// </summary>
        public string TraceId { get; set; }
    }
}
