using System;
using Abp.Domain.Entities;
using AqualLifeStyle.Domain.Common;

namespace AqualLifeStyle.Domain.Customers
{
    public class Customer : Entity<int>, IMayHaveTenant
    {
        public int? TenantId { get; set; }
        public long? UserId { get; private set; }
        public string Name { get; private set; }
        public EmailAddress Email { get; private set; }
        public int? MembershipId { get; private set; }
        public bool IsActive { get; private set; }

        protected Customer() { }

        private Customer(int? tenantId, string name, EmailAddress email, int? membershipId = null, bool isActive = true)
        {
            if (tenantId.HasValue && tenantId.Value <= 0)
            {
                throw new ArgumentException("TenantId must be valid.", nameof(tenantId));
            }

            TenantId = tenantId;
            SetName(name);
            Email = email ?? throw new ArgumentNullException(nameof(email));
            MembershipId = membershipId;
            IsActive = isActive;
        }

        public static Customer Create(int? tenantId, string name, EmailAddress email, int? membershipId = null)
        {
            return new Customer(tenantId, name, email, membershipId, true);
        }

        public void ChangeMembership(int? newMembershipId)
        {
            if (newMembershipId.HasValue && newMembershipId.Value <= 0)
            {
                throw new ArgumentException("MembershipId must be positive.", nameof(newMembershipId));
            }

            MembershipId = newMembershipId;
        }

        public void Rename(string name)
        {
            SetName(name);
        }

        public void ChangeEmail(EmailAddress email)
        {
            Email = email ?? throw new ArgumentNullException(nameof(email));
        }

        public void LinkUser(long userId)
        {
            if (userId <= 0) throw new ArgumentException("UserId must be positive.", nameof(userId));
            if (UserId.HasValue && UserId.Value != userId)
                throw new InvalidOperationException("A customer cannot be linked to a different user.");
            UserId = userId;
        }

        public void Activate() => IsActive = true;

        public void Deactivate() => IsActive = false;

        private void SetName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Customer name is required.", nameof(name));
            Name = name.Trim();
        }
    }
}
