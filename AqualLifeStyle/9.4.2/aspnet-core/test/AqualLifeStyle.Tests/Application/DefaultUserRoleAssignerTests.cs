using System.Linq;
using System.Threading.Tasks;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.EntityFrameworkCore.Seed.Tenants;
using AqualLifeStyle.Users;
using AqualLifeStyle.Users.Dto;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class DefaultUserRoleAssignerTests : AqualLifeStyleTestBase
    {
        private readonly IUserAppService _userAppService;

        public DefaultUserRoleAssignerTests()
        {
            _userAppService = Resolve<IUserAppService>();
        }

        [Fact]
        public async Task AssignRoles_SetsSystemAdmin_ForAdminRole()
        {
            var admin = await GetCurrentUserAsync();
            admin.SetRole(AquaUserRole.Guest);

            await UsingDbContextAsync(ctx =>
            {
                new DefaultUserRoleAssigner(ctx).AssignRoles(admin.TenantId.Value);
                return Task.CompletedTask;
            });

            var updated = await UsingDbContextAsync(ctx => ctx.Users.SingleAsync(u => u.Id == admin.Id));
            updated.Role.ShouldBe(AquaUserRole.SystemAdmin);
        }

        [Fact]
        public async Task AssignRoles_SetsGuest_ForUserWithoutRoles()
        {
            var newUser = await CreateUserAsync("noroles_" + System.Guid.NewGuid().ToString("N"));
            await RemoveAllUserRolesAsync(newUser.Id);

            await UsingDbContextAsync(ctx =>
            {
                new DefaultUserRoleAssigner(ctx).AssignRoles(newUser.TenantId.Value);
                return Task.CompletedTask;
            });

            var updated = await UsingDbContextAsync(ctx => ctx.Users.SingleAsync(u => u.Id == newUser.Id));
            updated.Role.ShouldBe(AquaUserRole.Guest);
        }

        [Fact]
        public async Task AssignRoles_SkipsUser_WhenRoleAlreadySet()
        {
            var admin = await GetCurrentUserAsync();
            admin.SetRole(AquaUserRole.SystemAdmin);

            await UsingDbContextAsync(ctx =>
            {
                new DefaultUserRoleAssigner(ctx).AssignRoles(admin.TenantId.Value);
                return Task.CompletedTask;
            });

            var updated = await UsingDbContextAsync(ctx => ctx.Users.SingleAsync(u => u.Id == admin.Id));
            updated.Role.ShouldBe(AquaUserRole.SystemAdmin);
        }

        private async Task<User> CreateUserAsync(string userName)
        {
            await _userAppService.CreateAsync(new CreateUserDto
            {
                EmailAddress = $"{userName}@test.com",
                IsActive = true,
                Name = userName,
                Surname = "User",
                Password = "123qwe",
                UserName = userName
            });

            return await UsingDbContextAsync(ctx => ctx.Users.SingleAsync(u => u.UserName == userName));
        }

        private async Task RemoveAllUserRolesAsync(long userId)
        {
            await UsingDbContextAsync(async ctx =>
            {
                var roles = ctx.UserRoles.Where(ur => ur.UserId == userId).ToList();
                ctx.UserRoles.RemoveRange(roles);
                await ctx.SaveChangesAsync();
            });
        }
    }
}
