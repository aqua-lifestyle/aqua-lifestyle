using System;
using System.Linq;
using Abp.Authorization.Users;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.EntityFrameworkCore.Seed.Tenants
{
    /// <summary>
    /// Repairs tenant accounts created before customer registration provisioned
    /// a linked Customer and default Guest role. The operation is idempotent.
    /// </summary>
    public sealed class DefaultCustomerAccountProvisioner
    {
        private readonly AqualLifeStyleDbContext _context;

        public DefaultCustomerAccountProvisioner(AqualLifeStyleDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public void Provision(int tenantId)
        {
            if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));

            var guestRole = _context.Roles.IgnoreQueryFilters()
                .Single(role => role.TenantId == tenantId && role.Name == "Guest");
            var validRoleAssignments = (
                from userRole in _context.UserRoles.IgnoreQueryFilters()
                join role in _context.Roles.IgnoreQueryFilters()
                    on userRole.RoleId equals role.Id
                where userRole.TenantId == tenantId
                    && role.TenantId == tenantId
                    && !role.IsDeleted
                select new { userRole.UserId, role.Name })
                .ToList();
            var usersWithRoles = validRoleAssignments
                .Select(assignment => assignment.UserId)
                .ToHashSet();
            var administratorUserIds = validRoleAssignments
                .Where(assignment => assignment.Name == "Admin" || assignment.Name == "SystemAdmin")
                .Select(assignment => assignment.UserId)
                .ToHashSet();
            var customers = _context.Customers.IgnoreQueryFilters()
                .Where(customer => customer.TenantId == tenantId)
                .ToList();
            var customerUserIds = customers.Select(customer => customer.UserId).ToHashSet();
            var customersByEmail = customers
                .GroupBy(customer => Normalize(customer.Email.Value))
                .ToDictionary(group => group.Key, group => group.First());
            var eligibleUsers = _context.Users.IgnoreQueryFilters()
                .Where(user => user.TenantId == tenantId && user.IsActive && !user.IsDeleted)
                .AsEnumerable()
                .Where(user => !administratorUserIds.Contains(user.Id))
                .ToList();
            var usersById = _context.Users.IgnoreQueryFilters()
                .Where(user => user.TenantId == tenantId)
                .ToDictionary(user => user.Id);

            foreach (var user in eligibleUsers)
            {
                if (!customerUserIds.Contains(user.Id))
                {
                    var normalizedEmail = Normalize(user.EmailAddress);
                    if (customersByEmail.TryGetValue(normalizedEmail, out var existingCustomer))
                    {
                        if (usersById.TryGetValue(existingCustomer.UserId, out var linkedUser) &&
                            !linkedUser.IsActive &&
                            linkedUser.UserName.StartsWith("customer_", StringComparison.OrdinalIgnoreCase))
                        {
                            // Legacy seed data used inactive placeholder users. This is an
                            // infrastructure repair that deliberately preserves the existing
                            // Customer, membership, and unique email while transferring its link.
                            _context.Entry(existingCustomer)
                                .Property(customer => customer.UserId)
                                .CurrentValue = user.Id;
                            customerUserIds.Add(user.Id);
                        }
                    }
                    else
                    {
                        var name = $"{user.Name} {user.Surname}".Trim();
                        var customer = Customer.Create(
                            tenantId,
                            user.Id,
                            string.IsNullOrWhiteSpace(name) ? user.UserName : name,
                            new EmailAddress(user.EmailAddress),
                            membershipId: null,
                            user: user);
                        var activeAreas = _context.Areas.IgnoreQueryFilters()
                            .Where(item => item.TenantId == tenantId && item.IsActive)
                            .Take(2)
                            .ToList();
                        if (activeAreas.Count != 1)
                            throw new InvalidOperationException(
                                "Customer account provisioning requires exactly one active Area in the Tenant.");
                        customer.AssignInitialArea(
                            activeAreas[0],
                            DateTime.UtcNow,
                            "Customer account provisioning");

                        _context.Customers.Add(customer);
                        customersByEmail.Add(normalizedEmail, customer);
                        customerUserIds.Add(user.Id);
                    }
                }

                if (!usersWithRoles.Contains(user.Id))
                {
                    _context.UserRoles.Add(new UserRole(tenantId, user.Id, guestRole.Id));
                }
            }

            _context.SaveChanges();
        }

        private static string Normalize(string email) =>
            email?.Trim().ToUpperInvariant() ?? string.Empty;
    }
}
