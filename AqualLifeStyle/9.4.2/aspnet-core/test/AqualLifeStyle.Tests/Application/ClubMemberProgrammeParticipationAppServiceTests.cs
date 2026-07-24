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
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.Domain.Memberships;
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
        public async Task StartingOnyxParticipation_ClearsLegacyDirectMembershipAssignment()
        {
            var customerId = await RegisterAndSignInCustomerAsync();
            var onyxMembershipId = await UsingDbContextAsync(1, async context =>
            {
                return await context.Memberships
                    .Where(membership => membership.MembershipType == MembershipType.Onyx)
                    .Select(membership => membership.Id)
                    .FirstAsync();
            });
            var customerRepository = Resolve<ICustomerRepository>();
            var customer = await customerRepository.GetAsync(customerId);
            customer.ChangeMembership(onyxMembershipId);
            await customerRepository.UpdateAsync(customer);

            var participation = await _participationService.StartDirectOnyxAsync(
                new StartDirectOnyxParticipationInput());

            participation.Status.ShouldBe("Awaiting full payment");
            await UsingDbContextAsync(1, async context =>
            {
                var customer = await context.Customers.SingleAsync(item => item.Id == customerId);
                customer.MembershipId.ShouldBeNull();
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

        [Fact]
        public async Task RecruiterParticipation_MustBelongToTheSameArea()
        {
            await RegisterAndSignInCustomerAsync();
            var recruiterCustomerId =
                await CreateActiveEntryParticipantAsync(2);

            var exception = await Should.ThrowAsync<UserFriendlyException>(() =>
                _participationService.StartEntryAsync(
                    new StartEntryParticipationInput
                    {
                        RecruiterCustomerId = recruiterCustomerId
                    }));

            exception.Message.ShouldBe("The recruiter could not be accepted.");
            exception.Details.ShouldContain(
                "not currently participating in Entry");
        }

        [Fact]
        public async Task OnyxRecruiterParticipation_MustBelongToTheSameArea()
        {
            await RegisterAndSignInCustomerAsync();
            var recruiterCustomerId =
                await CreateActiveOnyxParticipantAsync(2);

            var exception = await Should.ThrowAsync<UserFriendlyException>(() =>
                _participationService.StartDirectOnyxAsync(
                    new StartDirectOnyxParticipationInput
                    {
                        RecruiterCustomerId = recruiterCustomerId
                    }));

            exception.Message.ShouldBe("The recruiter could not be accepted.");
            exception.Details.ShouldContain(
                "not currently participating in Onyx");
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

        private async Task<int> CreateActiveEntryParticipantAsync(int tenantId)
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userId = await CreateTestUserAsync(
                tenantId,
                $"active-recruiter-{suffix}",
                $"active-recruiter-{suffix}@example.com");
            return await UsingDbContextAsync(tenantId, async context =>
            {
                var customer = Customer.Create(
                    tenantId,
                    userId,
                    "Active Recruiter",
                    new EmailAddress(
                        $"active-recruiter-customer-{suffix}@example.com"));
                context.Customers.Add(customer);
                await context.SaveChangesAsync();

                var participation = EntryParticipation.StartIndependently(
                    tenantId,
                    customer.Id,
                    Resolve<ICurrentProgrammeTermsProvider>().GetEntryTerms(),
                    DateTime.UtcNow.AddMinutes(-2));
                var registration = CreateConfirmedPayment(
                    tenantId,
                    customer.Id,
                    MemberPaymentPurpose.EntryRegistration,
                    $"cross-area-registration-{suffix}");
                participation.ApplyConfirmedActivationPayment(registration);
                var activation = CreateConfirmedPayment(
                    tenantId,
                    customer.Id,
                    MemberPaymentPurpose.EntryActivation,
                    $"cross-area-activation-{suffix}");
                participation.ApplyConfirmedActivationPayment(activation);
                context.MemberPayments.AddRange(registration, activation);
                context.EntryParticipations.Add(participation);
                await context.SaveChangesAsync();
                return customer.Id;
            });
        }

        private static MemberPayment CreateConfirmedPayment(
            int tenantId,
            int customerId,
            MemberPaymentPurpose purpose,
            string reference,
            decimal amount = 600m)
        {
            var payment = MemberPayment.CreatePending(
                tenantId,
                customerId,
                purpose,
                amount,
                "Test",
                reference,
                DateTime.UtcNow.AddMinutes(-1));
            payment.Confirm(DateTime.UtcNow);
            return payment;
        }

        private async Task<int> CreateActiveOnyxParticipantAsync(int tenantId)
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userId = await CreateTestUserAsync(
                tenantId,
                $"active-onyx-recruiter-{suffix}",
                $"active-onyx-recruiter-{suffix}@example.com");
            return await UsingDbContextAsync(tenantId, async context =>
            {
                var customer = Customer.Create(
                    tenantId,
                    userId,
                    "Active Onyx Recruiter",
                    new EmailAddress(
                        $"active-onyx-customer-{suffix}@example.com"));
                var membership = Membership.Create(
                    tenantId,
                    $"Onyx-{suffix}",
                    "Onyx cross-Area recruiter test",
                    MembershipType.Onyx);
                context.Customers.Add(customer);
                context.Memberships.Add(membership);
                await context.SaveChangesAsync();

                var participation = OnyxParticipation.StartDirectIndependently(
                    tenantId,
                    customer.Id,
                    membership.Id,
                    Resolve<ICurrentProgrammeTermsProvider>().GetDirectOnyxTerms(),
                    DateTime.UtcNow.AddMinutes(-1));
                var payment = CreateConfirmedPayment(
                    tenantId,
                    customer.Id,
                    MemberPaymentPurpose.OnyxDirectEntry,
                    $"cross-area-onyx-{suffix}",
                    6120m);
                participation.ApplyConfirmedDirectEntryPayment(payment);
                context.MemberPayments.Add(payment);
                context.OnyxParticipations.Add(participation);
                await context.SaveChangesAsync();
                return customer.Id;
            });
        }
    }
}
