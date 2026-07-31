using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Runtime.Session;
using Abp.Runtime.Validation;
using Abp.UI;
using AqualLifeStyle.Application.MyAccount;
using AqualLifeStyle.Application.MyAccount.Dto;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class MyAccountAppServiceTests : AqualLifeStyleTestBase
    {
        private readonly IMyAccountAppService _service;

        public MyAccountAppServiceTests()
        {
            _service = Resolve<IMyAccountAppService>();
        }

        private async Task<long> CreateAndLoginTestUserAsync()
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            var userId = await CreateTestUserAsync(AbpSession.TenantId, $"testuser{suffix}", $"testuser{suffix}@defaulttenant.com");
            SetCurrentUser(userId, AbpSession.TenantId);
            return userId;
        }

        [Fact]
        public async Task ChangePassword_ReplacesPasswordAndInvalidatesExistingSessions()
        {
            await CreateAndLoginTestUserAsync();

            string originalSecurityStamp = null;
            await UsingDbContextAsync(async context =>
            {
                var user = await context.Users.FindAsync(AbpSession.GetUserId());
                originalSecurityStamp = user.SecurityStamp;
            });

            await _service.ChangePasswordAsync(new ChangeMyPasswordInput
            {
                CurrentPassword = User.DefaultPassword,
                NewPassword = "PrivateAdminPassword123!"
            });

            await UsingDbContextAsync(async context =>
            {
                var user = await context.Users.FindAsync(AbpSession.GetUserId());
                var hasher = Resolve<IPasswordHasher<User>>();
                hasher.VerifyHashedPassword(user, user.Password, User.DefaultPassword)
                    .ShouldBe(PasswordVerificationResult.Failed);
                hasher.VerifyHashedPassword(user, user.Password, "PrivateAdminPassword123!")
                    .ShouldNotBe(PasswordVerificationResult.Failed);
                user.SecurityStamp.ShouldNotBe(originalSecurityStamp);
            });
        }

        [Fact]
        public async Task ChangePassword_WithIncorrectCurrentPassword_DoesNotChangePassword()
        {
            await CreateAndLoginTestUserAsync();

            var result = await _service.ChangePasswordAsync(new ChangeMyPasswordInput
            {
                CurrentPassword = "not-the-current-password",
                NewPassword = "PrivateAdminPassword123!"
            });

            result.Succeeded.ShouldBeFalse();
            result.Message.ShouldBe("Your current password is incorrect. No changes were made.");

            await UsingDbContextAsync(async context =>
            {
                var user = await context.Users.FindAsync(AbpSession.GetUserId());
                var hasher = Resolve<IPasswordHasher<User>>();
                hasher.VerifyHashedPassword(user, user.Password, User.DefaultPassword)
                    .ShouldNotBe(PasswordVerificationResult.Failed);
                user.AccessFailedCount.ShouldBeGreaterThan(0);
            });
        }

        [Fact]
        public async Task ChangePassword_WithoutSpecialCharacter_IsRejectedByServerValidation()
        {
            await CreateAndLoginTestUserAsync();

            await Should.ThrowAsync<AbpValidationException>(() =>
                _service.ChangePasswordAsync(new ChangeMyPasswordInput
                {
                    CurrentPassword = User.DefaultPassword,
                    NewPassword = "NoSpecialCharacter123"
                }));
        }

        [Fact]
        public async Task Profile_UpdatePersistsPersonalDetailsOnTheLinkedUserAndCustomer()
        {
            var userId = await CreateAndLoginTestUserAsync();
            await UsingDbContextAsync(async context =>
            {
                var user = await context.Users.FindAsync(userId);
                user.UpdateContactDetails("+27 71 000 0000", "1 Original Road, Johannesburg");
                context.Customers.Add(Customer.Create(
                    user.TenantId,
                    user.Id,
                    $"{user.Name} {user.Surname}",
                    new EmailAddress(user.EmailAddress),
                    null,
                    user));
                await context.SaveChangesAsync();
            });

            var updated = await _service.UpdateProfileAsync(new UpdateMyProfileInput
            {
                FirstName = "Updated",
                Surname = "Customer",
                EmailAddress = (await UsingDbContextAsync(context =>
                    context.Users.Where(user => user.Id == userId)
                        .Select(user => user.EmailAddress)
                        .SingleAsync())),
                ContactNumber = "+27 82 123 4567",
                HomeAddress = "25 New Home Avenue, Johannesburg"
            });

            updated.FirstName.ShouldBe("Updated");
            updated.Surname.ShouldBe("Customer");
            updated.ContactNumber.ShouldBe("+27 82 123 4567");
            updated.HomeAddress.ShouldBe("25 New Home Avenue, Johannesburg");

            await UsingDbContextAsync(async context =>
            {
                var customer = await context.Customers.Include(item => item.User)
                    .SingleAsync(item => item.UserId == userId);
                customer.Name.ShouldBe("Updated Customer");
                customer.Email.Value.ShouldBe(updated.EmailAddress);
                customer.User.PhoneNumber.ShouldBe(updated.ContactNumber);
                customer.User.HomeAddress.ShouldBe(updated.HomeAddress);
            });
        }

        [Fact]
        public async Task Profile_EmailChange_IsRejectedWithoutVerification()
        {
            var userId = await CreateAndLoginTestUserAsync();
            string originalEmail = null;
            await UsingDbContextAsync(async context =>
            {
                var user = await context.Users.FindAsync(userId);
                user.IsEmailConfirmed = true;
                originalEmail = user.EmailAddress;
                context.Customers.Add(Customer.Create(
                    user.TenantId,
                    user.Id,
                    $"{user.Name} {user.Surname}",
                    new EmailAddress(user.EmailAddress),
                    null,
                    user));
                await context.SaveChangesAsync();
            });

            var error = await Should.ThrowAsync<UserFriendlyException>(() =>
                _service.UpdateProfileAsync(new UpdateMyProfileInput
                {
                    FirstName = "Updated",
                    Surname = "Customer",
                    EmailAddress = $"unverified-{Guid.NewGuid():N}@defaulttenant.com",
                    ContactNumber = "+27 82 123 4567",
                    HomeAddress = "25 New Home Avenue, Johannesburg"
                }));

            error.Details.ShouldContain("require verification");
            await UsingDbContextAsync(async context =>
            {
                var user = await context.Users.FindAsync(userId);
                user.EmailAddress.ShouldBe(originalEmail);
                user.IsEmailConfirmed.ShouldBeTrue();
            });
        }
    }
}
