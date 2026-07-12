using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AqualLifeStyle.EntityFrameworkCore.Seed.Tenants
{
    public sealed class DefaultCustomerUserLinker
    {
        private readonly AqualLifeStyleDbContext _context;
        private readonly ILogger<DefaultCustomerUserLinker> _logger;

        public DefaultCustomerUserLinker(AqualLifeStyleDbContext context, ILogger<DefaultCustomerUserLinker> logger = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? NullLogger<DefaultCustomerUserLinker>.Instance;
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
                var email = Normalize(customer.Email.Value);
                if (!usersByEmail.TryGetValue(email, out var matches) || matches.Count == 0)
                {
                    _logger.LogWarning("No user match for customer {CustomerId} in tenant {TenantId}; email {Email}", customer.Id, tenantId, email);
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

        private static string Normalize(string email) => email?.Trim().ToUpperInvariant() ?? string.Empty;
    }
}
