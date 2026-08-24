using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Abp;
using Abp.Authorization.Users;
using Abp.Domain.Uow;
using Abp.UI;
using AqualLifeStyle.Application.Admin.Commissions;
using AqualLifeStyle.Application.Admin.Commissions.Dto;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Web.Tests.Integration
{
    [Collection("WeeklyCommissionPostgreSqlRegression")]
    public class AdminCommissionPayoutMutationPostgreSqlTests
        : AqualLifeStyleWebTestBase
    {
        [Theory]
        [InlineData(AdminCommissionProgramme.Entry)]
        [InlineData(AdminCommissionProgramme.Onyx)]
        public async Task ConcurrentDifferentPaymentReferences_OnlyOneApplicationCallWins(
            AdminCommissionProgramme programme)
        {
            if (!IsPostgreSqlRegressionMode())
            {
                return;
            }

            WriteProvenanceMarker(programme);
            LoginAsHostAdmin();
            var commissionId = await SeedReleasedCommissionAsync(programme);
            var firstReference = $"pg-payout-first-{Guid.NewGuid():N}";
            var secondReference = $"pg-payout-second-{Guid.NewGuid():N}";

            await using var blockerConnection = new NpgsqlConnection(ConnectionString);
            await blockerConnection.OpenAsync();
            await using var blockerTransaction =
                await blockerConnection.BeginTransactionAsync();
            await using (var blockerCommand = blockerConnection.CreateCommand())
            {
                blockerCommand.Transaction = blockerTransaction;
                blockerCommand.CommandText =
                    "SELECT pg_advisory_xact_lock(hashtextextended(@resource, 0))";
                blockerCommand.Parameters.AddWithValue(
                    "resource",
                    BuildLockResource(programme, commissionId));
                await blockerCommand.ExecuteNonQueryAsync();
            }

            var firstCall = CapturePaymentCallAsync(
                programme,
                commissionId,
                firstReference);
            var secondCall = CapturePaymentCallAsync(
                programme,
                commissionId,
                secondReference);

            try
            {
                await WaitForAdvisoryLockWaitersAsync(2);
            }
            finally
            {
                await blockerTransaction.CommitAsync();
            }

            var outcomes = await Task.WhenAll(firstCall, secondCall);
            outcomes.Count(outcome => outcome == null).ShouldBe(1);
            var rejected = outcomes.Single(outcome => outcome != null)
                .ShouldBeOfType<UserFriendlyException>();
            rejected.Details.ShouldContain(
                "already recorded with a different payment reference");

            var persisted = await GetPaymentFactsAsync(programme, commissionId);
            persisted.Status.ShouldBe(WeeklyCommissionPayoutStatus.Paid);
            persisted.Reference.ShouldBeOneOf(firstReference, secondReference);
            persisted.PaidAt.ShouldNotBeNull();

            await RecordPaymentAsync(
                programme,
                commissionId,
                persisted.Reference,
                "Idempotent retry of the winning payment reference.");
            var afterRetry = await GetPaymentFactsAsync(programme, commissionId);
            afterRetry.ShouldBe(persisted);

            var losingReference = persisted.Reference == firstReference
                ? secondReference
                : firstReference;
            var losingRetry = await Should.ThrowAsync<UserFriendlyException>(() =>
                RecordPaymentAsync(
                    programme,
                    commissionId,
                    losingReference,
                    "Rejected retry using a conflicting payment reference."));
            losingRetry.Details.ShouldContain(
                "already recorded with a different payment reference");
        }

        private async Task<Guid> SeedReleasedCommissionAsync(
            AdminCommissionProgramme programme)
        {
            var commissionId = Guid.NewGuid();
            var suffix = commissionId.ToString("N")[..12];
            var offsetSeconds = BitConverter.ToUInt32(commissionId.ToByteArray(), 0) %
                                (180u * 24u * 60u * 60u);
            var periodStart = new DateTime(
                    2024,
                    1,
                    1,
                    0,
                    0,
                    0,
                    DateTimeKind.Utc)
                .AddSeconds(offsetSeconds);
            var periodEnd = periodStart.AddDays(7).AddTicks(-1);
            var calculatedAt = periodEnd.AddMinutes(1);
            var releasedAt = calculatedAt.AddMinutes(1);
            var rulesVersion = $"payout-race-{suffix}";

            await WithTenantFiltersDisabledAsync(async context =>
            {
                context.Database.IsNpgsql().ShouldBeTrue();
                var userName = $"payout-{suffix}";
                var user = new User
                {
                    TenantId = 1,
                    UserName = userName,
                    EmailAddress = $"{userName}@example.test",
                    Name = "Payout",
                    Surname = "Race",
                    IsEmailConfirmed = true,
                    IsActive = true
                };
                user.SetNormalizedNames();
                var passwordHasher = new PasswordHasher<User>(
                    new OptionsWrapper<PasswordHasherOptions>(
                        new PasswordHasherOptions()));
                user.Password = passwordHasher.HashPassword(
                    user,
                    User.DefaultPassword);
                context.Users.Add(user);
                await context.SaveChangesAsync();

                var customer = Customer.Create(
                    1,
                    user.Id,
                    $"Payout Race {suffix}",
                    new EmailAddress(user.EmailAddress));
                context.Customers.Add(customer);
                await context.SaveChangesAsync();

                if (programme == AdminCommissionProgramme.Onyx)
                {
                    var membership = Membership.Create(
                        1,
                        $"Onyx payout race {suffix}",
                        "PostgreSQL payout mutation regression",
                        MembershipType.Onyx);
                    context.Memberships.Add(membership);
                    await context.SaveChangesAsync();

                    var planTerms = OnyxPlanTerms.Create(
                        $"onyx-plan-{suffix}",
                        periodStart.AddMonths(-1),
                        6120m);
                    var participation = OnyxParticipation.StartDirectIndependently(
                        1,
                        customer.Id,
                        membership.Id,
                        planTerms,
                        periodStart.AddDays(-2));
                    var payment = MemberPayment.CreatePending(
                        1,
                        customer.Id,
                        MemberPaymentPurpose.OnyxDirectEntry,
                        6120m,
                        "Test",
                        $"pg-onyx-payout-{suffix}",
                        periodStart.AddDays(-2).AddMinutes(1));
                    payment.Confirm(periodStart.AddDays(-2).AddMinutes(2));
                    participation.ApplyConfirmedDirectEntryPayment(payment);
                    participation.ApproveByAdministrator(
                        AbpSession.UserId.Value,
                        periodStart.AddDays(-2).AddMinutes(3));
                    context.MemberPayments.Add(payment);
                    context.OnyxParticipations.Add(participation);

                    var commissionTerms = OnyxCommissionTerms.Create(
                        rulesVersion,
                        periodStart.AddDays(-1),
                        50m,
                        20m,
                        12.62m,
                        5m,
                        4m);
                    var period = OnyxCommissionPeriod.CreateClosedPeriod(
                        1,
                        periodStart,
                        periodEnd,
                        "UTC",
                        calculatedAt,
                        commissionTerms);
                    context.OnyxCommissionPeriods.Add(period);
                    await context.SaveChangesAsync();

                    await context.Database.ExecuteSqlInterpolatedAsync($"""
                        INSERT INTO "OnyxWeeklyCommissions" (
                            "Id", "TenantId", "OnyxParticipationId", "CustomerId",
                            "CommissionPeriodId", "HighestQualifiedNetworkLevel",
                            "HighestCommissionedLevel", "TotalAmount", "Currency",
                            "RulesVersion", "CalculatedAt", "PayoutStatus", "ReleasedAt",
                            "ReleaseReason", "CreationTime", "IsDeleted")
                        VALUES (
                            {commissionId}, 1, {participation.Id}, {customer.Id}, {period.Id},
                            1, 1, 250, 'ZAR', {rulesVersion}, {calculatedAt},
                            {(int)WeeklyCommissionPayoutStatus.Released}, {releasedAt},
                            'PostgreSQL payout race fixture.', {calculatedAt}, FALSE);
                        """);
                }
                else
                {
                    var programmeTerms = EntryProgrammeTerms.CreateSingleJoiningPayment(
                        $"entry-plan-{suffix}",
                        periodStart.AddMonths(-1),
                        1200m,
                        600m,
                        7);
                    var participation = EntryParticipation.StartIndependently(
                        1,
                        customer.Id,
                        programmeTerms,
                        periodStart.AddDays(-2));
                    var payment = MemberPayment.CreatePending(
                        1,
                        customer.Id,
                        MemberPaymentPurpose.AQGreenJoining,
                        1200m,
                        "Test",
                        $"pg-entry-payout-{suffix}",
                        periodStart.AddDays(-2).AddMinutes(1));
                    payment.Confirm(periodStart.AddDays(-2).AddMinutes(2));
                    participation.ApplyConfirmedJoiningPayment(payment);
                    participation.ApproveByAdministrator(
                        AbpSession.UserId.Value,
                        periodStart.AddDays(-2).AddMinutes(3));
                    context.MemberPayments.Add(payment);
                    context.EntryParticipations.Add(participation);

                    var commissionTerms = EntryCommissionTerms.Create(
                        rulesVersion,
                        periodStart.AddDays(-1),
                        150m,
                        250m,
                        1250m);
                    var period = EntryCommissionPeriod.CreateClosedPeriod(
                        1,
                        periodStart,
                        periodEnd,
                        "UTC",
                        calculatedAt,
                        commissionTerms);
                    context.EntryCommissionPeriods.Add(period);
                    await context.SaveChangesAsync();

                    await context.Database.ExecuteSqlInterpolatedAsync($"""
                        INSERT INTO "EntryWeeklyCommissions" (
                            "Id", "TenantId", "EntryParticipationId", "CustomerId",
                            "CommissionPeriodId", "HighestCompletedLevel", "TotalAmount",
                            "Currency", "RulesVersion", "CalculatedAt", "PayoutStatus",
                            "ReleasedAt", "ReleaseReason", "CreationTime", "IsDeleted")
                        VALUES (
                            {commissionId}, 1, {participation.Id}, {customer.Id}, {period.Id},
                            1, 150, 'ZAR', {rulesVersion}, {calculatedAt},
                            {(int)WeeklyCommissionPayoutStatus.Released}, {releasedAt},
                            'PostgreSQL payout race fixture.', {calculatedAt}, FALSE);
                        """);
                }
            });

            return commissionId;
        }

        private async Task<Exception> CapturePaymentCallAsync(
            AdminCommissionProgramme programme,
            Guid commissionId,
            string paymentReference)
        {
            try
            {
                await RecordPaymentAsync(
                    programme,
                    commissionId,
                    paymentReference,
                    "Concurrent PostgreSQL payout mutation regression.");
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        private async Task RecordPaymentAsync(
            AdminCommissionProgramme programme,
            Guid commissionId,
            string paymentReference,
            string justification)
        {
            var service = IocManager.Resolve<IAdminCommissionAppService>();
            try
            {
                await service.RecordPaymentAsync(
                    new RecordWeeklyEarningPaymentInput
                    {
                        Id = commissionId,
                        Programme = programme,
                        PaymentReference = paymentReference,
                        Justification = justification
                    });
            }
            finally
            {
                IocManager.Release(service);
            }
        }

        private async Task WaitForAdvisoryLockWaitersAsync(int expectedCount)
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                await using var connection = new NpgsqlConnection(ConnectionString);
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT COUNT(*)
                    FROM pg_stat_activity
                    WHERE datname = current_database()
                      AND wait_event_type = 'Lock'
                      AND wait_event = 'advisory'
                      AND query LIKE '%pg_advisory_xact_lock%';
                    """;
                if (Convert.ToInt32(await command.ExecuteScalarAsync()) >= expectedCount)
                {
                    return;
                }

                await Task.Delay(50);
            }

            throw new InvalidOperationException(
                $"Expected {expectedCount} application-service calls to wait on the PostgreSQL advisory lock.");
        }

        private async Task<PaymentFacts> GetPaymentFactsAsync(
            AdminCommissionProgramme programme,
            Guid commissionId)
        {
            return await WithTenantFiltersDisabledAsync(async context =>
            {
                if (programme == AdminCommissionProgramme.Onyx)
                {
                    var commission = await context.OnyxWeeklyCommissions
                        .SingleAsync(item => item.Id == commissionId);
                    return new PaymentFacts(
                        commission.PayoutStatus,
                        commission.PaymentReference,
                        commission.PaidAt);
                }

                var entryCommission = await context.EntryWeeklyCommissions
                    .SingleAsync(item => item.Id == commissionId);
                return new PaymentFacts(
                    entryCommission.PayoutStatus,
                    entryCommission.PaymentReference,
                    entryCommission.PaidAt);
            });
        }

        private async Task WithTenantFiltersDisabledAsync(
            Func<AqualLifeStyleDbContext, Task> action)
        {
            var unitOfWorkManager = IocManager.Resolve<IUnitOfWorkManager>();
            using (var unitOfWork = unitOfWorkManager.Begin())
            using (unitOfWorkManager.Current.DisableFilter(
                AbpDataFilters.MayHaveTenant,
                AbpDataFilters.MustHaveTenant))
            {
                await UsingDbContextAsync(action);
                await unitOfWork.CompleteAsync();
            }
        }

        private async Task<T> WithTenantFiltersDisabledAsync<T>(
            Func<AqualLifeStyleDbContext, Task<T>> action)
        {
            var unitOfWorkManager = IocManager.Resolve<IUnitOfWorkManager>();
            using (var unitOfWork = unitOfWorkManager.Begin())
            using (unitOfWorkManager.Current.DisableFilter(
                AbpDataFilters.MayHaveTenant,
                AbpDataFilters.MustHaveTenant))
            {
                var result = await UsingDbContextAsync(action);
                await unitOfWork.CompleteAsync();
                return result;
            }
        }

        private static string BuildLockResource(
            AdminCommissionProgramme programme,
            Guid commissionId) =>
            $"weekly-commission-payout:{(programme == AdminCommissionProgramme.Onyx ? "onyx" : "entry")}:{commissionId:N}";

        private static bool IsPostgreSqlRegressionMode() =>
            string.Equals(
                Environment.GetEnvironmentVariable("REPRO_PG"),
                "true",
                StringComparison.OrdinalIgnoreCase);

        private static void WriteProvenanceMarker(
            AdminCommissionProgramme programme)
        {
            var markerDirectory = Environment.GetEnvironmentVariable("REPRO_MARKER_DIR");
            if (string.IsNullOrWhiteSpace(markerDirectory))
            {
                return;
            }

            Directory.CreateDirectory(markerDirectory);
            File.WriteAllText(
                Path.Combine(
                    markerDirectory,
                    $"admin-commission-payout-{programme.ToString().ToLowerInvariant()}-pg.ran"),
                $"PostgreSQL {programme} payout mutation application-service race executed.");
        }

        private static string ConnectionString =>
            Environment.GetEnvironmentVariable("REPRO_PG_CONNECTION") ??
            throw new InvalidOperationException(
                "REPRO_PG_CONNECTION is required for PostgreSQL payout mutation tests.");

        private sealed record PaymentFacts(
            WeeklyCommissionPayoutStatus Status,
            string Reference,
            DateTime? PaidAt);
    }
}
