using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Abp.Authorization.Roles;
using Abp.Authorization.Users;
using Abp.Domain.Uow;
using Abp.EntityFrameworkCore;
using AqualLifeStyle.Application.Admin.ProgrammeParticipations;
using AqualLifeStyle.Application.Admin.ProgrammeParticipations.Dto;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Authorization.Roles;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Areas;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.EntityFrameworkCore;
using AqualLifeStyle.Models.TokenAuth;
using AqualLifeStyle.MultiTenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Web.Tests.Controllers
{
    public sealed class DiscoveryPostgreSqlFactAttribute : FactAttribute
    {
        public DiscoveryPostgreSqlFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("REPRO_PG") != "true" ||
                Environment.GetEnvironmentVariable("REPRO_PG_SCHEMA_FROM_MODEL") != "true")
                Skip = "Requires disposable PostgreSQL with model-created schema; migrations are not executed.";
        }
    }

    public sealed class DiscoveryPostgreSqlTheoryAttribute : TheoryAttribute
    {
        public DiscoveryPostgreSqlTheoryAttribute()
        {
            if (Environment.GetEnvironmentVariable("REPRO_PG") != "true" ||
                Environment.GetEnvironmentVariable("REPRO_PG_SCHEMA_FROM_MODEL") != "true")
                Skip = "Requires disposable PostgreSQL with model-created schema; migrations are not executed.";
        }
    }

    [Collection("Authentication web journeys")]
    public sealed partial class AQGreenExistingMemberDiscoveryPostgreSqlTests : AqualLifeStyleWebTestBase
    {
        private const string Endpoint = "/api/services/app/AdminProgrammeParticipation/GetLegacyAQGreenReconciliation";
        private const string Password = "SyntheticDiscovery123!";
        private static readonly DateTime RecordedAt = new(2026, 8, 27, 8, 0, 0, DateTimeKind.Utc);
        private static readonly EntryProgrammeTerms Terms = EntryProgrammeTerms.CreateSingleJoiningPayment(
            "discovery-synthetic", RecordedAt.AddDays(-1), 1200m, 600m, 7);
        private static readonly PasswordHasher<User> Hasher = new(Options.Create(new PasswordHasherOptions()));

        [DiscoveryPostgreSqlFact]
        public async Task LegacyOnlyAssignment_IsDiscoverableWithoutInventedParticipationOrHistory()
        {
            var fixture = await SeedAsync();
            await SignInAsync(fixture.Reviewer);
            var result = await ReadAsync();
            var row = Row(result, fixture.LegacyOnly.CustomerId);
            row.GetProperty("discoveryKind").GetInt32().ShouldBe((int)LegacyAQGreenDiscoveryKind.LegacyAssignmentWithoutV2Participation);
            row.GetProperty("participationVersionEvidence").GetInt32().ShouldBe((int)AQGreenParticipationVersionEvidence.NoLiveParticipation);
            foreach (var name in new[] { "participationId", "termsVersion", "joiningAmount", "registrationAmount",
                "activationAmount", "monthlySubscriptionAmount", "joiningPaymentId" })
                row.GetProperty(name).ValueKind.ShouldBe(JsonValueKind.Null);
            Issues(row).ShouldContain("NO_LIVE_AQGREEN_PARTICIPATION");
            Issues(row).ShouldContain("SPONSOR_HISTORY_UNVERIFIABLE");
            Issues(row).ShouldContain("ACTIVATION_HISTORY_UNVERIFIABLE");
            Issues(row).ShouldContain("HISTORICAL_JOINING_PAYMENT_UNVERIFIABLE");
            row.GetProperty("verifiedPayments").GetArrayLength().ShouldBe(0);
        }

        [DiscoveryPostgreSqlFact]
        public async Task OverlappingLegacyAndV2Records_HaveOneStableDiscoveryIdentity()
        {
            var fixture = await SeedAsync();
            await SignInAsync(fixture.Reviewer);
            var first = await ReadAsync();
            var retry = await ReadAsync();
            var row = Row(first, fixture.Both.CustomerId);
            row.GetProperty("discoveryKind").GetInt32().ShouldBe((int)LegacyAQGreenDiscoveryKind.LegacyAssignmentWithV2Participation);
            row.GetProperty("participationId").GetGuid().ShouldBe(fixture.Both.ParticipationId.Value);
            row.GetProperty("discoveryId").GetString().ShouldBe($"aqgreen:1:{fixture.Both.CustomerId}");
            Row(retry, fixture.Both.CustomerId).GetProperty("discoveryId").GetString()
                .ShouldBe(row.GetProperty("discoveryId").GetString());
            Items(first).Count(item => item.GetProperty("customerId").GetInt32() == fixture.Both.CustomerId).ShouldBe(1);
            row.GetProperty("verifiedPayments").GetArrayLength().ShouldBe(2);
            row.GetProperty("checkoutAttempts").GetArrayLength().ShouldBe(1);
        }

        [DiscoveryPostgreSqlFact]
        public async Task ParticipationVersionAndMissingV2Evidence_AreExplicit()
        {
            var fixture = await SeedAsync();
            await SignInAsync(fixture.Reviewer);
            var result = await ReadAsync();
            var v2 = Row(result, fixture.V2Only.CustomerId);
            v2.GetProperty("discoveryKind").GetInt32().ShouldBe((int)LegacyAQGreenDiscoveryKind.V2ParticipationRequiringReconciliation);
            Issues(v2).ShouldContain("ATTRIBUTION_CONFIRMATION_MISSING");
            Issues(v2).ShouldContain("ACTIVATION_HISTORY_UNVERIFIABLE");
            var unknown = Row(result, fixture.Unversioned.CustomerId);
            unknown.GetProperty("discoveryKind").GetInt32().ShouldBe((int)LegacyAQGreenDiscoveryKind.HistoricalInformationUnverifiable);
            unknown.GetProperty("participationVersionEvidence").GetInt32().ShouldBe((int)AQGreenParticipationVersionEvidence.VersionUnverifiable);
            Issues(unknown).ShouldContain("PARTICIPATION_VERSION_UNVERIFIABLE");
            Issues(unknown).ShouldContain("SPONSOR_HISTORY_UNVERIFIABLE");
        }

        [DiscoveryPostgreSqlTheory]
        [InlineData("amount", "LINKED_JOINING_PAYMENT_FACTS_CONFLICT")]
        [InlineData("time", "CONFIRMED_PAYMENT_TIME_MISSING")]
        [InlineData("owner", "LINKED_JOINING_PAYMENT_UNVERIFIABLE")]
        [InlineData("tenant", "LINKED_JOINING_PAYMENT_UNVERIFIABLE")]
        public async Task ConflictingOrUnavailablePaymentFacts_AreReportedWithoutForeignDetails(string corruption, string issue)
        {
            var fixture = await SeedAsync();
            await UsingDbContextAsync(async context =>
            {
                if (corruption == "amount")
                    await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"MemberPayments\" SET \"Amount\" = 900 WHERE \"Id\" = {fixture.JoiningPaymentId}");
                if (corruption == "time")
                    await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"MemberPayments\" SET \"ConfirmedAt\" = NULL WHERE \"Id\" = {fixture.JoiningPaymentId}");
                if (corruption == "owner")
                    await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"MemberPayments\" SET \"CustomerId\" = {fixture.OtherArea.CustomerId} WHERE \"Id\" = {fixture.JoiningPaymentId}");
                if (corruption == "tenant")
                    await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"MemberPayments\" SET \"TenantId\" = {fixture.ForeignTenantId} WHERE \"Id\" = {fixture.JoiningPaymentId}");
            });
            await SignInAsync(fixture.Reviewer);
            var row = Row(await ReadAsync(), fixture.Both.CustomerId);
            Issues(row).ShouldContain(issue);
            if (corruption is "owner" or "tenant")
            {
                row.GetProperty("joiningPaymentId").ValueKind.ShouldBe(JsonValueKind.Null);
                row.GetProperty("verifiedPayments").EnumerateArray().ShouldNotContain(item =>
                    item.GetProperty("paymentId").GetGuid() == fixture.JoiningPaymentId);
            }
            if (corruption == "time")
                row.GetProperty("verifiedPayments").EnumerateArray().Single(item =>
                    item.GetProperty("paymentId").GetGuid() == fixture.JoiningPaymentId)
                    .GetProperty("confirmedAt").ValueKind.ShouldBe(JsonValueKind.Null);
        }

        [DiscoveryPostgreSqlFact]
        public async Task CoherentRecordedV2Facts_AreNotClassifiedAsAReconciliationDefect()
        {
            var fixture = await SeedAsync();
            await UsingDbContextAsync(async context =>
            {
                var entry = await context.EntryParticipations.IgnoreQueryFilters()
                    .SingleAsync(item => item.Id == fixture.V2Only.ParticipationId);
                var payment = MemberPayment.CreatePending(1, fixture.V2Only.CustomerId,
                    MemberPaymentPurpose.AQGreenJoining, 1200m, "Synthetic", Guid.NewGuid().ToString("N"), RecordedAt);
                payment.Confirm(RecordedAt.AddMinutes(1));
                context.MemberPayments.Add(payment);
                entry.ApplyConfirmedJoiningPayment(payment);
                entry.ApproveByAdministrator(fixture.Reviewer.Id, RecordedAt.AddMinutes(2));
                var attribution = await context.AQGreenRecruitmentAttributions.IgnoreQueryFilters()
                    .SingleAsync(item => item.ParticipantId == entry.Id);
                context.AQGreenRecruitmentAttributionConfirmations.Add(AQGreenRecruitmentAttributionConfirmation.Confirm(
                    attribution, RecordedAt, fixture.Reviewer.Id,
                    AQGreenAttributionConfirmationMethod.AuthorisedProspectiveRootConfirmation,
                    attribution.SourceReferenceId, AQGreenRecruitmentAttributionRules.CurrentVersion));
                var scope = AQGreenPlacementTreeScope.Create(1);
                context.AQGreenPlacementTreeScopes.Add(scope);
                context.AQGreenNetworkPlacements.Add(AQGreenNetworkPlacement.CreateRoot(
                    scope, entry.Id, entry.ActivatedAt.Value, AQGreenPlacementRules.CurrentVersion));
            });
            await SignInAsync(fixture.Reviewer);
            var row = Row(await ReadAsync(), fixture.V2Only.CustomerId);
            row.GetProperty("discoveryKind").GetInt32().ShouldBe((int)LegacyAQGreenDiscoveryKind.V2RecordedFacts);
            Issues(row).ShouldBeEmpty();
        }

        [DiscoveryPostgreSqlFact]
        public async Task UnverifiableMembershipOwnership_IsVisibleAsUncertaintyWithoutForeignMembershipData()
        {
            var fixture = await SeedAsync();
            await UsingDbContextAsync(async context =>
            {
                var foreignMembership = Membership.Create(fixture.ForeignTenantId, "Synthetic foreign membership",
                    "Not an authorized AQGreen assignment", MembershipType.AQGreen);
                context.Memberships.Add(foreignMembership);
                await context.SaveChangesAsync();
                var customer = await context.Customers.IgnoreQueryFilters().SingleAsync(item => item.Id == fixture.LegacyOnly.CustomerId);
                customer.ChangeMembership(foreignMembership.Id);
            });
            await SignInAsync(fixture.Reviewer);
            var row = Row(await ReadAsync(), fixture.LegacyOnly.CustomerId);
            row.GetProperty("discoveryKind").GetInt32().ShouldBe((int)LegacyAQGreenDiscoveryKind.HistoricalInformationUnverifiable);
            Issues(row).ShouldContain("MEMBERSHIP_ASSIGNMENT_UNVERIFIABLE");
            row.GetProperty("legacyMembershipId").ValueKind.ShouldBe(JsonValueKind.Null);
        }

        [DiscoveryPostgreSqlFact]
        public async Task TenantAndAreaScopes_ApplyBeforeCountAndPagination_ForBothSources()
        {
            var fixture = await SeedAsync();
            await SignInAsync(fixture.Reviewer);
            var result = await ReadAsync();
            result.GetProperty("totalCount").GetInt32().ShouldBe(4);
            Items(result).ShouldAllBe(item => item.GetProperty("tenantId").GetInt32() == 1 &&
                item.GetProperty("currentAreaId").GetGuid() == fixture.AreaId);
            Items(result).ShouldNotContain(item => item.GetProperty("customerId").GetInt32() == fixture.OtherArea.CustomerId);
            Items(result).ShouldNotContain(item => item.GetProperty("customerId").GetInt32() == fixture.OtherAreaV2.CustomerId);
            Items(result).ShouldNotContain(item => item.GetProperty("customerId").GetInt32() == fixture.Foreign.CustomerId);
            await ReadAsync($"TenantId={fixture.ForeignTenantId}", HttpStatusCode.Forbidden);
            await UsingDbContextAsync(async context =>
            {
                var assignment = await context.AreaAdminAssignments.IgnoreQueryFilters()
                    .SingleAsync(item => item.Id == fixture.AssignmentId);
                assignment.Revoke(DateTime.UtcNow);
            });
            (await ReadAsync()).GetProperty("totalCount").GetInt32().ShouldBe(0);
        }

        [DiscoveryPostgreSqlFact]
        public async Task PermissionAndHostBroadScope_AreRequired()
        {
            var denied = await SeedAsync(permission: false);
            await SignInAsync(denied.Reviewer);
            await ReadAsync(expected: HttpStatusCode.Forbidden);
            var host = await SeedAsync(central: true);
            await SignInAsync(host.Reviewer);
            await ReadAsync("TenantId=1", HttpStatusCode.Forbidden);
            var central = await SeedAsync(central: true, allTenants: true);
            await SignInAsync(central.Reviewer);
            var result = await ReadAsync("TenantId=1&MaxResultCount=1000");
            Items(result).ShouldAllBe(item => item.GetProperty("tenantId").GetInt32() == 1);
            Items(result).ShouldContain(item => item.GetProperty("customerId").GetInt32() == central.OtherArea.CustomerId);
            Items(result).ShouldContain(item => item.GetProperty("customerId").GetInt32() == central.OtherAreaV2.CustomerId);
        }

        [DiscoveryPostgreSqlFact]
        public async Task UnsupportedV2Rules_RemainUnverifiableInsteadOfBecomingLegacyOnly()
        {
            var fixture = await SeedAsync();
            await UsingDbContextAsync(context => context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"AQGreenRecruitmentAttributions\" SET \"RulesVersion\" = 'unsupported-synthetic' WHERE \"ParticipantId\" = {fixture.Both.ParticipationId}"));
            await SignInAsync(fixture.Reviewer);
            var row = Row(await ReadAsync(), fixture.Both.CustomerId);
            row.GetProperty("discoveryKind").GetInt32().ShouldBe((int)LegacyAQGreenDiscoveryKind.HistoricalInformationUnverifiable);
            row.GetProperty("participationVersionEvidence").GetInt32().ShouldBe((int)AQGreenParticipationVersionEvidence.VersionUnverifiable);
            Issues(row).ShouldContain("ATTRIBUTION_RULES_UNSUPPORTED");
        }

        [DiscoveryPostgreSqlFact]
        public async Task Pagination_IsDeterministicAndDoesNotMultiplyOverlappingEvidence()
        {
            var fixture = await SeedAsync();
            await SignInAsync(fixture.Reviewer);
            var full = await ReadAsync();
            var expected = Items(full).Select(item => item.GetProperty("discoveryId").GetString()).ToArray();
            var actual = new List<string>();
            for (var offset = 0; offset < 4; offset++)
            {
                var page = await ReadAsync($"MaxResultCount=1&SkipCount={offset}");
                page.GetProperty("totalCount").GetInt32().ShouldBe(4);
                var id = Items(page).Single().GetProperty("discoveryId").GetString();
                actual.Add(id);
                Items(await ReadAsync($"MaxResultCount=1&SkipCount={offset}"))
                    .Single().GetProperty("discoveryId").GetString().ShouldBe(id);
            }
            actual.ShouldBe(expected);
            actual.Distinct().Count().ShouldBe(4);
            Items(await ReadAsync("MaxResultCount=1&SkipCount=4")).ShouldBeEmpty();
        }

        [DiscoveryPostgreSqlFact]
        public async Task CheckoutOwnershipAndForeignPaymentLinks_AreWithheldWithExplicitIssues()
        {
            var fixture = await SeedAsync();
            await UsingDbContextAsync(context => context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"AQGreenJoiningCheckouts\" SET \"CustomerId\" = {fixture.OtherArea.CustomerId} WHERE \"ParticipationId\" = {fixture.Both.ParticipationId}"));
            await SignInAsync(fixture.Reviewer);
            var row = Row(await ReadAsync(), fixture.Both.CustomerId);
            Issues(row).ShouldContain("CHECKOUT_OWNERSHIP_UNVERIFIABLE");
            row.GetProperty("checkoutAttempts").GetArrayLength().ShouldBe(0);
            await UsingDbContextAsync(async context =>
            {
                var foreignPayment = MemberPayment.CreatePending(1, fixture.OtherArea.CustomerId,
                    MemberPaymentPurpose.AQGreenJoining, 1200m, "Synthetic", Guid.NewGuid().ToString("N"), RecordedAt);
                foreignPayment.Confirm(RecordedAt.AddMinutes(1));
                context.MemberPayments.Add(foreignPayment);
                await context.SaveChangesAsync();
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE \"AQGreenJoiningCheckouts\" SET \"CustomerId\" = {fixture.Both.CustomerId}, \"PaymentId\" = {foreignPayment.Id} WHERE \"ParticipationId\" = {fixture.Both.ParticipationId}");
            });
            row = Row(await ReadAsync(), fixture.Both.CustomerId);
            Issues(row).ShouldContain("CHECKOUT_PAYMENT_LINK_UNVERIFIABLE");
            row.GetProperty("checkoutAttempts").EnumerateArray().Single()
                .GetProperty("paymentId").ValueKind.ShouldBe(JsonValueKind.Null);
        }

        [DiscoveryPostgreSqlFact]
        public async Task Discovery_SucceedsInReadOnlyPostgreSqlTransaction_AndPreservesBusinessRows()
        {
            var fixture = await SeedAsync();
            await SignInAsync(fixture.Reviewer);
            var before = await BusinessSnapshotAsync();
            using (var uow = Resolve<IUnitOfWorkManager>().Begin(new UnitOfWorkOptions
            {
                IsTransactional = true,
                IsolationLevel = System.Transactions.IsolationLevel.RepeatableRead
            }))
            {
                var context = await Resolve<IDbContextProvider<AqualLifeStyleDbContext>>().GetDbContextAsync();
                await context.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY");
                var result = await Resolve<IAdminProgrammeParticipationAppService>()
                    .GetLegacyAQGreenReconciliationAsync(new LegacyAQGreenReconciliationListInput());
                result.TotalCount.ShouldBe(4);
                context.ChangeTracker.Entries().ShouldNotContain(item => item.State == EntityState.Added ||
                    item.State == EntityState.Modified || item.State == EntityState.Deleted);
                await uow.CompleteAsync();
            }
            await ReadAsync();
            (await BusinessSnapshotAsync()).ShouldBe(before);
        }

        private async Task<Fixture> SeedAsync(bool permission = true, bool central = false, bool allTenants = false, bool exportPermission = false)
        {
            return await UsingDbContextAsync(async context =>
            {
                context.Database.IsNpgsql().ShouldBeTrue();
                var suffix = Guid.NewGuid().ToString("N");
                var tenant = new Tenant("disc" + suffix[..10], "Synthetic discovery tenant");
                var area = Area.Create(1, "D" + suffix[..9], "Synthetic assigned Area");
                var otherArea = Area.Create(1, "E" + suffix[..9], "Synthetic other Area");
                var membership = Membership.Create(null, "Synthetic legacy AQGreen", "Synthetic assignment", MembershipType.AQGreen);
                var reviewer = CreateUser(central ? null : 1, "discreviewer" + suffix);
                reviewer.SetRole(AquaUserRole.SystemAdmin);
                var role = new Role(reviewer.TenantId, "disc" + suffix[..12], "Synthetic discovery reviewer");
                context.Tenants.Add(tenant);
                context.Areas.AddRange(area, otherArea);
                context.Memberships.Add(membership);
                context.Users.Add(reviewer);
                context.Roles.Add(role);
                await context.SaveChangesAsync();
                context.UserRoles.Add(new UserRole(reviewer.TenantId, reviewer.Id, role.Id));
                var permissions = new List<string> { AquaPermissions.Admin.Default, AquaPermissions.Admin.ProgrammeParticipations.Default };
                if (permission) permissions.Add(AquaPermissions.Admin.ProgrammeParticipations.ViewLegacyPaymentReconciliation);
                if (exportPermission) permissions.Add(AquaPermissions.Admin.ProgrammeParticipations.ExportLegacyReconciliationEvidence);
                if (allTenants) permissions.Add(AquaPermissions.Admin.AllTenants);
                context.Permissions.AddRange(permissions.Select(name => new RolePermissionSetting
                { TenantId = reviewer.TenantId, RoleId = role.Id, Name = name, IsGranted = true }));
                var assignment = central ? null : AreaAdminAssignment.Assign(area, reviewer.Id, 1, RecordedAt);
                if (assignment != null)
                {
                    context.AreaAdminAssignments.Add(assignment);
                    var reviewerCustomer = Customer.Create(1, reviewer.Id, "Synthetic reviewer", new EmailAddress(reviewer.EmailAddress));
                    reviewerCustomer.AssignInitialArea(area, RecordedAt, "Synthetic discovery administrator");
                    context.Customers.Add(reviewerCustomer);
                }
                var foreignArea = Area.Create(tenant.Id, "F" + suffix[..9], "Synthetic foreign Area");
                context.Areas.Add(foreignArea);
                await context.SaveChangesAsync();
                var legacy = await SubjectAsync(context, area, membership.Id, false, false, reviewer.Id);
                var both = await SubjectAsync(context, area, membership.Id, true, true, reviewer.Id);
                var v2 = await SubjectAsync(context, area, null, true, true, reviewer.Id);
                var unknown = await SubjectAsync(context, area, null, true, false, reviewer.Id);
                var other = await SubjectAsync(context, otherArea, membership.Id, false, false, reviewer.Id);
                var otherV2 = await SubjectAsync(context, otherArea, membership.Id, true, true, reviewer.Id);
                var foreign = await SubjectAsync(context, foreignArea, membership.Id, false, false, reviewer.Id);
                var participant = await context.EntryParticipations.IgnoreQueryFilters().SingleAsync(item => item.Id == both.ParticipationId);
                var payment = MemberPayment.CreatePending(1, both.CustomerId, MemberPaymentPurpose.AQGreenJoining,
                    1200m, "Synthetic", "discovery-joining-" + suffix, RecordedAt);
                payment.Confirm(RecordedAt.AddMinutes(1));
                context.MemberPayments.Add(payment);
                participant.ApplyConfirmedJoiningPayment(payment);
                var monthly = MemberPayment.CreatePending(1, both.CustomerId, MemberPaymentPurpose.EntryMonthlyCommitment,
                    600m, "Synthetic", "discovery-monthly-" + suffix, RecordedAt);
                monthly.Confirm(RecordedAt.AddMinutes(2));
                context.MemberPayments.Add(monthly);
                context.AQGreenJoiningCheckouts.Add(AQGreenJoiningCheckout.Create(1, participant.Id, both.CustomerId,
                    AQGreenJoiningPaymentSchedule.Full, AQGreenJoiningPaymentStage.Full, 1200m, "ZAR", RecordedAt));
                await context.SaveChangesAsync();
                return new Fixture(reviewer, area.Id, assignment?.Id, tenant.Id, payment.Id,
                    legacy, both, v2, unknown, other, otherV2, foreign);
            });
        }

        private static async Task<Subject> SubjectAsync(AqualLifeStyleDbContext context, Area area,
            int? membershipId, bool participation, bool v2, long actorId)
        {
            var name = "disc" + Guid.NewGuid().ToString("N");
            var user = CreateUser(area.TenantId, name);
            context.Users.Add(user);
            await context.SaveChangesAsync();
            var customer = Customer.Create(area.TenantId, user.Id, "Synthetic discovery member", new EmailAddress(user.EmailAddress));
            customer.AssignInitialArea(area, RecordedAt, "Synthetic discovery fixture");
            customer.ChangeMembership(membershipId);
            context.Customers.Add(customer);
            await context.SaveChangesAsync();
            if (!participation) return new Subject(customer.Id, null);
            var entry = EntryParticipation.StartIndependently(area.TenantId, customer.Id, Terms, RecordedAt);
            context.EntryParticipations.Add(entry);
            await context.SaveChangesAsync();
            if (v2)
                context.AQGreenRecruitmentAttributions.Add(AQGreenRecruitmentAttribution.Create(area.TenantId, entry.Id,
                    null, AQGreenRecruitmentAttributionKind.AuthorisedProspectiveRoot,
                    AQGreenAcquisitionSource.AuthorisedDirectAdmission, Guid.NewGuid(), RecordedAt, actorId,
                    "Synthetic discovery provenance", AQGreenRecruitmentAttributionRules.CurrentVersion));
            await context.SaveChangesAsync();
            return new Subject(customer.Id, entry.Id);
        }

        private static User CreateUser(int? tenantId, string name)
        {
            var user = new User { TenantId = tenantId, UserName = name, Name = "Synthetic", Surname = "Discovery",
                EmailAddress = name + "@example.test", IsActive = true, IsEmailConfirmed = true };
            user.SetNormalizedNames();
            user.Password = Hasher.HashPassword(user, Password);
            return user;
        }

        private async Task SignInAsync(User user)
        {
            AbpSession.TenantId = user.TenantId;
            await AuthenticateAsync(user.TenantId.HasValue ? "Default" : null,
                new AuthenticateModel { UserNameOrEmailAddress = user.UserName, Password = Password });
        }

        private async Task<JsonElement> ReadAsync(string query = "", HttpStatusCode expected = HttpStatusCode.OK)
        {
            using var response = await Client.GetAsync(Endpoint + "?" + query);
            response.StatusCode.ShouldBe(expected);
            if (expected != HttpStatusCode.OK) return default;
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return document.RootElement.GetProperty("result").Clone();
        }

        private static JsonElement[] Items(JsonElement result) => result.GetProperty("items").EnumerateArray().ToArray();
        private static JsonElement Row(JsonElement result, int customerId) => Items(result)
            .Single(item => item.GetProperty("customerId").GetInt32() == customerId);
        private static string[] Issues(JsonElement row) => row.GetProperty("historicalEvidenceIssues")
            .EnumerateArray().Select(item => item.GetString()).ToArray();

        private async Task<string> BusinessSnapshotAsync()
        {
            await using var connection = new NpgsqlConnection(Environment.GetEnvironmentVariable("REPRO_PG_CONNECTION"));
            await connection.OpenAsync();
            await using var tables = new NpgsqlCommand("SELECT tablename FROM pg_tables WHERE schemaname = 'public' AND tablename <> 'AbpAuditLogs' ORDER BY tablename", connection);
            var names = new List<string>();
            await using (var reader = await tables.ExecuteReaderAsync())
                while (await reader.ReadAsync()) names.Add(reader.GetString(0));
            names.ShouldNotContain("__EFMigrationsHistory");
            var snapshot = new List<string>();
            foreach (var name in names)
            {
                var quoted = '"' + name.Replace("\"", "\"\"") + '"';
                await using var command = new NpgsqlCommand($"SELECT COALESCE(string_agg(row_to_json(t)::text, E'\\n' ORDER BY row_to_json(t)::text), '') FROM {quoted} t", connection);
                snapshot.Add(name + ":" + (string)await command.ExecuteScalarAsync());
            }
            return string.Join("\n", snapshot);
        }

        private sealed record Subject(int CustomerId, Guid? ParticipationId);
        private sealed record Fixture(User Reviewer, Guid AreaId, Guid? AssignmentId, int ForeignTenantId,
            Guid JoiningPaymentId, Subject LegacyOnly, Subject Both, Subject V2Only, Subject Unversioned,
            Subject OtherArea, Subject OtherAreaV2, Subject Foreign);
    }
}
