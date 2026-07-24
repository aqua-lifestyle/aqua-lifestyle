using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Auditing;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.Runtime.Session;
using Abp.UI;
using AqualLifeStyle.Application.ProgrammeParticipations.Dto;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;

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
        private readonly ICurrentProgrammeTermsProvider _termsProvider;

        protected virtual DateTime UtcNow => DateTime.UtcNow;

        public ClubMemberProgrammeParticipationAppService(
            ICustomerRepository customerRepository,
            IMembershipRepository membershipRepository,
            IRepository<EntryParticipation, Guid> entryParticipationRepository,
            IRepository<OnyxParticipation, Guid> onyxParticipationRepository,
            ICurrentProgrammeTermsProvider termsProvider)
        {
            _customerRepository = customerRepository;
            _membershipRepository = membershipRepository;
            _entryParticipationRepository = entryParticipationRepository;
            _onyxParticipationRepository = onyxParticipationRepository;
            _termsProvider = termsProvider;
        }

        [AbpAuthorize(AquaPermissions.ProgrammeParticipations.ViewSelf)]
        public async Task<MyProgrammeParticipationsDto> GetMyParticipationsAsync()
        {
            var customer = await GetCurrentActiveCustomerAsync();
            var entry = await _entryParticipationRepository.FirstOrDefaultAsync(
                participation => participation.CustomerId == customer.Id);
            var onyx = await _onyxParticipationRepository.FirstOrDefaultAsync(
                participation => participation.CustomerId == customer.Id);

            return new MyProgrammeParticipationsDto
            {
                CustomerId = customer.Id,
                Entry = entry == null ? null : Map(entry),
                Onyx = onyx == null ? null : Map(onyx)
            };
        }

        [AbpAuthorize(AquaPermissions.ProgrammeParticipations.Join)]
        public async Task<ProgrammeParticipationDto> StartEntryAsync(
            StartEntryParticipationInput input)
        {
            input ??= new StartEntryParticipationInput();
            var customer = await GetCurrentActiveCustomerAsync();
            var existing = await _entryParticipationRepository.FirstOrDefaultAsync(
                participation => participation.CustomerId == customer.Id);
            if (existing != null)
            {
                EnsureSameRecruiter(existing.RecruiterCustomerId, input.RecruiterCustomerId, "Entry");
                return Map(existing);
            }

            EntryParticipation participation;
            if (input.RecruiterCustomerId.HasValue)
            {
                var recruiter = await GetActiveEntryRecruiterAsync(
                    customer.Id,
                    input.RecruiterCustomerId.Value);
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
                $"Entry participation started tenant={customer.TenantId} customer={customer.Id} independent={participation.JoinedIndependently}");
            return Map(participation);
        }

        [AbpAuthorize(AquaPermissions.ProgrammeParticipations.Join)]
        public async Task<ProgrammeParticipationDto> StartDirectOnyxAsync(
            StartDirectOnyxParticipationInput input)
        {
            input ??= new StartDirectOnyxParticipationInput();
            var customer = await GetCurrentActiveCustomerAsync();
            var existing = await _onyxParticipationRepository.FirstOrDefaultAsync(
                participation => participation.CustomerId == customer.Id);
            if (existing != null)
            {
                EnsureSameRecruiter(existing.RecruiterCustomerId, input.RecruiterCustomerId, "Onyx");
                return Map(existing);
            }

            var membership = await GetCurrentOnyxMembershipAsync(customer.TenantId.Value);
            OnyxParticipation participation;
            if (input.RecruiterCustomerId.HasValue)
            {
                var recruiter = await GetActiveOnyxRecruiterAsync(
                    customer.Id,
                    input.RecruiterCustomerId.Value);
                participation = OnyxParticipation.StartDirectUnderRecruiter(
                    customer.TenantId.Value,
                    customer.Id,
                    recruiter,
                    membership.Id,
                    _termsProvider.GetDirectOnyxTerms(),
                    UtcNow);
            }
            else
            {
                participation = OnyxParticipation.StartDirectIndependently(
                    customer.TenantId.Value,
                    customer.Id,
                    membership.Id,
                    _termsProvider.GetDirectOnyxTerms(),
                    UtcNow);
            }

            await _onyxParticipationRepository.InsertAsync(participation);
            await CurrentUnitOfWork.SaveChangesAsync();
            Logger.Info(
                $"Direct Onyx participation started tenant={customer.TenantId} customer={customer.Id} independent={participation.JoinedIndependently}");
            return Map(participation);
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
                        "The selected recruiter is not currently participating in Entry.");
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

        private static ProgrammeParticipationDto Map(EntryParticipation participation)
        {
            var awaitingRegistration =
                participation.Status == EntryParticipationStatus.AwaitingRegistrationPayment;
            var awaitingActivation =
                participation.Status == EntryParticipationStatus.AwaitingActivationPayment;
            return new ProgrammeParticipationDto
            {
                Id = participation.Id,
                ProgrammeName = "Entry",
                Status = EntryStatusLabel(participation.Status),
                IsActive = participation.Status == EntryParticipationStatus.Active,
                JoinedIndependently = participation.JoinedIndependently,
                RecruiterCustomerId = participation.RecruiterCustomerId,
                StartedAt = participation.StartedAt,
                ActivatedAt = participation.ActivatedAt,
                NextPaymentAmount = awaitingRegistration
                    ? participation.RegistrationPaymentAmount
                    : awaitingActivation
                        ? participation.ActivationPaymentAmount
                        : null,
                NextPaymentDescription = awaitingRegistration
                    ? "Registration payment"
                    : awaitingActivation
                        ? "Activation payment"
                        : null,
                Currency = participation.Currency,
                CanRecruitForThisProgramme = participation.IsQualifiedForNetwork
            };
        }

        private static ProgrammeParticipationDto Map(OnyxParticipation participation)
        {
            var awaitingPayment =
                participation.Status == OnyxParticipationStatus.AwaitingDirectEntryPayment;
            return new ProgrammeParticipationDto
            {
                Id = participation.Id,
                ProgrammeName = "Onyx",
                Status = awaitingPayment ? "Awaiting full payment" : "Active",
                IsActive = participation.Status == OnyxParticipationStatus.Active,
                JoinedIndependently = participation.JoinedIndependently,
                RecruiterCustomerId = participation.RecruiterCustomerId,
                StartedAt = participation.StartedAt,
                ActivatedAt = participation.ActivatedAt,
                NextPaymentAmount = awaitingPayment ? participation.DirectEntryAmount : null,
                NextPaymentDescription = awaitingPayment ? "Full Onyx participation payment" : null,
                Currency = participation.Currency,
                CanRecruitForThisProgramme =
                    participation.Status == OnyxParticipationStatus.Active
            };
        }

        private static string EntryStatusLabel(EntryParticipationStatus status)
        {
            return status switch
            {
                EntryParticipationStatus.AwaitingRegistrationPayment =>
                    "Awaiting registration payment",
                EntryParticipationStatus.AwaitingActivationPayment =>
                    "Awaiting activation payment",
                EntryParticipationStatus.Active => "Active",
                _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
            };
        }
    }
}
