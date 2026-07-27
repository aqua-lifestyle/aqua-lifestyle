using System;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using Abp.Auditing;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.Runtime.Session;
using Abp.UI;
using AqualLifeStyle.Application.ProgrammeParticipations.Dto;
using AqualLifeStyle.Application.Recruitment;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.Payments.Yoco;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AqualLifeStyle.Application.ProgrammeParticipations
{
    [AbpAuthorize]
    [Audited]
    public class ClubMemberProgrammeParticipationAppService
        : AqualLifeStyleAppServiceBase, IClubMemberProgrammeParticipationAppService
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly IMembershipRepository _membershipRepository;
        private readonly IRepository<EntryParticipation, Guid> _entryParticipationRepository;
        private readonly IRepository<OnyxParticipation, Guid> _onyxParticipationRepository;
        private readonly IRepository<DirectOnyxCheckoutIntent, Guid>
            _directOnyxCheckoutIntentRepository;
        private readonly IRepository<AQGreenJoiningCheckout, Guid>
            _aqGreenJoiningCheckoutRepository;
        private readonly IRepository<OnyxTravelBenefitEntitlement, Guid>
            _travelBenefitRepository;
        private readonly ICurrentProgrammeTermsProvider _termsProvider;
        private readonly IProgrammeInvitationResolver _invitationResolver;
        private readonly IYocoCheckoutGateway _yocoCheckoutGateway;
        private readonly IUnitOfWorkManager _unitOfWorkManager;
        private readonly IConfiguration _configuration;

        protected virtual DateTime UtcNow => DateTime.UtcNow;

        public ClubMemberProgrammeParticipationAppService(
            ICustomerRepository customerRepository,
            IMembershipRepository membershipRepository,
            IRepository<EntryParticipation, Guid> entryParticipationRepository,
            IRepository<OnyxParticipation, Guid> onyxParticipationRepository,
            IRepository<DirectOnyxCheckoutIntent, Guid> directOnyxCheckoutIntentRepository,
            IRepository<AQGreenJoiningCheckout, Guid> aqGreenJoiningCheckoutRepository,
            IRepository<OnyxTravelBenefitEntitlement, Guid> travelBenefitRepository,
            ICurrentProgrammeTermsProvider termsProvider,
            IProgrammeInvitationResolver invitationResolver,
            IYocoCheckoutGateway yocoCheckoutGateway,
            IUnitOfWorkManager unitOfWorkManager,
            IConfiguration configuration)
        {
            _customerRepository = customerRepository;
            _membershipRepository = membershipRepository;
            _entryParticipationRepository = entryParticipationRepository;
            _onyxParticipationRepository = onyxParticipationRepository;
            _directOnyxCheckoutIntentRepository = directOnyxCheckoutIntentRepository;
            _aqGreenJoiningCheckoutRepository = aqGreenJoiningCheckoutRepository;
            _travelBenefitRepository = travelBenefitRepository;
            _termsProvider = termsProvider;
            _invitationResolver = invitationResolver;
            _yocoCheckoutGateway = yocoCheckoutGateway;
            _unitOfWorkManager = unitOfWorkManager;
            _configuration = configuration;
        }

        [AbpAuthorize(AquaPermissions.ProgrammeParticipations.ViewSelf)]
        public async Task<MyProgrammeParticipationsDto> GetMyParticipationsAsync()
        {
            var customer = await GetCurrentActiveCustomerAsync();
            var entry = await _entryParticipationRepository.FirstOrDefaultAsync(
                participation => participation.CustomerId == customer.Id);
            var onyx = await _onyxParticipationRepository.FirstOrDefaultAsync(
                participation => participation.CustomerId == customer.Id);
            var directOnyxCheckout = await _directOnyxCheckoutIntentRepository.FirstOrDefaultAsync(
                intent =>
                    intent.CustomerId == customer.Id &&
                    intent.Status != HostedPaymentCheckoutStatus.Completed);
            AQGreenJoiningCheckout aqGreenCheckout = null;
            if (entry != null)
            {
                aqGreenCheckout = await _aqGreenJoiningCheckoutRepository.FirstOrDefaultAsync(
                    checkout =>
                        checkout.ParticipationId == entry.Id &&
                        checkout.Status != HostedPaymentCheckoutStatus.Completed);
            }
            var travelBenefit =
                await _travelBenefitRepository.FirstOrDefaultAsync(
                    entitlement => entitlement.CustomerId == customer.Id);

            return new MyProgrammeParticipationsDto
            {
                ClubMemberNumber = customer.ClubMemberNumber,
                Entry = entry == null
                    ? null
                    : Map(entry, await GetClubMemberNumberAsync(entry.RecruiterCustomerId)),
                Onyx = onyx == null
                    ? null
                    : Map(onyx, await GetClubMemberNumberAsync(onyx.RecruiterCustomerId)),
                PendingAQGreenCheckout = MapPendingCheckout(aqGreenCheckout),
                PendingDirectOnyxCheckout = directOnyxCheckout == null ||
                    string.IsNullOrWhiteSpace(directOnyxCheckout.CheckoutUrl)
                    ? null
                    : new PendingProgrammeCheckoutDto
                    {
                        Amount = directOnyxCheckout.Amount,
                        Currency = directOnyxCheckout.Currency,
                        CheckoutUrl = directOnyxCheckout.CheckoutUrl,
                        Status = "Awaiting payment"
                    },
                TravelBenefit = travelBenefit == null
                    ? null
                    : Map(travelBenefit)
            };
        }

        private static PendingProgrammeCheckoutDto MapPendingCheckout(
            HostedPaymentCheckout checkout) =>
            checkout == null || string.IsNullOrWhiteSpace(checkout.CheckoutUrl)
                ? null
                : new PendingProgrammeCheckoutDto
                {
                    Amount = checkout.Amount,
                    Currency = checkout.Currency,
                    CheckoutUrl = checkout.CheckoutUrl,
                    Status = "Awaiting payment"
                };

        private static OnyxTravelBenefitDto Map(
            OnyxTravelBenefitEntitlement entitlement) =>
            new OnyxTravelBenefitDto
            {
                Status = entitlement.Status == OnyxTravelBenefitStatus.Active
                    ? "Available"
                    : "Waiting period",
                EligibleAt = entitlement.EligibleAt,
                WaitingPeriodEndsAt = entitlement.WaitingPeriodEndsAt,
                ActivatedAt = entitlement.ActivatedAt,
                MemberTripContributionPercent =
                    entitlement.MemberTripContributionPercent
            };

        [AbpAuthorize(AquaPermissions.ProgrammeParticipations.Join)]
        [UnitOfWork]
        public async Task<ProgrammeParticipationDto> StartEntryAsync(
            StartEntryParticipationInput input)
        {
            input ??= new StartEntryParticipationInput();
            var customer = await GetCurrentActiveCustomerAsync();
            var recruiterCustomerId = await ResolveRequestedRecruiterAsync(
                input.RecruiterCustomerId,
                input.InviteCode,
                RecruitmentProgrammeKeys.AQGreen,
                customer);
            await ClearLegacyProgrammeMembershipAssignmentAsync(
                customer,
                MembershipType.AQGreen,
                "AQGreen");
            var existing = await _entryParticipationRepository.FirstOrDefaultAsync(
                participation => participation.CustomerId == customer.Id);
            if (existing != null)
            {
                EnsureSameRecruiter(existing.RecruiterCustomerId, recruiterCustomerId, "AQGreen");
                return Map(existing, await GetClubMemberNumberAsync(existing.RecruiterCustomerId));
            }

            EntryParticipation participation;
            if (recruiterCustomerId.HasValue)
            {
                var recruiter = await GetActiveEntryRecruiterAsync(
                    customer.Id,
                    recruiterCustomerId.Value);
                participation = EntryParticipation.StartUnderRecruiter(
                    customer.TenantId.Value,
                    customer.Id,
                    recruiter,
                    _termsProvider.GetEntryTerms(),
                    UtcNow);
            }
            else
            {
                participation = EntryParticipation.StartIndependently(
                    customer.TenantId.Value,
                    customer.Id,
                    _termsProvider.GetEntryTerms(),
                    UtcNow);
            }

            await _entryParticipationRepository.InsertAsync(participation);
            await CurrentUnitOfWork.SaveChangesAsync();
            Logger.Info(
                $"AQGreen participation started tenant={customer.TenantId} customer={customer.Id} independent={participation.JoinedIndependently}");
            return Map(participation, await GetClubMemberNumberAsync(participation.RecruiterCustomerId));
        }

        [AbpAuthorize(AquaPermissions.ProgrammeParticipations.Join)]
        [UnitOfWork(IsDisabled = true)]
        public async Task<ProgrammeCheckoutDto> CreateAQGreenJoiningCheckoutAsync()
        {
            var tenantId = GetRequiredTenantId("AQGreen payment is unavailable.");
            AQGreenJoiningCheckout paymentCheckout;

            using (var uow = _unitOfWorkManager.Begin(new UnitOfWorkOptions
            {
                IsTransactional = true,
                IsolationLevel = IsolationLevel.Serializable
            }))
            using (_unitOfWorkManager.Current.SetTenantId(tenantId))
            {
                var customer = await GetCurrentActiveCustomerAsync();
                var participation = await _entryParticipationRepository.FirstOrDefaultAsync(
                    candidate => candidate.CustomerId == customer.Id);
                if (participation == null)
                    throw new UserFriendlyException(
                        "Join AQGreen before starting payment.",
                        "Your recruiter placement must be recorded before checkout.");
                if (participation.Status == EntryParticipationStatus.Active)
                    throw new UserFriendlyException(
                        "Your AQGreen participation is already active.",
                        "No additional joining payment is required.");
                if (participation.JoiningPaymentAmount <= 0m ||
                    participation.RegistrationPaymentId.HasValue ||
                    participation.ActivationPaymentId.HasValue)
                    throw new UserFriendlyException(
                        "Online payment is unavailable for this historical AQGreen record.",
                        "Contact the club team so an existing payment is not charged again.");

                paymentCheckout = await _aqGreenJoiningCheckoutRepository.FirstOrDefaultAsync(
                    checkout => checkout.ParticipationId == participation.Id);
                if (paymentCheckout == null)
                {
                    paymentCheckout = AQGreenJoiningCheckout.Create(
                        tenantId,
                        participation.Id,
                        customer.Id,
                        participation.JoiningPaymentAmount,
                        participation.Currency,
                        UtcNow);
                    await _aqGreenJoiningCheckoutRepository.InsertAsync(paymentCheckout);
                    await _unitOfWorkManager.Current.SaveChangesAsync();
                }

                await uow.CompleteAsync();
            }

            if (!string.IsNullOrWhiteSpace(paymentCheckout.CheckoutUrl))
                return MapCheckout(paymentCheckout);

            var checkout = await CreateYocoCheckoutAsync(
                paymentCheckout,
                YocoCheckoutMetadata.AQGreenJoiningCheckoutId,
                "AQGreenJoining",
                "AQGreen joining payment",
                "aqgreen");

            using (var uow = _unitOfWorkManager.Begin(new UnitOfWorkOptions
            {
                IsTransactional = true
            }))
            using (_unitOfWorkManager.Current.SetTenantId(tenantId))
            {
                paymentCheckout = await _aqGreenJoiningCheckoutRepository.GetAsync(paymentCheckout.Id);
                paymentCheckout.RecordCheckout(checkout.Id, checkout.RedirectUrl, UtcNow);
                await _unitOfWorkManager.Current.SaveChangesAsync();
                await uow.CompleteAsync();
            }

            Logger.Info(
                $"AQGreen joining checkout created tenant={tenantId} checkout={paymentCheckout.Id}");
            return MapCheckout(paymentCheckout);
        }

        [AbpAuthorize(AquaPermissions.ProgrammeParticipations.Join)]
        [UnitOfWork(IsDisabled = true)]
        public async Task<ProgrammeCheckoutDto> CreateDirectOnyxCheckoutAsync(
            CreateDirectOnyxCheckoutInput input)
        {
            input ??= new CreateDirectOnyxCheckoutInput();
            var tenantId = GetRequiredTenantId("Onyx checkout is unavailable.");
            DirectOnyxCheckoutIntent intent;

            using (var uow = _unitOfWorkManager.Begin(new UnitOfWorkOptions
            {
                IsTransactional = true,
                IsolationLevel = IsolationLevel.Serializable
            }))
            using (_unitOfWorkManager.Current.SetTenantId(tenantId))
            {
                var customer = await GetCurrentActiveCustomerAsync();
                var recruiterCustomerId = await ResolveRequestedRecruiterAsync(
                    input.RecruiterCustomerId,
                    input.InviteCode,
                    RecruitmentProgrammeKeys.Onyx,
                    customer);
                if (recruiterCustomerId.HasValue)
                {
                    await GetActiveOnyxRecruiterAsync(
                        customer.Id,
                        recruiterCustomerId.Value);
                }

                var existingParticipation = await _onyxParticipationRepository.FirstOrDefaultAsync(
                    participation => participation.CustomerId == customer.Id);
                if (existingParticipation != null)
                    throw new UserFriendlyException(
                        "You already participate in Onyx.",
                        "No additional Onyx payment is required through this joining flow.");

                intent = await _directOnyxCheckoutIntentRepository.FirstOrDefaultAsync(
                    checkout => checkout.CustomerId == customer.Id);
                if (intent == null)
                {
                    var membership = await GetCurrentOnyxMembershipAsync(customer.TenantId.Value);
                    intent = DirectOnyxCheckoutIntent.Create(
                        customer.TenantId.Value,
                        customer.Id,
                        recruiterCustomerId,
                        input.InviteCode,
                        membership.Id,
                        _termsProvider.GetDirectOnyxTerms(),
                        UtcNow);
                    await _directOnyxCheckoutIntentRepository.InsertAsync(intent);
                    await _unitOfWorkManager.Current.SaveChangesAsync();
                }
                else
                {
                    EnsureSameCheckoutPlacement(intent, recruiterCustomerId, input.InviteCode);
                }

                await uow.CompleteAsync();
            }

            if (!string.IsNullOrWhiteSpace(intent.CheckoutUrl))
                return MapCheckout(intent);

            var checkout = await CreateYocoCheckoutAsync(
                intent,
                YocoCheckoutMetadata.DirectOnyxCheckoutIntentId,
                "OnyxDirectEntry",
                "Direct Onyx participation",
                "onyx");

            using (var uow = _unitOfWorkManager.Begin(new UnitOfWorkOptions
            {
                IsTransactional = true
            }))
            using (_unitOfWorkManager.Current.SetTenantId(tenantId))
            {
                intent = await _directOnyxCheckoutIntentRepository.GetAsync(intent.Id);
                intent.RecordCheckout(checkout.Id, checkout.RedirectUrl, UtcNow);
                await _unitOfWorkManager.Current.SaveChangesAsync();
                await uow.CompleteAsync();
            }

            Logger.Info(
                $"Direct Onyx checkout created tenant={tenantId} intent={intent.Id} independent={!intent.RecruiterCustomerId.HasValue}");
            return MapCheckout(intent);
        }

        private Uri GetClientRootAddress()
        {
            var configured = _configuration["App:ClientRootAddress"];
            var environment = _configuration["ASPNETCORE_ENVIRONMENT"];
            var isDevelopment = string.Equals(
                environment,
                "Development",
                StringComparison.OrdinalIgnoreCase);

            if (!Uri.TryCreate(configured, UriKind.Absolute, out var root) ||
                root.Scheme != Uri.UriSchemeHttps &&
                (!isDevelopment || root.Scheme != Uri.UriSchemeHttp))
                throw new UserFriendlyException(
                    "Online payment is temporarily unavailable.",
                    "The customer website address has not been configured correctly.");

            return root;
        }

        private static void EnsureSameCheckoutPlacement(
            DirectOnyxCheckoutIntent intent,
            int? recruiterCustomerId,
            string inviteCode)
        {
            var normalizedInviteCode = string.IsNullOrWhiteSpace(inviteCode)
                ? null
                : ProgrammeInvitationResolver.NormalizeCode(inviteCode);
            if (intent.RecruiterCustomerId == recruiterCustomerId &&
                string.Equals(intent.InviteCode, normalizedInviteCode, StringComparison.Ordinal))
                return;

            throw new UserFriendlyException(
                "An Onyx payment is already awaiting completion.",
                "Complete the existing checkout. Contact the club team if its recruiter placement is incorrect.");
        }

        private async Task<YocoCheckout> CreateYocoCheckoutAsync(
            HostedPaymentCheckout paymentCheckout,
            string referenceMetadataKey,
            string purpose,
            string description,
            string programme)
        {
            var clientRootAddress = GetClientRootAddress();
            var query = $"payment={{0}}&programme={Uri.EscapeDataString(programme)}";
            return await _yocoCheckoutGateway.CreateAsync(new CreateYocoCheckout
            {
                ReferenceId = paymentCheckout.Id,
                ReferenceMetadataKey = referenceMetadataKey,
                Purpose = purpose,
                Amount = paymentCheckout.Amount,
                Currency = paymentCheckout.Currency,
                Description = description,
                SuccessUrl = new Uri(clientRootAddress, $"member/programmes?{string.Format(query, "success")}").ToString(),
                CancelUrl = new Uri(clientRootAddress, $"member/programmes?{string.Format(query, "cancelled")}").ToString(),
                FailureUrl = new Uri(clientRootAddress, $"member/programmes?{string.Format(query, "failed")}").ToString()
            });
        }

        private static ProgrammeCheckoutDto MapCheckout(HostedPaymentCheckout checkout) =>
            new ProgrammeCheckoutDto
            {
                Amount = checkout.Amount,
                Currency = checkout.Currency,
                CheckoutUrl = checkout.CheckoutUrl
            };

        private async Task ClearLegacyProgrammeMembershipAssignmentAsync(
            Customer customer,
            MembershipType programmeMembershipType,
            string programmeName)
        {
            if (!customer.MembershipId.HasValue)
            {
                return;
            }

            Membership assignedMembership;
            using (CurrentUnitOfWork.DisableFilter(AbpDataFilters.MayHaveTenant))
            {
                assignedMembership = await _membershipRepository.FirstOrDefaultAsync(
                    customer.MembershipId.Value);
            }
            if (assignedMembership?.MembershipType != programmeMembershipType)
            {
                return;
            }

            customer.ChangeMembership(null);
            await _customerRepository.UpdateAsync(customer);
            Logger.Warn(
                $"Cleared legacy {programmeName} membership assignment tenant={customer.TenantId} customer={customer.Id}");
        }

        private async Task<int?> ResolveRequestedRecruiterAsync(
            int? recruiterCustomerId,
            string inviteCode,
            string programmeKey,
            Customer customer)
        {
            if (recruiterCustomerId.HasValue && !string.IsNullOrWhiteSpace(inviteCode))
            {
                throw InvalidRecruiter(
                    "Use either an invitation code or a recruiter reference, not both.");
            }

            if (string.IsNullOrWhiteSpace(inviteCode)) return recruiterCustomerId;
            return await _invitationResolver.ResolveRecruiterForJoiningAsync(
                inviteCode,
                programmeKey,
                customer.Id,
                customer.TenantId.Value);
        }

        private async Task<Customer> GetCurrentActiveCustomerAsync()
        {
            var tenantId = GetRequiredTenantId("Programme participation is unavailable.");
            var userId = AbpSession.GetUserId();
            var customer = await _customerRepository.FirstOrDefaultAsync(
                item => item.TenantId == tenantId && item.UserId == userId);
            if (customer == null)
            {
                throw new UserFriendlyException(
                    "Programme participation is unavailable.",
                    "No Club Member account is linked to your sign-in.");
            }

            if (!customer.IsActive)
            {
                throw new UserFriendlyException(
                    "Programme participation is unavailable.",
                    "Your Club Member account is inactive. Contact the club team for assistance.");
            }

            return customer;
        }

        private async Task<string> GetClubMemberNumberAsync(int? customerId)
        {
            if (!customerId.HasValue) return null;
            using (CurrentUnitOfWork.DisableFilter(AbpDataFilters.MayHaveTenant))
            {
                return await _customerRepository.GetAll()
                    .Where(customer => customer.Id == customerId.Value)
                    .Select(customer => customer.ClubMemberNumber)
                    .SingleOrDefaultAsync();
            }
        }

        private async Task<EntryParticipation> GetActiveEntryRecruiterAsync(
            int customerId,
            int recruiterCustomerId)
        {
            EnsureDifferentCustomers(customerId, recruiterCustomerId);
            using (CurrentUnitOfWork.DisableFilter(AbpDataFilters.MustHaveTenant))
            {
                var recruiter = await _entryParticipationRepository.FirstOrDefaultAsync(
                    participation =>
                        participation.CustomerId == recruiterCustomerId &&
                        participation.Status == EntryParticipationStatus.Active);
                if (recruiter == null)
                {
                    throw InvalidRecruiter(
                        "The selected recruiter is not currently participating in AQGreen.");
                }

                return recruiter;
            }
        }

        private async Task<OnyxParticipation> GetActiveOnyxRecruiterAsync(
            int customerId,
            int recruiterCustomerId)
        {
            EnsureDifferentCustomers(customerId, recruiterCustomerId);
            using (CurrentUnitOfWork.DisableFilter(AbpDataFilters.MustHaveTenant))
            {
                var recruiter = await _onyxParticipationRepository.FirstOrDefaultAsync(
                    participation =>
                        participation.CustomerId == recruiterCustomerId &&
                        participation.Status == OnyxParticipationStatus.Active);
                if (recruiter == null)
                {
                    throw InvalidRecruiter(
                        "The selected recruiter is not currently participating in Onyx.");
                }

                return recruiter;
            }
        }

        private async Task<Membership> GetCurrentOnyxMembershipAsync(int tenantId)
        {
            using (CurrentUnitOfWork.DisableFilter(AbpDataFilters.MayHaveTenant))
            {
                var membership = await _membershipRepository.GetAll()
                    .Where(item =>
                        item.IsActive &&
                        item.MembershipType == MembershipType.Onyx &&
                        (!item.TenantId.HasValue || item.TenantId == tenantId))
                    .OrderByDescending(item => item.TenantId.HasValue)
                    .FirstOrDefaultAsync();
                if (membership == null)
                {
                    throw new UserFriendlyException(
                        "Onyx participation is unavailable.",
                        "The Onyx programme has not been configured for this Area.");
                }

                return membership;
            }
        }

        private static void EnsureDifferentCustomers(int customerId, int recruiterCustomerId)
        {
            if (recruiterCustomerId <= 0)
            {
                throw InvalidRecruiter("Enter a valid recruiter reference.");
            }

            if (customerId == recruiterCustomerId)
            {
                throw InvalidRecruiter("You cannot recruit yourself.");
            }
        }

        private static void EnsureSameRecruiter(
            int? recordedRecruiterCustomerId,
            int? requestedRecruiterCustomerId,
            string programmeName)
        {
            if (recordedRecruiterCustomerId == requestedRecruiterCustomerId)
            {
                return;
            }

            throw new UserFriendlyException(
                $"{programmeName} participation already exists.",
                "The recruiter cannot be changed through the joining form. Contact an administrator if the recorded placement is incorrect.");
        }

        private static UserFriendlyException InvalidRecruiter(string details)
        {
            return new UserFriendlyException("The recruiter could not be accepted.", details);
        }

        private static ProgrammeParticipationDto Map(
            EntryParticipation participation,
            string recruiterClubMemberNumber)
        {
            var details = ProgrammeParticipationStatusPresenter.Describe(participation);
            return new ProgrammeParticipationDto
            {
                ProgrammeName = "AQGreen",
                Status = details.Status,
                IsActive = details.IsActive,
                JoinedIndependently = participation.JoinedIndependently,
                RecruiterClubMemberNumber = recruiterClubMemberNumber,
                StartedAt = participation.StartedAt,
                ActivatedAt = participation.ActivatedAt,
                NextPaymentAmount = details.NextPaymentAmount,
                NextPaymentDescription = details.NextPaymentDescription,
                Currency = participation.Currency,
                CanRecruitForThisProgramme = details.CanRecruit
            };
        }

        private static ProgrammeParticipationDto Map(
            OnyxParticipation participation,
            string recruiterClubMemberNumber)
        {
            var details = ProgrammeParticipationStatusPresenter.Describe(participation);
            return new ProgrammeParticipationDto
            {
                ProgrammeName = "Onyx",
                Status = details.Status,
                IsActive = details.IsActive,
                JoinedIndependently = participation.JoinedIndependently,
                RecruiterClubMemberNumber = recruiterClubMemberNumber,
                StartedAt = participation.StartedAt,
                ActivatedAt = participation.ActivatedAt,
                NextPaymentAmount = details.NextPaymentAmount,
                NextPaymentDescription = details.NextPaymentDescription,
                Currency = participation.Currency,
                CanRecruitForThisProgramme = details.CanRecruit
            };
        }
    }
}
