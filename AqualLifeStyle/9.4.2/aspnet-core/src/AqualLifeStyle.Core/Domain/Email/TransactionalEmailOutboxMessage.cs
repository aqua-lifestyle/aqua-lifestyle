using System;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;

namespace AqualLifeStyle.Domain.Email
{
    public enum TransactionalEmailStatus
    {
        Pending = 0,
        Processing = 1,
        Sent = 2
    }

    public class TransactionalEmailOutboxMessage : FullAuditedAggregateRoot<Guid>, IMayHaveTenant
    {
        public const int MaxNotificationTypeLength = 64;
        public const int MaxIdempotencyKeyLength = 160;
        public const int MaxRecipientLength = 256;
        public const int MaxSubjectLength = 256;
        public const int MaxProviderMessageIdLength = 128;
        public const int MaxErrorLength = 512;

        public int? TenantId { get; set; }
        public string NotificationType { get; private set; }
        public string IdempotencyKey { get; private set; }
        public string Recipient { get; private set; }
        public string Subject { get; private set; }
        public string HtmlBody { get; private set; }
        public string TextBody { get; private set; }
        public TransactionalEmailStatus Status { get; private set; }
        public int AttemptCount { get; private set; }
        public DateTime NextAttemptAt { get; private set; }
        public DateTime? ProcessingStartedAt { get; private set; }
        public string ProviderMessageId { get; private set; }
        public string LastError { get; private set; }
        public DateTime? SentAt { get; private set; }

        protected TransactionalEmailOutboxMessage() { }

        public static TransactionalEmailOutboxMessage Create(
            int? tenantId,
            string notificationType,
            string idempotencyKey,
            string recipient,
            string subject,
            string htmlBody,
            string textBody,
            DateTime createdAt)
        {
            return new TransactionalEmailOutboxMessage
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                NotificationType = Required(notificationType, nameof(notificationType), MaxNotificationTypeLength),
                IdempotencyKey = Required(idempotencyKey, nameof(idempotencyKey), MaxIdempotencyKeyLength),
                Recipient = Required(recipient, nameof(recipient), MaxRecipientLength),
                Subject = Required(subject, nameof(subject), MaxSubjectLength),
                HtmlBody = Required(htmlBody, nameof(htmlBody), int.MaxValue),
                TextBody = Required(textBody, nameof(textBody), int.MaxValue),
                Status = TransactionalEmailStatus.Pending,
                NextAttemptAt = createdAt
            };
        }

        public void StartAttempt(DateTime startedAt)
        {
            if (Status == TransactionalEmailStatus.Sent) return;
            Status = TransactionalEmailStatus.Processing;
            ProcessingStartedAt = startedAt;
            AttemptCount++;
        }

        public void MarkSent(string providerMessageId, DateTime sentAt)
        {
            ProviderMessageId = Required(providerMessageId, nameof(providerMessageId), MaxProviderMessageIdLength);
            SentAt = sentAt;
            Status = TransactionalEmailStatus.Sent;
            ProcessingStartedAt = null;
            LastError = null;
            // Token-bearing account links are no longer needed after successful transmission.
            HtmlBody = null;
            TextBody = null;
        }

        public void RecordFailure(string error, DateTime nextAttemptAt)
        {
            LastError = string.IsNullOrWhiteSpace(error)
                ? "Email delivery failed."
                : error.Trim().Substring(0, Math.Min(error.Trim().Length, MaxErrorLength));
            Status = TransactionalEmailStatus.Pending;
            ProcessingStartedAt = null;
            NextAttemptAt = nextAttemptAt;
        }

        private static string Required(string value, string name, int maximumLength)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"{name} is required.", name);
            var normalized = value.Trim();
            if (normalized.Length > maximumLength) throw new ArgumentException($"{name} is too long.", name);
            return normalized;
        }
    }
}
