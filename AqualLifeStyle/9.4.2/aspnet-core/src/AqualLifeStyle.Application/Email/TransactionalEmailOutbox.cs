using System;
using System.Collections.Generic;
using System.Transactions;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Uow;
using AqualLifeStyle.Domain.Email;
using Microsoft.Extensions.Logging;

namespace AqualLifeStyle.Email
{
    public interface ITransactionalEmailOutbox
    {
        Task<bool> EnqueueAsync(int? tenantId, string notificationType, string idempotencyKey, TransactionalEmail email);
        Task DeleteAsync(string idempotencyKey);
    }

    public sealed class TransactionalEmailOutbox : ITransactionalEmailOutbox, ITransientDependency
    {
        private readonly ITransactionalEmailOutboxRepository _repository;

        public TransactionalEmailOutbox(ITransactionalEmailOutboxRepository repository)
            => _repository = repository;

        public async Task<bool> EnqueueAsync(
            int? tenantId, string notificationType, string idempotencyKey, TransactionalEmail email)
        {
            if (email == null) throw new ArgumentNullException(nameof(email));
            if (await _repository.FirstOrDefaultAsync(message =>
                    message.IdempotencyKey == idempotencyKey) != null)
            {
                return false;
            }

            return await _repository.InsertIfMissingAsync(TransactionalEmailOutboxMessage.Create(
                tenantId, notificationType, idempotencyKey, email.Recipient, email.Subject,
                email.HtmlBody, email.TextBody, DateTime.UtcNow));
        }

        public Task DeleteAsync(string idempotencyKey)
            => _repository.DeleteByIdempotencyKeyAsync(idempotencyKey);
    }

    public class TransactionalEmailOutboxProcessor : ITransientDependency
    {
        private const int BatchSize = 20;
        private const int CandidateScanSize = 100;
        private static readonly TimeSpan StaleProcessingAge = TimeSpan.FromMinutes(10);

        private readonly ITransactionalEmailOutboxRepository _repository;
        private readonly ITransactionalEmailDeliveryGateway _deliveryGateway;
        private readonly IUnitOfWorkManager _unitOfWorkManager;
        private readonly ILogger<TransactionalEmailOutboxProcessor> _logger;

        public TransactionalEmailOutboxProcessor(
            ITransactionalEmailOutboxRepository repository,
            ITransactionalEmailDeliveryGateway deliveryGateway,
            IUnitOfWorkManager unitOfWorkManager,
            ILogger<TransactionalEmailOutboxProcessor> logger)
        {
            _repository = repository;
            _deliveryGateway = deliveryGateway;
            _unitOfWorkManager = unitOfWorkManager;
            _logger = logger;
        }

        [UnitOfWork(IsDisabled = true)]
        public virtual async Task ProcessPendingAsync()
        {
            var now = DateTime.UtcNow;
            IReadOnlyList<Guid> candidateIds;
            using (var unitOfWork = _unitOfWorkManager.Begin(new UnitOfWorkOptions
            {
                IsTransactional = false
            }))
            using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MayHaveTenant))
            {
                candidateIds = await _repository.GetEligibleMessageIdsAsync(
                    now,
                    now.Subtract(StaleProcessingAge),
                    CandidateScanSize);
                await unitOfWork.CompleteAsync();
            }

            var claimedCount = 0;
            foreach (var messageId in candidateIds)
            {
                if (claimedCount >= BatchSize) break;

                var processingToken = Guid.NewGuid();
                TransactionalEmailOutboxMessage claimed;
                var claimedAt = DateTime.UtcNow;
                using (var unitOfWork = _unitOfWorkManager.Begin(new UnitOfWorkOptions
                {
                    IsTransactional = true,
                    IsolationLevel = IsolationLevel.ReadCommitted
                }))
                using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MayHaveTenant))
                {
                    claimed = await _repository.TryClaimAsync(
                        messageId,
                        processingToken,
                        claimedAt,
                        claimedAt.Subtract(StaleProcessingAge));
                    await unitOfWork.CompleteAsync();
                }

                if (claimed == null) continue;
                claimedCount++;

                string providerId = null;
                Exception deliveryFailure = null;
                try
                {
                    providerId = await _deliveryGateway.SendAsync(new TransactionalEmail(
                        claimed.Recipient,
                        claimed.Subject,
                        claimed.HtmlBody,
                        claimed.TextBody,
                        claimed.IdempotencyKey));
                }
                catch (Exception exception)
                {
                    deliveryFailure = exception;
                }

                using (var unitOfWork = _unitOfWorkManager.Begin(new UnitOfWorkOptions
                {
                    IsTransactional = true,
                    IsolationLevel = IsolationLevel.ReadCommitted
                }))
                using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MayHaveTenant))
                {
                    var persisted = await _repository.FirstOrDefaultAsync(messageId);
                    if (persisted != null && persisted.IsClaimedBy(processingToken))
                    {
                        if (deliveryFailure == null)
                        {
                            persisted.MarkSent(providerId, DateTime.UtcNow);
                        }
                        else
                        {
                            // Recipient, body and provider credentials are deliberately excluded.
                            persisted.RecordFailure(
                                SafeError(deliveryFailure),
                                DateTime.UtcNow.AddMinutes(BackoffMinutes(persisted.AttemptCount)));
                        }
                    }

                    await unitOfWork.CompleteAsync();
                }

            }

            await EmitPendingTerminalAlertsAsync();
        }

        private async Task EmitPendingTerminalAlertsAsync()
        {
            IReadOnlyList<TransactionalEmailOutboxMessage> pendingAlerts;
            using (var unitOfWork = _unitOfWorkManager.Begin(new UnitOfWorkOptions { IsTransactional = false }))
            using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MayHaveTenant))
            {
                pendingAlerts = await _repository.GetPendingTerminalAlertsAsync(CandidateScanSize);
                await unitOfWork.CompleteAsync();
            }

            foreach (var failure in pendingAlerts)
            {
                // Alert delivery is at-least-once; monitoring deduplicates by OutboxId.
                _logger.LogError(
                    "EmailOperationsAlert AlertType=terminal_email_delivery_failed OutboxId={OutboxId} NotificationType={NotificationType} TenantId={TenantId} AttemptCount={AttemptCount}",
                    failure.Id,
                    failure.NotificationType,
                    failure.TenantId,
                    failure.AttemptCount);
                using var unitOfWork = _unitOfWorkManager.Begin(new UnitOfWorkOptions { IsTransactional = true });
                using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MayHaveTenant))
                {
                    await _repository.MarkTerminalAlertEmittedAsync(failure.Id, DateTime.UtcNow);
                    await unitOfWork.CompleteAsync();
                }
            }
        }

        private static int BackoffMinutes(int attempt)
            => attempt >= 6 ? 60 : 1 << Math.Max(0, attempt);

        private static string SafeError(Exception exception)
            => exception == null ? "Email delivery failed." : exception.GetType().Name + ": Email delivery failed.";
    }
}
