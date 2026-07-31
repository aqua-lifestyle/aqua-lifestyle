using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Abp.Domain.Uow;
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
        public async Task SuccessfulDelivery_WithNoProviderMessageId_DoesNotRetry()
        {
            var messageId = await InsertPendingMessageAsync("provider-id-optional");
            _deliveryGateway.SendAsync(Arg.Any<TransactionalEmail>(), Arg.Any<CancellationToken>())
                .Returns((string)null);

            await _processor.ProcessPendingAsync();
            await _processor.ProcessPendingAsync();

            await _deliveryGateway.Received(1).SendAsync(
                Arg.Is<TransactionalEmail>(email => email.Reference == "provider-id-optional"),
                Arg.Any<CancellationToken>());
            await UsingDbContextAsync(1, async context =>
            {
                var persisted = await context.TransactionalEmailOutboxMessages.SingleAsync(
                    message => message.Id == messageId);
                persisted.Status.ShouldBe(TransactionalEmailStatus.Sent);
                persisted.ProviderMessageId.ShouldBeNull();
                persisted.HtmlBody.ShouldBeNull();
                persisted.TextBody.ShouldBeNull();
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

        [Fact]
        public void RecordFailure_StopsRetryingAtTheMaximumAttemptCount()
        {
            var message = TransactionalEmailOutboxMessage.Create(
                1,
                "AccountEmail",
                "terminal-failure",
                "recipient@example.test",
                "Account email",
                "<p>body</p>",
                "body",
                DateTime.UtcNow);

            for (var attempt = 1; attempt <= TransactionalEmailOutboxMessage.MaxDeliveryAttempts; attempt++)
            {
                message.StartAttempt(Guid.NewGuid(), DateTime.UtcNow);
                message.RecordFailure("Delivery failed.", DateTime.UtcNow.AddMinutes(1));
            }

            message.Status.ShouldBe(TransactionalEmailStatus.Failed);
            message.AttemptCount.ShouldBe(TransactionalEmailOutboxMessage.MaxDeliveryAttempts);
            message.Recipient.ShouldBe("[redacted]");
            message.Subject.ShouldBe("[redacted]");
            message.HtmlBody.ShouldBeNull();
            message.TextBody.ShouldBeNull();
        }

        [Fact]
        public async Task AClaimedMessage_CannotBeClaimedAgainBeforeItBecomesStale()
        {
            var messageId = await InsertPendingMessageAsync("atomic-claim");
            var repository = Resolve<ITransactionalEmailOutboxRepository>();
            var unitOfWorkManager = Resolve<IUnitOfWorkManager>();
            var now = DateTime.UtcNow;
            TransactionalEmailOutboxMessage firstClaim;
            TransactionalEmailOutboxMessage secondClaim;

            using (var unitOfWork = unitOfWorkManager.Begin())
            using (unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MayHaveTenant))
            {
                firstClaim = await repository.TryClaimAsync(
                    messageId, Guid.NewGuid(), now, now.AddMinutes(-10));
                await unitOfWork.CompleteAsync();
            }

            using (var unitOfWork = unitOfWorkManager.Begin())
            using (unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MayHaveTenant))
            {
                secondClaim = await repository.TryClaimAsync(
                    messageId, Guid.NewGuid(), now, now.AddMinutes(-10));
                await unitOfWork.CompleteAsync();
            }

            firstClaim.ShouldNotBeNull();
            secondClaim.ShouldBeNull();
        }

        [Fact]
        public async Task EnqueueAsync_TreatsAnExistingIdempotencyKeyAsSuccessWithoutDuplicating()
        {
            var outbox = Resolve<ITransactionalEmailOutbox>();
            var email = new TransactionalEmail(
                "recipient@example.test",
                "Subject",
                "<p>Body</p>",
                "Body",
                "duplicate-enqueue");

            (await outbox.EnqueueAsync(1, "AccountEmail", "duplicate-enqueue", email)).ShouldBeTrue();
            (await outbox.EnqueueAsync(1, "AccountEmail", "duplicate-enqueue", email)).ShouldBeFalse();

            await UsingDbContextAsync(1, async context =>
                (await context.TransactionalEmailOutboxMessages.CountAsync(message =>
                    message.IdempotencyKey == "duplicate-enqueue")).ShouldBe(1));
        }

        [Fact]
        public async Task RepositoryInsert_HandlesAUniqueKeyRaceWithoutMaskingTheExistingMessage()
        {
            var repository = Resolve<ITransactionalEmailOutboxRepository>();
            var unitOfWorkManager = Resolve<IUnitOfWorkManager>();

            using (var unitOfWork = unitOfWorkManager.Begin())
            {
                (await repository.InsertIfMissingAsync(CreateMessage("repository-race"))).ShouldBeTrue();
                await unitOfWork.CompleteAsync();
            }

            using (var unitOfWork = unitOfWorkManager.Begin())
            {
                (await repository.InsertIfMissingAsync(CreateMessage("repository-race"))).ShouldBeFalse();
                await unitOfWork.CompleteAsync();
            }

            await UsingDbContextAsync(1, async context =>
                (await context.TransactionalEmailOutboxMessages.CountAsync(message =>
                    message.IdempotencyKey == "repository-race")).ShouldBe(1));
        }

        [Fact]
        public async Task AStaleClaim_CanBeRecoveredWithoutGivingTheOldWorkerOwnership()
        {
            var messageId = await InsertPendingMessageAsync("stale-claim");
            var repository = Resolve<ITransactionalEmailOutboxRepository>();
            var unitOfWorkManager = Resolve<IUnitOfWorkManager>();
            var oldToken = Guid.NewGuid();
            var newToken = Guid.NewGuid();

            using (var unitOfWork = unitOfWorkManager.Begin())
            using (unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MayHaveTenant))
            {
                (await repository.TryClaimAsync(
                    messageId,
                    oldToken,
                    DateTime.UtcNow,
                    DateTime.UtcNow.AddMinutes(-10))).ShouldNotBeNull();
                await unitOfWork.CompleteAsync();
            }

            await UsingDbContextAsync(1, async context =>
                await context.TransactionalEmailOutboxMessages
                    .Where(message => message.Id == messageId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(
                        message => message.ProcessingStartedAt,
                        DateTime.UtcNow.AddMinutes(-20))));

            TransactionalEmailOutboxMessage recovered;
            using (var unitOfWork = unitOfWorkManager.Begin())
            using (unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MayHaveTenant))
            {
                recovered = await repository.TryClaimAsync(
                    messageId,
                    newToken,
                    DateTime.UtcNow,
                    DateTime.UtcNow.AddMinutes(-10));
                await unitOfWork.CompleteAsync();
            }

            recovered.ShouldNotBeNull();
            recovered.IsClaimedBy(newToken).ShouldBeTrue();
            recovered.IsClaimedBy(oldToken).ShouldBeFalse();
            recovered.AttemptCount.ShouldBe(2);
        }

        [Fact]
        public async Task PendingTerminalAlert_IsRecoveredWithoutRetryingDelivery()
        {
            var message = CreateMessage("recover-terminal-alert");
            for (var attempt = 0; attempt < TransactionalEmailOutboxMessage.MaxDeliveryAttempts; attempt++)
            {
                message.StartAttempt(Guid.NewGuid(), DateTime.UtcNow);
                message.RecordFailure("Delivery failed.", DateTime.UtcNow.AddMinutes(-1));
            }
            await UsingDbContextAsync(1, async context =>
            {
                context.TransactionalEmailOutboxMessages.Add(message);
                await context.SaveChangesAsync();
            });

            await _processor.ProcessPendingAsync();

            await _deliveryGateway.DidNotReceive().SendAsync(
                Arg.Any<TransactionalEmail>(), Arg.Any<CancellationToken>());
            await UsingDbContextAsync(1, async context =>
            {
                var persisted = await context.TransactionalEmailOutboxMessages.SingleAsync(item => item.Id == message.Id);
                persisted.Status.ShouldBe(TransactionalEmailStatus.Failed);
                persisted.TerminalAlertEmittedAt.ShouldNotBeNull();
            });
        }

        [Fact]
        public void Backoff_ReachesAndRetainsTheSixtyMinuteCeiling()
        {
            var method = typeof(TransactionalEmailOutboxProcessor).GetMethod(
                "BackoffMinutes",
                BindingFlags.NonPublic | BindingFlags.Static);
            method.ShouldNotBeNull();

            ((int)method.Invoke(null, new object[] { 5 })).ShouldBe(32);
            ((int)method.Invoke(null, new object[] { 6 })).ShouldBe(60);
            ((int)method.Invoke(null, new object[] { 20 })).ShouldBe(60);
        }

        [Fact]
        public async Task AccountEmailThrottle_AllowsOnlyOneReservationUntilExpiry()
        {
            var repository = Resolve<IAccountEmailThrottleRepository>();
            var unitOfWorkManager = Resolve<IUnitOfWorkManager>();
            var now = DateTime.UtcNow;

            using (var unitOfWork = unitOfWorkManager.Begin())
            using (unitOfWorkManager.Current.SetTenantId(1))
            {
                (await repository.TryAcquireAsync(
                    "reset:1:HASH", 1, now, now.AddMinutes(5))).ShouldBeTrue();
                await unitOfWork.CompleteAsync();
            }

            using (var unitOfWork = unitOfWorkManager.Begin())
            using (unitOfWorkManager.Current.SetTenantId(1))
            {
                (await repository.TryAcquireAsync(
                    "reset:1:HASH", 1, now.AddMinutes(1), now.AddMinutes(6))).ShouldBeFalse();
                await unitOfWork.CompleteAsync();
            }
        }

        private Task<Guid> InsertPendingMessageAsync(string idempotencyKey)
        {
            var message = CreateMessage(idempotencyKey);
            return UsingDbContextAsync(1, async context =>
            {
                context.TransactionalEmailOutboxMessages.Add(message);
                await context.SaveChangesAsync();
                return message.Id;
            });
        }

        private static TransactionalEmailOutboxMessage CreateMessage(string idempotencyKey)
        {
            return TransactionalEmailOutboxMessage.Create(
                1,
                "AccountEmail",
                idempotencyKey,
                "recipient@example.test",
                "Account email",
                "<p>token-bearing link</p>",
                "token-bearing link",
                DateTime.UtcNow);
        }
    }
}
