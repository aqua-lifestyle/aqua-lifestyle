using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Authorization;
using Abp.UI;
using AqualLifeStyle.Application.Admin.Customers;
using AqualLifeStyle.Application.Admin.Customers.Dto;
using AqualLifeStyle.Authorization.Accounts;
using AqualLifeStyle.Authorization.Accounts.Dto;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.WebUtilities;
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
        public async Task CustomerLifecycle_CreatesUpdatesRemovesAndRestoresLinkedAccount()
        {
            var email = $"admin-created-{Guid.NewGuid():N}@example.com";
            var creationResult = await _service.CreateAsync(new AdminCreateCustomerInput
            {
                TenantId = 1,
                FirstName = "Ada",
                LastName = "Lovelace",
                Email = email,
                Password = "Temporary123!",
                IsActive = true,
                Justification = "Approved customer onboarding"
            });
            var created = creationResult.Customer;

            creationResult.RequiresRestoreConfirmation.ShouldBeFalse();
            created.TenantId.ShouldBe(1);
            created.Name.ShouldBe("Ada Lovelace");
            var originalCreationTime = created.CreationTime;
            var originalUserId = created.UserId;
            var createdUser = await Resolve<UserManager>().FindByEmailAsync(email);
            createdUser.ShouldNotBeNull();
            var originalPasswordHash = createdUser.Password;
            var originalSecurityStamp = createdUser.SecurityStamp;
            (await Resolve<UserManager>().CheckPasswordAsync(createdUser, "Temporary123!")).ShouldBeTrue();
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
            string removedSecurityStamp = null;
            await UsingDbContextAsync(async context =>
            {
                var customer = await context.Customers.IgnoreQueryFilters().SingleAsync(item => item.Id == created.Id);
                var user = await context.Users.IgnoreQueryFilters().SingleAsync(item => item.Id == customer.UserId);
                customer.IsDeleted.ShouldBeTrue();
                user.IsActive.ShouldBeFalse();
                removedSecurityStamp = user.SecurityStamp;
                removedSecurityStamp.ShouldNotBe(originalSecurityStamp);
            });

            var restorationCandidate = await _service.CreateAsync(new AdminCreateCustomerInput
            {
                TenantId = 1,
                FirstName = "Dora",
                LastName = "Shongwe",
                Email = email,
                IsActive = true,
                Justification = "Returning customer requested account restoration"
            });
            restorationCandidate.RequiresRestoreConfirmation.ShouldBeTrue();
            restorationCandidate.RemovedCustomer.CustomerId.ShouldBe(created.Id);
            await UsingDbContextAsync(async context =>
            {
                var untouchedCustomer = await context.Customers.IgnoreQueryFilters().SingleAsync(item => item.Id == created.Id);
                untouchedCustomer.IsDeleted.ShouldBeTrue();
                untouchedCustomer.Name.ShouldBe("Augusta Ada Lovelace");
            });

            var restorationResult = await _service.RestoreAsync(new AdminRestoreCustomerInput
            {
                CustomerId = restorationCandidate.RemovedCustomer.CustomerId,
                FirstName = "Dora",
                LastName = "Shongwe",
                Email = email,
                IsActive = true,
                Justification = "Returning customer explicitly approved for restoration"
            });
            var restored = restorationResult.Customer;
            restored.Id.ShouldBe(created.Id);
            restored.UserId.ShouldBe(originalUserId);
            restored.CreationTime.ShouldBe(originalCreationTime);
            restored.Name.ShouldBe("Dora Shongwe");
            restored.IsActive.ShouldBeTrue();
            var restoredUser = await Resolve<UserManager>().FindByEmailAsync(email);
            restoredUser.ShouldNotBeNull();
            restoredUser.IsActive.ShouldBeTrue();
            restoredUser.Password.ShouldBe(originalPasswordHash);
            restoredUser.SecurityStamp.ShouldNotBe(removedSecurityStamp);
            restoredUser.RequiresPasswordReset().ShouldBeTrue();
            (await Resolve<UserManager>().CheckPasswordAsync(restoredUser, "Temporary123!")).ShouldBeTrue();
            await UsingDbContextAsync(async context =>
            {
                var restoredCustomers = await context.Customers.IgnoreQueryFilters()
                    .Where(item => item.Email.Value == email)
                    .ToListAsync();
                restoredCustomers.Count.ShouldBe(1);
                restoredCustomers[0].IsDeleted.ShouldBeFalse();
            });

            var passwordSetupUri = new Uri(restorationResult.PasswordSetupUrl);
            var passwordSetupQuery = QueryHelpers.ParseQuery(passwordSetupUri.Query);
            var passwordSetupCompleted = await Resolve<IAccountAppService>().CompletePasswordSetup(new CompletePasswordSetupInput
            {
                AreaName = passwordSetupQuery["area"],
                UserId = long.Parse(passwordSetupQuery["userId"]),
                ResetToken = passwordSetupQuery["token"],
                NewPassword = "CustomerChosen123!"
            });
            passwordSetupCompleted.ShouldBeTrue();
            restoredUser = await Resolve<UserManager>().FindByEmailAsync(email);
            restoredUser.IsActive.ShouldBeTrue();
            restoredUser.RequiresPasswordReset().ShouldBeFalse();
            (await Resolve<UserManager>().CheckPasswordAsync(restoredUser, "CustomerChosen123!")).ShouldBeTrue();
            await Should.ThrowAsync<UserFriendlyException>(() =>
                Resolve<IAccountAppService>().CompletePasswordSetup(new CompletePasswordSetupInput
                {
                    AreaName = passwordSetupQuery["area"],
                    UserId = long.Parse(passwordSetupQuery["userId"]),
                    ResetToken = passwordSetupQuery["token"],
                    NewPassword = "AnotherPassword123!"
                }));
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
                Password = "Temporary123!",
                IsActive = true,
                Justification = "Invalid cross tenant attempt"
            }));
        }

        [Fact]
        public async Task MembershipPlans_ExcludeOnyxAndOtherAreaPlans_AndAllowPlatformAssignment()
        {
            var planIds = await UsingDbContextAsync((int?)null, async context =>
            {
                var platformPlan = Membership.Create(null, $"Platform-{Guid.NewGuid():N}", "Available in every Area", MembershipType.Jasper);
                var currentAreaPlan = Membership.Create(1, $"Area-1-{Guid.NewGuid():N}", "Available in the current Area", MembershipType.AQGreen);
                var onyxProgramme = Membership.Create(1, $"Onyx-{Guid.NewGuid():N}", "Joined through programme participation", MembershipType.Onyx);
                var otherAreaPlan = Membership.Create(2, $"Area-2-{Guid.NewGuid():N}", "Available in another Area", MembershipType.BusinessPremier);
                context.Memberships.AddRange(platformPlan, currentAreaPlan, onyxProgramme, otherAreaPlan);
                await context.SaveChangesAsync();
                return new[] { platformPlan.Id, currentAreaPlan.Id, onyxProgramme.Id, otherAreaPlan.Id };
            });

            var options = await _service.GetMembershipOptionsAsync(new AdminCustomerMembershipOptionsInput { TenantId = 1 });
            options.Select(option => option.Id).ShouldContain(planIds[0]);
            options.Select(option => option.Id).ShouldContain(planIds[1]);
            options.Select(option => option.Id).ShouldNotContain(planIds[2]);
            options.Select(option => option.Id).ShouldNotContain(planIds[3]);

            var email = $"platform-member-{Guid.NewGuid():N}@example.com";
            var customer = (await _service.CreateAsync(new AdminCreateCustomerInput
            {
                TenantId = 1,
                FirstName = "Platform",
                LastName = "Member",
                Email = email,
                Password = "Temporary123!",
                IsActive = true,
                Justification = "Approved customer onboarding"
            })).Customer;
            var updated = await _service.UpdateAsync(new AdminUpdateCustomerInput
            {
                Id = customer.Id,
                FirstName = "Platform",
                LastName = "Club Member",
                Email = email,
                MembershipId = planIds[0],
                IsActive = true,
                Justification = "Customer selected a platform membership plan"
            });

            updated.MembershipId.ShouldBe(planIds[0]);
            updated.MembershipName.ShouldNotBeNullOrWhiteSpace();
            await UsingDbContextAsync(async context =>
            {
                var persistedCustomer = await context.Customers.SingleAsync(item => item.Id == customer.Id);
                persistedCustomer.MembershipId.ShouldBe(planIds[0]);
                var roleNames = await (from assignment in context.UserRoles
                    join role in context.Roles on assignment.RoleId equals role.Id
                    where assignment.UserId == persistedCustomer.UserId
                    select role.Name).ToListAsync();
                roleNames.ShouldContain("Member");
                roleNames.ShouldNotContain("Guest");
            });
        }
    }
}
