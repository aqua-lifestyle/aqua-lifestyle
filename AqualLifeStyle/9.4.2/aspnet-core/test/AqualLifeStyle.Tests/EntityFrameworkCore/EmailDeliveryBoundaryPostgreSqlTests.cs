using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.EntityFrameworkCore;
using Abp.MultiTenancy;
using AqualLifeStyle.Domain.Email;
using AqualLifeStyle.EntityFrameworkCore;
using AqualLifeStyle.EntityFrameworkCore.Repositories;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.EntityFrameworkCore
{
    public partial class AQGreenMigrationRollbackPostgreSqlTests
    {
        [Fact]
        public async Task EmailDeliveryBoundary_PostgreSql_HoldsOldPendingAndStaleClaimsWithoutMutatingThem()
        {
            await using var context = CreateDbContext();
            var provider = Substitute.For<IDbContextProvider<AqualLifeStyleDbContext>>();
            provider.GetDbContext(Arg.Any<MultiTenancySides?>()).Returns(context);
            var repository = new TransactionalEmailOutboxRepository(provider);
            var now = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
            var cutoff = now.AddHours(-1);
            TransactionalEmailOutboxMessage Message(string key, DateTime createdAt)
            {
                var message = TransactionalEmailOutboxMessage.Create(null, "AccountEmail", key,
                    "synthetic@example.test", "Synthetic", "Synthetic body", "Synthetic body", createdAt);
                message.CreationTime = createdAt;
                return message;
            }

            var oldPending = Message("old-pending", cutoff.AddSeconds(-1));
            var oldProcessing = Message("old-processing", cutoff.AddSeconds(-1));
            oldProcessing.StartAttempt(Guid.NewGuid(), now.AddMinutes(-20));
            var boundary = Message("exact-boundary", cutoff);
            var freshStale = Message("fresh-stale", cutoff.AddSeconds(1));
            freshStale.StartAttempt(Guid.NewGuid(), now.AddMinutes(-20));
            var creationTimes = new[] { oldPending, oldProcessing, boundary, freshStale }
                .ToDictionary(x => x.Id, x => x.CreationTime);
            context.TransactionalEmailOutboxMessages.AddRange(oldPending, oldProcessing, boundary, freshStale);
            await context.SaveChangesAsync();
            // Pin audited timestamps after insert, independent of EF's audit clock.
            foreach (var item in new[] { oldPending, oldProcessing, boundary, freshStale })
                await context.TransactionalEmailOutboxMessages.Where(x => x.Id == item.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.CreationTime, creationTimes[item.Id]));

            var eligible = await repository.GetEligibleMessageIdsAsync(now, now.AddMinutes(-10), 50, cutoff);
            eligible.OrderBy(x => x).ShouldBe(new[] { boundary.Id, freshStale.Id }.OrderBy(x => x));
            (await repository.TryClaimAsync(oldPending.Id, Guid.NewGuid(), now, now.AddMinutes(-10), cutoff)).ShouldBeNull();
            (await repository.TryClaimAsync(oldProcessing.Id, Guid.NewGuid(), now, now.AddMinutes(-10), cutoff)).ShouldBeNull();
            var token = Guid.NewGuid();
            (await repository.TryClaimAsync(boundary.Id, token, now, now.AddMinutes(-10), cutoff))
                .IsClaimedBy(token).ShouldBeTrue();
            (await repository.TryClaimAsync(boundary.Id, Guid.NewGuid(), now, now.AddMinutes(-10), cutoff)).ShouldBeNull();
            (await repository.TryClaimAsync(freshStale.Id, Guid.NewGuid(), now, now.AddMinutes(-10), cutoff))
                .AttemptCount.ShouldBe(2);
            context.ChangeTracker.Clear();
            var held = await context.TransactionalEmailOutboxMessages
                .Where(x => x.Id == oldPending.Id || x.Id == oldProcessing.Id).ToListAsync();
            held.Single(x => x.Id == oldPending.Id).Status.ShouldBe(TransactionalEmailStatus.Pending);
            held.Single(x => x.Id == oldPending.Id).AttemptCount.ShouldBe(0);
            held.Single(x => x.Id == oldProcessing.Id).Status.ShouldBe(TransactionalEmailStatus.Processing);
            held.Single(x => x.Id == oldProcessing.Id).AttemptCount.ShouldBe(1);
            held.ShouldAllBe(x => x.HtmlBody == "Synthetic body" && x.ProviderMessageId == null);
        }

        [Fact]
        public async Task VerificationOnly_PostgreSql_FiltersEachClaimAndConcurrentWorkersAcquireOnce()
        {
            await using var context = CreateDbContext();
            var provider = Substitute.For<IDbContextProvider<AqualLifeStyleDbContext>>();
            provider.GetDbContext(Arg.Any<MultiTenancySides?>()).Returns(context);
            var repository = new TransactionalEmailOutboxRepository(provider);
            var now = DateTime.UtcNow; var cutoff = now.AddMinutes(-5);
            var messages = new[] { (1, "EmailVerification", "allowed"), (2, "EmailVerification", "other-tenant"),
                (1, "PasswordReset", "other-type"), (1, "EmailVerification", "historical") }
                .Select(item => TransactionalEmailOutboxMessage.Create(item.Item1, item.Item2, item.Item3,
                    "synthetic@example.test", "Synthetic", "body", "body", now)).ToArray();
            context.TransactionalEmailOutboxMessages.AddRange(messages); await context.SaveChangesAsync();
            var historical = messages.Single(x => x.IdempotencyKey == "historical");
            await context.TransactionalEmailOutboxMessages.Where(x => x.Id == historical.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.CreationTime, cutoff.AddSeconds(-1)));
            var allowed = messages.Single(x => x.IdempotencyKey == "allowed");
            (await repository.GetEligibleMessageIdsAsync(now.AddSeconds(1), now.AddMinutes(-10), 50, cutoff, true))
                .ShouldBe(new[] { allowed.Id });
            foreach (var held in messages.Where(x => x.Id != allowed.Id))
                (await repository.TryClaimAsync(held.Id, Guid.NewGuid(), now.AddSeconds(1), now.AddMinutes(-10), cutoff, true))
                    .ShouldBeNull();

            async Task<TransactionalEmailOutboxMessage> Claim()
            {
                await using var worker = CreateDbContext();
                var workerProvider = Substitute.For<IDbContextProvider<AqualLifeStyleDbContext>>();
                workerProvider.GetDbContext(Arg.Any<MultiTenancySides?>()).Returns(worker);
                return await new TransactionalEmailOutboxRepository(workerProvider).TryClaimAsync(allowed.Id,
                    Guid.NewGuid(), now.AddSeconds(1), now.AddMinutes(-10), cutoff, true);
            }
            var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Claim()));
            results.Count(x => x != null).ShouldBe(1);
            context.ChangeTracker.Clear();
            (await context.TransactionalEmailOutboxMessages.SingleAsync(x => x.Id == allowed.Id)).AttemptCount.ShouldBe(1);
            foreach (var held in await context.TransactionalEmailOutboxMessages.Where(x => x.Id != allowed.Id).ToListAsync())
            {
                held.Status.ShouldBe(TransactionalEmailStatus.Pending); held.AttemptCount.ShouldBe(0);
            }
        }
    }
}
