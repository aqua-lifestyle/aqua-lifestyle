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
    }
}
