using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using Abp.Auditing;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.Runtime.Session;
using Abp.UI;
using AqualLifeStyle.Application.EntryMonthlyObligations.Dto;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.Payments.Yoco;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AqualLifeStyle.Application.EntryMonthlyObligations
{
    [Audited]
    public class ClubMemberEntryMonthlyObligationAppService
        : AqualLifeStyleAppServiceBase,
            IClubMemberEntryMonthlyObligationAppService
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly IRepository<EntryMonthlyObligation, Guid>
            _obligationRepository;
        private readonly IRepository<AQGreenMonthlyObligationCheckout, Guid>
            _checkoutRepository;
        private readonly IYocoCheckoutGateway _yocoCheckoutGateway;
        private readonly IHostedPaymentCheckoutLock _hostedPaymentCheckoutLock;
        private readonly IEntryMonthlyObligationSchedulingLock _obligationLock;
        private readonly IUnitOfWorkManager _unitOfWorkManager;
        private readonly IConfiguration _configuration;

        protected virtual DateTime UtcNow => DateTime.UtcNow;

        public ClubMemberEntryMonthlyObligationAppService(
            ICustomerRepository customerRepository,
            IRepository<EntryMonthlyObligation, Guid> obligationRepository,
            IRepository<AQGreenMonthlyObligationCheckout, Guid> checkoutRepository,
            IYocoCheckoutGateway yocoCheckoutGateway,
            IHostedPaymentCheckoutLock hostedPaymentCheckoutLock,
            IEntryMonthlyObligationSchedulingLock obligationLock,
            IUnitOfWorkManager unitOfWorkManager,
            IConfiguration configuration)
        {
            _customerRepository = customerRepository;
            _obligationRepository = obligationRepository;
            _checkoutRepository = checkoutRepository;
            _yocoCheckoutGateway = yocoCheckoutGateway;
            _hostedPaymentCheckoutLock = hostedPaymentCheckoutLock;
            _obligationLock = obligationLock;
            _unitOfWorkManager = unitOfWorkManager;
            _configuration = configuration;
        }

        [AbpAuthorize(AquaPermissions.EntryMonthlyObligations.ViewSelf)]
        public async Task<IReadOnlyList<EntryMonthlyObligationDto>>
            GetMyObligationsAsync()
        {
            var tenantId = GetRequiredTenantId(
                "Your AQGreen monthly commitments are unavailable.");
            var customer = await _customerRepository.FirstOrDefaultAsync(
                item =>
                    item.TenantId == tenantId &&
                    item.UserId == AbpSession.GetUserId());
            if (customer == null || !customer.IsActive)
            {
                throw new UserFriendlyException(
                    "Your AQGreen monthly commitments are unavailable.",
                    "An active Club Member account is required.");
            }

            var obligations = await _obligationRepository.GetAll()
                .Where(item => item.CustomerId == customer.Id)
                .OrderByDescending(item => item.PeriodYear)
                .ThenByDescending(item => item.PeriodMonth)
                .ToListAsync();

            return obligations.Select(item =>
                    EntryMonthlyObligationDtoMapper.Map(
                        item,
                        customer.Name,
                        customer.Email.Value))
                .ToList();
        }

        [AbpAuthorize(AquaPermissions.EntryMonthlyObligations.Pay)]
        [UnitOfWork(IsDisabled = true)]
        public async Task<EntryMonthlyObligationCheckoutDto> CreateCheckoutAsync(
            CreateEntryMonthlyObligationCheckoutInput input)
        {
            if (input == null || input.ObligationId == Guid.Empty)
                throw NotPayable();

            var tenantId = GetRequiredTenantId(
                "AQGreen monthly payment is unavailable.");
            AQGreenMonthlyObligationCheckout paymentCheckout;
            var ownsCheckoutPreparation = false;

            using (var uow = _unitOfWorkManager.Begin(new UnitOfWorkOptions
            {
                IsTransactional = true,
                IsolationLevel = IsolationLevel.ReadCommitted
            }))
            using (_unitOfWorkManager.Current.SetTenantId(tenantId))
            {
                var customer = await GetCurrentActiveCustomerAsync(tenantId);
                await _obligationLock.AcquireAsync();
                var obligation = await _obligationRepository.FirstOrDefaultAsync(item =>
                    item.Id == input.ObligationId &&
                    item.TenantId == tenantId &&
                    item.CustomerId == customer.Id);
                if (obligation == null ||
                    obligation.Status == EntryMonthlyObligationStatus.Paid ||
                    obligation.PaymentId.HasValue ||
                    obligation.OutstandingAmount != obligation.AmountDue)
                    throw NotPayable();

                var blockingCheckouts = await _checkoutRepository.GetAll()
                    .Where(checkout =>
                        checkout.EntryMonthlyObligationId == obligation.Id &&
                        (checkout.Status == HostedPaymentCheckoutStatus.PreparingCheckout ||
                         checkout.Status == HostedPaymentCheckoutStatus.AwaitingPayment ||
                         checkout.Status == HostedPaymentCheckoutStatus.Completed))
                    .ToListAsync();
                if (blockingCheckouts.Count > 1)
                    throw new InvalidOperationException(
                        "More than one blocking checkout exists for this AQGreen monthly obligation.");

                paymentCheckout = blockingCheckouts.SingleOrDefault();
                if (paymentCheckout?.Status == HostedPaymentCheckoutStatus.Completed)
                    throw new UserFriendlyException(
                        "This AQGreen monthly payment requires review.",
                        "Contact the club team before attempting another payment for this month.");
                if (paymentCheckout == null)
                {
                    paymentCheckout = AQGreenMonthlyObligationCheckout.Create(
                        obligation,
                        UtcNow);
                    await _checkoutRepository.InsertAsync(paymentCheckout);
                    await _unitOfWorkManager.Current.SaveChangesAsync();
                    ownsCheckoutPreparation = true;
                }
                else if (paymentCheckout.Status ==
                         HostedPaymentCheckoutStatus.PreparingCheckout)
                {
                    // The provider request is idempotent by checkout ID, so a retry can
                    // recover after either the external call or local persistence failed.
                    ownsCheckoutPreparation = true;
                }

                await uow.CompleteAsync();
            }

            if (!string.IsNullOrWhiteSpace(paymentCheckout.CheckoutUrl))
                return MapCheckout(paymentCheckout);
            if (!ownsCheckoutPreparation)
                throw new UserFriendlyException(
                    "Your AQGreen monthly checkout is still being prepared.",
                    "Try again shortly. Do not start a competing payment.");

            var clientRootAddress = GetClientRootAddress();
            var period = $"{paymentCheckout.PeriodYear:D4}-{paymentCheckout.PeriodMonth:D2}";
            var checkout = await _yocoCheckoutGateway.CreateAsync(new CreateYocoCheckout
            {
                ReferenceId = paymentCheckout.Id,
                ReferenceMetadataKey =
                    YocoCheckoutMetadata.AQGreenMonthlyObligationCheckoutId,
                Purpose = YocoCheckoutMetadata.AQGreenMonthlyObligationPurpose,
                Amount = paymentCheckout.Amount,
                Currency = paymentCheckout.Currency,
                Description = $"AQGreen monthly commitment {period}",
                SuccessUrl = new Uri(
                    clientRootAddress,
                    "member/entry-commitments?payment=success").ToString(),
                CancelUrl = new Uri(
                    clientRootAddress,
                    "member/entry-commitments?payment=cancelled").ToString(),
                FailureUrl = new Uri(
                    clientRootAddress,
                    "member/entry-commitments?payment=failed").ToString()
            });

            using (var uow = _unitOfWorkManager.Begin(new UnitOfWorkOptions
            {
                IsTransactional = true
            }))
            using (_unitOfWorkManager.Current.SetTenantId(tenantId))
            {
                await _hostedPaymentCheckoutLock.AcquireCheckoutAsync(paymentCheckout.Id);
                paymentCheckout = await _checkoutRepository.GetAsync(paymentCheckout.Id);
                if (paymentCheckout.Status == HostedPaymentCheckoutStatus.PreparingCheckout)
                {
                    paymentCheckout.RecordCheckout(
                        checkout.Id,
                        checkout.RedirectUrl,
                        UtcNow);
                }
                else if (paymentCheckout.Status != HostedPaymentCheckoutStatus.AwaitingPayment ||
                         !string.Equals(
                             paymentCheckout.ProviderCheckoutId,
                             checkout.Id,
                             StringComparison.Ordinal) ||
                         !string.Equals(
                             paymentCheckout.CheckoutUrl,
                             checkout.RedirectUrl,
                             StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "The AQGreen monthly checkout changed while provider details were being recorded.");
                }
                await _unitOfWorkManager.Current.SaveChangesAsync();
                await uow.CompleteAsync();
            }

            Logger.Info(
                $"AQGreen monthly checkout created tenant={tenantId} checkout={paymentCheckout.Id} obligation={paymentCheckout.EntryMonthlyObligationId}");
            return MapCheckout(paymentCheckout);
        }

        private async Task<Customer> GetCurrentActiveCustomerAsync(int tenantId)
        {
            var customer = await _customerRepository.FirstOrDefaultAsync(item =>
                item.TenantId == tenantId &&
                item.UserId == AbpSession.GetUserId());
            if (customer == null || !customer.IsActive)
                throw new UserFriendlyException(
                    "AQGreen monthly payment is unavailable.",
                    "An active Club Member account is required.");
            return customer;
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

        private static EntryMonthlyObligationCheckoutDto MapCheckout(
            AQGreenMonthlyObligationCheckout checkout) =>
            new EntryMonthlyObligationCheckoutDto
            {
                CheckoutId = checkout.Id,
                ObligationId = checkout.EntryMonthlyObligationId,
                PeriodYear = checkout.PeriodYear,
                PeriodMonth = checkout.PeriodMonth,
                Amount = checkout.Amount,
                Currency = checkout.Currency,
                CheckoutUrl = checkout.CheckoutUrl
            };

        private static UserFriendlyException NotPayable() =>
            new UserFriendlyException(
                "This AQGreen monthly commitment is not available for payment.",
                "Select an unpaid commitment from your own account or contact the club team.");
    }
}
