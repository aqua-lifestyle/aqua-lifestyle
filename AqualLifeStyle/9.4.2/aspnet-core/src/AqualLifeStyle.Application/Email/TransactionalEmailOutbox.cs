using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using AqualLifeStyle.Domain.Email;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Email
{
    public interface ITransactionalEmailOutbox
    {
        Task EnqueueAsync(int? tenantId, string notificationType, string idempotencyKey, TransactionalEmail email);
    }

    public sealed class TransactionalEmailOutbox : ITransactionalEmailOutbox, ITransientDependency
    {
        private readonly IRepository<TransactionalEmailOutboxMessage, Guid> _repository;

        public TransactionalEmailOutbox(IRepository<TransactionalEmailOutboxMessage, Guid> repository)
            => _repository = repository;

        public async Task EnqueueAsync(
            int? tenantId, string notificationType, string idempotencyKey, TransactionalEmail email)
        {
            if (email == null) throw new ArgumentNullException(nameof(email));
            if (await _repository.GetAll().AnyAsync(message => message.IdempotencyKey == idempotencyKey)) return;
            await _repository.InsertAsync(TransactionalEmailOutboxMessage.Create(
                tenantId, notificationType, idempotencyKey, email.Recipient, email.Subject,
                email.HtmlBody, email.TextBody, DateTime.UtcNow));
        }
    }

    public class TransactionalEmailOutboxProcessor : ITransientDependency
    {
        private readonly IRepository<TransactionalEmailOutboxMessage, Guid> _repository;
        private readonly ITransactionalEmailDeliveryGateway _deliveryGateway;
        private readonly IUnitOfWorkManager _unitOfWorkManager;

        public TransactionalEmailOutboxProcessor(
            IRepository<TransactionalEmailOutboxMessage, Guid> repository,
            ITransactionalEmailDeliveryGateway deliveryGateway,
            IUnitOfWorkManager unitOfWorkManager)
        {
            _repository = repository;
            _deliveryGateway = deliveryGateway;
            _unitOfWorkManager = unitOfWorkManager;
        }

        [UnitOfWork]
        public virtual async Task ProcessPendingAsync()
        {
            var now = DateTime.UtcNow;
            TransactionalEmailOutboxMessage[] messages;
            using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MayHaveTenant))
            {
                messages = await _repository.GetAll()
                    .Where(message =>
                        (message.Status == TransactionalEmailStatus.Pending && message.NextAttemptAt <= now) ||
                        (message.Status == TransactionalEmailStatus.Processing && message.ProcessingStartedAt < now.AddMinutes(-10)))
                    .OrderBy(message => message.NextAttemptAt)
                    .Take(20)
                    .ToArrayAsync();
            }

            foreach (var message in messages)
            {
                using (_unitOfWorkManager.Current.SetTenantId(message.TenantId))
                {
                    message.StartAttempt(now);
                    await _unitOfWorkManager.Current.SaveChangesAsync();
                    try
                    {
                        var providerId = await _deliveryGateway.SendAsync(new TransactionalEmail(
                            message.Recipient, message.Subject, message.HtmlBody, message.TextBody,
                            message.IdempotencyKey));
                        message.MarkSent(providerId, DateTime.UtcNow);
                    }
                    catch (Exception exception)
                    {
                        // Recipient, body and provider credentials are deliberately excluded.
                        message.RecordFailure(SafeError(exception), DateTime.UtcNow.AddMinutes(BackoffMinutes(message.AttemptCount)));
                    }
                    await _unitOfWorkManager.Current.SaveChangesAsync();
                }
            }
        }

        private static int BackoffMinutes(int attempt) => Math.Min(60, 1 << Math.Min(attempt, 5));

        private static string SafeError(Exception exception)
            => exception == null ? "Email delivery failed." : exception.GetType().Name + ": Email delivery failed.";
    }
}
