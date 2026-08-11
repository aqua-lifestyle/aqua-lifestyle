using System;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;

namespace AqualLifeStyle.Domain.Areas
{
    /// <summary>
    /// A business and administrative subdivision inside a Tenant. A Tenant
    /// remains the hard data-isolation boundary; an Area never crosses it.
    /// </summary>
    public class Area : FullAuditedAggregateRoot<Guid>, IMustHaveTenant
    {
        public const int MaxNameLength = 128;
        public const int MaxCodeLength = 16;

        public int TenantId { get; set; }
        public string Name { get; private set; }
        public string Code { get; private set; }
        public bool IsActive { get; private set; }

        protected Area()
        {
        }

        public static Area Create(int tenantId, string code, string name)
        {
            if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));
            return new Area
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = NormalizeCode(code),
                Name = NormalizeName(name),
                IsActive = true
            };
        }

        public void Rename(string name) => Name = NormalizeName(name);

        public void Activate() => IsActive = true;

        public void Deactivate() => IsActive = false;

        private static string NormalizeCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Area code is required.", nameof(code));
            var normalized = code.Trim().ToUpperInvariant();
            if (normalized.Length > MaxCodeLength)
                throw new ArgumentException($"Area code cannot exceed {MaxCodeLength} characters.", nameof(code));
            return normalized;
        }

        private static string NormalizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Area name is required.", nameof(name));
            var normalized = name.Trim();
            if (normalized.Length > MaxNameLength)
                throw new ArgumentException($"Area name cannot exceed {MaxNameLength} characters.", nameof(name));
            return normalized;
        }
    }
}
