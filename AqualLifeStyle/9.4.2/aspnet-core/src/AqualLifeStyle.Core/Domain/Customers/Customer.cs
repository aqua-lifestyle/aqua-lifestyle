using System;
using Abp.Domain.Entities;
using AqualLifeStyle.Domain.Common;

namespace AqualLifeStyle.Domain.Customers
{
    public class Customer : Entity<int>, IMayHaveTenant
    {
        public int? TenantId { get; set; }
        public string Name { get; private set; }
        public EmailAddress Email { get; private set; }
        public int? MembershipId { get; private set; }
        public bool IsActive { get; private set; }

        protected Customer() { }

        private Customer(int? tenantId, string name, EmailAddress email, int? membershipId = null, bool isActive = true)
        {
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

        public static Customer Create(string name, EmailAddress email, int? membershipId = null)
            => Create(1, name, email, membershipId);

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

        public void Activate() => IsActive = true;

        public void Deactivate() => IsActive = false;

        private void SetName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Customer name is required.", nameof(name));
            Name = name.Trim();
        }
    }
}
