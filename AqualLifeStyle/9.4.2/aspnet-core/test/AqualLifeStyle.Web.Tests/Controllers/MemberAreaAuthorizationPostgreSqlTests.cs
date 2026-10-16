using System;
using System.Collections.Generic;
using System.Linq;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Abp.Authorization.Roles;
using Abp.Authorization.Users;
using Abp.Runtime.Session;
using Abp.Runtime.Security;
using Abp.Domain.Uow;
using AqualLifeStyle.Application.Admin.Customers;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Authorization.Roles;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.Areas;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using AqualLifeStyle.Models.TokenAuth;
using AqualLifeStyle.MultiTenancy;
using Castle.MicroKernel.Registration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Web.Tests.Controllers
{
    public sealed class MemberAreaPostgreSqlFactAttribute : FactAttribute
    {
        public MemberAreaPostgreSqlFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("MEMBER_AREA_AUTHORIZATION") != "true")
                Skip = "Requires explicitly selected disposable PostgreSQL member authorization fixtures.";
        }
    }

    [Collection("Authentication web journeys")]
    public sealed class MemberAreaAuthorizationPostgreSqlTests : AqualLifeStyleWebTestBase
    {
        private const string Endpoint = "/api/services/app/AdminMember/";
        private static readonly string Password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)) + "!Aa1";
        private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        private static readonly string[] MemberPermissions = {
            AquaPermissions.Admin.Members.View, AquaPermissions.Admin.Members.Edit,
            AquaPermissions.Admin.Members.Suspend, AquaPermissions.Admin.Members.ChangeTier
        };

        public MemberAreaAuthorizationPostgreSqlTests()
        {
            IocManager.IocContainer.Register(Component.For<IAbpSession>().ImplementedBy<ClaimsAbpSession>()
                .Named("member-scope-claims").IsDefault().LifestyleSingleton());
            AbpSession.UserId = null; AbpSession.TenantId = null;
        }

        protected override IWebHostBuilder CreateWebHostBuilder()
        {
            var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("REPRO_PG_CONNECTION") ?? "");
            if (Environment.GetEnvironmentVariable("MEMBER_AREA_AUTHORIZATION") != "true" ||
                Environment.GetEnvironmentVariable("REPRO_PG") != "true" ||
                Environment.GetEnvironmentVariable("REPRO_TRANSACTIONAL") != "true" ||
                Environment.GetEnvironmentVariable("REPRO_PG_SCHEMA_FROM_MODEL") != "true" ||
                connection.Host != "127.0.0.1" ||
                !connection.Database.StartsWith("member_area_authorization_", StringComparison.Ordinal))
                throw new InvalidOperationException("Member authorization tests require a disposable loopback PostgreSQL database.");
            return base.CreateWebHostBuilder().UseEnvironment("Development");
        }

        [MemberAreaPostgreSqlFact]
        public async Task AssignedArea_ListGetAndPagingUseAuthorizedRowsAndActualArea()
        {
            var f = await SeedAsync();
            await SignInAsync(f);
            var page = await GetAsync($"GetAll?Keyword={f.Keyword}&MaxResultCount=1");
            page.GetProperty("totalCount").GetInt32().ShouldBe(2);
            var first = page.GetProperty("items").EnumerateArray().Single();
            first.GetProperty("areaId").GetGuid().ShouldBe(f.AreaId);
            first.GetProperty("areaName").GetString().ShouldBe("TEST assigned Area");
            first.GetProperty("tenantId").GetInt32().ShouldBe(1);
            var next = await GetAsync($"GetAll?Keyword={f.Keyword}&MaxResultCount=1&SkipCount=1");
            next.GetProperty("totalCount").GetInt32().ShouldBe(2);
            next.GetProperty("items")[0].GetProperty("id").GetInt32().ShouldNotBe(first.GetProperty("id").GetInt32());
            var end = await GetAsync($"GetAll?Keyword={f.Keyword}&MaxResultCount=1&SkipCount=2");
            end.GetProperty("totalCount").GetInt32().ShouldBe(2);
            end.GetProperty("items").GetArrayLength().ShouldBe(0);
            (await GetAsync($"GetAll?Keyword={f.Keyword}&AreaId={f.OtherAreaId}"))
                .GetProperty("totalCount").GetInt32().ShouldBe(0);
            await GetAsync($"GetAll?TenantId={f.ForeignTenantId}", HttpStatusCode.Forbidden);
            (await GetAsync($"Get?Id={f.MemberId}")).GetProperty("areaName").GetString().ShouldBe("TEST assigned Area");
            (await GetAsync($"GetMembershipOptions?Id={f.MemberId}")).GetArrayLength().ShouldBeGreaterThan(0);
            // Preserve search fields previously supported by the original table.
            (await GetAsync($"GetAll?Keyword={f.Keyword}-address" )).GetProperty("totalCount").GetInt32().ShouldBe(1);
            (await GetAsync($"GetAll?Keyword={f.Keyword}-plan" )).GetProperty("totalCount").GetInt32().ShouldBe(2);
        }

        [MemberAreaPostgreSqlFact]
        public async Task RemovedMembersAndDeletedLinkedUsersRemainExcludedForTenantAndHost()
        {
            foreach (var host in new[] { false, true })
            {
                var f = await SeedAsync(host, allTenants: host);
                var secondId = await UsingDbContextAsync(async db =>
                {
                    var first = await db.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == f.MemberId);
                    first.IsDeleted = true;
                    var second = await db.Customers.IgnoreQueryFilters().Include(c => c.User)
                        .SingleAsync(c => c.Name == "TEST " + f.Keyword + "-second");
                    second.User.IsDeleted = true;
                    return second.Id;
                });
                await SignInAsync(f);
                var list = await GetAsync($"GetAll?Keyword={f.Keyword}&TenantId=1&AreaId={f.AreaId}");
                list.GetProperty("totalCount").GetInt32().ShouldBe(0);
                list.GetProperty("items").GetArrayLength().ShouldBe(0);
                foreach (var id in new[] { f.MemberId, secondId })
                {
                    await GetAsync($"Get?Id={id}", HttpStatusCode.Forbidden);
                    await GetAsync($"GetMembershipOptions?Id={id}", HttpStatusCode.Forbidden);
                    var before = await FingerprintAsync(id);
                    await PostAsync("Suspend", new { id, justification = "TEST removed member" }, HttpStatusCode.Forbidden);
                    (await FingerprintAsync(id)).ShouldBe(before);
                }
            }
        }

        [MemberAreaPostgreSqlFact]
        public async Task AssignedArea_ProfileTierAndSuspensionPersistTogetherWithLinkedUser()
        {
            var f = await SeedAsync();
            await SignInAsync(f);
            await PostAsync("EditProfile", Edit(f), HttpStatusCode.OK);
            await PostAsync("ChangeTier", new { id = f.MemberId, membershipId = f.UpgradeId, justification = "TEST isolated tier change" }, HttpStatusCode.OK);
            await PostAsync("Suspend", new { id = f.MemberId, justification = "TEST isolated suspension" }, HttpStatusCode.OK);
            await UsingDbContextAsync(async db =>
            {
                var member = await db.Customers.IgnoreQueryFilters().Include(c => c.User).SingleAsync(c => c.Id == f.MemberId);
                member.Name.ShouldBe("TEST Updated Member");
                member.MembershipId.ShouldBe(f.UpgradeId);
                member.IsActive.ShouldBeFalse();
                member.User.IsActive.ShouldBeFalse();
                member.AreaId.ShouldBe(f.AreaId);
            });
        }

        [MemberAreaPostgreSqlFact]
        public async Task OtherAreaAndForeignTenant_AllDirectMemberOperationsDeniedWithoutWrites()
        {
            var f = await SeedAsync();
            await SignInAsync(f);
            foreach (var target in new[] { f.OtherMemberId, f.ForeignMemberId })
            {
                var before = await FingerprintAsync(target);
                await GetAsync($"Get?Id={target}", HttpStatusCode.Forbidden);
                await GetAsync($"GetMembershipOptions?Id={target}", HttpStatusCode.Forbidden);
                await PostAsync("EditProfile", Edit(f, target), HttpStatusCode.Forbidden);
                await PostAsync("ChangeTier", new { id = target, membershipId = f.UpgradeId, justification = "TEST unauthorized tier" }, HttpStatusCode.Forbidden);
                await PostAsync("Suspend", new { id = target, justification = "TEST unauthorized suspension" }, HttpStatusCode.Forbidden);
                (await FingerprintAsync(target)).ShouldBe(before);
            }
        }

        [MemberAreaPostgreSqlFact]
        public async Task MultipleAreaAssignmentsNarrowIndependentlyOfTenantAndClientFilter()
        {
            var f = await SeedAsync();
            await UsingDbContextAsync(async db =>
            {
                var area = await db.Areas.IgnoreQueryFilters().SingleAsync(a => a.Id == f.OtherAreaId);
                db.AreaAdminAssignments.Add(AreaAdminAssignment.Assign(area, f.Administrator.Id, 1, DateTime.UtcNow.AddMinutes(-1)));
            });
            await SignInAsync(f);
            (await GetAsync($"GetAll?Keyword={f.Keyword}")).GetProperty("totalCount").GetInt32().ShouldBe(3);
            (await GetAsync($"GetAll?Keyword={f.Keyword}&AreaId={f.OtherAreaId}" )).GetProperty("totalCount").GetInt32().ShouldBe(1);
            await GetAsync($"Get?Id={f.OtherMemberId}");
            await GetAsync($"Get?Id={f.ForeignMemberId}", HttpStatusCode.Forbidden);
        }

        [MemberAreaPostgreSqlFact]
        public async Task RevokedInactiveDeletedAndUnassignedAreasFailClosed()
        {
            foreach (var state in new[] { "revoked", "inactive", "deleted", "future" })
            {
                var f = await SeedAsync();
                await UsingDbContextAsync(async db =>
                {
                    var area = await db.Areas.IgnoreQueryFilters().SingleAsync(a => a.Id == f.AreaId);
                    var grant = await db.AreaAdminAssignments.IgnoreQueryFilters().SingleAsync(a => a.Id == f.AssignmentId);
                    if (state == "revoked") grant.Revoke(DateTime.UtcNow);
                    if (state == "inactive") area.Deactivate();
                    if (state == "deleted") area.IsDeleted = true;
                    if (state == "future")
                    {
                        grant.Revoke(DateTime.UtcNow);
                        db.AreaAdminAssignments.Add(AreaAdminAssignment.Assign(area, f.Administrator.Id, 1, DateTime.UtcNow.AddDays(1)));
                    }
                });
                await SignInAsync(f);
                (await GetAsync($"GetAll?Keyword={f.Keyword}")).GetProperty("totalCount").GetInt32().ShouldBe(0);
                await GetAsync($"Get?Id={f.MemberId}", HttpStatusCode.Forbidden);
                await GetAsync($"Get?Id={f.UnassignedMemberId}", HttpStatusCode.Forbidden);
                await PostAsync("Suspend", new { id = f.MemberId, justification = "TEST unavailable authority" }, HttpStatusCode.Forbidden);
            }
        }

        [MemberAreaPostgreSqlFact]
        public async Task AssignmentAndAllTenantsDoNotReplaceActionSpecificPermission()
        {
            foreach (var host in new[] { false, true })
            {
                var f = await SeedAsync(host, allTenants: host, permissions: new[] { AquaPermissions.Admin.Members.View });
                await SignInAsync(f);
                await GetAsync($"Get?Id={f.MemberId}");
                await GetAsync($"GetMembershipOptions?Id={f.MemberId}", HttpStatusCode.Forbidden);
                var before = await FingerprintAsync(f.MemberId);
                await PostAsync("EditProfile", Edit(f), HttpStatusCode.Forbidden);
                await PostAsync("Suspend", new { id = f.MemberId, justification = "TEST missing permission" }, HttpStatusCode.Forbidden);
                await PostAsync("ChangeTier", new { id = f.MemberId, membershipId = f.UpgradeId, justification = "TEST missing permission" }, HttpStatusCode.Forbidden);
                (await FingerprintAsync(f.MemberId)).ShouldBe(before);
            }
            var noView = await SeedAsync(permissions: Array.Empty<string>());
            await SignInAsync(noView);
            await GetAsync("GetAll", HttpStatusCode.Forbidden);
        }

        [MemberAreaPostgreSqlFact]
        public async Task HostRequiresAllTenantsAndAuthorizedHostRetainsCrossTenantOversight()
        {
            var denied = await SeedAsync(host: true);
            await SignInAsync(denied);
            await GetAsync("GetAll", HttpStatusCode.Forbidden);
            await GetAsync($"Get?Id={denied.MemberId}", HttpStatusCode.Forbidden);
            await GetAsync($"GetMembershipOptions?Id={denied.MemberId}", HttpStatusCode.Forbidden);
            await PostAsync("EditProfile", Edit(denied), HttpStatusCode.Forbidden);
            await PostAsync("Suspend", new { id = denied.MemberId, justification = "TEST host without AllTenants" }, HttpStatusCode.Forbidden);
            await PostAsync("ChangeTier", new { id = denied.MemberId, membershipId = denied.UpgradeId, justification = "TEST host without AllTenants" }, HttpStatusCode.Forbidden);

            var f = await SeedAsync(host: true, allTenants: true);
            await SignInAsync(f);
            await GetAsync($"Get?Id={f.ForeignMemberId}");
            var foreign = await GetAsync($"GetAll?Keyword={f.Keyword}&TenantId={f.ForeignTenantId}");
            foreign.GetProperty("totalCount").GetInt32().ShouldBe(1);
            foreign.GetProperty("items")[0].GetProperty("tenantId").GetInt32().ShouldBe(f.ForeignTenantId);
            (await GetAsync($"GetAll?Keyword={f.Keyword}&TenantId=1&AreaId={f.ForeignAreaId}" )).GetProperty("totalCount").GetInt32().ShouldBe(0);
            var unassigned = await GetAsync($"Get?Id={f.UnassignedMemberId}");
            unassigned.GetProperty("areaId").ValueKind.ShouldBe(JsonValueKind.Null);
            unassigned.GetProperty("areaName").ValueKind.ShouldBe(JsonValueKind.Null);
            var options = await GetAsync($"GetMembershipOptions?Id={f.ForeignMemberId}");
            options.EnumerateArray().Any(plan => plan.GetProperty("id").GetInt32() == f.ForeignUpgradeId).ShouldBeTrue();
            options.EnumerateArray().Any(plan => plan.GetProperty("id").GetInt32() == f.GlobalPlanId).ShouldBeTrue();
            options.EnumerateArray().Any(plan => plan.GetProperty("id").GetInt32() == f.UpgradeId).ShouldBeFalse();
            var beforeWrongPlan = await FingerprintAsync(f.ForeignMemberId);
            // Existing ABP UserFriendlyException contract returns HTTP 500 for
            // this plan-validation category; require the specific semantic denial.
            var rejectedPlan = await PostAsync("ChangeTier", new { id = f.ForeignMemberId, membershipId = f.UpgradeId,
                justification = "TEST wrong Tenant plan" }, HttpStatusCode.InternalServerError);
            rejectedPlan.GetProperty("details").GetString().ShouldBe("The selected membership plan is unavailable or inactive.");
            (await FingerprintAsync(f.ForeignMemberId)).ShouldBe(beforeWrongPlan);
            await PostAsync("EditProfile", Edit(f, f.ForeignMemberId), HttpStatusCode.OK);
            await PostAsync("ChangeTier", new { id = f.ForeignMemberId, membershipId = f.ForeignUpgradeId,
                justification = "TEST authorized Host plan change" }, HttpStatusCode.OK);
            (await GetAsync($"Get?Id={f.ForeignMemberId}")).GetProperty("membershipId").GetInt32().ShouldBe(f.ForeignUpgradeId);
            await PostAsync("Suspend", new { id = f.ForeignMemberId, justification = "TEST authorized host suspension" }, HttpStatusCode.OK);
        }

        [MemberAreaPostgreSqlFact]
        public async Task RevocationCommittedWhileWaitingOnCustomerLockDeniesMutation()
        {
            var f = await SeedAsync();
            await SignInAsync(f);
            var before = await FingerprintAsync(f.MemberId);
            await using var blocker = new NpgsqlConnection(Environment.GetEnvironmentVariable("REPRO_PG_CONNECTION"));
            await blocker.OpenAsync();
            await using var transaction = await blocker.BeginTransactionAsync();
            await using (var command = new NpgsqlCommand("SELECT 1 FROM public.\"Customers\" WHERE \"Id\"=@id FOR UPDATE", blocker, transaction))
            {
                command.Parameters.AddWithValue("id", f.MemberId);
                await command.ExecuteScalarAsync();
            }
            var mutation = PostAsync("EditProfile", Edit(f), HttpStatusCode.Forbidden);
            try
            {
                await WaitForLockWaiterAsync();
                await using var revoke = new NpgsqlCommand("UPDATE public.\"AreaAdminAssignments\" SET \"RevokedAt\"=NOW() WHERE \"Id\"=@id", blocker, transaction);
                revoke.Parameters.AddWithValue("id", f.AssignmentId);
                await revoke.ExecuteNonQueryAsync();
            }
            finally { await transaction.CommitAsync(); }
            await mutation;
            (await FingerprintAsync(f.MemberId)).ShouldBe(before);
        }

        [MemberAreaPostgreSqlFact]
        public async Task MutationHoldsCustomerAreaAndAssignmentLocksUntilCommit()
        {
            var f = await SeedAsync();
            var pause = new PausedUpdater(IocManager.Resolve<IAdminCustomerProfileUpdater>());
            IocManager.IocContainer.Register(Component.For<IAdminCustomerProfileUpdater>().Instance(pause).IsDefault());
            await SignInAsync(f);
            var mutation = PostAsync("EditProfile", Edit(f), HttpStatusCode.OK);
            try
            {
                await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
                foreach (var sql in new[] {
                    "UPDATE public.\"Customers\" SET \"LastModificationTime\"=NOW() WHERE \"Id\"=@id",
                    "UPDATE public.\"Areas\" SET \"IsActive\"=false WHERE \"Id\"=@id",
                    "UPDATE public.\"AreaAdminAssignments\" SET \"RevokedAt\"=NOW() WHERE \"Id\"=@id" })
                {
                    await using var connection = new NpgsqlConnection(Environment.GetEnvironmentVariable("REPRO_PG_CONNECTION"));
                    await connection.OpenAsync();
                    await using (var timeout = new NpgsqlCommand("SET lock_timeout='300ms'", connection)) await timeout.ExecuteNonQueryAsync();
                    await using var command = new NpgsqlCommand(sql, connection);
                    command.Parameters.AddWithValue("id", sql.Contains("\"Customers\"") ? (object)f.MemberId : sql.Contains("\"Areas\"") ? f.AreaId : f.AssignmentId);
                    (await Should.ThrowAsync<PostgresException>(() => command.ExecuteNonQueryAsync())).SqlState.ShouldBe("55P03");
                }
            }
            finally { pause.Continue.TrySetResult(true); }
            await mutation;
        }

        [MemberAreaPostgreSqlFact]
        public async Task CustomerAndLegacyMemberAliasesCannotBypassMemberAreaScope()
        {
            var f = await SeedAsync(customerPermissions: true);
            await SignInAsync(f);
            using (var listResponse = await Client.GetAsync($"/api/services/app/AdminCustomer/GetAll?Keyword={f.Keyword}&MaxResultCount=100"))
            {
                var list = await ReadAsync(listResponse, HttpStatusCode.OK);
                list.GetProperty("items").EnumerateArray().ShouldAllBe(row =>
                    row.GetProperty("id").GetInt32() != f.OtherMemberId && row.GetProperty("id").GetInt32() != f.ForeignMemberId);
            }
            using (var legacyResponse = await Client.GetAsync("/api/services/app/Customer/GetAll"))
            {
                var list = await ReadAsync(legacyResponse, HttpStatusCode.OK);
                list.EnumerateArray().ShouldAllBe(row => row.GetProperty("id").GetInt32() != f.OtherMemberId &&
                    row.GetProperty("id").GetInt32() != f.ForeignMemberId);
            }
            foreach (var id in new[] { f.OtherMemberId, f.ForeignMemberId })
            {
                var before = await FingerprintAsync(id);
                using (var response = await Client.GetAsync($"/api/services/app/AdminCustomer/Get?Id={id}"))
                    await ReadAsync(response, HttpStatusCode.Forbidden);
                await CustomerCommandAsync("Update", CustomerInput(f, id), HttpStatusCode.Forbidden);
                await CustomerCommandAsync("Delete", new { id, justification = "TEST rejected alias removal" }, HttpStatusCode.Forbidden);
                (await FingerprintAsync(id)).ShouldBe(before);
            }
            await UsingDbContextAsync(async db => (await db.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == f.OtherMemberId)).IsDeleted = true);
            var removedBefore = await FingerprintAsync(f.OtherMemberId);
            await CustomerCommandAsync("Restore", CustomerInput(f, f.OtherMemberId), HttpStatusCode.Forbidden);
            await CustomerCommandAsync("Create", CustomerInput(f, f.OtherMemberId), HttpStatusCode.Forbidden);
            (await FingerprintAsync(f.OtherMemberId)).ShouldBe(removedBefore);
            // Legitimate assigned-Area update/delete/restore remains available.
            await CustomerCommandAsync("Update", CustomerInput(f, f.MemberId), HttpStatusCode.OK);
            await CustomerCommandAsync("Delete", new { id = f.MemberId, justification = "TEST authorized alias removal" }, HttpStatusCode.OK);
            await CustomerCommandAsync("Restore", CustomerInput(f, f.MemberId), HttpStatusCode.OK);
            await GetAsync($"Get?Id={f.MemberId}");
        }

        [MemberAreaPostgreSqlFact]
        public async Task CreatingMemberThroughCustomerAliasWithoutAuthorityRollsBackIdentityAndOutbox()
        {
            var f = await SeedAsync(host: true, customerPermissions: true);
            await SignInAsync(f);
            var email = "new-" + f.Keyword + "@example.test";
            await CustomerCommandAsync("Create", new { tenantId = f.ForeignTenantId, firstName = "TEST", lastName = "New Member",
                email, contactNumber = "+12025550100", homeAddress = "TEST member creation", password = Password,
                membershipId = f.ForeignPlanId, isActive = true, justification = "TEST uncommissioned member scope" }, HttpStatusCode.Forbidden);
            await UsingDbContextAsync(async db =>
            {
                (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.EmailAddress == email)).ShouldBeFalse();
                (await db.Customers.IgnoreQueryFilters().AnyAsync(c => c.Email.Value == email)).ShouldBeFalse();
                (await db.TransactionalEmailOutboxMessages.IgnoreQueryFilters().AnyAsync(m => m.Recipient == email)).ShouldBeFalse();
            });
        }

        [MemberAreaPostgreSqlFact]
        public async Task ConcurrentNonMemberPromotionCannotBypassCustomerAliasAreaScope()
        {
            var f = await SeedAsync(customerPermissions: true);
            var before = await FingerprintAsync(f.OtherMemberId);
            await UsingDbContextAsync(async db => (await db.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == f.OtherMemberId)).ChangeMembership(null));
            await SignInAsync(f);
            await using var blocker = new NpgsqlConnection(Environment.GetEnvironmentVariable("REPRO_PG_CONNECTION"));
            await blocker.OpenAsync();
            await using var transaction = await blocker.BeginTransactionAsync();
            await using (var command = new NpgsqlCommand("SELECT 1 FROM public.\"Customers\" WHERE \"Id\"=@id FOR UPDATE", blocker, transaction))
            {
                command.Parameters.AddWithValue("id", f.OtherMemberId);
                await command.ExecuteScalarAsync();
            }
            var mutation = CustomerCommandAsync("Update", new { id = f.OtherMemberId, firstName = "TEST unauthorized",
                lastName = "Change", email = "unauthorized-" + f.Keyword + "@example.test", contactNumber = "+12025550101",
                homeAddress = "TEST stale nonmember update", membershipId = (int?)null, isActive = false,
                justification = "TEST concurrent classification" }, HttpStatusCode.Forbidden);
            try
            {
                await WaitForLockWaiterAsync();
                await using var promote = new NpgsqlCommand("UPDATE public.\"Customers\" SET \"MembershipId\"=@plan WHERE \"Id\"=@id", blocker, transaction);
                promote.Parameters.AddWithValue("plan", f.PlanId); promote.Parameters.AddWithValue("id", f.OtherMemberId);
                await promote.ExecuteNonQueryAsync();
            }
            finally { await transaction.CommitAsync(); }
            await mutation;
            (await FingerprintAsync(f.OtherMemberId)).ShouldBe(before);
        }

        [MemberAreaPostgreSqlFact]
        public async Task DownstreamFailureRollsBackFlushedCustomerAndIdentityChanges()
        {
            var f = await SeedAsync();
            var before = await FingerprintAsync(f.MemberId);
            var failing = new FailingAfterSaveUpdater(IocManager.Resolve<IAdminCustomerProfileUpdater>(), IocManager.Resolve<IUnitOfWorkManager>());
            IocManager.IocContainer.Register(Component.For<IAdminCustomerProfileUpdater>().Instance(failing).IsDefault());
            await SignInAsync(f);
            await PostAsync("EditProfile", Edit(f), HttpStatusCode.InternalServerError);
            failing.Flushed.ShouldBeTrue();
            (await FingerprintAsync(f.MemberId)).ShouldBe(before);
        }

        [MemberAreaPostgreSqlFact]
        public async Task ImportWithoutAreaAuthorityRollsBackMemberIdentityAndCustomer()
        {
            var f = await SeedAsync(customerPermissions: true);
            await PrepareImportAreaAsync(f);
            await SignInAsync(f);
            var email = "import-denied-" + f.Keyword + "@example.test";
            var preview = await PreviewImportAsync(f, email, f.PlanId);
            await UsingDbContextAsync(async db => db.AreaAdminAssignments.Remove(
                await db.AreaAdminAssignments.IgnoreQueryFilters().SingleAsync(a => a.Id == f.AssignmentId)));
            var result = await ConfirmImportAsync(preview);
            result.GetProperty("importedRows").GetInt32().ShouldBe(0);
            result.GetProperty("failedRows").GetInt32().ShouldBe(1);
            result.GetProperty("errors")[0].GetProperty("message").GetString().ShouldBe("Authorization failed while importing this row.");
            await AssertNoIdentityOrCustomerAsync(email);
        }

        [MemberAreaPostgreSqlFact]
        public async Task RestoreWithDemotionHoldsOriginalAreaAndGrantThroughCommit()
        {
            var f = await SeedAsync(customerPermissions: true);
            await UsingDbContextAsync(async db => (await db.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == f.MemberId)).IsDeleted = true);
            var pause = new PausedUpdater(IocManager.Resolve<IAdminCustomerProfileUpdater>());
            IocManager.IocContainer.Register(Component.For<IAdminCustomerProfileUpdater>().Instance(pause).IsDefault());
            await SignInAsync(f);
            var restore = CustomerCommandAsync("Restore", DemotingRestore(f), HttpStatusCode.OK);
            try
            {
                await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
                foreach (var sql in new[] {
                    "UPDATE public.\"Areas\" SET \"IsActive\"=false WHERE \"Id\"=@id",
                    "UPDATE public.\"AreaAdminAssignments\" SET \"RevokedAt\"=NOW() WHERE \"Id\"=@id" })
                {
                    await using var connection = new NpgsqlConnection(Environment.GetEnvironmentVariable("REPRO_PG_CONNECTION"));
                    await connection.OpenAsync();
                    await using (var timeout = new NpgsqlCommand("SET lock_timeout='300ms'", connection)) await timeout.ExecuteNonQueryAsync();
                    await using var command = new NpgsqlCommand(sql, connection);
                    command.Parameters.AddWithValue("id", sql.Contains("\"Areas\"") ? (object)f.AreaId : f.AssignmentId);
                    (await Should.ThrowAsync<PostgresException>(() => command.ExecuteNonQueryAsync())).SqlState.ShouldBe("55P03");
                }
            }
            finally { pause.Continue.TrySetResult(true); await restore; }
            await UsingDbContextAsync(async db =>
            {
                var restored = await db.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == f.MemberId);
                restored.IsDeleted.ShouldBeFalse(); restored.MembershipId.ShouldBeNull();
                (await db.AreaAdminAssignments.IgnoreQueryFilters().SingleAsync(a => a.Id == f.AssignmentId)).Revoke(DateTime.UtcNow);
            });
        }

        [MemberAreaPostgreSqlFact]
        public async Task ImportAllowsAssignedMembersAndPreservesNonmemberRows()
        {
            foreach (var host in new[] { false, true })
            {
                var f = await SeedAsync(host, allTenants: host, customerPermissions: true);
                await PrepareImportAreaAsync(f);
                await SignInAsync(f);
                var email = "import-allowed-" + f.Keyword + "@example.test";
                var preview = await PreviewImportAsync(f, email, f.PlanId);
                var result = await ConfirmImportAsync(preview);
                result.GetProperty("importedRows").GetInt32().ShouldBe(1,
                    $"Host={host}; row errors={result.GetProperty("errors").GetRawText()}");
                result.GetProperty("failedRows").GetInt32().ShouldBe(0);
                await UsingDbContextAsync(async db =>
                {
                    var customer = await db.Customers.IgnoreQueryFilters().Include(c => c.User).SingleAsync(c => c.Email.Value == email);
                    customer.AreaId.ShouldBe(f.AreaId); customer.TenantId.ShouldBe(1);
                    customer.MembershipId.ShouldBe(f.PlanId); customer.User.TenantId.ShouldBe(1);
                });
            }
            // Import permission still authorizes a non-member without member Area authority.
            var nonmember = await SeedAsync(customerPermissions: true);
            await PrepareImportAreaAsync(nonmember);
            await SignInAsync(nonmember);
            await UsingDbContextAsync(async db => (await db.AreaAdminAssignments.IgnoreQueryFilters()
                .SingleAsync(a => a.Id == nonmember.AssignmentId)).Revoke(DateTime.UtcNow));
            var nonmemberEmail = "nonmember-" + nonmember.Keyword + "@example.test";
            var nonmemberPreview = await PreviewImportAsync(nonmember, nonmemberEmail, null);
            (await ConfirmImportAsync(nonmemberPreview)).GetProperty("importedRows").GetInt32().ShouldBe(1);
            await UsingDbContextAsync(async db => (await db.Customers.IgnoreQueryFilters()
                .SingleAsync(c => c.Email.Value == nonmemberEmail)).MembershipId.ShouldBeNull());
        }

        [MemberAreaPostgreSqlFact]
        public async Task ImportRechecksRevokedFutureInactiveAndInsufficientAreaAuthority()
        {
            foreach (var state in new[] { "revoked", "future", "inactive", "other-area", "host-without-alltenants" })
            {
                var f = await SeedAsync(host: state == "host-without-alltenants", customerPermissions: true);
                await PrepareImportAreaAsync(f);
                await SignInAsync(f);
                var email = "import-state-" + f.Keyword + "@example.test";
                var preview = await PreviewImportAsync(f, email, f.PlanId);
                await UsingDbContextAsync(async db =>
                {
                    if (f.AssignmentId.HasValue)
                    {
                        var assignment = await db.AreaAdminAssignments.IgnoreQueryFilters().SingleAsync(a => a.Id == f.AssignmentId);
                        assignment.Revoke(DateTime.UtcNow);
                        var area = await db.Areas.IgnoreQueryFilters().SingleAsync(a => a.Id == f.AreaId);
                        if (state == "future") db.AreaAdminAssignments.Add(AreaAdminAssignment.Assign(area,
                            f.Administrator.Id, 1, DateTime.UtcNow.AddDays(1)));
                        if (state == "other-area")
                        {
                            var other = await db.Areas.IgnoreQueryFilters().SingleAsync(a => a.Id == f.OtherAreaId);
                            other.Activate();
                            db.AreaAdminAssignments.Add(AreaAdminAssignment.Assign(other, f.Administrator.Id, 1, DateTime.UtcNow.AddMinutes(-1)));
                            other.Deactivate();
                        }
                        if (state == "inactive") area.Deactivate();
                    }
                });
                var result = await ConfirmImportAsync(preview);
                result.GetProperty("importedRows").GetInt32().ShouldBe(0, state);
                result.GetProperty("failedRows").GetInt32().ShouldBe(1, state);
                if (state != "inactive") result.GetProperty("errors")[0].GetProperty("message").GetString()
                    .ShouldBe("Authorization failed while importing this row.", state);
                await AssertNoIdentityOrCustomerAsync(email);
            }
        }

        [MemberAreaPostgreSqlFact]
        public async Task ImportWaitingOnAreaLockDeniesCommittedRevocationAndRollsBack()
        {
            var f = await SeedAsync(customerPermissions: true);
            await PrepareImportAreaAsync(f); await SignInAsync(f);
            var email = "import-race-" + f.Keyword + "@example.test";
            var preview = await PreviewImportAsync(f, email, f.PlanId);
            await using var blocker = new NpgsqlConnection(Environment.GetEnvironmentVariable("REPRO_PG_CONNECTION"));
            await blocker.OpenAsync(); await using var transaction = await blocker.BeginTransactionAsync();
            await using (var command = new NpgsqlCommand("SELECT 1 FROM public.\"Areas\" WHERE \"Id\"=@id FOR UPDATE", blocker, transaction))
            { command.Parameters.AddWithValue("id", f.AreaId); await command.ExecuteScalarAsync(); }
            var import = ConfirmImportAsync(preview);
            try
            {
                await WaitForLockWaiterAsync();
                await using var revoke = new NpgsqlCommand("UPDATE public.\"AreaAdminAssignments\" SET \"RevokedAt\"=NOW() WHERE \"Id\"=@id", blocker, transaction);
                revoke.Parameters.AddWithValue("id", f.AssignmentId); await revoke.ExecuteNonQueryAsync();
            }
            finally { await transaction.CommitAsync(); }
            var result = await import;
            result.GetProperty("importedRows").GetInt32().ShouldBe(0);
            result.GetProperty("errors")[0].GetProperty("message").GetString().ShouldBe("Authorization failed while importing this row.");
            await AssertNoIdentityOrCustomerAsync(email);
        }

        [MemberAreaPostgreSqlFact]
        public async Task RestoreWithDemotionDeniesRevocationCommittedDuringCustomerLockWait()
        {
            var f = await SeedAsync(customerPermissions: true);
            await UsingDbContextAsync(async db => (await db.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == f.MemberId)).IsDeleted = true);
            var before = await FingerprintAsync(f.MemberId); await SignInAsync(f);
            await using var blocker = new NpgsqlConnection(Environment.GetEnvironmentVariable("REPRO_PG_CONNECTION"));
            await blocker.OpenAsync(); await using var transaction = await blocker.BeginTransactionAsync();
            await using (var command = new NpgsqlCommand("SELECT 1 FROM public.\"Customers\" WHERE \"Id\"=@id FOR UPDATE", blocker, transaction))
            { command.Parameters.AddWithValue("id", f.MemberId); await command.ExecuteScalarAsync(); }
            var restore = CustomerCommandAsync("Restore", DemotingRestore(f), HttpStatusCode.Forbidden);
            try
            {
                await WaitForLockWaiterAsync();
                await using var revoke = new NpgsqlCommand("UPDATE public.\"AreaAdminAssignments\" SET \"RevokedAt\"=NOW() WHERE \"Id\"=@id", blocker, transaction);
                revoke.Parameters.AddWithValue("id", f.AssignmentId); await revoke.ExecuteNonQueryAsync();
            }
            finally { await transaction.CommitAsync(); }
            await restore; (await FingerprintAsync(f.MemberId)).ShouldBe(before);
        }

        [MemberAreaPostgreSqlFact]
        public async Task RestoreWithDemotionRollsBackFlushedDeletedCustomerAndIdentityOnFailure()
        {
            var f = await SeedAsync(customerPermissions: true);
            await UsingDbContextAsync(async db => (await db.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == f.MemberId)).IsDeleted = true);
            var before = await FingerprintAsync(f.MemberId);
            var failing = new FailingAfterSaveUpdater(IocManager.Resolve<IAdminCustomerProfileUpdater>(), IocManager.Resolve<IUnitOfWorkManager>());
            IocManager.IocContainer.Register(Component.For<IAdminCustomerProfileUpdater>().Instance(failing).IsDefault());
            await SignInAsync(f);
            await CustomerCommandAsync("Restore", DemotingRestore(f), HttpStatusCode.InternalServerError);
            failing.Flushed.ShouldBeTrue(); (await FingerprintAsync(f.MemberId)).ShouldBe(before);
            await UsingDbContextAsync(async db => (await db.TransactionalEmailOutboxMessages.IgnoreQueryFilters()
                .AnyAsync(m => m.Recipient == f.Keyword + "-first@example.test")).ShouldBeFalse());
        }

        private async Task PrepareImportAreaAsync(Fixture f)
        {
            await UsingDbContextAsync(async db =>
            {
                foreach (var area in await db.Areas.IgnoreQueryFilters().Where(a => a.TenantId == 1 && a.Id != f.AreaId).ToListAsync())
                    area.Deactivate();
                if (!await db.Roles.IgnoreQueryFilters().AnyAsync(r => r.TenantId == 1 && r.Name == "Guest"))
                    db.Roles.Add(new Role(1, "Guest", "Guest"));
            });
        }

        private async Task<string> PreviewImportAsync(Fixture f, string email, int? membershipId)
        {
            var csv = "FirstName,LastName,Email,ContactNumber,HomeAddress,MembershipId,IsActive\r\n" +
                $"TEST,Import,{email},+12025550100,TEST isolated import,{membershipId},true\r\n";
            return await PreviewCsvAsync(csv);
        }

        private async Task<string> PreviewCsvAsync(string csv, int tenantId = 1)
        {
            using var response = await Client.PostAsync("/api/services/app/CustomerImport/Preview",
                new StringContent(JsonSerializer.Serialize(new { tenantId, fileName = "synthetic.csv",
                    contentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(csv)) }, Json), Encoding.UTF8, "application/json"));
            var result = await ReadAsync(response, HttpStatusCode.OK);
            result.GetProperty("canImport").GetBoolean().ShouldBeTrue(result.GetProperty("errors").GetRawText());
            return result.GetProperty("previewId").GetString();
        }

        private async Task<JsonElement> ConfirmImportAsync(string preview)
        {
            using var response = await Client.PostAsync("/api/services/app/CustomerImport/Import",
                new StringContent(JsonSerializer.Serialize(new { previewId = preview }, Json), Encoding.UTF8, "application/json"));
            return await ReadAsync(response, HttpStatusCode.OK);
        }

        private async Task AssertNoIdentityOrCustomerAsync(string email)
        {
            await UsingDbContextAsync(async db =>
            {
                (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.EmailAddress == email)).ShouldBeFalse();
                (await db.Customers.IgnoreQueryFilters().AnyAsync(c => c.Email.Value == email)).ShouldBeFalse();
                (await db.TransactionalEmailOutboxMessages.IgnoreQueryFilters().AnyAsync(m => m.Recipient == email)).ShouldBeFalse();
            });
        }

        private static object DemotingRestore(Fixture f) => new { customerId = f.MemberId,
            firstName = "TEST", lastName = "Restored Nonmember", email = f.Keyword + "-first@example.test",
            contactNumber = "+12025550100", homeAddress = "TEST restored isolated fixture",
            membershipId = (int?)null, isActive = true, justification = "TEST source-member restore with demotion" };

        [MemberAreaPostgreSqlFact]
        public async Task ImportKeepsSuccessfulNonmemberRowsWhenMemberRowIsDenied()
        {
            var f = await SeedAsync(customerPermissions: true);
            await PrepareImportAreaAsync(f); await SignInAsync(f);
            var guest1 = "mixed-first-" + f.Keyword + "@example.test";
            var denied = "mixed-denied-" + f.Keyword + "@example.test";
            var guest2 = "mixed-last-" + f.Keyword + "@example.test";
            var csv = "FirstName,LastName,Email,ContactNumber,HomeAddress,MembershipId,IsActive\r\n" +
                $"TEST,First,{guest1},+12025550100,TEST isolated import,,true\r\n" +
                $"TEST,Denied,{denied},+12025550100,TEST isolated import,{f.PlanId},true\r\n" +
                $"TEST,Last,{guest2},+12025550100,TEST isolated import,,true\r\n";
            var preview = await PreviewCsvAsync(csv);
            await UsingDbContextAsync(async db => (await db.AreaAdminAssignments.IgnoreQueryFilters()
                .SingleAsync(a => a.Id == f.AssignmentId)).Revoke(DateTime.UtcNow));
            var result = await ConfirmImportAsync(preview);
            result.GetProperty("totalRows").GetInt32().ShouldBe(3);
            result.GetProperty("importedRows").GetInt32().ShouldBe(2);
            result.GetProperty("failedRows").GetInt32().ShouldBe(1);
            result.GetProperty("errors")[0].GetProperty("rowNumber").GetInt32().ShouldBe(3);
            await AssertNoIdentityOrCustomerAsync(denied);
            await UsingDbContextAsync(async db =>
            {
                (await db.Customers.IgnoreQueryFilters().CountAsync(c => c.Email.Value == guest1 || c.Email.Value == guest2)).ShouldBe(2);
                (await db.Users.IgnoreQueryFilters().CountAsync(u => u.EmailAddress == guest1 || u.EmailAddress == guest2)).ShouldBe(2);
            });
        }

        [MemberAreaPostgreSqlFact]
        public async Task ImportPermissionAndTenantCannotBeReplacedByMemberOrHostPermissions()
        {
            var f = await SeedAsync(customerPermissions: true, importPermission: false);
            await PrepareImportAreaAsync(f); await SignInAsync(f);
            using (var response = await Client.PostAsync("/api/services/app/CustomerImport/Preview",
                new StringContent(JsonSerializer.Serialize(new { tenantId = 1, fileName = "synthetic.csv",
                    contentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("TEST")) }, Json), Encoding.UTF8, "application/json")))
                await ReadAsync(response, HttpStatusCode.Forbidden);
            // Assert the actual action boundary, independently of preview parsing.
            using (var response = await Client.PostAsync("/api/services/app/CustomerImport/Import",
                new StringContent("{\"previewId\":\"synthetic-unissued-preview\"}", Encoding.UTF8, "application/json")))
                await ReadAsync(response, HttpStatusCode.Forbidden);
            var tenant = await SeedAsync(customerPermissions: true, allTenants: true);
            await PrepareImportAreaAsync(tenant); await SignInAsync(tenant);
            using var crossTenant = await Client.PostAsync("/api/services/app/CustomerImport/Preview",
                new StringContent(JsonSerializer.Serialize(new { tenantId = tenant.ForeignTenantId,
                    fileName = "synthetic.csv", contentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("TEST")) }, Json), Encoding.UTF8, "application/json"));
            await ReadAsync(crossTenant, HttpStatusCode.Forbidden);
        }

        [MemberAreaPostgreSqlFact]
        public async Task RemovedAuthorityLockApiRejectsLiveRowsAndLiveApprovalStillRejectsRemovedRows()
        {
            var f = await SeedAsync();
            var manager = IocManager.Resolve<IUnitOfWorkManager>();
            var stabilizer = IocManager.Resolve<IAQGreenApprovalAuthorityStabilizer>();
            var options = new UnitOfWorkOptions { Scope = System.Transactions.TransactionScopeOption.RequiresNew,
                IsTransactional = true, IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted };
            using (var uow = manager.Begin(options))
            {
                await Should.ThrowAsync<AQGreenPlacementConflictException>(() => stabilizer.StabilizeRemovedCustomerAsync(1, f.MemberId, f.Administrator.Id));
                await uow.CompleteAsync();
            }
            await UsingDbContextAsync(async db => (await db.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == f.MemberId)).IsDeleted = true);
            using (var uow = manager.Begin(options))
            {
                await Should.ThrowAsync<AQGreenPlacementConflictException>(() => stabilizer.StabilizeAsync(1, f.MemberId, f.Administrator.Id));
                (await stabilizer.StabilizeRemovedCustomerAsync(1, f.MemberId, f.Administrator.Id)).ShouldBe(f.AreaId);
                await uow.CompleteAsync();
            }
            await UsingDbContextAsync(async db => (await db.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == f.MemberId)).IsDeleted.ShouldBeTrue());
        }

        private async Task<Fixture> SeedAsync(bool host = false, bool allTenants = false, string[] permissions = null,
            bool customerPermissions = false, bool importPermission = true)
        {
            return await UsingDbContextAsync(async db =>
            {
                db.Database.IsNpgsql().ShouldBeTrue();
                db.SuppressAutoSetTenantId = true;
                if (!await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == 1))
                {
                    var tenant = new Tenant("Default", "TEST member authorization Tenant");
                    db.Tenants.Add(tenant); await db.SaveChangesAsync(); tenant.Id.ShouldBe(1);
                }
                var suffix = Guid.NewGuid().ToString("N")[..10];
                var keyword = "scope" + suffix;
                var foreignTenant = new Tenant("foreign" + suffix, "TEST foreign Tenant");
                db.Tenants.Add(foreignTenant); await db.SaveChangesAsync();
                var area = Area.Create(1, "A" + suffix, "TEST assigned Area");
                var other = Area.Create(1, "B" + suffix, "TEST other Area");
                var foreign = Area.Create(foreignTenant.Id, "C" + suffix, "TEST foreign Area");
                db.Areas.AddRange(area, other, foreign);
                var plan = Membership.Create(1, keyword + "-plan", "TEST plan", MembershipType.Jasper);
                var upgrade = Membership.Create(1, keyword + "-upgrade", "TEST upgrade", MembershipType.BusinessPremier);
                var foreignPlan = Membership.Create(foreignTenant.Id, keyword + "-foreign-plan", "TEST foreign plan", MembershipType.Jasper);
                var foreignUpgrade = Membership.Create(foreignTenant.Id, keyword + "-foreign-upgrade", "TEST foreign upgrade", MembershipType.BusinessPremier);
                var globalPlan = Membership.Create(null, keyword + "-global", "TEST global plan", MembershipType.Jasper);
                db.Memberships.AddRange(plan, upgrade, foreignPlan, foreignUpgrade, globalPlan);
                var admin = NewUser(host ? null : 1, "admin" + suffix);
                admin.SetRole(AquaUserRole.SystemAdmin);
                db.Users.Add(admin);
                var role = new Role(admin.TenantId, "member-scope" + suffix, "TEST member operator");
                db.Roles.Add(role); await db.SaveChangesAsync();
                db.UserRoles.Add(new UserRole(admin.TenantId, admin.Id, role.Id));
                admin.TenantId.ShouldBe(host ? (int?)null : 1);
                var granted = new List<string> { AquaPermissions.Admin.Default, AquaPermissions.Admin.Members.Default };
                granted.AddRange(permissions ?? MemberPermissions);
                if (customerPermissions) granted.AddRange(new[] { AquaPermissions.Admin.Customers.Default,
                    AquaPermissions.Admin.Customers.View, AquaPermissions.Admin.Customers.Edit, AquaPermissions.Admin.Customers.Delete,
                    AquaPermissions.Admin.Customers.Create, AquaPermissions.Members.View });
                if (customerPermissions && importPermission) granted.Add(AquaPermissions.Admin.Customers.Import);
                if (allTenants) granted.Add(AquaPermissions.Admin.AllTenants);
                db.Permissions.AddRange(granted.Select(name => new RolePermissionSetting { TenantId = admin.TenantId, RoleId = role.Id, Name = name, IsGranted = true }));
                var assignment = host ? null : AreaAdminAssignment.Assign(area, admin.Id, 1, DateTime.UtcNow.AddMinutes(-1));
                if (assignment != null) db.AreaAdminAssignments.Add(assignment);
                if (!host)
                {
                    var operatorCustomer = Customer.Create(1, admin.Id, "TEST member operator", new EmailAddress(admin.EmailAddress), user: admin);
                    operatorCustomer.AssignInitialArea(area, DateTime.UtcNow.AddMinutes(-1), "TEST operator ownership");
                    db.Customers.Add(operatorCustomer);
                }
                foreach (var tenantId in new[] { 1, foreignTenant.Id })
                    foreach (var roleName in new[] { "Member", "Guest" })
                        if (!await db.Roles.IgnoreQueryFilters().AnyAsync(r => r.TenantId == tenantId && r.Name == roleName))
                            db.Roles.Add(new Role(tenantId, roleName, roleName));
                await db.SaveChangesAsync();
                async Task<Customer> Member(string name, int tenantId, Area memberArea, int membershipId)
                {
                    var user = NewUser(tenantId, name); db.Users.Add(user); await db.SaveChangesAsync();
                    var member = Customer.Create(tenantId, user.Id, "TEST " + name, new EmailAddress(user.EmailAddress), membershipId, user);
                    if (memberArea != null) member.AssignInitialArea(memberArea, DateTime.UtcNow.AddMinutes(-1), "TEST fixture");
                    db.Customers.Add(member); await db.SaveChangesAsync(); return member;
                }
                var first = await Member(keyword + "-first", 1, area, plan.Id);
                first.User.UpdateContactDetails(first.User.PhoneNumber, keyword + "-address");
                await Member(keyword + "-second", 1, area, plan.Id);
                var secondArea = await Member(keyword + "-other", 1, other, plan.Id);
                var foreignMember = await Member(keyword + "-foreign", foreignTenant.Id, foreign, foreignPlan.Id);
                var unassigned = await Member(keyword + "-unassigned", 1, null, plan.Id);
                // Deliberately inconsistent legacy plan must not distort visible page totals.
                await Member(keyword + "-invalid-plan", 1, area, foreignPlan.Id);
                await db.SaveChangesAsync();
                return new Fixture(admin, keyword, first.Id, secondArea.Id, foreignMember.Id, unassigned.Id,
                    foreignTenant.Id, plan.Id, foreignPlan.Id, foreignUpgrade.Id, globalPlan.Id, upgrade.Id, area.Id, other.Id, foreign.Id, assignment?.Id);
            });
        }

        private static User NewUser(int? tenantId, string name)
        {
            var user = new User { TenantId = tenantId, UserName = name, Name = "TEST", Surname = "Member",
                EmailAddress = name + "@example.test",
                IsActive = true, IsEmailConfirmed = true };
            user.UpdateContactDetails("+12025550100", "TEST isolated fixture");
            user.SetRole(AquaUserRole.Member); user.SetNormalizedNames();
            user.Password = new PasswordHasher<User>(Options.Create(new PasswordHasherOptions())).HashPassword(user, Password);
            return user;
        }

        private async Task SignInAsync(Fixture fixture)
        {
            Client.DefaultRequestHeaders.Clear(); AbpSession.UserId = null; AbpSession.TenantId = null;
            var authentication = await AuthenticateAsync(fixture.Administrator.TenantId.HasValue ? "Default" : null,
                new AuthenticateModel { UserNameOrEmailAddress = fixture.Administrator.UserName, Password = Password });
            AbpSession.UserId = null; AbpSession.TenantId = null;
            var claims = new JwtSecurityTokenHandler().ReadJwtToken(authentication.AccessToken).Claims.ToList();
            claims.SingleOrDefault(claim => claim.Type == AbpClaimTypes.TenantId)?.Value
                .ShouldBe(fixture.Administrator.TenantId?.ToString());
            using var sessionResponse = await Client.GetAsync("/api/services/app/Session/GetCurrentLoginInformations");
            var session = await ReadAsync(sessionResponse, HttpStatusCode.OK);
            session.GetProperty("user").GetProperty("id").GetInt64().ShouldBe(fixture.Administrator.Id);
            var tenant = session.GetProperty("tenant");
            (tenant.ValueKind == JsonValueKind.Null ? (int?)null : tenant.GetProperty("id").GetInt32())
                .ShouldBe(fixture.Administrator.TenantId);
        }

        private async Task<JsonElement> GetAsync(string path, HttpStatusCode expected = HttpStatusCode.OK)
        {
            using var response = await Client.GetAsync(Endpoint + path);
            return await ReadAsync(response, expected);
        }

        private async Task<JsonElement> PostAsync(string path, object input, HttpStatusCode expected)
        {
            using var response = await Client.PostAsync(Endpoint + path, new StringContent(JsonSerializer.Serialize(input, Json), Encoding.UTF8, "application/json"));
            return await ReadAsync(response, expected);
        }

        private async Task<JsonElement> CustomerCommandAsync(string path, object input, HttpStatusCode expected)
        {
            var method = path == "Update" ? HttpMethod.Put : path == "Delete" ? HttpMethod.Delete : HttpMethod.Post;
            var query = "";
            if (method == HttpMethod.Delete)
            {
                var fields = JsonSerializer.SerializeToElement(input, Json);
                query = "?Id=" + fields.GetProperty("id").GetInt32() + "&Justification=" +
                    Uri.EscapeDataString(fields.GetProperty("justification").GetString());
            }
            using var request = new HttpRequestMessage(method, "/api/services/app/AdminCustomer/" + path + query)
            { Content = new StringContent(JsonSerializer.Serialize(input, Json), Encoding.UTF8, "application/json") };
            using var response = await Client.SendAsync(request);
            return await ReadAsync(response, expected);
        }

        private static object CustomerInput(Fixture f, int id) => new { id, customerId = id, tenantId = 1,
            firstName = "TEST", lastName = "Member", email = f.Keyword + (id == f.OtherMemberId ? "-other" : "-first") + "@example.test",
            contactNumber = "+12025550100", homeAddress = "TEST alias fixture", password = Password,
            membershipId = f.PlanId, isActive = true, justification = "TEST member record alias" };

        private static async Task<JsonElement> ReadAsync(HttpResponseMessage response, HttpStatusCode expected)
        {
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            response.StatusCode.ShouldBe(expected, body.RootElement.TryGetProperty("error", out var error) ? error.GetRawText() : "Unexpected HTTP status");
            if (expected == HttpStatusCode.OK) return body.RootElement.GetProperty("result").Clone();
            body.RootElement.GetProperty("success").GetBoolean().ShouldBeFalse();
            return body.RootElement.GetProperty("error").Clone();
        }

        private async Task<string> FingerprintAsync(int id) => await UsingDbContextAsync(async db =>
        {
            var member = await db.Customers.IgnoreQueryFilters().Include(c => c.User).SingleAsync(c => c.Id == id);
            return JsonSerializer.Serialize(new { member.Name, member.MembershipId, member.AreaId, member.IsActive, member.IsDeleted,
                member.DeletionTime, member.DeleterUserId,
                UserName = member.User.Name, member.User.Surname, member.User.EmailAddress, member.User.PhoneNumber, member.User.HomeAddress,
                UserActive = member.User.IsActive, member.User.SecurityStamp, member.User.IsEmailConfirmed,
                RequiresPasswordReset = member.User.RequiresPasswordReset() });
        });

        private static object Edit(Fixture f, int? target = null) => new { id = target ?? f.MemberId,
            firstName = "TEST Updated", lastName = "Member", email = "scope-edit-" + f.Keyword + "@example.test",
            contactNumber = "+12025550101", homeAddress = "TEST updated isolated fixture", justification = "TEST authorized fixture correction" };

        private static async Task WaitForLockWaiterAsync()
        {
            await using var connection = new NpgsqlConnection(Environment.GetEnvironmentVariable("REPRO_PG_CONNECTION"));
            await connection.OpenAsync();
            for (var attempt = 0; attempt < 200; attempt++)
            {
                await using var command = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE datname=current_database() AND wait_event_type='Lock' AND pid<>pg_backend_pid())", connection);
                if ((bool)await command.ExecuteScalarAsync()) return;
                await Task.Delay(25);
            }
            throw new TimeoutException("Expected the isolated member mutation to wait on its authority lock.");
        }

        private sealed class PausedUpdater : IAdminCustomerProfileUpdater
        {
            private readonly IAdminCustomerProfileUpdater _inner;
            public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource<bool> Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public PausedUpdater(IAdminCustomerProfileUpdater inner) => _inner = inner;
            public async Task UpdateAsync(Customer customer, AdminCustomerProfileUpdate update)
            {
                Entered.TrySetResult(true);
                await Continue.Task.WaitAsync(TimeSpan.FromSeconds(30));
                await _inner.UpdateAsync(customer, update);
            }
        }

        private sealed class FailingAfterSaveUpdater : IAdminCustomerProfileUpdater
        {
            private readonly IAdminCustomerProfileUpdater _inner;
            private readonly IUnitOfWorkManager _units;
            public bool Flushed { get; private set; }
            public FailingAfterSaveUpdater(IAdminCustomerProfileUpdater inner, IUnitOfWorkManager units)
            { _inner = inner; _units = units; }
            public async Task UpdateAsync(Customer customer, AdminCustomerProfileUpdate update)
            {
                await _inner.UpdateAsync(customer, update);
                await _units.Current.SaveChangesAsync();
                Flushed = true;
                throw new InvalidOperationException("TEST failure after database flush");
            }
        }

        private sealed record Fixture(User Administrator, string Keyword, int MemberId, int OtherMemberId, int ForeignMemberId,
            int UnassignedMemberId, int ForeignTenantId, int PlanId, int ForeignPlanId, int ForeignUpgradeId, int GlobalPlanId, int UpgradeId, Guid AreaId, Guid OtherAreaId, Guid ForeignAreaId, Guid? AssignmentId);
    }
}
