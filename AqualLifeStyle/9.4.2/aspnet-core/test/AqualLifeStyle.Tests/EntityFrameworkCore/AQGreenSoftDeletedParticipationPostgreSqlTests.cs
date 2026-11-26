using System;
using System.Linq;
using System.Threading.Tasks;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.EntityFrameworkCore
{
    [Collection(AQGreenPlacementAllocatorPostgreSqlCollection.Name)]
    public sealed class AQGreenSoftDeletedParticipationPostgreSqlTests
    {
        private readonly AQGreenPlacementAllocatorPostgreSqlFixture _fixture;

        public AQGreenSoftDeletedParticipationPostgreSqlTests(
            AQGreenPlacementAllocatorPostgreSqlFixture fixture) =>
            _fixture = fixture;

        [Fact]
        public async Task SoftDeletedParticipationDoesNotReserveTheLiveCustomerSlot()
        {
            await using var lease = await _fixture.CreateDatabaseAsync();
            await using var connection = new NpgsqlConnection(lease.ConnectionString());
            await connection.OpenAsync();
            await using var context = _fixture.CreateDbContext(connection);

            var retired = await context.EntryParticipations.IgnoreQueryFilters()
                .SingleAsync(item => item.TenantId == 1 && item.CustomerId == 64);
            var retiredId = retired.Id;
            retired.IsDeleted = true;
            retired.DeletionTime = DateTime.UtcNow;
            await context.SaveChangesAsync();

            var startedAt = DateTime.UtcNow;
            var terms = EntryProgrammeTerms.CreateSingleJoiningPayment(
                "soft-delete-index-regression",
                startedAt,
                joiningPaymentAmount: 1200m,
                monthlyCommitmentAmount: 600m,
                gracePeriodDays: 7);
            var current = EntryParticipation.StartIndependently(1, 64, terms, startedAt);
            context.EntryParticipations.Add(current);
            await context.SaveChangesAsync();

            (await context.EntryParticipations.CountAsync(
                item => item.TenantId == 1 && item.CustomerId == 64 && !item.IsDeleted)).ShouldBe(1);

            var history = await context.EntryParticipations.IgnoreQueryFilters()
                .Where(item => item.TenantId == 1 && item.CustomerId == 64)
                .OrderBy(item => item.IsDeleted)
                .ToListAsync();
            history.Count.ShouldBe(2);
            history.Single(item => item.Id == retiredId).IsDeleted.ShouldBeTrue();
            history.Single(item => item.Id == current.Id).IsDeleted.ShouldBeFalse();
        }
    }
}
