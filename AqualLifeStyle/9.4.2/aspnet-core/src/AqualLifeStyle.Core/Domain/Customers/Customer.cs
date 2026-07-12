using System;
using Abp.Domain.Entities;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.Common;

namespace AqualLifeStyle.Domain.Customers
{
    public class Customer : Entity<int>, IMayHaveTenant
    {
        public int? TenantId { get; set; }
        public long UserId { get; private set; }
        public virtual User User { get; set; }
        public string Name { get; private set; }
        public EmailAddress Email { get; private set; }
        public int? MembershipId { get; set; }
        public bool IsActive { get; private set; }

        protected Customer() { }

        private Customer(int? tenantId, long userId, string name, EmailAddress email, int? membershipId = null, bool isActive = true)
        {
            if (tenantId.HasValue && tenantId.Value <= 0)
            {
                throw new ArgumentException("TenantId must be valid.", nameof(tenantId));
            }

            if (userId <= 0)
            {
                throw new ArgumentException("UserId must be positive.", nameof(userId));
            }

            TenantId = tenantId;
            UserId = userId;
            SetName(name);
            Email = email ?? throw new ArgumentNullException(nameof(email));
            MembershipId = membershipId;
            IsActive = isActive;
        }

        public static Customer Create(int? tenantId, long userId, string name, EmailAddress email, int? membershipId = null, User user = null)
        {
            var customer = new Customer(tenantId, userId, name, email, membershipId, true);
            if (user != null)
            {
                customer.User = user;
            }
            return customer;
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
            if (UserId == userId) return;
            if (UserId != default) throw new InvalidOperationException($"Customer is already linked to UserId={UserId}.");
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
