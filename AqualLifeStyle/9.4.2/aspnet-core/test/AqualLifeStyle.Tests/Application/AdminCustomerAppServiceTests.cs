using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Authorization;
using Abp.Authorization.Users;
using Abp.UI;
using AqualLifeStyle.Application.Admin.Customers;
using AqualLifeStyle.Application.Admin.Customers.Dto;
using AqualLifeStyle.Application.ProgrammeParticipations;
using AqualLifeStyle.Authorization.Accounts;
using AqualLifeStyle.Authorization.Accounts.Dto;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
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
        public async Task EditingActiveAQGreenParticipant_PreservesMemberAccessWithoutLegacyMembership()
        {
            var email = $"aqgreen-member-{Guid.NewGuid():N}@example.com";
            var customer = (await _service.CreateAsync(new AdminCreateCustomerInput
            {
                TenantId = 1,
                FirstName = "Active",
                LastName = "Participant",
                Email = email,
                ContactNumber = "+27 72 123 4567",
                HomeAddress = "12 Programme Road, Johannesburg",
                Password = "Temporary123!",
                IsActive = true,
                Justification = "Creating an AQGreen participant"
            })).Customer;

            await UsingDbContextAsync(1, async context =>
            {
                var participation = EntryParticipation.StartIndependently(
                    1,
                    customer.Id,
                    Resolve<ICurrentProgrammeTermsProvider>().GetEntryTerms(),
                    DateTime.UtcNow.AddMinutes(-2));
                var payment = MemberPayment.CreatePending(
                    1,
                    customer.Id,
                    MemberPaymentPurpose.AQGreenJoining,
                    1200m,
                    "Test",
                    $"aqgreen-admin-edit-{Guid.NewGuid():N}",
                    DateTime.UtcNow.AddMinutes(-1));
                payment.Confirm(DateTime.UtcNow);
                participation.ApplyConfirmedJoiningPayment(payment);
                context.MemberPayments.Add(payment);
                context.EntryParticipations.Add(participation);
                var user = await context.Users.SingleAsync(item => item.Id == customer.UserId);
                user.SetRole(AquaUserRole.Member);
                await context.SaveChangesAsync();
            });

            await _service.UpdateAsync(new AdminUpdateCustomerInput
            {
                Id = customer.Id,
                FirstName = "Updated",
                LastName = "Participant",
                Email = email,
                ContactNumber = "+27 72 123 4567",
                HomeAddress = "12 Programme Road, Johannesburg",
                MembershipId = null,
                IsActive = true,
                Justification = "Correcting the participant name"
            });

            await UsingDbContextAsync(1, async context =>
            {
                var persisted = await context.Customers.Include(item => item.User)
                    .SingleAsync(item => item.Id == customer.Id);
                persisted.MembershipId.ShouldBeNull();
                persisted.User.Role.ShouldBe(AquaUserRole.Member);
                var roleNames = await (from assignment in context.UserRoles
                    join role in context.Roles on assignment.RoleId equals role.Id
                    where assignment.UserId == persisted.UserId
                    select role.Name).ToListAsync();
                roleNames.ShouldContain(AquaUserRole.Member.ToString());
                roleNames.ShouldNotContain(AquaUserRole.Guest.ToString());
            });
        }

        [Fact]
        public async Task EditingCustomerProfile_DoesNotReplaceHigherBusinessRole()
        {
            var email = $"area-leader-profile-{Guid.NewGuid():N}@example.com";
            var customer = (await _service.CreateAsync(new AdminCreateCustomerInput
            {
                TenantId = 1,
                FirstName = "Area",
                LastName = "Leader",
                Email = email,
                ContactNumber = "+27 73 123 4567",
                HomeAddress = "14 Leadership Lane, Johannesburg",
                Password = "Temporary123!",
                IsActive = true,
                Justification = "Creating a customer-linked Area Leader"
            })).Customer;
            await UsingDbContextAsync(1, async context =>
            {
                var user = await context.Users.SingleAsync(item => item.Id == customer.UserId);
                user.SetRole(AquaUserRole.AreaLeader);
                var guestRoleId = await context.Roles
                    .Where(role => role.Name == AquaUserRole.Guest.ToString())
                    .Select(role => role.Id)
                    .SingleAsync();
                var areaLeaderRoleId = await context.Roles
                    .Where(role => role.Name == AquaUserRole.AreaLeader.ToString())
                    .Select(role => role.Id)
                    .SingleAsync();
                context.UserRoles.RemoveRange(context.UserRoles.Where(assignment =>
                    assignment.UserId == customer.UserId && assignment.RoleId == guestRoleId));
                context.UserRoles.Add(new UserRole(1, customer.UserId, areaLeaderRoleId));
                await context.SaveChangesAsync();
            });

            await _service.UpdateAsync(new AdminUpdateCustomerInput
            {
                Id = customer.Id,
                FirstName = "Updated Area",
                LastName = "Leader",
                Email = email,
                ContactNumber = "+27 73 123 4567",
                HomeAddress = "14 Leadership Lane, Johannesburg",
                MembershipId = null,
                IsActive = true,
                Justification = "Correcting the Area Leader profile"
            });

            await UsingDbContextAsync(1, async context =>
            {
                var persisted = await context.Customers.Include(item => item.User)
                    .SingleAsync(item => item.Id == customer.Id);
                persisted.User.Role.ShouldBe(AquaUserRole.AreaLeader);
                var roleNames = await (from assignment in context.UserRoles
                    join role in context.Roles on assignment.RoleId equals role.Id
                    where assignment.UserId == persisted.UserId
                    select role.Name).ToListAsync();
                roleNames.ShouldContain(AquaUserRole.AreaLeader.ToString());
                roleNames.ShouldNotContain(AquaUserRole.Guest.ToString());
            });
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
                ContactNumber = "+27 71 111 1111",
                HomeAddress = "1 Customer Street, Johannesburg",
                Password = "Temporary123!",
                IsActive = true,
                Justification = "Approved customer onboarding"
            });
            var created = creationResult.Customer;

            creationResult.RequiresRestoreConfirmation.ShouldBeFalse();
            created.TenantId.ShouldBe(1);
            created.Name.ShouldBe("Ada Lovelace");
            created.ContactNumber.ShouldBe("+27 71 111 1111");
            created.HomeAddress.ShouldBe("1 Customer Street, Johannesburg");
            var originalCreationTime = created.CreationTime;
            var originalUserId = created.UserId;
            var createdUser = await Resolve<UserManager>().FindByEmailAsync(email);
            createdUser.ShouldNotBeNull();
            createdUser.IsEmailConfirmed.ShouldBeFalse();
            var originalPasswordHash = createdUser.Password;
            var originalSecurityStamp = createdUser.SecurityStamp;
            (await Resolve<UserManager>().CheckPasswordAsync(createdUser, "Temporary123!")).ShouldBeTrue();
            await UsingDbContextAsync(async context =>
            {
                var customer = await context.Customers.Include(item => item.User).SingleAsync(item => item.Id == created.Id);
                customer.User.EmailAddress.ShouldBe(email);
                var verification = await context.TransactionalEmailOutboxMessages.SingleAsync(message =>
                    message.NotificationType == "EmailVerification" &&
                    message.Recipient == email &&
                    message.IdempotencyKey.Contains("admin-created"));
                verification.HtmlBody.ShouldContain("/verify-email?");
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
                ContactNumber = "+27 72 222 2222",
                HomeAddress = "2 Updated Avenue, Johannesburg",
                IsActive = false,
                Justification = "Customer requested an account pause"
            });
            updated.Name.ShouldBe("Augusta Ada Lovelace");
            updated.IsActive.ShouldBeFalse();
            updated.ContactNumber.ShouldBe("+27 72 222 2222");
            updated.HomeAddress.ShouldBe("2 Updated Avenue, Johannesburg");

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
                ContactNumber = "+27 73 333 3333",
                HomeAddress = "3 Restore Road, Johannesburg",
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
                ContactNumber = "+27 73 333 3333",
                HomeAddress = "3 Restore Road, Johannesburg",
                IsActive = true,
                Justification = "Returning customer explicitly approved for restoration"
            });
            var restored = restorationResult.Customer;
            restored.Id.ShouldBe(created.Id);
            restored.UserId.ShouldBe(originalUserId);
            restored.CreationTime.ShouldBe(originalCreationTime);
            restored.Name.ShouldBe("Dora Shongwe");
            restored.IsActive.ShouldBeTrue();
            restored.ContactNumber.ShouldBe("+27 73 333 3333");
            restored.HomeAddress.ShouldBe("3 Restore Road, Johannesburg");
            var restoredUser = await Resolve<UserManager>().FindByEmailAsync(email);
            restoredUser.ShouldNotBeNull();
            restoredUser.IsActive.ShouldBeTrue();
            restoredUser.Password.ShouldBe(originalPasswordHash);
            restoredUser.SecurityStamp.ShouldNotBe(removedSecurityStamp);
            restoredUser.RequiresPasswordReset().ShouldBeTrue();
            await UsingDbContextAsync(async context =>
                (await context.TransactionalEmailOutboxMessages.CountAsync(message =>
                    message.NotificationType == "EmailVerification" &&
                    message.Recipient == email)).ShouldBe(2));
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
            var passwordSetupFragment = QueryHelpers.ParseQuery(passwordSetupUri.Fragment.TrimStart('#'));
            var passwordSetupCompleted = await Resolve<IAccountAppService>().CompletePasswordSetup(new CompletePasswordSetupInput
            {
                AreaName = passwordSetupQuery["area"],
                UserId = long.Parse(passwordSetupQuery["userId"]),
                ResetToken = passwordSetupFragment["token"],
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
                    ResetToken = passwordSetupFragment["token"],
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
                ContactNumber = "+27 74 444 4444",
                HomeAddress = "4 Cross Street, Cape Town",
                Password = "Temporary123!",
                IsActive = true,
                Justification = "Invalid cross tenant attempt"
            }));
        }

        [Fact]
        public async Task UpdateEmail_RevokesSessionsAndRequiresVerificationBeforePasswordReset()
        {
            var originalEmail = $"confirmed-{Guid.NewGuid():N}@example.com";
            var newEmail = $"changed-{Guid.NewGuid():N}@example.com";
            var customer = (await _service.CreateAsync(new AdminCreateCustomerInput
            {
                TenantId = 1,
                FirstName = "Confirmed",
                LastName = "Customer",
                Email = originalEmail,
                ContactNumber = "+27 71 111 1111",
                HomeAddress = "1 Verified Street, Johannesburg",
                Password = "Temporary123!",
                IsActive = true,
                Justification = "Creating a verified customer"
            })).Customer;
            string originalSecurityStamp = null;
            await UsingDbContextAsync(1, async context =>
            {
                var user = await context.Users.SingleAsync(item => item.Id == customer.UserId);
                user.IsEmailConfirmed = true;
                originalSecurityStamp = user.SecurityStamp;
                await context.SaveChangesAsync();
            });

            await _service.UpdateAsync(new AdminUpdateCustomerInput
            {
                Id = customer.Id,
                FirstName = "Confirmed",
                LastName = "Customer",
                Email = newEmail,
                ContactNumber = "+27 71 111 1111",
                HomeAddress = "1 Verified Street, Johannesburg",
                IsActive = true,
                Justification = "Customer requested an email change"
            });
            await Resolve<IAccountAppService>().RequestPasswordReset(new RequestAccountEmailInput
            {
                AreaName = "Default",
                EmailAddress = newEmail
            });

            await UsingDbContextAsync(1, async context =>
            {
                var user = await context.Users.SingleAsync(item => item.Id == customer.UserId);
                user.IsEmailConfirmed.ShouldBeFalse();
                user.SecurityStamp.ShouldNotBe(originalSecurityStamp);
                (await context.TransactionalEmailOutboxMessages.CountAsync(message =>
                    message.NotificationType == "EmailVerification" &&
                    message.Recipient == newEmail)).ShouldBe(1);
                (await context.TransactionalEmailOutboxMessages.CountAsync(message =>
                    message.NotificationType == "PasswordReset" &&
                    message.Recipient == newEmail)).ShouldBe(0);
            });
        }

        [Fact]
        public async Task MembershipPlans_ExcludeProgrammeConfigurationsAndOtherAreaPlans_AndAllowPlatformAssignment()
        {
            var planIds = await UsingDbContextAsync((int?)null, async context =>
            {
                var platformPlan = Membership.Create(null, $"Platform-{Guid.NewGuid():N}", "Available in every Area", MembershipType.Jasper);
                var currentAreaPlan = Membership.Create(1, $"Area-1-{Guid.NewGuid():N}", "Available in the current Area", MembershipType.BusinessPremier);
                var aqGreenProgramme = Membership.Create(1, $"AQGreen-{Guid.NewGuid():N}", "Joined through programme participation", MembershipType.AQGreen);
                var onyxProgramme = Membership.Create(1, $"Onyx-{Guid.NewGuid():N}", "Joined through programme participation", MembershipType.Onyx);
                var otherAreaPlan = Membership.Create(2, $"Area-2-{Guid.NewGuid():N}", "Available in another Area", MembershipType.Jasper);
                context.Memberships.AddRange(platformPlan, currentAreaPlan, aqGreenProgramme, onyxProgramme, otherAreaPlan);
                await context.SaveChangesAsync();
                return new[] { platformPlan.Id, currentAreaPlan.Id, aqGreenProgramme.Id, onyxProgramme.Id, otherAreaPlan.Id };
            });

            var options = await _service.GetMembershipOptionsAsync(new AdminCustomerMembershipOptionsInput { TenantId = 1 });
            options.Select(option => option.Id).ShouldContain(planIds[0]);
            options.Select(option => option.Id).ShouldContain(planIds[1]);
            options.Select(option => option.Id).ShouldNotContain(planIds[2]);
            options.Select(option => option.Id).ShouldNotContain(planIds[3]);
            options.Select(option => option.Id).ShouldNotContain(planIds[4]);

            var email = $"platform-member-{Guid.NewGuid():N}@example.com";
            var customer = (await _service.CreateAsync(new AdminCreateCustomerInput
            {
                TenantId = 1,
                FirstName = "Platform",
                LastName = "Member",
                Email = email,
                ContactNumber = "+27 75 555 5555",
                HomeAddress = "5 Platform Place, Johannesburg",
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
                ContactNumber = "+27 75 555 5555",
                HomeAddress = "5 Platform Place, Johannesburg",
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
