using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Authorization;
using AqualLifeStyle.Application.Admin.Users;
using AqualLifeStyle.Application.Admin.Users.Dto;
using AqualLifeStyle.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using AqualLifeStyle.Authorization.Users;

namespace AqualLifeStyle.Tests.Application
{
    public class AdminUserAppServiceTests : AqualLifeStyleTestBase
    {
        private readonly IAdminUserAppService _service;

        public AdminUserAppServiceTests()
        {
            _service = Resolve<IAdminUserAppService>();
        }

        [Fact]
        public async Task UserLifecycle_CreatesUpdatesAssignsRoleResetsPasswordAndSoftDeletes()
        {
            var email = $"managed-{Guid.NewGuid():N}@example.com";
            var created = await _service.CreateAsync(new AdminCreateUserInput
            {
                TenantId = 1, FirstName = "Grace", LastName = "Hopper", Email = email,
                Password = "SafePassword123!", Role = AquaUserRole.Guest, IsActive = true,
                Justification = "Approved support account"
            });
            created.TenantId.ShouldBe(1);
            created.Role.ShouldBe(AquaUserRole.Guest);

            var updated = await _service.UpdateAsync(new AdminUpdateUserInput
            {
                Id = created.Id, FirstName = "Rear Admiral Grace", LastName = "Hopper", Email = email,
                IsActive = true, Justification = "Corrected display name"
            });
            updated.FirstName.ShouldBe("Rear Admiral Grace");

            var assigned = await _service.AssignRoleAsync(new AdminAssignUserRoleInput
            {
                Id = created.Id, Role = AquaUserRole.Member, Justification = "Membership approved"
            });
            assigned.Role.ShouldBe(AquaUserRole.Member);
            await UsingDbContextAsync(async context =>
            {
                var roles = await (from assignment in context.UserRoles
                    join role in context.Roles on assignment.RoleId equals role.Id
                    where assignment.UserId == created.Id select role.Name).ToListAsync();
                roles.ShouldContain("Member");
            });
            await _service.ResetPasswordAsync(new AdminResetUserPasswordInput
            {
                Id = created.Id, NewPassword = "Replacement123!", Justification = "Verified support request"
            });
            await _service.DeleteAsync(new AdminDeleteUserInput
            {
                Id = created.Id, Justification = "Temporary support account expired"
            });

            await UsingDbContextAsync(async context =>
            {
                var user = await context.Users.IgnoreQueryFilters().SingleAsync(item => item.Id == created.Id);
                user.IsDeleted.ShouldBeTrue();
                user.IsActive.ShouldBeFalse();
            });
        }

        [Fact]
        public async Task Create_RejectsCrossTenantRequestForTenantAdmin()
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => _service.CreateAsync(new AdminCreateUserInput
            {
                TenantId = 2, FirstName = "Cross", LastName = "Tenant",
                Email = $"cross-user-{Guid.NewGuid():N}@example.com", Password = "SafePassword123!",
                Role = AquaUserRole.Guest, IsActive = true, Justification = "Invalid cross tenant attempt"
            }));
        }

        [Fact]
        public async Task NonAdmin_CannotCreateAdminUser()
        {
            // Create a non-admin user manually to ensure no admin role is assigned
            long nonAdminId = 0;
            await UsingDbContextAsync(async context =>
            {
                var user = new User
                {
                    TenantId = 1,
                    UserName = $"nonadmin{Guid.NewGuid():N}",
                    Name = "Regular",
                    Surname = "User",
                    EmailAddress = $"nonadmin{Guid.NewGuid():N}@example.com",
                    IsEmailConfirmed = true,
                    IsActive = true
                };
                user.SetNormalizedNames();
                user.SetRole(AquaUserRole.Guest);
                user.Password = new PasswordHasher<User>(new OptionsWrapper<PasswordHasherOptions>(new PasswordHasherOptions())).HashPassword(user, User.DefaultPassword);
                context.Users.Add(user);
                await context.SaveChangesAsync();
                nonAdminId = user.Id;
            });

            // Act as the non-admin user
            SetCurrentUser(nonAdminId, 1);

            // Assert that creating an admin user is forbidden
            await Should.ThrowAsync<AbpAuthorizationException>(() => _service.CreateAsync(new AdminCreateUserInput
            {
                TenantId = 1,
                FirstName = "Attempt",
                LastName = "Fail",
                Email = $"attempt-{Guid.NewGuid():N}@example.com",
                Password = "SafePassword123!",
                Role = AquaUserRole.SystemAdmin,
                IsActive = true,
                Justification = "Should be denied"
            }));
        }

        [Fact]
        public async Task Admin_CanCreateAdminUser()
        {
            // Ensure we're acting as the default tenant admin
            LoginAsDefaultTenantAdmin();

            var email = $"admin-created-{Guid.NewGuid():N}@example.com";

            var created = await _service.CreateAsync(new AdminCreateUserInput
            {
                TenantId = 1,
                FirstName = "Creator",
                LastName = "Admin",
                Email = email,
                Password = "SafePassword123!",
                Role = AquaUserRole.SystemAdmin,
                IsActive = true,
                Justification = "Test admin creation"
            });

            created.ShouldNotBeNull();
            created.TenantId.ShouldBe(1);
            created.Email.ShouldBe(email);
            created.Role.ShouldBe(AquaUserRole.SystemAdmin);

            await UsingDbContextAsync(async context =>
            {
                var user = await context.Users.SingleOrDefaultAsync(u => u.Id == created.Id && u.TenantId == 1);
                user.ShouldNotBeNull();
                user.EmailAddress.ShouldBe(email);
                user.IsActive.ShouldBeTrue();
                user.Role.ShouldBe(AquaUserRole.SystemAdmin);
            });
        }
    }
}
