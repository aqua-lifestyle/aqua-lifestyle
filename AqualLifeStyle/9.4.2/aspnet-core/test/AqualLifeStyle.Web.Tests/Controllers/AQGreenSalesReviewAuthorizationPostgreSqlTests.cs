using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Abp.Authorization.Roles;
using Abp.Authorization.Users;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Authorization.Roles;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Areas;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Onyx;
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
    public sealed class SalesReviewPostgreSqlFactAttribute : FactAttribute
    {
        public SalesReviewPostgreSqlFactAttribute()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("REPRO_PG"),
                    "true", StringComparison.OrdinalIgnoreCase))
                Skip = "Requires REPRO_PG=true and a disposable PostgreSQL database.";
        }
    }

    // These journeys share authentication state and, in PostgreSQL mode, one
    // persistent fixture database. Startup repair seeders must not run while
    // another journey is midway through creating its identity/customer fixture.
    // Explicit Task.WhenAll operations inside regression bodies stay concurrent.
    [CollectionDefinition("Authentication web journeys", DisableParallelization = true)]
    public sealed class AuthenticationWebJourneyCollection { }

    [Collection("Authentication web journeys")]
    public sealed class AQGreenSalesReviewAuthorizationPostgreSqlTests : AqualLifeStyleWebTestBase
    {
        private const string Endpoint = "/api/services/app/AdminAQGreenWeeklySalesEligibility/";
        private const string Password = "SyntheticSales123!";
        private static readonly DateTime Week = new(2026, 8, 20, 22, 0, 0, DateTimeKind.Utc);
        private static readonly EntryProgrammeTerms Terms = EntryProgrammeTerms.CreateSingleJoiningPayment(
            "sales-authorization-test", Week.AddMonths(-1), 1200m, 600m, 7);

        [SalesReviewPostgreSqlFact]
        public async Task AreaAdmin_ConfirmExactRetryAndScopedReads_PreserveDurableAudit()
        {
            var fixture = await SeedAsync();
            await SignInAsync(fixture.Reviewer);
            using var gate = Gate.Enable(1);
            var first = await PostAsync("Confirm", Confirm(fixture.Assigned), HttpStatusCode.OK);
            var retry = await PostAsync("Confirm", Confirm(fixture.Assigned), HttpStatusCode.OK);
            first.GetProperty("id").GetGuid().ShouldBe(retry.GetProperty("id").GetGuid());
            first.GetProperty("reviewedAt").GetString().ShouldBe(retry.GetProperty("reviewedAt").GetString());
            first.GetProperty("reviewedByUserId").GetInt64().ShouldBe(fixture.Reviewer.Id);
            first.GetProperty("thresholdResult").GetInt32().ShouldBe(1);

            var list = await GetAsync("GetAll?MaxResultCount=100", HttpStatusCode.OK);
            list.GetProperty("items").EnumerateArray().ShouldAllBe(row =>
                row.GetProperty("participantId").GetGuid() == fixture.Assigned.ParticipantId);
            list.GetProperty("totalCount").GetInt32().ShouldBe(1);
            await GetAsync($"Get?Id={first.GetProperty("id").GetGuid()}", HttpStatusCode.OK);
            await GetAsync($"GetLatestClosedWeek?TenantId=1&ParticipantId={fixture.Assigned.ParticipantId}", HttpStatusCode.OK);
            await GetAsync($"Get?Id={fixture.OtherArea.DecisionId}", HttpStatusCode.Forbidden);
            await GetAsync($"GetLatestClosedWeek?TenantId=1&ParticipantId={fixture.OtherArea.ParticipantId}", HttpStatusCode.Forbidden);
            await AssertDurableAsync(fixture.Assigned, fixture.Reviewer.Id, AQGreenWeeklySalesReviewStatus.Confirmed);
        }

        [SalesReviewPostgreSqlFact]
        public async Task AreaAdmin_HoldAndReject_AreScopedAndIdempotent()
        {
            var fixture = await SeedAsync(seedAssignedDecision: false);
            await SignInAsync(fixture.Reviewer);
            using var gate = Gate.Enable(1);
            var held = await PostAsync("BeginReview", Target(fixture.Assigned), HttpStatusCode.OK);
            held.GetProperty("reviewStatus").GetInt32().ShouldBe(1);
            held.GetProperty("reviewedAt").ValueKind.ShouldBe(JsonValueKind.Null);
            await UsingDbContextAsync(async context =>
            {
                var decision = await context.AQGreenWeeklySalesEligibilityDecisions.IgnoreQueryFilters()
                    .SingleAsync(row => row.ParticipantId == fixture.Assigned.ParticipantId);
                decision.CreatorUserId.ShouldBe(fixture.Reviewer.Id);
                decision.CreationTime.ShouldNotBe(default);
                decision.ReviewedByUserId.ShouldBeNull("a hold is not a finalized review");
            });
            var rejected = await PostAsync("Reject", Reject(fixture.Assigned), HttpStatusCode.OK);
            var retry = await PostAsync("Reject", Reject(fixture.Assigned), HttpStatusCode.OK);
            rejected.GetProperty("id").GetGuid().ShouldBe(retry.GetProperty("id").GetGuid());
            var conflict = await PostAsync("Confirm", Confirm(fixture.Assigned), HttpStatusCode.InternalServerError);
            conflict.ValueKind.ShouldBe(JsonValueKind.Null);
            await AssertDurableAsync(fixture.Assigned, fixture.Reviewer.Id, AQGreenWeeklySalesReviewStatus.Rejected);
        }

        [SalesReviewPostgreSqlFact]
        public async Task AreaAdmin_CrossAreaDirectReadsAndWrites_AreDeniedWithoutMutation()
        {
            var fixture = await SeedAsync();
            await SignInAsync(fixture.Reviewer);
            using var gate = Gate.Enable(1);
            await AssertDeniedActionsAsync(fixture.OtherArea);
            await AssertHeldAsync(fixture.OtherArea);
        }

        [SalesReviewPostgreSqlFact]
        public async Task AreaAdmin_CrossTenantDirectReadsAndWrites_AreDeniedWithoutMutation()
        {
            var fixture = await SeedAsync();
            await SignInAsync(fixture.Reviewer);
            using var gate = Gate.Enable(fixture.OtherTenant.TenantId);
            await AssertDeniedActionsAsync(fixture.OtherTenant, detailStatus: HttpStatusCode.InternalServerError);
            await GetAsync($"GetAll?TenantId={fixture.OtherTenant.TenantId}", HttpStatusCode.Forbidden);
            await AssertHeldAsync(fixture.OtherTenant);
        }

        [SalesReviewPostgreSqlFact]
        public async Task MissingReviewPermission_WithAreaAssignment_CannotReadOrWrite()
        {
            var fixture = await SeedAsync(reviewPermission: false);
            await SignInAsync(fixture.Reviewer);
            using var gate = Gate.Enable(1);
            await AssertDeniedActionsAsync(fixture.Assigned);
            await GetAsync("GetAll", HttpStatusCode.Forbidden);
            await AssertHeldAsync(fixture.Assigned);
        }

        [SalesReviewPostgreSqlFact]
        public async Task RevokedAreaAssignment_DeniesExactRetryWithoutChangingFinalHistory()
        {
            var fixture = await SeedAsync();
            await SignInAsync(fixture.Reviewer);
            using var gate = Gate.Enable(1);
            await PostAsync("Confirm", Confirm(fixture.Assigned), HttpStatusCode.OK);
            await UsingDbContextAsync(async context =>
            {
                var assignment = await context.AreaAdminAssignments.IgnoreQueryFilters()
                    .SingleAsync(row => row.Id == fixture.AssignmentId);
                assignment.Revoke(DateTime.UtcNow);
            });
            await PostAsync("Confirm", Confirm(fixture.Assigned), HttpStatusCode.Forbidden);
            await GetAsync("GetAll", HttpStatusCode.Forbidden);
            await AssertDurableAsync(fixture.Assigned, fixture.Reviewer.Id, AQGreenWeeklySalesReviewStatus.Confirmed);
        }

        [SalesReviewPostgreSqlFact]
        public async Task AreaAuthority_IsLockedUntilTheReviewCommits()
        {
            var fixture = await SeedAsync();
            await SignInAsync(fixture.Reviewer);
            using var gate = Gate.Enable(1);
            var connectionString = Environment.GetEnvironmentVariable("REPRO_PG_CONNECTION");
            await using var blocker = new NpgsqlConnection(connectionString);
            await blocker.OpenAsync();
            await using var transaction = await blocker.BeginTransactionAsync();
            await using (var command = new NpgsqlCommand(
                "LOCK TABLE public.\"AQGreenWeeklySalesEligibilityDecisions\" IN ACCESS EXCLUSIVE MODE", blocker, transaction))
                await command.ExecuteNonQueryAsync();
            var pending = PostAsync("Confirm", Confirm(fixture.Assigned), HttpStatusCode.OK);
            var committed = false;
            try
            {
                await using var observer = new NpgsqlConnection(connectionString);
                await observer.OpenAsync();
                var waiting = false;
                for (var attempt = 0; attempt < 100; attempt++)
                {
                    await using var command = new NpgsqlCommand(
                        "SELECT EXISTS (SELECT 1 FROM pg_locks WHERE relation = " +
                        "'public.\"AQGreenWeeklySalesEligibilityDecisions\"'::regclass " +
                        "AND mode = 'AccessShareLock' AND NOT granted)", observer);
                    if ((bool)await command.ExecuteScalarAsync()) { waiting = true; break; }
                    await Task.Delay(50);
                }
                waiting.ShouldBeTrue("the review must wait after taking its Area authority locks");
                await using (var command = new NpgsqlCommand(
                    "SET lock_timeout = '200ms'; UPDATE public.\"AreaAdminAssignments\" " +
                    "SET \"RevokedAt\" = NOW() WHERE \"Id\" = @id", observer))
                {
                    command.Parameters.AddWithValue("id", fixture.AssignmentId);
                    var denied = await Should.ThrowAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
                    denied.SqlState.ShouldBe("55P03");
                }
                await transaction.CommitAsync();
                committed = true;
                await pending;
                await AssertDurableAsync(fixture.Assigned, fixture.Reviewer.Id, AQGreenWeeklySalesReviewStatus.Confirmed);
            }
            finally
            {
                if (!committed) await transaction.RollbackAsync();
                try { await pending; } catch when (!committed) { }
            }
        }

        [SalesReviewPostgreSqlFact]
        public async Task InactiveArea_DeniesReviewEvenWithUnrevokedAssignment()
        {
            var fixture = await SeedAsync();
            await UsingDbContextAsync(async context =>
            {
                var area = await context.Areas.IgnoreQueryFilters().SingleAsync(row => row.Id == fixture.Assigned.AreaId);
                area.Deactivate();
            });
            await SignInAsync(fixture.Reviewer);
            using var gate = Gate.Enable(1);
            await AssertDeniedActionsAsync(fixture.Assigned);
            await AssertHeldAsync(fixture.Assigned);
        }

        [SalesReviewPostgreSqlFact]
        public async Task CentralReviewer_WithExplicitAllTenants_CanReviewOtherTenant()
        {
            var fixture = await SeedAsync(central: true, allTenants: true);
            await SignInAsync(fixture.Reviewer);
            using var gate = Gate.Enable(fixture.OtherTenant.TenantId);
            await PostAsync("Confirm", Confirm(fixture.OtherTenant), HttpStatusCode.OK);
            await GetAsync($"Get?Id={fixture.OtherTenant.DecisionId}", HttpStatusCode.OK);
            await AssertDurableAsync(fixture.OtherTenant, fixture.Reviewer.Id, AQGreenWeeklySalesReviewStatus.Confirmed);
            // Review delegation conveys no commission calculation, release or payout permission.
            foreach (var action in new[] { "CalculateLatestClosedWeek", "Release", "RecordPayment" })
                await SendAsync(HttpMethod.Post, "/api/services/app/AdminCommission/" + action,
                    new { id = Guid.NewGuid(), tenantId = 1, programme = 0,
                        justification = "Synthetic denial check", paymentReference = "synthetic-no-payout" }, HttpStatusCode.Forbidden);
        }

        [SalesReviewPostgreSqlFact]
        public async Task ClientSuppliedOverrides_CannotSetThresholdAuditOrCommission()
        {
            var fixture = await SeedAsync();
            await SignInAsync(fixture.Reviewer);
            using var gate = Gate.Enable(1);
            var result = await PostAsync("Confirm", new
            {
                tenantId = 1, participantId = fixture.Assigned.ParticipantId,
                commissionWeekStartUtc = Week, sprayQuantity = 5, oneLitreQuantity = 4,
                fiveLitreQuantity = 5, evidenceReferences = new[] { "synthetic:shortfall" },
                thresholdOverride = true, thresholdResult = 1, commissionAmount = 9000m,
                payoutStatus = 3, reviewedByUserId = 999999L, rulesVersion = "untrusted-client-version"
            }, HttpStatusCode.OK);
            result.GetProperty("thresholdResult").GetInt32().ShouldBe(2);
            result.GetProperty("reviewedByUserId").GetInt64().ShouldBe(fixture.Reviewer.Id);
            result.GetProperty("salesEligibilityRulesVersion").GetString().ShouldBe(AQGreenWeeklySalesEligibilityRules.CurrentVersion);
            await AssertDurableAsync(fixture.Assigned, fixture.Reviewer.Id, AQGreenWeeklySalesReviewStatus.Confirmed);
        }

        [SalesReviewPostgreSqlFact]
        public async Task CentralReviewer_WithoutBroaderGrant_IsDenied()
        {
            var fixture = await SeedAsync(central: true);
            await SignInAsync(fixture.Reviewer);
            using var gate = Gate.Enable(1);
            await AssertDeniedActionsAsync(fixture.Assigned);
            await GetAsync("GetAll", HttpStatusCode.Forbidden);
            await AssertHeldAsync(fixture.Assigned);
        }

        [SalesReviewPostgreSqlFact]
        public async Task CentralReviewer_WithoutReviewPermission_IsDenied()
        {
            var fixture = await SeedAsync(reviewPermission: false, central: true, allTenants: true);
            await SignInAsync(fixture.Reviewer);
            using var gate = Gate.Enable(1);
            await AssertDeniedActionsAsync(fixture.Assigned);
            await AssertHeldAsync(fixture.Assigned);
        }

        [SalesReviewPostgreSqlFact]
        public async Task UnauthenticatedDirectApi_IsDenied()
        {
            var fixture = await SeedAsync();
            AbpSession.UserId = null;
            AbpSession.TenantId = null;
            Client.DefaultRequestHeaders.Authorization = null;
            using var gate = Gate.Enable(1);
            await PostAsync("Confirm", Confirm(fixture.Assigned), HttpStatusCode.Unauthorized);
            await GetAsync("GetAll", HttpStatusCode.Unauthorized);
            await AssertHeldAsync(fixture.Assigned);
        }

        [SalesReviewPostgreSqlFact]
        public async Task ExistingSeededTenantAndHostAdmins_RetainScopedCompatibility()
        {
            var fixture = await SeedAsync();
            LoginAsDefaultTenantAdmin();
            var tenantAdminId = AbpSession.UserId.Value;
            await UsingDbContextAsync(async context =>
            {
                var user = await context.Users.IgnoreQueryFilters().SingleAsync(row => row.Id == tenantAdminId);
                user.Password = Hasher.HashPassword(user, Password);
                user.IsEmailConfirmed = true;
                var area = await context.Areas.IgnoreQueryFilters().SingleAsync(row => row.Id == fixture.Assigned.AreaId);
                context.AreaAdminAssignments.Add(AreaAdminAssignment.Assign(area, user.Id, 1, DateTime.UtcNow.AddMinutes(-1)));
            });
            await AuthenticateAsync("Default", new AuthenticateModel { UserNameOrEmailAddress = "admin", Password = Password });
            using var gate = Gate.Enable(1);
            await PostAsync("Confirm", Confirm(fixture.Assigned), HttpStatusCode.OK);
            await AssertDurableAsync(fixture.Assigned, tenantAdminId, AQGreenWeeklySalesReviewStatus.Confirmed);
            LoginAsHostAdmin();
            var hostId = AbpSession.UserId.Value;
            await UsingDbContextAsync(async context =>
            {
                var user = await context.Users.IgnoreQueryFilters().SingleAsync(row => row.Id == hostId);
                user.Password = Hasher.HashPassword(user, Password);
                user.IsEmailConfirmed = true;
            });
            await AuthenticateAsync(null, new AuthenticateModel { UserNameOrEmailAddress = "admin", Password = Password });
            await PostAsync("Confirm", Confirm(fixture.OtherArea), HttpStatusCode.OK);
            await AssertDurableAsync(fixture.OtherArea, hostId, AQGreenWeeklySalesReviewStatus.Confirmed);
        }

        private async Task<Fixture> SeedAsync(bool reviewPermission = true, bool central = false, bool allTenants = false,
            bool seedAssignedDecision = true)
        {
            return await UsingDbContextAsync(async context =>
            {
                context.Database.IsNpgsql().ShouldBeTrue();
                var suffix = Guid.NewGuid().ToString("N");
                var otherTenant = new Tenant("sales" + suffix[..10], "Synthetic sales tenant");
                context.Tenants.Add(otherTenant);
                await context.SaveChangesAsync();
                var reviewer = CreateUser(central ? null : 1, "reviewer" + suffix);
                reviewer.SetRole(AquaUserRole.SystemAdmin);
                var role = new Role(reviewer.TenantId, "sales" + suffix[..12], "Synthetic sales reviewer");
                context.Users.Add(reviewer);
                context.Roles.Add(role);
                await context.SaveChangesAsync();
                context.UserRoles.Add(new UserRole(reviewer.TenantId, reviewer.Id, role.Id));
                var permissions = new List<string> { AquaPermissions.Admin.Default, AquaPermissions.Admin.Commissions.Default };
                if (reviewPermission) permissions.Add(AquaPermissions.Admin.Commissions.ReviewAQGreenWeeklySalesEligibility);
                if (allTenants) permissions.Add(AquaPermissions.Admin.AllTenants);
                context.Permissions.AddRange(permissions.Select(name => new RolePermissionSetting
                {
                    TenantId = reviewer.TenantId, RoleId = role.Id, Name = name, IsGranted = true
                }));
                var assigned = await SubjectAsync(context, 1, "assigned" + suffix, seedAssignedDecision);
                var otherArea = await SubjectAsync(context, 1, "other" + suffix);
                var foreign = await SubjectAsync(context, otherTenant.Id, "foreign" + suffix);
                var assignmentId = Guid.Empty;
                if (!central)
                {
                    var area = await context.Areas.IgnoreQueryFilters().SingleAsync(row => row.Id == assigned.AreaId);
                    var reviewerCustomer = Customer.Create(1, reviewer.Id, "Synthetic reviewer",
                        new EmailAddress(reviewer.EmailAddress));
                    reviewerCustomer.AssignInitialArea(area, Week, "Synthetic administrator fixture");
                    context.Customers.Add(reviewerCustomer);
                    var assignment = AreaAdminAssignment.Assign(area, reviewer.Id, 1, DateTime.UtcNow.AddMinutes(-1));
                    assignmentId = assignment.Id;
                    context.AreaAdminAssignments.Add(assignment);
                }
                await context.SaveChangesAsync();
                return new Fixture(reviewer, assigned, otherArea, foreign, assignmentId);
            });
        }

        private static async Task<Subject> SubjectAsync(AqualLifeStyle.EntityFrameworkCore.AqualLifeStyleDbContext context,
            int tenantId, string name, bool seedDecision = true)
        {
            var user = CreateUser(tenantId, name);
            var area = Area.Create(tenantId, name[..12], "Synthetic Area " + name);
            context.Users.Add(user);
            context.Areas.Add(area);
            await context.SaveChangesAsync();
            var customer = Customer.Create(tenantId, user.Id, "Synthetic " + name, new EmailAddress(name + "@example.test"));
            customer.AssignInitialArea(area, Week.AddMonths(-1), "Synthetic review authorization fixture");
            context.Customers.Add(customer);
            await context.SaveChangesAsync();
            var participant = EntryParticipation.StartIndependently(tenantId, customer.Id, Terms, Week.AddMonths(-1));
            context.EntryParticipations.Add(participant);
            await context.SaveChangesAsync();
            var decision = AQGreenWeeklySalesEligibilityDecision.Begin(tenantId, participant.Id,
                AQGreenCommissionWeek.FromStartUtc(Week), AQGreenWeeklySalesEligibilityRules.CurrentVersion);
            if (seedDecision)
            {
                context.AQGreenWeeklySalesEligibilityDecisions.Add(decision);
                await context.SaveChangesAsync();
            }
            return new Subject(tenantId, participant.Id, seedDecision ? decision.Id : Guid.Empty, area.Id);
        }

        private static User CreateUser(int? tenantId, string name)
        {
            var user = new User { TenantId = tenantId, UserName = name, Name = "Synthetic", Surname = "Reviewer",
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

        private async Task AssertDeniedActionsAsync(Subject subject, HttpStatusCode detailStatus = HttpStatusCode.Forbidden)
        {
            await PostAsync("BeginReview", Target(subject), HttpStatusCode.Forbidden);
            await PostAsync("Confirm", Confirm(subject), HttpStatusCode.Forbidden);
            await PostAsync("Reject", Reject(subject), HttpStatusCode.Forbidden);
            await GetAsync($"GetLatestClosedWeek?TenantId={subject.TenantId}&ParticipantId={subject.ParticipantId}", HttpStatusCode.Forbidden);
            await GetAsync($"Get?Id={subject.DecisionId}", detailStatus);
        }

        private async Task AssertHeldAsync(Subject subject) => await UsingDbContextAsync(async context =>
        {
            var decision = await context.AQGreenWeeklySalesEligibilityDecisions.IgnoreQueryFilters().SingleAsync(row => row.Id == subject.DecisionId);
            decision.ReviewStatus.ShouldBe(AQGreenWeeklySalesReviewStatus.HeldForEvidence);
            decision.ReviewedAt.ShouldBeNull();
            decision.ReviewedByUserId.ShouldBeNull();
            (await context.AQGreenWeeklySalesEvidenceReferences.IgnoreQueryFilters().CountAsync(row => row.DecisionId == subject.DecisionId)).ShouldBe(0);
        });

        private async Task AssertDurableAsync(Subject subject, long reviewerId, AQGreenWeeklySalesReviewStatus status) => await UsingDbContextAsync(async context =>
        {
            var decision = await context.AQGreenWeeklySalesEligibilityDecisions.IgnoreQueryFilters().Include(row => row.EvidenceReferences)
                .SingleAsync(row => row.ParticipantId == subject.ParticipantId && row.CommissionWeekStartUtc == Week);
            decision.ReviewStatus.ShouldBe(status);
            decision.ReviewedByUserId.ShouldBe(reviewerId);
            decision.ReviewedAt.ShouldNotBeNull();
            decision.SalesEligibilityRulesVersion.ShouldBe(AQGreenWeeklySalesEligibilityRules.CurrentVersion);
            decision.EvidenceReferences.ShouldHaveSingleItem().Source.ShouldBe(AQGreenWeeklySalesEvidenceSource.ManualReview);
            (await context.EntryWeeklyCommissions.IgnoreQueryFilters().CountAsync(row => row.EntryParticipationId == subject.ParticipantId)).ShouldBe(0);
        });

        private Task<JsonElement> PostAsync(string action, object body, HttpStatusCode status) => SendAsync(HttpMethod.Post, Endpoint + action, body, status);
        private Task<JsonElement> GetAsync(string action, HttpStatusCode status) => SendAsync(HttpMethod.Get, Endpoint + action, null, status);
        private async Task<JsonElement> SendAsync(HttpMethod method, string url, object body, HttpStatusCode status)
        {
            using var request = new HttpRequestMessage(method, url);
            if (body != null) request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            using var response = await Client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            response.StatusCode.ShouldBe(status, content);
            if (status == HttpStatusCode.InternalServerError && url.EndsWith("/Confirm", StringComparison.Ordinal))
                content.ShouldContain("conflicting finalized decision");
            using var json = JsonDocument.Parse(content);
            return json.RootElement.TryGetProperty("result", out var result) ? result.Clone() : json.RootElement.Clone();
        }

        private static object Target(Subject subject) => new { subject.TenantId, subject.ParticipantId, commissionWeekStartUtc = Week };
        private static object Confirm(Subject subject) => new { subject.TenantId, subject.ParticipantId, commissionWeekStartUtc = Week,
            sprayQuantity = 5, oneLitreQuantity = 5, fiveLitreQuantity = 5, evidenceReferences = new[] { "synthetic:manual-5-5-5" } };
        private static object Reject(Subject subject) => new { subject.TenantId, subject.ParticipantId, commissionWeekStartUtc = Week,
            rejectionReason = "Synthetic evidence could not be confirmed", evidenceReferences = new[] { "synthetic:manual-reject" } };
        private static PasswordHasher<User> Hasher => new(new OptionsWrapper<PasswordHasherOptions>(new PasswordHasherOptions()));
        private AQGreenV2ContinuousSalesReviewGate Gate => (AQGreenV2ContinuousSalesReviewGate)IocManager.Resolve<IAQGreenWeeklySalesReviewGate>();
        private sealed record Subject(int TenantId, Guid ParticipantId, Guid DecisionId, Guid AreaId);
        private sealed record Fixture(User Reviewer, Subject Assigned, Subject OtherArea, Subject OtherTenant, Guid AssignmentId);
    }
}
