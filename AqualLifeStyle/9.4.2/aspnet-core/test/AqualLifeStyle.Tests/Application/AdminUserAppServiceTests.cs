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
using Abp.UI;

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
        public void CreateDto_DoesNotExposePasswordOrActivation()
        {
            typeof(AdminCreateUserInput).GetProperty("Password").ShouldBeNull();
            typeof(AdminCreateUserInput).GetProperty("IsActive").ShouldBeNull();
            typeof(AdminResetUserPasswordInput).GetProperty("NewPassword").ShouldBeNull();
            typeof(AdminCreateUserInput).GetCustomAttributes(typeof(Abp.Auditing.DisableAuditingAttribute), true)
                .ShouldHaveSingleItem();
        }

        [Fact]
        public async Task Create_IssuesInactiveSetupRequiredInvitationWithoutAdministratorPassword()
        {
            var email = $"managed-{Guid.NewGuid():N}@example.com";
            var created = await _service.CreateAsync(new AdminCreateUserInput
            {
                TenantId = 1, FirstName = "Grace", LastName = "Hopper", Email = email,
                Role = AquaUserRole.Guest,
                Justification = "Approved support account"
            });
            created.TenantId.ShouldBe(1);
            created.Role.ShouldBe(AquaUserRole.Guest);
            created.IsActive.ShouldBeFalse();
            created.InvitationStatus.ShouldBe("Pending");
            created.RequiresPasswordSetup.ShouldBeTrue();
            await UsingDbContextAsync(async context =>
            {
                var user = await context.Users.SingleAsync(item => item.Id == created.Id);
                user.IsActive.ShouldBeFalse();
                user.IsEmailConfirmed.ShouldBeFalse();
                user.RequiresPasswordReset().ShouldBeTrue();
                (await context.InternalAccountInvitations.CountAsync(item => item.UserId == created.Id)).ShouldBe(1);
                (await context.TransactionalEmailOutboxMessages.CountAsync(message =>
                    message.NotificationType == "InternalAccountInvitation" && message.Recipient == email)).ShouldBe(1);
            });
        }

        [Fact]
        public async Task Create_RejectsCrossTenantRequestForTenantAdmin()
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => _service.CreateAsync(new AdminCreateUserInput
            {
                TenantId = 2, FirstName = "Cross", LastName = "Tenant",
                Email = $"cross-user-{Guid.NewGuid():N}@example.com",
                Role = AquaUserRole.Guest, Justification = "Invalid cross tenant attempt"
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
                Role = AquaUserRole.SystemAdmin,
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
                Role = AquaUserRole.SystemAdmin,
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
                user.IsActive.ShouldBeFalse();
                user.IsEmailConfirmed.ShouldBeFalse();
                user.RequiresPasswordReset().ShouldBeTrue();
                user.Role.ShouldBe(AquaUserRole.SystemAdmin);
            });
        }

        [Fact]
        public async Task HostList_IncludesInvitationStatusAcrossTenantFilter()
        {
            var created = await _service.CreateAsync(new AdminCreateUserInput
            {
                TenantId = 1,
                FirstName = "Host",
                LastName = "Visible",
                Email = $"host-visible-{Guid.NewGuid():N}@example.com",
                Role = AquaUserRole.Guest,
                Justification = "Host list regression"
            });

            LoginAsHostAdmin();
            var listed = await _service.GetAllAsync(new AdminUserListInput { TenantId = 1 });
            listed.Items.Single(item => item.Id == created.Id).InvitationStatus.ShouldBe("Pending");
            listed.Items.Single(item => item.Id == created.Id).RequiresPasswordSetup.ShouldBeTrue();
        }

        [Fact]
        public async Task ResetPassword_RejectsInactiveAccountWithoutQueuingEmail()
        {
            LoginAsDefaultTenantAdmin();
            var listed = await _service.GetAllAsync(new AdminUserListInput { IsActive = true });
            var target = listed.Items.First(item => !item.RequiresPasswordSetup);
            var outboxCount = 0;
            await UsingDbContextAsync(async context =>
            {
                var user = await context.Users.SingleAsync(item => item.Id == target.Id);
                user.IsActive = false;
                outboxCount = await context.TransactionalEmailOutboxMessages.CountAsync();
                await context.SaveChangesAsync();
            });

            await Should.ThrowAsync<UserFriendlyException>(() => _service.ResetPasswordAsync(
                new AdminResetUserPasswordInput
                {
                    Id = target.Id,
                    Justification = "Inactive account must not receive an unusable link"
                }));

            await UsingDbContextAsync(async context =>
                (await context.TransactionalEmailOutboxMessages.CountAsync()).ShouldBe(outboxCount));
        }
    }
}
