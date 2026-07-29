using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AqualLifeStyle.Domain.Email;
using AqualLifeStyle.Email;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class TransactionalEmailOutboxProcessorTests : AqualLifeStyleTestBase
    {
        private readonly ITransactionalEmailDeliveryGateway _deliveryGateway;
        private readonly TransactionalEmailOutboxProcessor _processor;

        public TransactionalEmailOutboxProcessorTests()
        {
            _deliveryGateway = Resolve<ITransactionalEmailDeliveryGateway>();
            _deliveryGateway.ClearReceivedCalls();
            _processor = Resolve<TransactionalEmailOutboxProcessor>();
        }

        [Fact]
        public async Task SuccessfulDelivery_IsSentOnceAndClearsTokenBearingBodies()
        {
            var messageId = await InsertPendingMessageAsync("verification-success");
            _deliveryGateway.SendAsync(Arg.Any<TransactionalEmail>(), Arg.Any<CancellationToken>())
                .Returns("bird-message-1");

            await _processor.ProcessPendingAsync();
            await _processor.ProcessPendingAsync();

            await _deliveryGateway.Received(1).SendAsync(
                Arg.Is<TransactionalEmail>(email => email.Reference == "verification-success"),
                Arg.Any<CancellationToken>());
            await UsingDbContextAsync(1, async context =>
            {
                var persisted = await context.TransactionalEmailOutboxMessages.SingleAsync(
                    message => message.Id == messageId);
                persisted.Status.ShouldBe(TransactionalEmailStatus.Sent);
                persisted.ProviderMessageId.ShouldBe("bird-message-1");
                persisted.HtmlBody.ShouldBeNull();
                persisted.TextBody.ShouldBeNull();
                persisted.AttemptCount.ShouldBe(1);
            });
        }

        [Fact]
        public async Task FailedDelivery_RetainsSafeRetryStateAndThenUsesTheSameIdempotencyKey()
        {
            var messageId = await InsertPendingMessageAsync("password-reset-retry");
            _deliveryGateway.SendAsync(Arg.Any<TransactionalEmail>(), Arg.Any<CancellationToken>())
                .Returns<Task<string>>(_ => throw new HttpRequestException("provider body and recipient@example.test"));

            await _processor.ProcessPendingAsync();

            await UsingDbContextAsync(1, async context =>
            {
                var failed = await context.TransactionalEmailOutboxMessages.SingleAsync(
                    message => message.Id == messageId);
                failed.Status.ShouldBe(TransactionalEmailStatus.Pending);
                failed.AttemptCount.ShouldBe(1);
                failed.LastError.ShouldBe("HttpRequestException: Email delivery failed.");
                failed.LastError.ShouldNotContain("recipient@example.test");
                failed.RecordFailure(failed.LastError, DateTime.UtcNow.AddMinutes(-1));
            });

            _deliveryGateway.SendAsync(Arg.Any<TransactionalEmail>(), Arg.Any<CancellationToken>())
                .Returns("bird-message-2");
            await _processor.ProcessPendingAsync();

            await _deliveryGateway.Received(2).SendAsync(
                Arg.Is<TransactionalEmail>(email => email.Reference == "password-reset-retry"),
                Arg.Any<CancellationToken>());
            await UsingDbContextAsync(1, async context =>
            {
                var sent = await context.TransactionalEmailOutboxMessages.SingleAsync(
                    message => message.Id == messageId);
                sent.Status.ShouldBe(TransactionalEmailStatus.Sent);
                sent.AttemptCount.ShouldBe(2);
                sent.ProviderMessageId.ShouldBe("bird-message-2");
            });
        }

        private Task<Guid> InsertPendingMessageAsync(string idempotencyKey)
        {
            var message = TransactionalEmailOutboxMessage.Create(
                1,
                "AccountEmail",
                idempotencyKey,
                "recipient@example.test",
                "Account email",
                "<p>token-bearing link</p>",
                "token-bearing link",
                DateTime.UtcNow);
            return UsingDbContextAsync(1, async context =>
            {
                context.TransactionalEmailOutboxMessages.Add(message);
                await context.SaveChangesAsync();
                return message.Id;
            });
        }
    }
}
