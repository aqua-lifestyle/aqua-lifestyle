using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Authorization;
using Abp.Authorization.Roles;
using Abp.Configuration;
using AqualLifeStyle.Application.Customers;
using AqualLifeStyle.Application.Memberships;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Authorization.Accounts;
using AqualLifeStyle.Authorization.Accounts.Dto;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class AccountRegistrationTests : AqualLifeStyleTestBase
    {
        private readonly IAccountAppService _accountAppService;
        private readonly ICustomerAppService _customerAppService;
        private readonly IMembershipAppService _membershipAppService;

        public AccountRegistrationTests()
        {
            _accountAppService = Resolve<IAccountAppService>();
            _customerAppService = Resolve<ICustomerAppService>();
            _membershipAppService = Resolve<IMembershipAppService>();
        }

        [Fact]
        public async Task Register_ProvisionsCustomerWithGuestAccess()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var email = $"customer_{suffix}@test.com";
            var userName = $"customer_{suffix}";

            using (UsingTenantId(1))
            {
                var settingManager = Resolve<ISettingManager>();
                await settingManager.ChangeSettingForTenantAsync(1, "Abp.Account.IsSelfRegistrationEnabled", "true");

                var result = await _accountAppService.Register(new RegisterInput
                {
                    EmailAddress = email,
                    Name = "New",
                    Password = "Customer!101",
                    Surname = "Customer",
                    UserName = userName
                });

                result.CanLogin.ShouldBeTrue();

                await UsingDbContextAsync(async context =>
                {
                    var user = await context.Users.SingleAsync(item => item.UserName == userName);
                    var customer = await context.Customers.SingleAsync(item => item.UserId == user.Id);
                    var roleNames = await (
                        from userRole in context.UserRoles
                        join role in context.Roles on userRole.RoleId equals role.Id
                        where userRole.UserId == user.Id
                        select role.Name).ToListAsync();
                    var guestRole = await context.Roles.SingleAsync(role => role.TenantId == 1 && role.Name == "Guest");
                    var guestPermissions = await context.Permissions
                        .OfType<RolePermissionSetting>()
                        .Where(permission => permission.TenantId == 1 && permission.RoleId == guestRole.Id && permission.IsGranted)
                        .Select(permission => permission.Name)
                        .ToListAsync();

                    customer.Email.Value.ShouldBe(email);
                    customer.MembershipId.ShouldBeNull();
                    roleNames.ShouldContain("Guest");
                    guestPermissions.ShouldContain(PermissionNames.Pages_Products);
                    guestPermissions.ShouldContain(AquaPermissions.Members.ViewSelf);
                    guestPermissions.ShouldContain(AquaPermissions.Memberships.ViewSelf);
                    guestPermissions.ShouldContain(AquaPermissions.ProgrammeParticipations.ViewSelf);
                    guestPermissions.ShouldContain(AquaPermissions.ProgrammeParticipations.Join);
                    guestPermissions.ShouldContain(AquaPermissions.Orders.Place);
                    guestPermissions.ShouldNotContain(PermissionNames.Pages_Customers);
                    guestPermissions.ShouldNotContain(PermissionNames.Pages_Memberships);

                    SetCurrentUser(user.Id, user.TenantId);
                });

                var currentCustomer = await _customerAppService.GetMyCustomerAsync();
                currentCustomer.Email.ShouldBe(email);
                (await _membershipAppService.GetActiveTiersAsync()).ShouldNotBeEmpty();
                await Should.ThrowAsync<AbpAuthorizationException>(() =>
                    _customerAppService.GetAllAsync());
                await Should.ThrowAsync<AbpAuthorizationException>(() =>
                    _membershipAppService.GetAllAsync());
            }
        }
    }
}
