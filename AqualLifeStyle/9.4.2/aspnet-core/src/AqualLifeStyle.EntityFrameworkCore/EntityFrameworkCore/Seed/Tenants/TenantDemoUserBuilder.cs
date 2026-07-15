using System;
using System.Linq;
using Abp.Authorization.Users;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AqualLifeStyle.EntityFrameworkCore.Seed.Tenants
{
    /// <summary>Creates or repairs the account shared by a tenant demo scenario.</summary>
    internal sealed class TenantDemoUserBuilder
    {
        private readonly AqualLifeStyleDbContext _context;
        private readonly int _tenantId;
        private readonly PasswordHasher<User> _passwordHasher =
            new PasswordHasher<User>(new OptionsWrapper<PasswordHasherOptions>(new PasswordHasherOptions()));

        public TenantDemoUserBuilder(AqualLifeStyleDbContext context, int tenantId)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _tenantId = tenantId;
        }

        public Customer Create(
            string userName,
            string password,
            string email,
            string name,
            string surname,
            string roleName,
            AquaUserRole role,
            int? membershipId = null)
        {
            var user = _context.Users.IgnoreQueryFilters()
                .SingleOrDefault(item => item.TenantId == _tenantId && item.UserName == userName);
            if (user == null)
            {
                user = new User
                {
                    TenantId = _tenantId,
                    UserName = userName,
                    Name = name,
                    Surname = surname,
                    EmailAddress = email,
                    IsActive = true,
                    IsEmailConfirmed = true
                };
                user.SetNormalizedNames();
                user.SetRole(role);
                user.Password = _passwordHasher.HashPassword(user, password);
                _context.Users.Add(user);
                _context.SaveChanges();
            }

            user.IsActive = true;
            user.IsEmailConfirmed = true;
            user.SetRole(role);
            user.Password = _passwordHasher.HashPassword(user, password);

            var assignedRole = _context.Roles.IgnoreQueryFilters()
                .Single(item => item.TenantId == _tenantId && item.Name == roleName);
            var assignments = _context.UserRoles.IgnoreQueryFilters()
                .Where(item => item.TenantId == _tenantId && item.UserId == user.Id)
                .ToList();
            _context.UserRoles.RemoveRange(assignments.Where(item => item.RoleId != assignedRole.Id));
            if (assignments.All(item => item.RoleId != assignedRole.Id))
            {
                _context.UserRoles.Add(new UserRole(_tenantId, user.Id, assignedRole.Id));
            }

            var customer = _context.Customers.IgnoreQueryFilters()
                .SingleOrDefault(item => item.TenantId == _tenantId && item.UserId == user.Id);
            if (customer == null)
            {
                customer = Customer.Create(
                    _tenantId,
                    user.Id,
                    $"{name} {surname}".Trim(),
                    new EmailAddress(email),
                    membershipId,
                    user);
                _context.Customers.Add(customer);
            }
            else if (membershipId.HasValue && !customer.MembershipId.HasValue)
            {
                customer.ChangeMembership(membershipId.Value);
            }

            _context.SaveChanges();
            return customer;
        }
    }
}
