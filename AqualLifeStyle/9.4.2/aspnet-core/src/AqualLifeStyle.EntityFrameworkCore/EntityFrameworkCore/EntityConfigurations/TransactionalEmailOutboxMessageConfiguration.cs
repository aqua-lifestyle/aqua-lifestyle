using AqualLifeStyle.Domain.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqualLifeStyle.EntityFrameworkCore.EntityConfigurations
{
    internal sealed class TransactionalEmailOutboxMessageConfiguration
        : IEntityTypeConfiguration<TransactionalEmailOutboxMessage>
    {
        public void Configure(EntityTypeBuilder<TransactionalEmailOutboxMessage> builder)
        {
            builder.ToTable("TransactionalEmailOutboxMessages");
            builder.Property(message => message.NotificationType).IsRequired().HasMaxLength(TransactionalEmailOutboxMessage.MaxNotificationTypeLength);
            builder.Property(message => message.IdempotencyKey).IsRequired().HasMaxLength(TransactionalEmailOutboxMessage.MaxIdempotencyKeyLength);
            builder.Property(message => message.Recipient).IsRequired().HasMaxLength(TransactionalEmailOutboxMessage.MaxRecipientLength);
            builder.Property(message => message.Subject).IsRequired().HasMaxLength(TransactionalEmailOutboxMessage.MaxSubjectLength);
            builder.Property(message => message.HtmlBody);
            builder.Property(message => message.TextBody);
            builder.Property(message => message.ProviderMessageId).HasMaxLength(TransactionalEmailOutboxMessage.MaxProviderMessageIdLength);
            builder.Property(message => message.LastError).HasMaxLength(TransactionalEmailOutboxMessage.MaxErrorLength);
            builder.Property(message => message.ProcessingToken);
            builder.HasIndex(message => message.IdempotencyKey).IsUnique();
            builder.HasIndex(message => new { message.Status, message.NextAttemptAt });
            builder.HasIndex(message => new { message.Status, message.TerminalAlertEmittedAt });
        }
    }
}
