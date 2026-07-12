using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;

namespace AqualLifeStyle.EntityFrameworkCore.Seed.Tenants
{
    public sealed class DefaultCustomerUserLinker
    {
        private readonly AqualLifeStyleDbContext _context;
        private readonly ILogger<DefaultCustomerUserLinker> _logger;
        private readonly PasswordHasher<User> _passwordHasher;

        public DefaultCustomerUserLinker(AqualLifeStyleDbContext context, ILogger<DefaultCustomerUserLinker> logger = null, PasswordHasher<User> passwordHasher = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? NullLogger<DefaultCustomerUserLinker>.Instance;
            _passwordHasher = passwordHasher ?? new PasswordHasher<User>(new OptionsWrapper<PasswordHasherOptions>(new PasswordHasherOptions()));
        }

        public void Link(int tenantId)
        {
            if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));

            var usersByEmail = _context.Users.IgnoreQueryFilters()
                .Where(user => user.TenantId == tenantId && !user.IsDeleted)
                .AsEnumerable()
                .GroupBy(user => Normalize(user.EmailAddress))
                .ToDictionary(group => group.Key, group => group.ToList());

            var customers = _context.Customers.IgnoreQueryFilters()
                .Where(customer => customer.TenantId == tenantId)
                .ToList();
            var assignedUserIds = new HashSet<long>(customers.Select(customer => customer.UserId));

            foreach (var customer in customers)
            {
                if (customer.UserId > 0)
                {
                    _logger.LogInformation("Customer {CustomerId} is already linked to user {UserId} in tenant {TenantId}", customer.Id, customer.UserId, tenantId);
                    continue;
                }

                var email = Normalize(customer.Email.Value);
                if (!usersByEmail.TryGetValue(email, out var matches) || matches.Count == 0)
                {
                    _logger.LogWarning("No user match for customer {CustomerId} in tenant {TenantId}; email {Email}; creating placeholder user", customer.Id, tenantId, email);
                    var placeholderUser = CreatePlaceholderUser(tenantId, customer);
                    _context.Users.Add(placeholderUser);
                    _context.SaveChanges();
                    customer.LinkUser(placeholderUser.Id);
                    assignedUserIds.Add(placeholderUser.Id);
                    continue;
                }

                if (matches.Count != 1 || assignedUserIds.Contains(matches[0].Id))
                {
                    _logger.LogWarning("Ambiguous user match for customer {CustomerId} in tenant {TenantId}; match count {MatchCount}", customer.Id, tenantId, matches.Count);
                    continue;
                }

                customer.LinkUser(matches[0].Id);
                assignedUserIds.Add(matches[0].Id);
                _logger.LogInformation("Linked customer {CustomerId} to user {UserId} in tenant {TenantId}", customer.Id, matches[0].Id, tenantId);
            }

            _context.SaveChanges();
        }

        private User CreatePlaceholderUser(int tenantId, Customer customer)
        {
            var user = new User
            {
                TenantId = tenantId,
                UserName = $"customer_{customer.Id}",
                Name = customer.Name.Length > 64 ? customer.Name.Substring(0, 64) : customer.Name,
                Surname = "Customer",
                EmailAddress = customer.Email.Value,
                IsActive = false,
                IsEmailConfirmed = false,
                IsLockoutEnabled = true,
                IsPhoneNumberConfirmed = false,
                IsTwoFactorEnabled = false,
                AccessFailedCount = 0
            };

            user.SetNormalizedNames();
            user.SetRole(AquaUserRole.Member);
            user.Password = _passwordHasher.HashPassword(user, "MIGRATED_ACCOUNT_REQUIRES_PASSWORD_RESET");

            return user;
        }

        private static string Normalize(string email) => email?.Trim().ToUpperInvariant() ?? string.Empty;
    }
}
