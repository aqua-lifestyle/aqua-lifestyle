using System.Linq;
using System.Threading.Tasks;
using Abp.Authorization.Users;
using AqualLifeStyle.Authorization.Roles;
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

        [Fact]
        public async Task Provision_CreatesCustomerAndGuestRole_ForExistingUnlinkedAccount()
        {
            var user = await CreateUserAsync("customer_" + System.Guid.NewGuid().ToString("N"));
            await RemoveAllUserRolesAsync(user.Id);

            await UsingDbContextAsync(context =>
            {
                new DefaultCustomerAccountProvisioner(context).Provision(user.TenantId.Value);
                return Task.CompletedTask;
            });

            await UsingDbContextAsync(async context =>
            {
                var customer = await context.Customers.SingleAsync(item => item.UserId == user.Id);
                var roleName = await (
                    from userRole in context.UserRoles
                    join role in context.Roles on userRole.RoleId equals role.Id
                    where userRole.UserId == user.Id
                    select role.Name).SingleAsync();

                customer.MembershipId.ShouldBeNull();
                roleName.ShouldBe("Guest");
            });
        }

        [Fact]
        public async Task Provision_AssignsGuestRole_WhenOnlyRoleLinkIsDeleted()
        {
            var user = await CreateUserAsync("stale_role_" + System.Guid.NewGuid().ToString("N"));
            await RemoveAllUserRolesAsync(user.Id);

            await UsingDbContextAsync(async context =>
            {
                var staleRole = new Role(
                    user.TenantId,
                    "Deleted_" + System.Guid.NewGuid().ToString("N"),
                    "Deleted role")
                {
                    IsDeleted = true
                };
                context.Roles.Add(staleRole);
                await context.SaveChangesAsync();
                context.UserRoles.Add(new UserRole(user.TenantId, user.Id, staleRole.Id));
                await context.SaveChangesAsync();
            });

            await UsingDbContextAsync(context =>
            {
                new DefaultCustomerAccountProvisioner(context).Provision(user.TenantId.Value);
                return Task.CompletedTask;
            });

            var activeRoleNames = await UsingDbContextAsync(async context =>
                await (
                    from userRole in context.UserRoles
                    join role in context.Roles on userRole.RoleId equals role.Id
                    where userRole.UserId == user.Id
                    select role.Name).ToListAsync());

            activeRoleNames.ShouldContain("Guest");
        }

        [Fact]
        public async Task Provision_CreatesCustomer_ForExistingGuestWithoutProfile()
        {
            var user = await CreateUserAsync("guest_profile_" + System.Guid.NewGuid().ToString("N"));
            await RemoveAllUserRolesAsync(user.Id);

            await UsingDbContextAsync(async context =>
            {
                var guestRole = await context.Roles.SingleAsync(
                    role => role.TenantId == user.TenantId && role.Name == "Guest");
                context.UserRoles.Add(new UserRole(user.TenantId, user.Id, guestRole.Id));
                await context.SaveChangesAsync();
            });

            await UsingDbContextAsync(context =>
            {
                new DefaultCustomerAccountProvisioner(context).Provision(user.TenantId.Value);
                return Task.CompletedTask;
            });

            var customerExists = await UsingDbContextAsync(context =>
                context.Customers.AnyAsync(customer => customer.UserId == user.Id));

            customerExists.ShouldBeTrue();
        }

        [Fact]
        public async Task TenantSeed_DoesNotRefreshDemoAccounts_WhenDemoDataIsDisabled()
        {
            const string sentinelPassword = "not-a-demo-password-hash";
            await UsingDbContextAsync(async context =>
            {
                var demoUser = await context.Users.SingleAsync(
                    user => user.UserName == AreaLeaderDemoDataBuilder.UserName);
                demoUser.Password = sentinelPassword;
                await context.SaveChangesAsync();
            });

            await UsingDbContextAsync(context =>
            {
                new TenantRoleAndUserBuilder(context, 1, seedDemoData: false).Create();
                return Task.CompletedTask;
            });

            var password = await UsingDbContextAsync(async context =>
                await context.Users
                    .Where(user => user.UserName == AreaLeaderDemoDataBuilder.UserName)
                    .Select(user => user.Password)
                    .SingleAsync());

            password.ShouldBe(sentinelPassword);
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
