using System;
using System.Linq;
using System.Threading.Tasks;
using AqualLifeStyle.Application.Admin.Customers;
using AqualLifeStyle.Application.Admin.Customers.Dto;
using AqualLifeStyle.Application.Admin.Members;
using AqualLifeStyle.Application.Admin.Members.Dto;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class AdminMemberAppServiceTests : AqualLifeStyleTestBase
    {
        private readonly IAdminCustomerAppService _customerAdministration;
        private readonly IAdminMemberAppService _memberAdministration;

        public AdminMemberAppServiceTests()
        {
            _customerAdministration = Resolve<IAdminCustomerAppService>();
            _memberAdministration = Resolve<IAdminMemberAppService>();
        }

        [Fact]
        public async Task MemberLifecycle_EditsProfileChangesTierAndSuspendsLinkedAccount()
        {
            var membershipIds = await UsingDbContextAsync(async context =>
            {
                var entryTier = Membership.Create(1, $"Starter-{Guid.NewGuid():N}", "Starter membership", MembershipType.Jasper);
                var upgradedTier = Membership.Create(1, $"Upgrade-{Guid.NewGuid():N}", "Upgraded membership", MembershipType.BusinessPremier);
                context.Memberships.AddRange(entryTier, upgradedTier);
                await context.SaveChangesAsync();
                return new[] { entryTier.Id, upgradedTier.Id };
            });
            var originalEmail = $"member-{Guid.NewGuid():N}@example.com";
            var member = (await _customerAdministration.CreateAsync(new AdminCreateCustomerInput
            {
                TenantId = 1,
                FirstName = "Original",
                LastName = "Member",
                Email = originalEmail,
                ContactNumber = "+27 76 666 6666",
                HomeAddress = "6 Member Way, Johannesburg",
                Password = "Temporary123!",
                MembershipId = membershipIds[0],
                IsActive = true,
                Justification = "Approved member onboarding"
            })).Customer;

            var updatedEmail = $"updated-member-{Guid.NewGuid():N}@example.com";
            var edited = await _memberAdministration.EditProfileAsync(new EditMemberProfileInput
            {
                Id = member.Id,
                FirstName = "Updated",
                LastName = "Member",
                Email = updatedEmail,
                ContactNumber = "+27 77 777 7777",
                HomeAddress = "7 Updated Way, Johannesburg",
                Justification = "Member requested a profile correction"
            });
            edited.FirstName.ShouldBe("Updated");
            edited.Email.ShouldBe(updatedEmail);
            edited.ContactNumber.ShouldBe("+27 77 777 7777");
            edited.HomeAddress.ShouldBe("7 Updated Way, Johannesburg");

            var upgraded = await _memberAdministration.ChangeTierAsync(new ChangeMemberTierInput
            {
                Id = member.Id,
                MembershipId = membershipIds[1],
                Justification = "Member qualified for the upgraded tier"
            });
            upgraded.MembershipId.ShouldBe(membershipIds[1]);

            var suspended = await _memberAdministration.SuspendAsync(new SuspendMemberInput
            {
                Id = member.Id,
                Justification = "Account review requires a temporary suspension"
            });
            suspended.IsActive.ShouldBeFalse();

            await UsingDbContextAsync(async context =>
            {
                var persistedMember = await context.Customers.Include(customer => customer.User)
                    .SingleAsync(customer => customer.Id == member.Id);
                persistedMember.Name.ShouldBe("Updated Member");
                persistedMember.Email.Value.ShouldBe(updatedEmail);
                persistedMember.User.PhoneNumber.ShouldBe("+27 77 777 7777");
                persistedMember.User.HomeAddress.ShouldBe("7 Updated Way, Johannesburg");
                persistedMember.MembershipId.ShouldBe(membershipIds[1]);
                persistedMember.IsActive.ShouldBeFalse();
                persistedMember.User.IsActive.ShouldBeFalse();
                var roles = await (from assignment in context.UserRoles
                    join role in context.Roles on assignment.RoleId equals role.Id
                    where assignment.UserId == persistedMember.UserId
                    select role.Name).ToListAsync();
                roles.ShouldContain("Member");
            });
        }
    }
}
