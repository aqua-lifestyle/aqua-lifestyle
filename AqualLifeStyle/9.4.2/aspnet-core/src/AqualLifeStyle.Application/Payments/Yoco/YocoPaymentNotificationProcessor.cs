using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using AqualLifeStyle.Domain.Payments;
using Microsoft.Extensions.Configuration;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Email;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Payments.Yoco
{
    public sealed class VerifiedYocoPaymentNotification
    {
        public string EventId { get; set; }
        public string EventType { get; set; }
        public string PaymentId { get; set; }
        public int AmountInCents { get; set; }
        public string Currency { get; set; }
        public string Mode { get; set; }
        public DateTimeOffset ConfirmedAt { get; set; }
        public string PayloadHash { get; set; }
        public IReadOnlyDictionary<string, JsonElement> Metadata { get; set; }
    }

    public sealed class YocoWebhookValidationException : InvalidOperationException
    {
        public YocoWebhookValidationException(string message) : base(message) { }
        public YocoWebhookValidationException(string message, Exception inner) : base(message, inner) { }
    }

    public sealed class YocoWebhookTransientException : Exception
    {
        public YocoWebhookTransientException(string message) : base(message) { }
    }

    public class YocoPaymentNotificationProcessor : ITransientDependency
    {
        private readonly ProgrammePaymentConfirmationProcessor _confirmationProcessor;
        private readonly IConfiguration _configuration;
        private readonly IRepository<DirectOnyxCheckoutIntent, Guid> _onyxCheckoutRepository;
        private readonly IRepository<AQGreenJoiningCheckout, Guid> _aqGreenCheckoutRepository;
        private readonly IRepository<YocoWebhookReceipt, Guid> _receiptRepository;
        private readonly IUnitOfWorkManager _unitOfWorkManager;
        private readonly IRepository<MemberPayment, Guid> _paymentRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly ITransactionalEmailOutbox _emailOutbox;
        private readonly TransactionalEmailTemplateBuilder _emailTemplates;
        private readonly IHostedPaymentCheckoutLock _hostedPaymentCheckoutLock;

        public YocoPaymentNotificationProcessor(
            ProgrammePaymentConfirmationProcessor confirmationProcessor,
            IConfiguration configuration,
            IRepository<DirectOnyxCheckoutIntent, Guid> onyxCheckoutRepository,
            IRepository<AQGreenJoiningCheckout, Guid> aqGreenCheckoutRepository,
            IRepository<YocoWebhookReceipt, Guid> receiptRepository,
            IUnitOfWorkManager unitOfWorkManager,
            IRepository<MemberPayment, Guid> paymentRepository,
            ICustomerRepository customerRepository,
            ITransactionalEmailOutbox emailOutbox,
            TransactionalEmailTemplateBuilder emailTemplates,
            IHostedPaymentCheckoutLock hostedPaymentCheckoutLock)
        {
            _confirmationProcessor = confirmationProcessor;
            _configuration = configuration;
            _onyxCheckoutRepository = onyxCheckoutRepository;
            _aqGreenCheckoutRepository = aqGreenCheckoutRepository;
            _receiptRepository = receiptRepository;
            _unitOfWorkManager = unitOfWorkManager;
            _paymentRepository = paymentRepository;
            _customerRepository = customerRepository;
            _emailOutbox = emailOutbox;
            _emailTemplates = emailTemplates;
            _hostedPaymentCheckoutLock = hostedPaymentCheckoutLock;
        }

        [UnitOfWork]
        public virtual async Task ProcessAsync(VerifiedYocoPaymentNotification notification)
        {
            if (notification == null) throw new ArgumentNullException(nameof(notification));
            var isSucceeded = string.Equals(
                notification.EventType,
                "payment.succeeded",
                StringComparison.Ordinal);
            var isFailed = string.Equals(
                notification.EventType,
                "payment.failed",
                StringComparison.Ordinal);
            if (!isSucceeded && !isFailed)
                return;

            var configuredMode = _configuration["Yoco:Mode"]?.Trim();
            if (string.IsNullOrWhiteSpace(configuredMode) ||
                !string.Equals(configuredMode, notification.Mode, StringComparison.OrdinalIgnoreCase))
                throw new YocoWebhookValidationException("The Yoco payment mode does not match this deployment.");
            if (notification.AmountInCents <= 0)
                throw new YocoWebhookValidationException("The Yoco payment amount is invalid.");
            if (string.IsNullOrWhiteSpace(notification.PaymentId))
                throw new YocoWebhookValidationException("The Yoco payment reference is missing.");
            if (notification.PaymentId.Trim().Length > YocoWebhookReceipt.MaxPaymentIdLength)
                throw new YocoWebhookValidationException("The Yoco payment reference is invalid.");
            if (string.IsNullOrWhiteSpace(notification.EventId))
                throw new YocoWebhookValidationException("The Yoco event reference is missing.");
            if (notification.EventId.Trim().Length > YocoWebhookReceipt.MaxEventIdLength)
                throw new YocoWebhookValidationException("The Yoco event reference is invalid.");
            if (notification.ConfirmedAt == default)
                throw new YocoWebhookValidationException("The Yoco payment confirmation time is missing.");
            if (!IsSha256Hash(notification.PayloadHash))
                throw new YocoWebhookValidationException("The Yoco event payload hash is invalid.");
            var providerCheckoutId = GetRequiredMetadataText(
                notification.Metadata,
                YocoCheckoutMetadata.ProviderCheckoutId,
                "The Yoco payment is missing its provider checkout reference.");
            if (providerCheckoutId.Length > HostedPaymentCheckout.MaxProviderCheckoutIdLength)
                throw new YocoWebhookValidationException(
                    "The Yoco provider checkout reference is invalid.");
            var paymentPurpose = GetRequiredMetadataText(
                notification.Metadata,
                YocoCheckoutMetadata.Purpose,
                "The Yoco payment purpose is missing.");
            var confirmedAt = notification.ConfirmedAt.UtcDateTime;

            try
            {
                var checkout = await ResolveCheckoutAsync(providerCheckoutId);
                await _hostedPaymentCheckoutLock.AcquireCheckoutAsync(
                    checkout.ReferenceId);
                checkout = await ResolveCheckoutAsync(providerCheckoutId);
                EnsureNotificationMatchesCheckout(notification, checkout, paymentPurpose);
                YocoWebhookReceipt existingReceipt;
                using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MustHaveTenant))
                {
                    existingReceipt = await _receiptRepository.FirstOrDefaultAsync(
                        receipt => receipt.EventId == notification.EventId.Trim());
                }
                if (existingReceipt != null)
                {
                    if (!existingReceipt.Matches(
                            notification.PaymentId,
                            providerCheckoutId,
                            notification.PayloadHash,
                            checkout.Programme,
                            checkout.ReferenceId))
                        throw new YocoWebhookValidationException(
                            "The Yoco event reference is already associated with different payment facts.");
                    return;
                }

                using (_unitOfWorkManager.Current.SetTenantId(checkout.TenantId))
                {
                    if (isFailed)
                    {
                        await _receiptRepository.InsertAsync(YocoWebhookReceipt.Record(
                            checkout.TenantId,
                            notification.EventId,
                            notification.PaymentId,
                            providerCheckoutId,
                            notification.PayloadHash,
                            checkout.Programme,
                            checkout.ReferenceId,
                            DateTime.UtcNow));
                        await _unitOfWorkManager.Current.SaveChangesAsync();
                        return;
                    }

                    ProgrammePaymentConfirmationResult confirmation;
                    switch (checkout.Programme)
                    {
                        case YocoCheckoutProgramme.Onyx:
                            confirmation = await _confirmationProcessor.ProcessDirectOnyxCheckoutAsync(
                                checkout.ReferenceId,
                                "Yoco",
                                notification.PaymentId,
                                providerCheckoutId,
                                notification.AmountInCents / 100m,
                                notification.Currency,
                                confirmedAt);
                            break;
                        case YocoCheckoutProgramme.AQGreen:
                            confirmation = await _confirmationProcessor.ProcessAQGreenJoiningCheckoutAsync(
                                checkout.ReferenceId,
                                "Yoco",
                                notification.PaymentId,
                                providerCheckoutId,
                                notification.AmountInCents / 100m,
                                notification.Currency,
                                confirmedAt);
                            break;
                        default:
                            throw new YocoWebhookValidationException(
                                "The Yoco checkout programme is not supported.");
                    }

                    await EnqueuePaymentConfirmationAsync(confirmation, checkout.Programme);

                    await _receiptRepository.InsertAsync(YocoWebhookReceipt.Record(
                        checkout.TenantId,
                        notification.EventId,
                        notification.PaymentId,
                        providerCheckoutId,
                        notification.PayloadHash,
                        checkout.Programme,
                        checkout.ReferenceId,
                        DateTime.UtcNow));
                    await _unitOfWorkManager.Current.SaveChangesAsync();
                }
            }
            catch (YocoWebhookTransientException)
            {
                throw;
            }
            catch (YocoWebhookValidationException)
            {
                throw;
            }
            catch (InvalidOperationException ex)
            {
                throw new YocoWebhookValidationException("The Yoco webhook is invalid.", ex);
            }
        }

        private async Task EnqueuePaymentConfirmationAsync(
            ProgrammePaymentConfirmationResult confirmation,
            YocoCheckoutProgramme programme)
        {
            var payment = await _paymentRepository.GetAsync(confirmation.PaymentId);
            var customer = await _customerRepository.GetAsync(payment.CustomerId);
            var programmeName = programme == YocoCheckoutProgramme.AQGreen ? "AQGreen" : "Onyx";
            var key = $"payment-confirmed:{payment.Id}";
            if (confirmation.AwaitingAdministrativeApproval)
            {
                await _emailOutbox.EnqueueAsync(payment.TenantId, "PaymentConfirmation", key,
                    _emailTemplates.ParticipationAwaitingApproval(
                        customer.Name,
                        customer.Email.Value,
                        programmeName,
                        key));
                return;
            }

            await _emailOutbox.EnqueueAsync(payment.TenantId, "PaymentConfirmation", key,
                _emailTemplates.PaymentConfirmation(
                    customer.Name,
                    customer.Email.Value,
                    programmeName,
                    payment.Amount,
                    payment.Currency,
                    payment.ExternalReference,
                    payment.ConfirmedAt.Value,
                    key));
        }

        private async Task<ResolvedYocoCheckout> ResolveCheckoutAsync(
            string providerCheckoutId)
        {
            DirectOnyxCheckoutIntent onyxCheckout;
            AQGreenJoiningCheckout aqGreenCheckout;
            using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MustHaveTenant))
            {
                onyxCheckout = await _onyxCheckoutRepository.GetAll()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        checkout => checkout.ProviderCheckoutId == providerCheckoutId);
                aqGreenCheckout = await _aqGreenCheckoutRepository.GetAll()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        checkout => checkout.ProviderCheckoutId == providerCheckoutId);
            }

            if (onyxCheckout != null && aqGreenCheckout != null)
                throw new YocoWebhookValidationException(
                    "The Yoco checkout reference matches more than one programme payment.");
            if (onyxCheckout != null)
                return new ResolvedYocoCheckout(
                    onyxCheckout.TenantId,
                    onyxCheckout.Id,
                    YocoCheckoutProgramme.Onyx,
                    onyxCheckout.Amount,
                    onyxCheckout.Currency,
                    YocoCheckoutMetadata.DirectOnyxPurpose);
            if (aqGreenCheckout != null)
                return new ResolvedYocoCheckout(
                    aqGreenCheckout.TenantId,
                    aqGreenCheckout.Id,
                    YocoCheckoutProgramme.AQGreen,
                    aqGreenCheckout.Amount,
                    aqGreenCheckout.Currency,
                    YocoCheckoutMetadata.AQGreenJoiningPurpose);

            throw new YocoWebhookTransientException(
                "The Yoco checkout has not been recorded locally yet.");
        }

        private static bool IsSha256Hash(string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.Trim().Length != YocoWebhookReceipt.Sha256HexLength)
                return false;
            foreach (var character in value.Trim())
            {
                if (!char.IsDigit(character) &&
                    (character < 'a' || character > 'f') &&
                    (character < 'A' || character > 'F'))
                    return false;
            }
            return true;
        }

        private static void EnsureNotificationMatchesCheckout(
            VerifiedYocoPaymentNotification notification,
            ResolvedYocoCheckout checkout,
            string paymentPurpose)
        {
            if (checkout.Amount != notification.AmountInCents / 100m ||
                !string.Equals(
                    checkout.Currency,
                    notification.Currency?.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                throw new YocoWebhookValidationException(
                    "The Yoco payment amount or currency does not match the recorded checkout.");
            if (!string.Equals(
                    checkout.ExpectedPurpose,
                    paymentPurpose,
                    StringComparison.Ordinal))
                throw new YocoWebhookValidationException(
                    "The Yoco payment purpose does not match the recorded checkout.");
        }

        private static string GetRequiredMetadataText(
            IReadOnlyDictionary<string, JsonElement> metadata,
            string key,
            string errorMessage)
        {
            if (metadata != null &&
                metadata.TryGetValue(key, out var value) &&
                value.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(value.GetString()))
                return value.GetString().Trim();
            throw new YocoWebhookValidationException(errorMessage);
        }

        private sealed class ResolvedYocoCheckout
        {
            public int TenantId { get; }
            public Guid ReferenceId { get; }
            public YocoCheckoutProgramme Programme { get; }
            public decimal Amount { get; }
            public string Currency { get; }
            public string ExpectedPurpose { get; }

            public ResolvedYocoCheckout(
                int tenantId,
                Guid referenceId,
                YocoCheckoutProgramme programme,
                decimal amount,
                string currency,
                string expectedPurpose)
            {
                TenantId = tenantId;
                ReferenceId = referenceId;
                Programme = programme;
                Amount = amount;
                Currency = currency;
                ExpectedPurpose = expectedPurpose;
            }
        }
    }
}
