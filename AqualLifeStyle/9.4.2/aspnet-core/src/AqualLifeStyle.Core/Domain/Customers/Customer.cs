using System;
using System.Collections.Generic;
using System.Linq;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.Areas;
using AqualLifeStyle.Domain.Common;

namespace AqualLifeStyle.Domain.Customers
{
    public class Customer : FullAuditedAggregateRoot<int>, IMayHaveTenant
    {
        private readonly List<CustomerAreaAssignment> _areaAssignments = new();
        public const int MaxClubMemberNumberLength = 16;

        public int? TenantId { get; set; }
        public long UserId { get; private set; }
        public virtual User User { get; set; }
        public string ClubMemberNumber { get; private set; }
        public string Name { get; private set; }
        public EmailAddress Email { get; private set; }
        public int? MembershipId { get; private set; }
        public Guid? AreaId { get; private set; }
        public virtual Area Area { get; private set; }
        public IReadOnlyCollection<CustomerAreaAssignment> AreaAssignments =>
            _areaAssignments.AsReadOnly();
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
            ClubMemberNumber = $"CLB-{SecurePublicCode.Generate(12)}";
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

        public void AssignInitialArea(Area area, DateTime effectiveFrom, string reason)
        {
            EnsureAreaBelongsToCustomerTenant(area);
            if (AreaId.HasValue || _areaAssignments.Any(item => item.IsCurrent))
                throw new InvalidOperationException("The customer already has a current Area assignment.");

            AreaId = area.Id;
            Area = area;
            _areaAssignments.Add(CustomerAreaAssignment.Start(
                TenantId.Value,
                area.Id,
                effectiveFrom,
                reason));
        }

        public void MoveToArea(Area area, DateTime effectiveFrom, string reason)
        {
            EnsureAreaBelongsToCustomerTenant(area);
            var current = _areaAssignments.SingleOrDefault(item => item.IsCurrent);
            if (current == null)
                throw new InvalidOperationException("Load the current Area assignment before moving the customer.");
            if (current.AreaId == area.Id) return;

            current.End(effectiveFrom);
            AreaId = area.Id;
            Area = area;
            _areaAssignments.Add(CustomerAreaAssignment.Start(
                TenantId.Value,
                area.Id,
                effectiveFrom,
                reason));
        }

        private void EnsureAreaBelongsToCustomerTenant(Area area)
        {
            if (area == null) throw new ArgumentNullException(nameof(area));
            if (!TenantId.HasValue || TenantId.Value <= 0)
                throw new InvalidOperationException("A host Customer cannot be assigned to a business Area.");
            if (area.TenantId != TenantId.Value)
                throw new InvalidOperationException("The Customer and Area must belong to the same Tenant.");
            if (!area.IsActive)
                throw new InvalidOperationException("A Customer cannot be assigned to an inactive Area.");
        }

        private void SetName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Customer name is required.", nameof(name));
            Name = name.Trim();
        }
    }
}
