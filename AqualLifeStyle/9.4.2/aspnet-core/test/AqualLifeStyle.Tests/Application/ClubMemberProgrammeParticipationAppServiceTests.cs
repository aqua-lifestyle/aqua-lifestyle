using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Configuration;
using Abp.UI;
using AqualLifeStyle.Application.ProgrammeParticipations;
using AqualLifeStyle.Application.ProgrammeParticipations.Dto;
using AqualLifeStyle.Authorization.Accounts;
using AqualLifeStyle.Authorization.Accounts.Dto;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class ClubMemberProgrammeParticipationAppServiceTests
        : AqualLifeStyleTestBase
    {
        private readonly IAccountAppService _accountAppService;
        private readonly IClubMemberProgrammeParticipationAppService _participationService;

        public ClubMemberProgrammeParticipationAppServiceTests()
        {
            _accountAppService = Resolve<IAccountAppService>();
            _participationService = Resolve<IClubMemberProgrammeParticipationAppService>();
        }

        [Fact]
        public async Task ClubMember_CanStartEntryAndDirectOnyxIndependently()
        {
            var customerId = await RegisterAndSignInCustomerAsync();

            var initial = await _participationService.GetMyParticipationsAsync();
            initial.CustomerId.ShouldBe(customerId);
            initial.CanJoinEntry.ShouldBeTrue();
            initial.CanJoinOnyxDirectly.ShouldBeTrue();

            var entry = await _participationService.StartEntryAsync(
                new StartEntryParticipationInput());
            var repeatedEntry = await _participationService.StartEntryAsync(
                new StartEntryParticipationInput());
            var onyx = await _participationService.StartDirectOnyxAsync(
                new StartDirectOnyxParticipationInput());

            entry.Id.ShouldBe(repeatedEntry.Id);
            entry.ProgrammeName.ShouldBe("Entry");
            entry.Status.ShouldBe("Awaiting registration payment");
            entry.JoinedIndependently.ShouldBeTrue();
            entry.NextPaymentAmount.ShouldBe(600m);
            entry.NextPaymentDescription.ShouldBe("Registration payment");
            entry.CanRecruitForThisProgramme.ShouldBeFalse();

            onyx.ProgrammeName.ShouldBe("Onyx");
            onyx.Status.ShouldBe("Awaiting full payment");
            onyx.JoinedIndependently.ShouldBeTrue();
            onyx.NextPaymentAmount.ShouldBe(6120m);
            onyx.NextPaymentDescription.ShouldBe("Full Onyx participation payment");
            onyx.CanRecruitForThisProgramme.ShouldBeFalse();

            await UsingDbContextAsync(1, async context =>
            {
                (await context.EntryParticipations.CountAsync(
                    participation => participation.CustomerId == customerId)).ShouldBe(1);
                (await context.OnyxParticipations.CountAsync(
                    participation => participation.CustomerId == customerId)).ShouldBe(1);
            });
        }

        [Fact]
        public async Task JoiningUnderRecruiter_RequiresActiveParticipationInSameProgramme()
        {
            var customerId = await RegisterAndSignInCustomerAsync();
            var recruiterCustomerId = await CreateAwaitingEntryParticipantAsync();

            var exception = await Should.ThrowAsync<UserFriendlyException>(() =>
                _participationService.StartEntryAsync(new StartEntryParticipationInput
                {
                    RecruiterCustomerId = recruiterCustomerId
                }));

            exception.Message.ShouldBe("The recruiter could not be accepted.");
            exception.Details.ShouldContain("not currently participating in Entry");

            await UsingDbContextAsync(1, async context =>
            {
                (await context.EntryParticipations.AnyAsync(
                    participation => participation.CustomerId == customerId)).ShouldBeFalse();
            });
        }

        [Fact]
        public async Task ExistingParticipation_DoesNotAllowSilentRecruiterReplacement()
        {
            await RegisterAndSignInCustomerAsync();
            var entry = await _participationService.StartEntryAsync(
                new StartEntryParticipationInput());

            var exception = await Should.ThrowAsync<UserFriendlyException>(() =>
                _participationService.StartEntryAsync(new StartEntryParticipationInput
                {
                    RecruiterCustomerId = 999999
                }));

            exception.Message.ShouldBe("Entry participation already exists.");
            exception.Details.ShouldContain("cannot be changed through the joining form");
            (await _participationService.GetMyParticipationsAsync()).Entry.Id.ShouldBe(entry.Id);
        }

        private async Task<int> RegisterAndSignInCustomerAsync()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userName = $"programme-{suffix}";
            var email = $"{userName}@example.com";
            using (UsingTenantId(1))
            {
                await Resolve<ISettingManager>().ChangeSettingForTenantAsync(
                    1,
                    "Abp.Account.IsSelfRegistrationEnabled",
                    "true");
                await _accountAppService.Register(new RegisterInput
                {
                    EmailAddress = email,
                    Name = "Programme",
                    Password = "Customer!101",
                    Surname = "Member",
                    UserName = userName
                });

                return await UsingDbContextAsync(1, async context =>
                {
                    var user = await context.Users.SingleAsync(item => item.UserName == userName);
                    var customer = await context.Customers.SingleAsync(
                        item => item.UserId == user.Id);
                    SetCurrentUser(user.Id, 1);
                    return customer.Id;
                });
            }
        }

        private async Task<int> CreateAwaitingEntryParticipantAsync()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userId = await CreateTestUserAsync(
                1,
                $"awaiting-recruiter-{suffix}",
                $"awaiting-recruiter-{suffix}@example.com");
            return await UsingDbContextAsync(1, async context =>
            {
                var customer = Customer.Create(
                    1,
                    userId,
                    "Awaiting Recruiter",
                    new EmailAddress($"awaiting-customer-{suffix}@example.com"));
                context.Customers.Add(customer);
                await context.SaveChangesAsync();

                context.EntryParticipations.Add(EntryParticipation.StartIndependently(
                    1,
                    customer.Id,
                    Resolve<ICurrentProgrammeTermsProvider>().GetEntryTerms(),
                    DateTime.UtcNow));
                await context.SaveChangesAsync();
                return customer.Id;
            });
        }
    }
}
