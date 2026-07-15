using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Authorization;
using AqualLifeStyle.Application.Admin.Customers;
using AqualLifeStyle.Application.Admin.Customers.Dto;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class AdminCustomerAppServiceTests : AqualLifeStyleTestBase
    {
        private readonly IAdminCustomerAppService _service;

        public AdminCustomerAppServiceTests()
        {
            _service = Resolve<IAdminCustomerAppService>();
        }

        [Fact]
        public async Task CustomerLifecycle_CreatesLinkedAccount_UpdatesAndSoftDeletes()
        {
            var email = $"admin-created-{Guid.NewGuid():N}@example.com";
            var created = await _service.CreateAsync(new AdminCreateCustomerInput
            {
                TenantId = 1,
                FirstName = "Ada",
                LastName = "Lovelace",
                Email = email,
                IsActive = true,
                Justification = "Approved customer onboarding"
            });

            created.TenantId.ShouldBe(1);
            created.Name.ShouldBe("Ada Lovelace");
            await UsingDbContextAsync(async context =>
            {
                var customer = await context.Customers.Include(item => item.User).SingleAsync(item => item.Id == created.Id);
                customer.User.EmailAddress.ShouldBe(email);
                var roles = await (from assignment in context.UserRoles
                    join role in context.Roles on assignment.RoleId equals role.Id
                    where assignment.UserId == customer.UserId
                    select role.Name).ToListAsync();
                roles.ShouldContain("Guest");
            });

            var updated = await _service.UpdateAsync(new AdminUpdateCustomerInput
            {
                Id = created.Id,
                FirstName = "Augusta Ada",
                LastName = "Lovelace",
                Email = email,
                IsActive = false,
                Justification = "Customer requested an account pause"
            });
            updated.Name.ShouldBe("Augusta Ada Lovelace");
            updated.IsActive.ShouldBeFalse();

            await _service.DeleteAsync(new AdminDeleteCustomerInput
            {
                Id = created.Id,
                Justification = "Duplicate registration confirmed"
            });
            await UsingDbContextAsync(async context =>
            {
                var customer = await context.Customers.IgnoreQueryFilters().SingleAsync(item => item.Id == created.Id);
                var user = await context.Users.IgnoreQueryFilters().SingleAsync(item => item.Id == customer.UserId);
                customer.IsDeleted.ShouldBeTrue();
                user.IsActive.ShouldBeFalse();
            });
        }

        [Fact]
        public async Task Create_RejectsCrossTenantRequestForTenantAdmin()
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => _service.CreateAsync(new AdminCreateCustomerInput
            {
                TenantId = 2,
                FirstName = "Cross",
                LastName = "Tenant",
                Email = $"cross-{Guid.NewGuid():N}@example.com",
                IsActive = true,
                Justification = "Invalid cross tenant attempt"
            }));
        }
    }
}
