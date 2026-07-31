using System;
using Abp.Domain.Entities;

namespace AqualLifeStyle.Domain.Email
{
    public sealed class AccountEmailThrottle : Entity<string>, IMustHaveTenant
    {
        public const int MaxKeyLength = 160;

        public int TenantId { get; set; }
        public DateTime ExpiresAt { get; private set; }

        private AccountEmailThrottle() { }

        public static AccountEmailThrottle Create(
            string key,
            int tenantId,
            DateTime expiresAt)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("A throttle key is required.", nameof(key));
            var normalizedKey = key.Trim();
            if (normalizedKey.Length > MaxKeyLength) throw new ArgumentException("The throttle key is too long.", nameof(key));
            if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));

            return new AccountEmailThrottle
            {
                Id = normalizedKey,
                TenantId = tenantId,
                ExpiresAt = expiresAt
            };
        }
    }
}
