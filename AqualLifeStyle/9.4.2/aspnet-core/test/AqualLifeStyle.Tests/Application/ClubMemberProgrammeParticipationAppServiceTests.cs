using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Configuration;
using Abp.Domain.Uow;
using Abp.Runtime.Session;
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
                persisted.Code.ShouldBe(invitation.Code);
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
                _participationService.CreateDirectOnyxCheckoutAsync(
                    new CreateDirectOnyxCheckoutInput { InviteCode = inviteCode }));
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
        public async Task CrossTenantInvitation_CannotCreateAQGreenPlacement()
        {
            var recruiterCustomerId = await CreateActiveEntryParticipantAsync(2);
            var inviteCode = await UsingDbContextAsync(2, async context =>
            {
                var participationId = await context.EntryParticipations
                    .Where(item => item.CustomerId == recruiterCustomerId)
                    .Select(item => item.Id)
                    .SingleAsync();
                var invitation = ProgrammeInvitation.Create(
                    2,
                    RecruitmentProgrammeKeys.AQGreen,
                    participationId);
                context.ProgrammeInvitations.Add(invitation);
                await context.SaveChangesAsync();
                return invitation.Code;
            });
            var inviteeCustomerId = await RegisterAndSignInCustomerAsync();

            var exception = await Should.ThrowAsync<UserFriendlyException>(() =>
                _participationService.StartEntryAsync(
                    new StartEntryParticipationInput { InviteCode = inviteCode }));

            exception.Details.ShouldContain("different organisation");
            await UsingDbContextAsync(1, async context =>
                (await context.EntryParticipations.AnyAsync(item =>
                    item.CustomerId == inviteeCustomerId)).ShouldBeFalse());
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

            var first = await _participationService.CreateDirectOnyxCheckoutAsync(
                new CreateDirectOnyxCheckoutInput { InviteCode = inviteCode });
            var repeated = await _participationService.CreateDirectOnyxCheckoutAsync(
                new CreateDirectOnyxCheckoutInput { InviteCode = inviteCode });
            repeated.CheckoutUrl.ShouldBe(first.CheckoutUrl);

            await UsingDbContextAsync(1, async context =>
            {
                (await context.OnyxParticipations.AnyAsync(item =>
                    item.CustomerId == inviteeCustomerId)).ShouldBeFalse();
                var checkout = await context.DirectOnyxCheckoutIntents.SingleAsync(item =>
                    item.CustomerId == inviteeCustomerId);
                checkout.RecruiterCustomerId.ShouldBe(recruiterCustomerId);
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
                _participationService.CreateDirectOnyxCheckoutAsync(
                    new CreateDirectOnyxCheckoutInput { InviteCode = inviteCode }));
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
                "Member invitations are not currently configured for this programme.");
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
            var onyxCheckout = await _participationService.CreateDirectOnyxCheckoutAsync(
                new CreateDirectOnyxCheckoutInput());

            repeatedEntry.Status.ShouldBe(entry.Status);
            entry.ProgrammeName.ShouldBe("AQGreen");
            entry.Status.ShouldBe("Awaiting joining payment");
            entry.JoinedIndependently.ShouldBeTrue();
            entry.NextPaymentAmount.ShouldBe(1200m);
            entry.NextPaymentDescription.ShouldBe("Full AQGreen joining payment");
            entry.CanRecruitForThisProgramme.ShouldBeFalse();

            onyxCheckout.Amount.ShouldBe(6120m);
            onyxCheckout.Currency.ShouldBe("ZAR");
            onyxCheckout.CheckoutUrl.ShouldStartWith("https://payments.example.test/");

            await UsingDbContextAsync(1, async context =>
            {
                (await context.EntryParticipations.CountAsync(
                    participation => participation.CustomerId == customerId)).ShouldBe(1);
                (await context.OnyxParticipations.CountAsync(
                    participation => participation.CustomerId == customerId)).ShouldBe(0);
                (await context.DirectOnyxCheckoutIntents.CountAsync(
                    intent => intent.CustomerId == customerId)).ShouldBe(1);
            });
        }

        [Fact]
        public async Task DirectOnyxCheckout_CreatesParticipationOnlyAfterVerifiedPayment()
        {
            var customerId = await RegisterAndSignInCustomerAsync();
            var checkout = await _participationService.CreateDirectOnyxCheckoutAsync(
                new CreateDirectOnyxCheckoutInput());
            checkout.Amount.ShouldBe(6120m);

            var intentId = await UsingDbContextAsync(1, async context =>
            {
                (await context.OnyxParticipations.AnyAsync(item =>
                    item.CustomerId == customerId)).ShouldBeFalse();
                (await context.MemberPayments.AnyAsync(item =>
                    item.CustomerId == customerId &&
                    item.Purpose == MemberPaymentPurpose.OnyxDirectEntry)).ShouldBeFalse();
                var intent = await context.DirectOnyxCheckoutIntents.SingleAsync(item =>
                    item.CustomerId == customerId);
                intent.Status.ShouldBe(HostedPaymentCheckoutStatus.AwaitingPayment);
                return intent.Id;
            });

            var processor = Resolve<ProgrammePaymentConfirmationProcessor>();
            await Should.ThrowAsync<InvalidOperationException>(() =>
                processor.ProcessDirectOnyxCheckoutAsync(
                    intentId,
                    "Yoco",
                    $"wrong-amount-{Guid.NewGuid():N}",
                    $"checkout_{intentId:N}",
                    6119m,
                    "ZAR",
                    DateTime.UtcNow));

            await UsingDbContextAsync(1, async context =>
            {
                (await context.OnyxParticipations.AnyAsync(item =>
                    item.CustomerId == customerId)).ShouldBeFalse();
                (await context.MemberPayments.AnyAsync(item =>
                    item.CustomerId == customerId &&
                    item.Purpose == MemberPaymentPurpose.OnyxDirectEntry)).ShouldBeFalse();
            });

            var paymentReference = $"yoco-payment-{Guid.NewGuid():N}";
            var first = await processor.ProcessDirectOnyxCheckoutAsync(
                intentId,
                "Yoco",
                paymentReference,
                $"checkout_{intentId:N}",
                6120m,
                "ZAR",
                DateTime.UtcNow);
            var repeated = await processor.ProcessDirectOnyxCheckoutAsync(
                intentId,
                "Yoco",
                paymentReference,
                $"checkout_{intentId:N}",
                6120m,
                "ZAR",
                DateTime.UtcNow);
            repeated.WasAlreadyProcessed.ShouldBeTrue();
            repeated.PaymentId.ShouldBe(first.PaymentId);
            repeated.ParticipationId.ShouldBe(first.ParticipationId);

            await UsingDbContextAsync(1, async context =>
            {
                var participation = await context.OnyxParticipations.SingleAsync(item =>
                    item.CustomerId == customerId);
                participation.Status.ShouldBe(OnyxParticipationStatus.PaymentConfirmedAwaitingApproval);
                participation.RecruiterCustomerId.ShouldBeNull();
                participation.DirectEntryPaymentId.ShouldBe(first.PaymentId);
                var intent = await context.DirectOnyxCheckoutIntents.SingleAsync(item =>
                    item.Id == intentId);
                intent.Status.ShouldBe(HostedPaymentCheckoutStatus.Completed);
                intent.ParticipationId.ShouldBe(participation.Id);
                (await context.MemberPayments.CountAsync(item =>
                    item.CustomerId == customerId &&
                    item.Purpose == MemberPaymentPurpose.OnyxDirectEntry)).ShouldBe(1);
            });
        }

        [Fact]
        public async Task AQGreenCheckout_AwaitsApprovalAfterOneVerifiedTwelveHundredRandPayment()
        {
            var customerId = await RegisterAndSignInCustomerAsync();
            await _participationService.StartEntryAsync(
                new StartEntryParticipationInput());
            var checkoutResult = await _participationService
                .CreateAQGreenJoiningCheckoutAsync(new CreateAQGreenJoiningCheckoutInput
                {
                    Schedule = AQGreenJoiningPaymentSchedule.Full
                });
            var repeatedCheckout = await _participationService
                .CreateAQGreenJoiningCheckoutAsync(new CreateAQGreenJoiningCheckoutInput
                {
                    Schedule = AQGreenJoiningPaymentSchedule.Full
                });

            repeatedCheckout.CheckoutUrl.ShouldBe(checkoutResult.CheckoutUrl);
            checkoutResult.Amount.ShouldBe(1200m);
            checkoutResult.Currency.ShouldBe("ZAR");

            var checkout = await UsingDbContextAsync(1, async context =>
            {
                var persisted = await context.AQGreenJoiningCheckouts.SingleAsync(
                    item => item.CustomerId == customerId);
                persisted.Status.ShouldBe(HostedPaymentCheckoutStatus.AwaitingPayment);
                (await context.MemberPayments.AnyAsync(item =>
                    item.CustomerId == customerId &&
                    item.Purpose == MemberPaymentPurpose.AQGreenJoining)).ShouldBeFalse();
                (await context.EntryParticipations.SingleAsync(item =>
                    item.CustomerId == customerId)).Status.ShouldBe(
                    EntryParticipationStatus.AwaitingJoiningPayment);
                return persisted;
            });

            var processor = Resolve<ProgrammePaymentConfirmationProcessor>();
            await Should.ThrowAsync<InvalidOperationException>(() =>
                processor.ProcessAQGreenJoiningCheckoutAsync(
                    checkout.Id,
                    "Yoco",
                    $"wrong-checkout-{Guid.NewGuid():N}",
                    "checkout_that_does_not_match",
                    1200m,
                    "ZAR",
                    DateTime.UtcNow));
            await Should.ThrowAsync<InvalidOperationException>(() =>
                processor.ProcessAQGreenJoiningCheckoutAsync(
                    checkout.Id,
                    "Yoco",
                    $"wrong-amount-{Guid.NewGuid():N}",
                    checkout.ProviderCheckoutId,
                    600m,
                    "ZAR",
                    DateTime.UtcNow));

            await UsingDbContextAsync(1, async context =>
            {
                (await context.MemberPayments.AnyAsync(item =>
                    item.CustomerId == customerId &&
                    item.Purpose == MemberPaymentPurpose.AQGreenJoining)).ShouldBeFalse();
                (await context.EntryParticipations.SingleAsync(item =>
                    item.CustomerId == customerId)).Status.ShouldBe(
                    EntryParticipationStatus.AwaitingJoiningPayment);
            });

            var paymentReference = $"aqgreen-payment-{Guid.NewGuid():N}";
            var first = await processor.ProcessAQGreenJoiningCheckoutAsync(
                checkout.Id,
                "Yoco",
                paymentReference,
                checkout.ProviderCheckoutId,
                1200m,
                "ZAR",
                DateTime.UtcNow);
            var repeated = await processor.ProcessAQGreenJoiningCheckoutAsync(
                checkout.Id,
                "Yoco",
                paymentReference,
                checkout.ProviderCheckoutId,
                1200m,
                "ZAR",
                DateTime.UtcNow);

            repeated.WasAlreadyProcessed.ShouldBeTrue();
            repeated.PaymentId.ShouldBe(first.PaymentId);
            await UsingDbContextAsync(1, async context =>
            {
                var participation = await context.EntryParticipations.SingleAsync(
                    item => item.CustomerId == customerId);
                participation.Status.ShouldBe(EntryParticipationStatus.PaymentConfirmedAwaitingApproval);
                participation.JoiningPaymentId.ShouldBe(first.PaymentId);
                var persistedCheckout = await context.AQGreenJoiningCheckouts.SingleAsync(
                    item => item.Id == checkout.Id);
                persistedCheckout.Status.ShouldBe(HostedPaymentCheckoutStatus.Completed);
                persistedCheckout.PaymentId.ShouldBe(first.PaymentId);
                (await context.MemberPayments.CountAsync(item =>
                    item.CustomerId == customerId &&
                    item.Purpose == MemberPaymentPurpose.AQGreenJoining)).ShouldBe(1);
            });
        }

        [Fact]
        public async Task AQGreenCheckout_PreparingRequestCannotCreateASecondProviderCheckout()
        {
            var customerId = await RegisterAndSignInCustomerAsync();
            await _participationService.StartEntryAsync(
                new StartEntryParticipationInput());
            await UsingDbContextAsync(1, async context =>
            {
                var participation = await context.EntryParticipations.SingleAsync(item =>
                    item.CustomerId == customerId);
                participation.SelectJoiningPaymentSchedule(
                    AQGreenJoiningPaymentSchedule.Full);
                context.AQGreenJoiningCheckouts.Add(AQGreenJoiningCheckout.Create(
                    1,
                    participation.Id,
                    customerId,
                    AQGreenJoiningPaymentSchedule.Full,
                    AQGreenJoiningPaymentStage.Full,
                    1200m,
                    "ZAR",
                    DateTime.UtcNow));
                await context.SaveChangesAsync();
            });

            var exception = await Should.ThrowAsync<UserFriendlyException>(() =>
                _participationService.CreateAQGreenJoiningCheckoutAsync(
                    new CreateAQGreenJoiningCheckoutInput
                    {
                        Schedule = AQGreenJoiningPaymentSchedule.Full
                    }));

            exception.Message.ShouldContain("still being prepared");
            await UsingDbContextAsync(1, async context =>
                (await context.AQGreenJoiningCheckouts.CountAsync(item =>
                    item.CustomerId == customerId)).ShouldBe(1));
        }

        [Fact]
        public async Task RejectedParticipation_ReturnsTheRecordedDecisionReasonToTheCustomer()
        {
            var customerId = await RegisterAndSignInCustomerAsync();
            const string reason = "Identity evidence requires correction before activation.";
            var decidedAt = DateTime.UtcNow;

            await UsingDbContextAsync(1, async context =>
            {
                var participation = EntryParticipation.StartIndependently(
                    1,
                    customerId,
                    Resolve<ICurrentProgrammeTermsProvider>().GetEntryTerms(),
                    decidedAt.AddMinutes(-2));
                var payment = CreateConfirmedPayment(
                    1,
                    customerId,
                    MemberPaymentPurpose.AQGreenJoining,
                    $"rejected-customer-state-{Guid.NewGuid():N}",
                    1200m);
                participation.ApplyConfirmedJoiningPayment(payment);
                participation.RejectByAdministrator(1L, reason, decidedAt);
                context.MemberPayments.Add(payment);
                context.EntryParticipations.Add(participation);
                await context.SaveChangesAsync();
            });

            var result = await _participationService.GetMyParticipationsAsync();

            result.Entry.Status.ShouldBe("Declined");
            result.Entry.DecisionReason.ShouldBe(reason);
            result.Entry.DecidedAt.ShouldBe(decidedAt);
        }

        [Fact]
        public async Task DirectOnyxCheckout_PreparingRequestCannotCreateASecondProviderCheckout()
        {
            var customerId = await RegisterAndSignInCustomerAsync();
            await UsingDbContextAsync(1, async context =>
            {
                var membership = await context.Memberships.SingleAsync(item =>
                    item.MembershipType == MembershipType.Onyx);
                context.DirectOnyxCheckoutIntents.Add(DirectOnyxCheckoutIntent.Create(
                    1,
                    customerId,
                    null,
                    null,
                    membership.Id,
                    OnyxPlanTerms.Create(
                        "preparing-test",
                        new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                        6120m),
                    DateTime.UtcNow));
                await context.SaveChangesAsync();
            });

            var exception = await Should.ThrowAsync<UserFriendlyException>(() =>
                _participationService.CreateDirectOnyxCheckoutAsync(
                    new CreateDirectOnyxCheckoutInput()));

            exception.Message.ShouldContain("still being prepared");
            await UsingDbContextAsync(1, async context =>
                (await context.DirectOnyxCheckoutIntents.CountAsync(item =>
                    item.CustomerId == customerId)).ShouldBe(1));
        }

        [Fact]
        public async Task DirectOnyxCheckout_AuthoritativeFailureAllowsOneNewCheckout()
        {
            var customerId = await RegisterAndSignInCustomerAsync();
            await _participationService.CreateDirectOnyxCheckoutAsync(
                new CreateDirectOnyxCheckoutInput());

            var failedCheckoutId = await UsingDbContextAsync(1, async context =>
            {
                var checkout = await context.DirectOnyxCheckoutIntents.SingleAsync(item =>
                    item.CustomerId == customerId);
                checkout.RecordProviderFailure(
                    DateTime.UtcNow,
                    "Authoritative provider failure for retry test");
                await context.SaveChangesAsync();
                return checkout.Id;
            });

            await _participationService.CreateDirectOnyxCheckoutAsync(
                new CreateDirectOnyxCheckoutInput());

            await UsingDbContextAsync(1, async context =>
            {
                var attempts = await context.DirectOnyxCheckoutIntents
                    .Where(item => item.CustomerId == customerId)
                    .ToListAsync();
                attempts.Count.ShouldBe(2);
                attempts.Single(item => item.Id == failedCheckoutId).Status.ShouldBe(
                    HostedPaymentCheckoutStatus.Failed);
                attempts.Count(item =>
                    item.Status == HostedPaymentCheckoutStatus.AwaitingPayment).ShouldBe(1);
                (await context.OnyxParticipations.AnyAsync(item =>
                    item.CustomerId == customerId)).ShouldBeFalse();
                (await context.MemberPayments.AnyAsync(item =>
                    item.CustomerId == customerId)).ShouldBeFalse();
            });
        }

        [Fact]
        public async Task AQGreenCheckout_AllowsTwoInstallmentsForANewParticipation()
        {
            var customerId = await RegisterAndSignInCustomerAsync();
            await _participationService.StartEntryAsync(
                new StartEntryParticipationInput());
            var checkout = await _participationService.CreateAQGreenJoiningCheckoutAsync(
                new CreateAQGreenJoiningCheckoutInput
                {
                    Schedule = AQGreenJoiningPaymentSchedule.TwoInstallments
                });

            checkout.Amount.ShouldBe(600m);

            await UsingDbContextAsync(1, async context =>
            {
                var participation = await context.EntryParticipations.SingleAsync(
                    item => item.CustomerId == customerId);
                participation.Status.ShouldBe(
                    EntryParticipationStatus.AwaitingJoiningPayment);
                participation.JoiningPaymentSchedule.ShouldBe(
                    AQGreenJoiningPaymentSchedule.TwoInstallments);
                participation.JoiningInstallmentAmount.ShouldBe(600m);

                var persistedCheckout = await context.AQGreenJoiningCheckouts.SingleAsync(
                    item => item.CustomerId == customerId);
                persistedCheckout.Schedule.ShouldBe(
                    AQGreenJoiningPaymentSchedule.TwoInstallments);
                persistedCheckout.Stage.ShouldBe(
                    AQGreenJoiningPaymentStage.FirstInstallment);
                persistedCheckout.Amount.ShouldBe(600m);
                (await context.MemberPayments.AnyAsync(item =>
                    item.CustomerId == customerId)).ShouldBeFalse();
            });
        }

        [Fact]
        public async Task AQGreenCheckout_AllowsAPreviouslyVerifiedInstallmentToBeCompleted()
        {
            var customerId = await RegisterAndSignInCustomerAsync();
            await UsingDbContextAsync(1, async context =>
            {
                var startedAt = DateTime.UtcNow;
                var participation = EntryParticipation.StartIndependently(
                    1,
                    customerId,
                    EntryProgrammeTerms.CreateFlexibleJoiningPayment(
                        "historical-flexible-1200",
                        new DateTime(2026, 7, 26, 0, 0, 0, DateTimeKind.Utc),
                        1200m,
                        600m,
                        600m,
                        7),
                    startedAt);
                participation.SelectJoiningPaymentSchedule(
                    AQGreenJoiningPaymentSchedule.TwoInstallments);
                var firstPayment = MemberPayment.CreatePending(
                    1,
                    customerId,
                    MemberPaymentPurpose.AQGreenJoining,
                    600m,
                    "Yoco",
                    $"historical-first-{Guid.NewGuid():N}",
                    startedAt,
                    "ZAR");
                firstPayment.Confirm(startedAt.AddMinutes(1));
                participation.ApplyConfirmedJoiningPayment(
                    firstPayment,
                    AQGreenJoiningPaymentStage.FirstInstallment);
                context.MemberPayments.Add(firstPayment);
                context.EntryParticipations.Add(participation);
                await context.SaveChangesAsync();
            });

            var checkout = await _participationService
                .CreateAQGreenJoiningCheckoutAsync(
                    new CreateAQGreenJoiningCheckoutInput
                    {
                        Schedule = AQGreenJoiningPaymentSchedule.TwoInstallments
                    });

            checkout.Amount.ShouldBe(600m);
            await UsingDbContextAsync(1, async context =>
            {
                var persisted = await context.AQGreenJoiningCheckouts.SingleAsync(
                    item => item.CustomerId == customerId);
                persisted.Schedule.ShouldBe(
                    AQGreenJoiningPaymentSchedule.TwoInstallments);
                persisted.Stage.ShouldBe(
                    AQGreenJoiningPaymentStage.SecondInstallment);
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

            await _participationService.CreateDirectOnyxCheckoutAsync(
                new CreateDirectOnyxCheckoutInput());

            await UsingDbContextAsync(1, async context =>
            {
                var customer = await context.Customers.SingleAsync(item => item.Id == customerId);
                customer.MembershipId.ShouldBe(onyxMembershipId);
                (await context.OnyxParticipations.AnyAsync(item =>
                    item.CustomerId == customerId)).ShouldBeFalse();
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

            exception.Message.ShouldBe("The network placement could not be accepted.");
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
        public async Task AQGreenRecruitment_RejectsCrossTenantRecruiter()
        {
            var customerId = await RegisterAndSignInCustomerAsync();
            var recruiterCustomerId =
                await CreateActiveEntryParticipantAsync(2);

            var exception = await Should.ThrowAsync<UserFriendlyException>(() =>
                _participationService.StartEntryAsync(
                new StartEntryParticipationInput
                {
                    RecruiterCustomerId = recruiterCustomerId
                }));

            exception.Details.ShouldContain("not currently participating in AQGreen");
            await UsingDbContextAsync(1, async context =>
                (await context.EntryParticipations.AnyAsync(item =>
                    item.CustomerId == customerId)).ShouldBeFalse());
        }

        [Fact]
        public async Task OnyxRecruitment_RejectsCrossTenantRecruiter()
        {
            var customerId = await RegisterAndSignInCustomerAsync();
            var recruiterCustomerId =
                await CreateActiveOnyxParticipantAsync(2);

            var exception = await Should.ThrowAsync<UserFriendlyException>(() =>
                _participationService.CreateDirectOnyxCheckoutAsync(
                new CreateDirectOnyxCheckoutInput
                {
                    RecruiterCustomerId = recruiterCustomerId
                }));

            exception.Details.ShouldContain("not currently participating in Onyx");
            await UsingDbContextAsync(1, async context =>
            {
                (await context.DirectOnyxCheckoutIntents.AnyAsync(item =>
                    item.CustomerId == customerId)).ShouldBeFalse();
                (await context.OnyxParticipations.AnyAsync(item =>
                    item.CustomerId == customerId)).ShouldBeFalse();
            });
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
            await _participationService.CreateAQGreenJoiningCheckoutAsync(
                new CreateAQGreenJoiningCheckoutInput
                {
                    Schedule = AQGreenJoiningPaymentSchedule.Full
                });
            var checkout = await UsingDbContextAsync(1, context =>
                context.AQGreenJoiningCheckouts.SingleAsync(
                    item => item.CustomerId == customerId));
            await Resolve<ProgrammePaymentConfirmationProcessor>()
                .ProcessAQGreenJoiningCheckoutAsync(
                    checkout.Id,
                    "Test",
                    $"invite-aqgreen-{Guid.NewGuid():N}",
                    checkout.ProviderCheckoutId,
                    1200m,
                    "ZAR",
                    DateTime.UtcNow);
            await ApproveAndPromoteAsync(customerId, onyx: false);
        }

        private async Task ActivateCurrentOnyxParticipationAsync(int customerId)
        {
            await _participationService.CreateDirectOnyxCheckoutAsync(
                new CreateDirectOnyxCheckoutInput());
            var intent = await UsingDbContextAsync(1, context => context.DirectOnyxCheckoutIntents
                .SingleAsync(item => item.CustomerId == customerId));
            await Resolve<ProgrammePaymentConfirmationProcessor>().ProcessDirectOnyxCheckoutAsync(
                intent.Id,
                "Test",
                $"invite-onyx-{Guid.NewGuid():N}",
                intent.ProviderCheckoutId,
                6120m,
                "ZAR",
                DateTime.UtcNow);
            await ApproveAndPromoteAsync(customerId, onyx: true);
        }

        private async Task ApproveAndPromoteAsync(int customerId, bool onyx)
        {
            using var uow = LocalIocManager.Resolve<IUnitOfWorkManager>().Begin(
                new UnitOfWorkOptions { IsTransactional = true });
            using (LocalIocManager.Resolve<IUnitOfWorkManager>().Current.SetTenantId(1))
            {
                await UsingDbContextAsync(1, async context =>
                {
                    if (onyx)
                    {
                        var participation = await context.OnyxParticipations
                            .SingleAsync(item => item.CustomerId == customerId);
                        participation.ApproveByAdministrator(
                            AbpSession.GetUserId(),
                            DateTime.UtcNow);
                    }
                    else
                    {
                        var participation = await context.EntryParticipations
                            .SingleAsync(item => item.CustomerId == customerId);
                        participation.ApproveByAdministrator(
                            AbpSession.GetUserId(),
                            DateTime.UtcNow);
                    }
                });
                var customer = await UsingDbContextAsync(1, async context =>
                    await context.Customers.SingleAsync(item => item.Id == customerId));
                await Resolve<ActiveProgrammeParticipantRoleSynchronizer>()
                    .PromoteGuestToMemberAsync(customer.Id);
                await uow.CompleteAsync();
            }
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
                var joiningPayment = CreateConfirmedPayment(
                    tenantId,
                    customer.Id,
                    MemberPaymentPurpose.AQGreenJoining,
                    $"cross-tenant-joining-{suffix}",
                    1200m);
                participation.ApplyConfirmedJoiningPayment(joiningPayment);
                participation.ApproveByAdministrator(1L, DateTime.UtcNow);
                context.MemberPayments.Add(joiningPayment);
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
                    "Onyx cross-Tenant recruiter test",
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
                    $"cross-tenant-onyx-{suffix}",
                    6120m);
                participation.ApplyConfirmedDirectEntryPayment(payment);
                participation.ApproveByAdministrator(1L, DateTime.UtcNow);
                context.MemberPayments.Add(payment);
                context.OnyxParticipations.Add(participation);
                await context.SaveChangesAsync();
                return customer.Id;
            });
        }
    }
}
