using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Configuration;
using Abp.UI;
using AqualLifeStyle.Application.ProgrammeParticipations;
using AqualLifeStyle.Application.ProgrammeParticipations.Dto;
using AqualLifeStyle.Application.Recruitment;
using AqualLifeStyle.Application.Recruitment.Dto;
using AqualLifeStyle.Authorization.Accounts;
using AqualLifeStyle.Authorization.Accounts.Dto;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.Domain.Memberships;
using AqualLifeStyle.Domain.Recruitment;
using AqualLifeStyle.Payments;
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
        private readonly IProgrammeInvitationAppService _invitationService;

        public ClubMemberProgrammeParticipationAppServiceTests()
        {
            _accountAppService = Resolve<IAccountAppService>();
            _participationService = Resolve<IClubMemberProgrammeParticipationAppService>();
            _invitationService = Resolve<IProgrammeInvitationAppService>();
        }

        [Fact]
        public async Task ActiveParticipant_GetsOneStableSecureInvitationAndPublicPreview()
        {
            var customerId = await RegisterAndSignInCustomerAsync();
            await ActivateCurrentAQGreenParticipationAsync(customerId);

            var first = await _invitationService.GetMyInvitationsAsync();
            var repeated = await _invitationService.GetMyInvitationsAsync();
            var invitation = first.Invitations.Single();

            invitation.Code.Length.ShouldBe(ProgrammeInvitation.CodeLength);
            invitation.Code.All(character => "23456789ABCDEFGHJKLMNPQRSTUVWXYZ".Contains(character)).ShouldBeTrue();
            repeated.Invitations.Single().Code.ShouldBe(invitation.Code);
            invitation.ProgrammeName.ShouldBe("AQGreen");
            invitation.ClubMemberNumber.ShouldStartWith("CLB-");

            var preview = await _invitationService.GetPreviewAsync(
                new ProgrammeInvitationCodeInput { InviteCode = invitation.Code.ToLowerInvariant() });
            preview.RecruiterName.ShouldBe("Programme Member");
            preview.ProgrammeName.ShouldBe("AQGreen");
            preview.RecruiterEligible.ShouldBeTrue();
            preview.RecruiterClubMemberNumber.ShouldBe(invitation.ClubMemberNumber);
            preview.AreaName.ShouldBe("Default");

            await UsingDbContextAsync(1, async context =>
            {
                (await context.ProgrammeInvitations.CountAsync()).ShouldBe(1);
                var persisted = await context.ProgrammeInvitations.SingleAsync();
                persisted.ProgrammeParticipationId.ShouldNotBe(Guid.Empty);
                persisted.Code.ShouldNotContain(customerId.ToString());
            });
        }

        [Fact]
        public async Task InvitationJoining_IsIdempotentAndEnforcesProgrammeAndIdentityBoundaries()
        {
            var recruiterCustomerId = await RegisterAndSignInCustomerAsync();
            await ActivateCurrentAQGreenParticipationAsync(recruiterCustomerId);
            var inviteCode = (await _invitationService.GetMyInvitationsAsync())
                .Invitations.Single().Code;

            var inviteeCustomerId = await RegisterAndSignInCustomerAsync();
            var first = await _participationService.StartEntryAsync(
                new StartEntryParticipationInput { InviteCode = inviteCode });
            var repeated = await _participationService.StartEntryAsync(
                new StartEntryParticipationInput { InviteCode = inviteCode });
            repeated.Status.ShouldBe(first.Status);

            var programmeException = await Should.ThrowAsync<UserFriendlyException>(() =>
                _participationService.StartDirectOnyxAsync(
                    new StartDirectOnyxParticipationInput { InviteCode = inviteCode }));
            programmeException.Details.ShouldContain("different programme");

            await UsingDbContextAsync(1, async context =>
            {
                var participation = await context.EntryParticipations.SingleAsync(item =>
                    item.CustomerId == inviteeCustomerId);
                participation.RecruiterCustomerId.ShouldBe(recruiterCustomerId);
                (await context.OnyxParticipations.AnyAsync(item =>
                    item.CustomerId == inviteeCustomerId)).ShouldBeFalse();
                (await context.ProgrammeInvitations.AnyAsync(item =>
                    item.Code == inviteCode)).ShouldBeTrue();
            });

            var recruiterUserId = await UsingDbContextAsync(1, async context =>
                (await context.Customers.SingleAsync(item => item.Id == recruiterCustomerId)).UserId);
            SetCurrentUser(recruiterUserId, 1);
            var selfException = await Should.ThrowAsync<UserFriendlyException>(() =>
                _participationService.StartEntryAsync(
                    new StartEntryParticipationInput { InviteCode = inviteCode }));
            selfException.Details.ShouldContain("own invitation");
        }

        [Fact]
        public async Task OnyxInvitation_IsIdempotentAndCannotCreateAQGreenPlacement()
        {
            var recruiterCustomerId = await RegisterAndSignInCustomerAsync();
            await ActivateCurrentOnyxParticipationAsync(recruiterCustomerId);
            var inviteCode = (await _invitationService.GetMyInvitationsAsync())
                .Invitations.Single(item =>
                    item.ProgrammeKey == RecruitmentProgrammeKeys.Onyx).Code;

            var inviteeCustomerId = await RegisterAndSignInCustomerAsync();
            var mismatch = await Should.ThrowAsync<UserFriendlyException>(() =>
                _participationService.StartEntryAsync(
                    new StartEntryParticipationInput { InviteCode = inviteCode }));
            mismatch.Details.ShouldContain("different programme");

            await UsingDbContextAsync(1, async context =>
            {
                (await context.EntryParticipations.AnyAsync(item =>
                    item.CustomerId == inviteeCustomerId)).ShouldBeFalse();
                (await context.OnyxParticipations.AnyAsync(item =>
                    item.CustomerId == inviteeCustomerId)).ShouldBeFalse();
                (await context.ProgrammeInvitations.AnyAsync(item =>
                    item.Code == inviteCode)).ShouldBeTrue();
            });

            var first = await _participationService.StartDirectOnyxAsync(
                new StartDirectOnyxParticipationInput { InviteCode = inviteCode });
            var repeated = await _participationService.StartDirectOnyxAsync(
                new StartDirectOnyxParticipationInput { InviteCode = inviteCode });
            repeated.Status.ShouldBe(first.Status);

            await UsingDbContextAsync(1, async context =>
            {
                var participation = await context.OnyxParticipations.SingleAsync(item =>
                    item.CustomerId == inviteeCustomerId);
                participation.RecruiterCustomerId.ShouldBe(recruiterCustomerId);
                (await context.EntryParticipations.AnyAsync(item =>
                    item.CustomerId == inviteeCustomerId)).ShouldBeFalse();
            });
        }

        [Theory]
        [InlineData("JASPER")]
        [InlineData("BUSINESSPREMIER")]
        [InlineData("FUTURE-PROGRAMME")]
        public async Task UnsupportedInvitation_CannotCreateParticipationOrPlacement(
            string programmeKey)
        {
            var inviteCode = await UsingDbContextAsync(1, async context =>
            {
                var invitation = ProgrammeInvitation.Create(
                    1,
                    programmeKey,
                    Guid.NewGuid());
                context.ProgrammeInvitations.Add(invitation);
                await context.SaveChangesAsync();
                return invitation.Code;
            });
            var inviteeCustomerId = await RegisterAndSignInCustomerAsync();

            var aqGreenMismatch = await Should.ThrowAsync<UserFriendlyException>(() =>
                _participationService.StartEntryAsync(
                    new StartEntryParticipationInput { InviteCode = inviteCode }));
            aqGreenMismatch.Details.ShouldContain("different programme");
            var onyxMismatch = await Should.ThrowAsync<UserFriendlyException>(() =>
                _participationService.StartDirectOnyxAsync(
                    new StartDirectOnyxParticipationInput { InviteCode = inviteCode }));
            onyxMismatch.Details.ShouldContain("different programme");

            await UsingDbContextAsync(1, async context =>
            {
                (await context.EntryParticipations.AnyAsync(item =>
                    item.CustomerId == inviteeCustomerId)).ShouldBeFalse();
                (await context.OnyxParticipations.AnyAsync(item =>
                    item.CustomerId == inviteeCustomerId)).ShouldBeFalse();
                var invitation = await context.ProgrammeInvitations.SingleAsync(item =>
                    item.Code == inviteCode);
                invitation.ProgrammeKey.ShouldBe(programmeKey);
            });
        }

        [Fact]
        public async Task InvitationWithoutAssociatedParticipation_CannotMutateJoiningState()
        {
            var inviteCode = await UsingDbContextAsync(1, async context =>
            {
                var invitation = ProgrammeInvitation.Create(
                    1,
                    RecruitmentProgrammeKeys.AQGreen,
                    Guid.NewGuid());
                context.ProgrammeInvitations.Add(invitation);
                await context.SaveChangesAsync();
                return invitation.Code;
            });
            var inviteeCustomerId = await RegisterAndSignInCustomerAsync();

            var exception = await Should.ThrowAsync<UserFriendlyException>(() =>
                _participationService.StartEntryAsync(
                    new StartEntryParticipationInput { InviteCode = inviteCode }));
            exception.Details.ShouldContain("not currently eligible");

            await UsingDbContextAsync(1, async context =>
            {
                (await context.EntryParticipations.AnyAsync(item =>
                    item.CustomerId == inviteeCustomerId)).ShouldBeFalse();
                (await context.ProgrammeInvitations.AnyAsync(item =>
                    item.Code == inviteCode)).ShouldBeTrue();
            });
        }

        [Fact]
        public async Task MissingInvitation_CannotMutateJoiningState()
        {
            var inviteeCustomerId = await RegisterAndSignInCustomerAsync();

            var exception = await Should.ThrowAsync<UserFriendlyException>(() =>
                _participationService.StartEntryAsync(
                    new StartEntryParticipationInput
                    {
                        InviteCode = "ZZZZZZZZZZZZ"
                    }));
            exception.Details.ShouldContain("not found");

            await UsingDbContextAsync(1, async context =>
                (await context.EntryParticipations.AnyAsync(item =>
                    item.CustomerId == inviteeCustomerId)).ShouldBeFalse());
        }

        [Fact]
        public async Task IneligibleRecruiterInvitation_CanBePreviewedButCannotCreatePlacement()
        {
            var recruiterCustomerId = await CreateAwaitingEntryParticipantAsync();
            var inviteCode = await UsingDbContextAsync(1, async context =>
            {
                var participationId = await context.EntryParticipations
                    .Where(item => item.CustomerId == recruiterCustomerId)
                    .Select(item => item.Id)
                    .SingleAsync();
                var invitation = ProgrammeInvitation.Create(
                    1,
                    RecruitmentProgrammeKeys.AQGreen,
                    participationId);
                context.ProgrammeInvitations.Add(invitation);
                await context.SaveChangesAsync();
                return invitation.Code;
            });

            var preview = await _invitationService.GetPreviewAsync(
                new ProgrammeInvitationCodeInput { InviteCode = inviteCode });
            preview.RecruiterEligible.ShouldBeFalse();

            var inviteeCustomerId = await RegisterAndSignInCustomerAsync();
            var exception = await Should.ThrowAsync<UserFriendlyException>(() =>
                _participationService.StartEntryAsync(
                    new StartEntryParticipationInput { InviteCode = inviteCode }));
            exception.Details.ShouldContain("not currently eligible");

            await UsingDbContextAsync(1, async context =>
                (await context.EntryParticipations.AnyAsync(item =>
                    item.CustomerId == inviteeCustomerId)).ShouldBeFalse());
        }

        [Theory]
        [InlineData(MembershipType.Jasper)]
        [InlineData(MembershipType.BusinessPremier)]
        public void UnsupportedMembershipType_DoesNotInheritARecruitmentPolicy(
            MembershipType membershipType)
        {
            var resolver = Resolve<IProgrammeRecruitmentPolicyResolver>();

            resolver.GetAll().Select(policy => policy.ProgrammeKey)
                .ShouldBe(new[]
                {
                    RecruitmentProgrammeKeys.AQGreen,
                    RecruitmentProgrammeKeys.Onyx
                }, ignoreOrder: true);
            var exception = Should.Throw<UserFriendlyException>(() =>
                resolver.Resolve(membershipType.ToString()));

            exception.Details.ShouldBe(
                "Recruitment is not currently configured for this programme.");
        }

        [Fact]
        public async Task ClubMember_CanStartAQGreenAndDirectOnyxIndependently()
        {
            var customerId = await RegisterAndSignInCustomerAsync();

            var initial = await _participationService.GetMyParticipationsAsync();
            initial.ClubMemberNumber.ShouldStartWith("CLB-");
            initial.CanJoinEntry.ShouldBeTrue();
            initial.CanJoinOnyxDirectly.ShouldBeTrue();

            var entry = await _participationService.StartEntryAsync(
                new StartEntryParticipationInput());
            var repeatedEntry = await _participationService.StartEntryAsync(
                new StartEntryParticipationInput());
            var onyx = await _participationService.StartDirectOnyxAsync(
                new StartDirectOnyxParticipationInput());

            repeatedEntry.Status.ShouldBe(entry.Status);
            entry.ProgrammeName.ShouldBe("AQGreen");
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
        public async Task StartingAQGreenParticipation_ClearsLegacyDirectMembershipAssignment()
        {
            var customerId = await RegisterAndSignInCustomerAsync();
            var aqGreenMembershipId = await UsingDbContextAsync(1, async context =>
            {
                return await context.Memberships
                    .Where(membership => membership.MembershipType == MembershipType.AQGreen)
                    .Select(membership => membership.Id)
                    .FirstAsync();
            });
            var customerRepository = Resolve<ICustomerRepository>();
            var customer = await customerRepository.GetAsync(customerId);
            customer.ChangeMembership(aqGreenMembershipId);
            await customerRepository.UpdateAsync(customer);

            var participation = await _participationService.StartEntryAsync(
                new StartEntryParticipationInput());

            participation.ProgrammeName.ShouldBe("AQGreen");
            await UsingDbContextAsync(1, async context =>
            {
                var persistedCustomer = await context.Customers.SingleAsync(
                    item => item.Id == customerId);
                persistedCustomer.MembershipId.ShouldBeNull();
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
            exception.Details.ShouldContain("not currently participating in AQGreen");

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

            exception.Message.ShouldBe("AQGreen participation already exists.");
            exception.Details.ShouldContain("cannot be changed through the joining form");
            (await _participationService.GetMyParticipationsAsync()).Entry.Status.ShouldBe(entry.Status);
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
                "not currently participating in AQGreen");
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
                    ContactNumber = "+27 74 567 8901",
                    HomeAddress = "40 Programme Lane, Johannesburg",
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

        private async Task ActivateCurrentAQGreenParticipationAsync(int customerId)
        {
            await _participationService.StartEntryAsync(new StartEntryParticipationInput());
            var suffix = Guid.NewGuid().ToString("N");
            var processor = Resolve<ProgrammePaymentConfirmationProcessor>();
            await processor.ProcessAsync(CreateConfirmation(
                customerId,
                MemberPaymentPurpose.EntryRegistration,
                $"invite-registration-{suffix}"));
            await processor.ProcessAsync(CreateConfirmation(
                customerId,
                MemberPaymentPurpose.EntryActivation,
                $"invite-activation-{suffix}"));
        }

        private async Task ActivateCurrentOnyxParticipationAsync(int customerId)
        {
            await _participationService.StartDirectOnyxAsync(
                new StartDirectOnyxParticipationInput());
            await Resolve<ProgrammePaymentConfirmationProcessor>().ProcessAsync(
                CreateConfirmation(
                    customerId,
                    MemberPaymentPurpose.OnyxDirectEntry,
                    $"invite-onyx-{Guid.NewGuid():N}",
                    6120m));
        }

        private static ConfirmedProgrammePayment CreateConfirmation(
            int customerId,
            MemberPaymentPurpose purpose,
            string reference,
            decimal amount = 600m) =>
            new ConfirmedProgrammePayment(
                1,
                customerId,
                purpose,
                amount,
                "ZAR",
                "Test",
                reference,
                DateTime.UtcNow.AddMinutes(-1),
                DateTime.UtcNow);

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
