using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Abp;
using Abp.Authorization.Users;
using Abp.Events.Bus;
using Abp.Events.Bus.Entities;
using Abp.MultiTenancy;
using Abp.Runtime.Session;
using Abp.TestBase;
using AqualLifeStyle.Authorization.Roles;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.EntityFrameworkCore;
using AqualLifeStyle.EntityFrameworkCore.Seed.Host;
using AqualLifeStyle.EntityFrameworkCore.Seed.Tenants;
using AqualLifeStyle.MultiTenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
using Npgsql;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.EntityFrameworkCore
{
    public class WeeklyCommissionCalculationPostgreSqlTests : IAsyncLifetime
    {
        private const string PostgresImage = "postgres:16-alpine";
        private readonly string _containerName =
            $"weekly-commission-calculation-pg-{Guid.NewGuid():N}";
        private readonly string _databaseName = $"weekly_commission_test_{Guid.NewGuid():N}";
        private readonly int _hostPort;

        public WeeklyCommissionCalculationPostgreSqlTests()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            _hostPort = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
        }

        public async Task InitializeAsync()
        {
            await StartPostgreSqlContainerAsync();
            await CreateTestDatabaseAsync();
            await MigrateToLatestAsync();
        }

        public Task DisposeAsync()
        {
            return StopPostgreSqlContainerAsync();
        }

        [Fact]
        public async Task ClosedWeek_CalculationRoundTrips_AndDuplicatePeriodIsRejected()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var customerId = await SeedCustomerAndUserAsync(suffix);
            var participationId = await SeedQualifiedEntryParticipationAsync(customerId, suffix);
            var periodStart = new DateTime(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc);
            var periodEnd = new DateTime(2026, 8, 13, 23, 59, 59, 999, DateTimeKind.Utc);

            await using (var context = CreateDbContext())
            {
                var participation = await context.EntryParticipations.SingleAsync(p => p.Id == participationId);
                var termsVersion = EntryCommissionTermsVersion.Create(
                    $"test-entry-commission-2026-08",
                    new DateTime(2026, 7, 16, 22, 0, 0, DateTimeKind.Utc),
                    150m, 250m, 1250m);
                context.EntryCommissionTermsVersions.Add(termsVersion);

                var areaRecord = AreaActivationStateRecord.Record(
                    Guid.NewGuid(), 1, true,
                    new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                    null, "PostgreSQL weekly commission test baseline",
                    AreaActivationStateRecordKind.ObservedBaseline);
                context.AreaActivationStateRecords.Add(areaRecord);
                await context.SaveChangesAsync();

                var terms = termsVersion.ToTerms();
                var period = EntryCommissionPeriod.CreateClosedPeriod(
                    1, periodStart, periodEnd, "Africa/Johannesburg",
                    periodEnd.AddMinutes(1), terms);
                context.EntryCommissionPeriods.Add(period);

                var network = await context.EntryParticipations
                    .Include(p => p.RecruiterCorrections)
                    .Where(p => p.TenantId == 1 && p.Status == EntryParticipationStatus.Active)
                    .ToListAsync();
                CompleteInMemoryNetworkToLevelFive(
                    participation,
                    network,
                    suffix);
                var obligations = await context.EntryMonthlyObligations
                    .Where(o => network.Select(n => n.Id).Contains(o.EntryParticipationId))
                    .ToListAsync();

                var calculator = new EntryWeeklyCommissionCalculator(new EntryNetworkQualificationEvaluator());
                var commission = calculator.Calculate(
                    participation, period, terms, network, obligations);

                context.EntryWeeklyCommissions.Add(commission);
                await context.SaveChangesAsync();
            }

            var firstPeriodCount = await CountAsync("EntryCommissionPeriods");
            var firstCommissionCount = await CountAsync("EntryWeeklyCommissions");
            firstPeriodCount.ShouldBe(1);
            firstCommissionCount.ShouldBe(1);

            await using (var context = CreateDbContext())
            {
                var existingPeriod = await context.EntryCommissionPeriods
                    .FirstOrDefaultAsync(p =>
                        p.TenantId == 1 &&
                        p.PeriodStart == periodStart &&
                        p.PeriodEnd == periodEnd);
                existingPeriod.ShouldNotBeNull();

                var persistedCommission = await context.EntryWeeklyCommissions
                    .Include(commission => commission.Components)
                    .SingleAsync();
                persistedCommission.HighestQualifiedNetworkLevel.ShouldBe(5);
                persistedCommission.HighestCommissionedLevel.ShouldBe(3);
                persistedCommission.TotalAmount.ShouldBe(1650m);
                persistedCommission.Components
                    .Select(component => component.Level)
                    .OrderBy(level => level)
                    .ShouldBe(new[] { 1, 2, 3 });
            }

            await Assert.ThrowsAsync<DbUpdateException>(async () =>
            {
                await CalculateAndPersistDuplicateRunAsync(periodStart, periodEnd);
            });

            var secondPeriodCount = await CountAsync("EntryCommissionPeriods");
            var secondCommissionCount = await CountAsync("EntryWeeklyCommissions");
            secondPeriodCount.ShouldBe(firstPeriodCount);
            secondCommissionCount.ShouldBe(firstCommissionCount);
        }

        [Fact]
        public async Task TenantScopedQuery_ExcludesCrossTenantFifthRecruitAndLedger()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var customerId = await SeedCustomerAndUserAsync(suffix);
            var participationId = await SeedQualifiedEntryParticipationAsync(
                customerId,
                suffix,
                directRecruitCount: EntryNetworkQualificationEvaluator.BranchSize - 1);
            var rootCustomerId = await GetParticipationCustomerIdAsync(participationId);
            var crossTenantParticipationId = await SeedCrossTenantRecruitAsync(
                rootCustomerId,
                suffix);
            var periodStart = new DateTime(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc);
            var periodEnd = new DateTime(2026, 8, 13, 23, 59, 59, 999, DateTimeKind.Utc);

            await using (var context = CreateDbContext())
            {
                var network = await context.EntryParticipations
                    .Include(participation => participation.RecruiterCorrections)
                    .Where(participation =>
                        participation.TenantId == 1 &&
                        participation.Status == EntryParticipationStatus.Active)
                    .ToListAsync();
                network.Count.ShouldBe(5);
                network.ShouldNotContain(participation =>
                    participation.Id == crossTenantParticipationId);

                var termsVersion = EntryCommissionTermsVersion.Create(
                    $"tenant-safe-{suffix}",
                    new DateTime(2026, 7, 16, 22, 0, 0, DateTimeKind.Utc),
                    150m,
                    250m,
                    1250m);
                context.EntryCommissionTermsVersions.Add(termsVersion);
                var period = EntryCommissionPeriod.CreateClosedPeriod(
                    1,
                    periodStart,
                    periodEnd,
                    "Africa/Johannesburg",
                    periodEnd.AddMinutes(1),
                    termsVersion.ToTerms());
                context.EntryCommissionPeriods.Add(period);

                var effectiveNetwork = EffectiveProgrammeNetwork.BuildAQGreen(
                    1,
                    network,
                    periodEnd);
                var obligations = await context.EntryMonthlyObligations
                    .Where(obligation => network
                        .Select(participation => participation.Id)
                        .Contains(obligation.EntryParticipationId))
                    .ToListAsync();
                var calculator = new EntryWeeklyCommissionCalculator(
                    new EntryNetworkQualificationEvaluator());
                var commissions = network
                    .Select(participation => calculator.Calculate(
                        participation,
                        period,
                        termsVersion.ToTerms(),
                        effectiveNetwork,
                        obligations))
                    .ToList();
                context.EntryWeeklyCommissions.AddRange(commissions);
                await context.SaveChangesAsync();
            }

            await using (var context = CreateDbContext())
            {
                var period = await context.EntryCommissionPeriods
                    .SingleAsync(item =>
                        item.TenantId == 1 &&
                        item.PeriodStart == periodStart &&
                        item.PeriodEnd == periodEnd);
                var commissions = await context.EntryWeeklyCommissions
                    .Where(item => item.CommissionPeriodId == period.Id)
                    .ToListAsync();
                commissions.Count.ShouldBe(5);
                commissions.All(item => item.TenantId == 1).ShouldBeTrue();
                commissions.ShouldNotContain(item =>
                    item.EntryParticipationId == crossTenantParticipationId);
                commissions.Single(item =>
                        item.EntryParticipationId == participationId)
                    .HighestQualifiedNetworkLevel.ShouldBe(0);
            }
        }

        private async Task StartPostgreSqlContainerAsync()
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments =
                    $"run -d --name {_containerName} -e POSTGRES_DB=postgres -e POSTGRES_USER=aqualifestyle -e POSTGRES_PASSWORD=aqualifestyle -p {_hostPort}:5432 {PostgresImage}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start Docker process for PostgreSQL.");
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Docker run failed: {error}");
            }

            TraceLine($"Started PostgreSQL container: {output.Trim()} on port {_hostPort}");

            for (var attempt = 0; attempt < 30; attempt++)
            {
                try
                {
                    await using var connection = new NpgsqlConnection(BuildAdminConnectionString());
                    await connection.OpenAsync();
                    TraceLine("PostgreSQL is ready.");
                    return;
                }
                catch
                {
                    await Task.Delay(1000);
                }
            }

            throw new InvalidOperationException("PostgreSQL container did not become ready in time.");
        }

        private async Task StopPostgreSqlContainerAsync()
        {
            var stopInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"rm -f {_containerName}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using var process = Process.Start(stopInfo);
            if (process == null)
            {
                return;
            }

            await process.StandardOutput.ReadToEndAsync();
            await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            TraceLine($"Stopped PostgreSQL container: {_containerName}");
        }

        private string BuildAdminConnectionString() =>
            $"Host=localhost;Port={_hostPort};Database=postgres;Username=aqualifestyle;Password=aqualifestyle";

        private string BuildTestConnectionString() =>
            $"Host=localhost;Port={_hostPort};Database={_databaseName};Username=aqualifestyle;Password=aqualifestyle";

        private AqualLifeStyleDbContext CreateDbContext()
        {
            var optionsBuilder = new DbContextOptionsBuilder<AqualLifeStyleDbContext>();
            optionsBuilder.UseNpgsql(BuildTestConnectionString());
            return new AqualLifeStyleDbContext(optionsBuilder.Options);
        }

        private async Task CreateTestDatabaseAsync()
        {
            await using var connection = new NpgsqlConnection(BuildAdminConnectionString());
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"""CREATE DATABASE "{_databaseName}" WITH OWNER = aqualifestyle;""";
            await command.ExecuteNonQueryAsync();
            TraceLine($"Created test database: {_databaseName}");
        }

        private async Task MigrateToLatestAsync()
        {
            await using var context = CreateDbContext();
            await context.Database.MigrateAsync();

            var defaultTenant = context.Tenants.IgnoreQueryFilters().FirstOrDefault(t => t.TenancyName == "Default");
            if (defaultTenant == null)
            {
                defaultTenant = new Tenant("Default", "Default");
                context.Tenants.Add(defaultTenant);
                await context.SaveChangesAsync();
            }

            TraceLine("Migrated to latest.");
        }

        private async Task<long> CountAsync(string table)
        {
            await using var connection = new NpgsqlConnection(BuildTestConnectionString());
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"""SELECT COUNT(*) FROM "{table}" """, connection);
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt64(result);
        }

        private async Task<int> SeedCustomerAndUserAsync(string suffix)
        {
            await using var context = CreateDbContext();
            var userName = $"wc-{suffix}";
            var user = new User
            {
                TenantId = 1,
                UserName = userName,
                EmailAddress = $"{userName}@t.test",
                Name = "Weekly",
                Surname = "Commission",
                IsEmailConfirmed = true,
                IsActive = true
            };
            user.SetNormalizedNames();
            var passwordHasher = new PasswordHasher<User>(
                new OptionsWrapper<PasswordHasherOptions>(
                    new PasswordHasherOptions()));
            user.Password = passwordHasher.HashPassword(user, User.DefaultPassword);
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var customer = Customer.Create(
                1,
                user.Id,
                $"Weekly Commission Member {suffix}",
                new AqualLifeStyle.Domain.Common.EmailAddress($"{userName}@t.test"));
            context.Customers.Add(customer);

            var adminRole = context.Roles.IgnoreQueryFilters().FirstOrDefault(r => r.TenantId == 1 && r.Name == StaticRoleNames.Tenants.Admin);
            if (adminRole != null)
            {
                context.UserRoles.Add(new UserRole(1, user.Id, adminRole.Id));
            }

            await context.SaveChangesAsync();
            return customer.Id;
        }

        private async Task<Guid> SeedQualifiedEntryParticipationAsync(
            int customerId,
            string suffix,
            int directRecruitCount = EntryNetworkQualificationEvaluator.BranchSize)
        {
            await using var context = CreateDbContext();
            var terms = EntryProgrammeTerms.Create(
                $"entry-2026-08-{suffix}",
                new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                registrationPaymentAmount: 600m,
                activationPaymentAmount: 600m,
                monthlyCommitmentAmount: 600m,
                gracePeriodDays: 7);

            var mainCustomer = await context.Customers.SingleAsync(c => c.Id == customerId);
            var participation = EntryParticipation.StartIndependently(
                1,
                mainCustomer.Id,
                terms,
                new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc));

            var registrationPayment = MemberPayment.CreatePending(
                1,
                mainCustomer.Id,
                MemberPaymentPurpose.EntryRegistration,
                600m,
                "Test",
                $"commission-registration-{suffix}",
                new DateTime(2026, 8, 1, 9, 1, 0, DateTimeKind.Utc));
            registrationPayment.Confirm(new DateTime(2026, 8, 1, 9, 2, 0, DateTimeKind.Utc));
            participation.ApplyConfirmedActivationPayment(registrationPayment);

            var activationPayment = MemberPayment.CreatePending(
                1,
                mainCustomer.Id,
                MemberPaymentPurpose.EntryActivation,
                600m,
                "Test",
                $"commission-activation-{suffix}",
                new DateTime(2026, 8, 1, 9, 3, 0, DateTimeKind.Utc));
            activationPayment.Confirm(new DateTime(2026, 8, 1, 9, 4, 0, DateTimeKind.Utc));
            participation.ApplyConfirmedActivationPayment(activationPayment);
            participation.ApproveByAdministrator(1, new DateTime(2026, 8, 1, 9, 5, 0, DateTimeKind.Utc));

            context.MemberPayments.AddRange(registrationPayment, activationPayment);
            context.EntryParticipations.Add(participation);
            await context.SaveChangesAsync();

            for (var index = 0; index < directRecruitCount; index++)
            {
                var recruitUserName = $"wc-r{index}-{suffix}";
                var recruitUser = new User
                {
                    TenantId = 1,
                    UserName = recruitUserName,
                    EmailAddress = $"{recruitUserName}@t.test",
                    Name = "Recruit",
                    Surname = index.ToString(),
                    IsEmailConfirmed = true,
                    IsActive = true
                };
                recruitUser.SetNormalizedNames();
                var recruitPasswordHasher = new PasswordHasher<User>(
                    new OptionsWrapper<PasswordHasherOptions>(
                        new PasswordHasherOptions()));
                recruitUser.Password = recruitPasswordHasher.HashPassword(recruitUser, User.DefaultPassword);
                context.Users.Add(recruitUser);
                await context.SaveChangesAsync();

                var recruitCustomer = Customer.Create(
                    1,
                    recruitUser.Id,
                    $"Weekly Commission Recruit {index}",
                    new AqualLifeStyle.Domain.Common.EmailAddress($"{recruitUserName}@t.test"));
                context.Customers.Add(recruitCustomer);
                await context.SaveChangesAsync();

                var recruit = EntryParticipation.StartUnderRecruiter(
                    1,
                    recruitCustomer.Id,
                    participation,
                    terms,
                    new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc));

                var recruitRegistration = MemberPayment.CreatePending(
                    1,
                    recruitCustomer.Id,
                    MemberPaymentPurpose.EntryRegistration,
                    600m,
                    "Test",
                    $"commission-recruit-registration-{index}-{suffix}",
                    new DateTime(2026, 8, 1, 9, 1, 0, DateTimeKind.Utc));
                recruitRegistration.Confirm(new DateTime(2026, 8, 1, 9, 2, 0, DateTimeKind.Utc));
                recruit.ApplyConfirmedActivationPayment(recruitRegistration);

                var recruitActivation = MemberPayment.CreatePending(
                    1,
                    recruitCustomer.Id,
                    MemberPaymentPurpose.EntryActivation,
                    600m,
                    "Test",
                    $"commission-recruit-activation-{index}-{suffix}",
                    new DateTime(2026, 8, 1, 9, 3, 0, DateTimeKind.Utc));
                recruitActivation.Confirm(new DateTime(2026, 8, 1, 9, 4, 0, DateTimeKind.Utc));
                recruit.ApplyConfirmedActivationPayment(recruitActivation);
                recruit.ApproveByAdministrator(1, new DateTime(2026, 8, 1, 9, 5, 0, DateTimeKind.Utc));

                context.MemberPayments.AddRange(recruitRegistration, recruitActivation);
                context.EntryParticipations.Add(recruit);
                await context.SaveChangesAsync();
            }

            return participation.Id;
        }

        private async Task<int> GetParticipationCustomerIdAsync(Guid participationId)
        {
            await using var context = CreateDbContext();
            return await context.EntryParticipations
                .Where(participation => participation.Id == participationId)
                .Select(participation => participation.CustomerId)
                .SingleAsync();
        }

        private async Task<Guid> SeedCrossTenantRecruitAsync(
            int recruiterCustomerId,
            string suffix)
        {
            await using var context = CreateDbContext();
            var tenant = new Tenant($"OtherTenant{suffix}", $"Other Tenant {suffix}");
            context.Tenants.Add(tenant);
            await context.SaveChangesAsync();

            var userName = $"wc-cross-{suffix}";
            var user = new User
            {
                TenantId = tenant.Id,
                UserName = userName,
                EmailAddress = $"{userName}@t.test",
                Name = "Cross",
                Surname = "Tenant",
                IsEmailConfirmed = true,
                IsActive = true
            };
            user.SetNormalizedNames();
            var passwordHasher = new PasswordHasher<User>(
                new OptionsWrapper<PasswordHasherOptions>(
                    new PasswordHasherOptions()));
            user.Password = passwordHasher.HashPassword(user, User.DefaultPassword);
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var customer = Customer.Create(
                tenant.Id,
                user.Id,
                $"Cross Tenant Recruit {suffix}",
                new AqualLifeStyle.Domain.Common.EmailAddress($"{userName}@t.test"));
            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            var terms = EntryProgrammeTerms.CreateSingleJoiningPayment(
                $"entry-cross-tenant-{suffix}",
                new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                1200m,
                600m,
                7);
            var participation = EntryParticipation.StartIndependently(
                tenant.Id,
                customer.Id,
                terms,
                new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc));

            // Simulate an already-corrupt stored relationship. New domain and
            // application paths reject this cross-tenant placement before it is set.
            var recruiterProperty = typeof(EntryParticipation)
                .GetProperty(nameof(EntryParticipation.RecruiterCustomerId));
            recruiterProperty.ShouldNotBeNull();
            recruiterProperty.SetValue(participation, recruiterCustomerId);

            var payment = MemberPayment.CreatePending(
                tenant.Id,
                customer.Id,
                MemberPaymentPurpose.AQGreenJoining,
                1200m,
                "Test",
                $"commission-cross-tenant-{suffix}",
                new DateTime(2026, 8, 1, 9, 1, 0, DateTimeKind.Utc));
            payment.Confirm(new DateTime(2026, 8, 1, 9, 2, 0, DateTimeKind.Utc));
            participation.ApplyConfirmedJoiningPayment(payment);
            participation.ApproveByAdministrator(
                1,
                new DateTime(2026, 8, 1, 9, 3, 0, DateTimeKind.Utc));

            context.MemberPayments.Add(payment);
            context.EntryParticipations.Add(participation);
            await context.SaveChangesAsync();
            return participation.Id;
        }

        private static void CompleteInMemoryNetworkToLevelFive(
            EntryParticipation root,
            List<EntryParticipation> network,
            string suffix)
        {
            var structuralTerms = EntryProgrammeTerms.CreateSingleJoiningPayment(
                version: $"entry-level-five-{suffix}",
                effectiveFrom: new DateTime(
                    2026,
                    7,
                    1,
                    0,
                    0,
                    0,
                    DateTimeKind.Utc),
                joiningPaymentAmount: 1200m,
                monthlyCommitmentAmount: 600m,
                gracePeriodDays: 7);
            var currentLevel = network
                .Where(participation =>
                    participation.RecruiterCustomerId == root.CustomerId)
                .OrderBy(participation => participation.ActivatedAt)
                .ThenBy(participation => participation.Id)
                .Take(EntryNetworkQualificationEvaluator.BranchSize)
                .ToList();
            currentLevel.Count.ShouldBe(
                EntryNetworkQualificationEvaluator.BranchSize);
            var nextCustomerId = 100000;

            for (var depth = 2;
                 depth <= EntryNetworkQualificationEvaluator.MaximumLevel;
                 depth++)
            {
                var nextLevel = new List<EntryParticipation>();
                foreach (var recruiter in currentLevel)
                {
                    for (var index = 0;
                         index < EntryNetworkQualificationEvaluator.BranchSize;
                         index++)
                    {
                        var startedAt = new DateTime(
                            2026,
                            8,
                            1,
                            9,
                            depth,
                            0,
                            DateTimeKind.Utc);
                        var recruit = EntryParticipation.StartUnderRecruiter(
                            1,
                            nextCustomerId,
                            recruiter,
                            structuralTerms,
                            startedAt);
                        var payment = MemberPayment.CreatePending(
                            1,
                            nextCustomerId,
                            MemberPaymentPurpose.AQGreenJoining,
                            1200m,
                            "Test",
                            $"commission-level-five-{suffix}-{nextCustomerId}",
                            startedAt.AddSeconds(1));
                        payment.Confirm(startedAt.AddSeconds(2));
                        recruit.ApplyConfirmedJoiningPayment(payment);
                        recruit.ApproveByAdministrator(
                            1,
                            startedAt.AddSeconds(3));
                        network.Add(recruit);
                        nextLevel.Add(recruit);
                        nextCustomerId++;
                    }
                }

                currentLevel = nextLevel;
            }
        }

        private async Task<Guid> CalculateAndPersistFirstRunAsync(Guid participationId)
        {
            await using var context = CreateDbContext();
            var participation = await context.EntryParticipations.SingleAsync(p => p.Id == participationId);
            var termsVersion = EntryCommissionTermsVersion.Create(
                $"test-entry-commission-2026-08",
                new DateTime(2026, 7, 16, 22, 0, 0, DateTimeKind.Utc),
                150m,
                250m,
                1250m);
            context.EntryCommissionTermsVersions.Add(termsVersion);

            var areaRecord = AreaActivationStateRecord.Record(
                Guid.NewGuid(),
                1,
                true,
                new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                null,
                "PostgreSQL weekly commission test baseline",
                AreaActivationStateRecordKind.ObservedBaseline);
            context.AreaActivationStateRecords.Add(areaRecord);
            await context.SaveChangesAsync();

            var terms = termsVersion.ToTerms();
            var periodStart = new DateTime(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc);
            var periodEnd = new DateTime(2026, 8, 13, 23, 59, 59, 999, DateTimeKind.Utc);
            var period = EntryCommissionPeriod.CreateClosedPeriod(
                1,
                periodStart,
                periodEnd,
                "Africa/Johannesburg",
                periodEnd.AddMinutes(1),
                terms);
            context.EntryCommissionPeriods.Add(period);

            var network = await context.EntryParticipations
                .Include(p => p.RecruiterCorrections)
                .Where(p => p.TenantId == 1 && p.Status == EntryParticipationStatus.Active)
                .ToListAsync();
            var obligations = await context.EntryMonthlyObligations
                .Where(o => network.Select(n => n.Id).Contains(o.EntryParticipationId))
                .ToListAsync();

            var calculator = new EntryWeeklyCommissionCalculator(new EntryNetworkQualificationEvaluator());
            var commission = calculator.Calculate(
                participation,
                period,
                terms,
                network,
                obligations);

            context.EntryWeeklyCommissions.Add(commission);
            await context.SaveChangesAsync();
            return period.Id;
        }

        private async Task CalculateAndPersistDuplicateRunAsync(DateTime periodStart, DateTime periodEnd)
        {
            await using var context = CreateDbContext();
            var participation = await context.EntryParticipations
                .FirstAsync(p => p.TenantId == 1 && p.Status == EntryParticipationStatus.Active);
            var termsVersion = await context.EntryCommissionTermsVersions.SingleAsync();
            var terms = termsVersion.ToTerms();

            var duplicatePeriod = EntryCommissionPeriod.CreateClosedPeriod(
                1,
                periodStart,
                periodEnd,
                "Africa/Johannesburg",
                periodEnd.AddMinutes(1),
                terms);
            context.EntryCommissionPeriods.Add(duplicatePeriod);

            var network = await context.EntryParticipations
                .Include(p => p.RecruiterCorrections)
                .Where(p => p.TenantId == 1 && p.Status == EntryParticipationStatus.Active)
                .ToListAsync();
            var obligations = await context.EntryMonthlyObligations
                .Where(o => network.Select(n => n.Id).Contains(o.EntryParticipationId))
                .ToListAsync();

            var calculator = new EntryWeeklyCommissionCalculator(new EntryNetworkQualificationEvaluator());
            var commission = calculator.Calculate(
                participation,
                duplicatePeriod,
                terms,
                network,
                obligations);

            context.EntryWeeklyCommissions.Add(commission);
            await context.SaveChangesAsync();
        }

        private void TraceLine(string message)
        {
            Console.WriteLine($"[WeeklyCommissionPostgreSqlTest] {message}");
        }
    }
}
