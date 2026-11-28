using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Transactions;
using Abp.Domain.Uow;
using Abp.EntityFrameworkCore;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using OfficeOpenXml;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Web.Tests.Controllers
{
    public sealed partial class AQGreenExistingMemberDiscoveryPostgreSqlTests
    {
        private const string RosterEndpoint = "/api/services/app/AdminProgrammeParticipation/PreviewLegacyAQGreenRoster";
        private const string RosterHeaders = "OldMemberId,FirstName,LastName,Email,ContactNumber,HomeAddress,ClubMemberNumber,Programme,MembershipStatus,NetworkParentOldMemberId,SponsorOldMemberId,PlacementSlot,IsRoot";
        private const string AssociationHeaders = "OldMemberId,MemberName,Email,ContactNumber,NetworkOwnerName,NetworkOwnerOldMemberId,RecordedNetworkPosition";

        [DiscoveryPostgreSqlFact]
        public async Task RosterPreview_CsvMatchesAndClassifiesWithoutChangingBusinessRows()
        {
            var fixture = await SeedAsync(importPermission: true);
            var keys = await UsingDbContextAsync(async db => await db.Customers.IgnoreQueryFilters()
                .Where(c => new[] { fixture.V2Only.CustomerId, fixture.OtherArea.CustomerId, fixture.Foreign.CustomerId,
                    fixture.LegacyOnly.CustomerId }.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => new { c.ClubMemberNumber, Email = c.Email.Value }));
            string Matched(string id, Subject subject) => ExplicitRow(id, keys[subject.CustomerId].Email, keys[subject.CustomerId].ClubMemberNumber);
            var rows = new[] { Matched("0001", fixture.V2Only), ExplicitRow("0002"), ExplicitRow("0002"),
                Matched("0003", fixture.OtherArea), Matched("0004", fixture.Foreign),
                ExplicitRow("0005", keys[fixture.LegacyOnly.CustomerId].Email) };
            await SignInAsync(fixture.Reviewer);
            var before = await BusinessSnapshotAsync();

            var result = await PostRosterAsync("example.csv", Csv(rows));

            result.GetProperty("acceptedRows").GetInt32().ShouldBe(1);
            result.GetProperty("duplicateRows").GetInt32().ShouldBe(2);
            result.GetProperty("unmatchedRows").GetInt32().ShouldBe(2);
            result.GetProperty("ambiguousRows").GetInt32().ShouldBe(1);
            var reported = result.GetProperty("rows").EnumerateArray().ToArray();
            reported.Select(row => row.GetProperty("oldMemberId").GetString())
                .ShouldBe(new[] { "0001", "0002", "0002", "0003", "0004", "0005" });
            reported[0].GetProperty("matchedCustomerId").GetInt32().ShouldBe(fixture.V2Only.CustomerId);
            reported[0].GetProperty("existingParticipationId").GetGuid().ShouldBe(fixture.V2Only.ParticipationId.Value);
            foreach (var row in reported.Skip(1)) row.GetProperty("matchedCustomerId").ValueKind.ShouldBe(JsonValueKind.Null);
            reported[5].GetProperty("issues").EnumerateArray().Select(issue => issue.GetString())
                .ShouldContain("AccountMatchRequiresCorroboration");
            (await BusinessSnapshotAsync()).ShouldBe(before);
        }

        [DiscoveryPostgreSqlFact]
        public async Task RosterPreview_UsesExistingMembershipEvidenceWithoutExposingForeignPlansOrWritingHistory()
        {
            var fixture = await SeedAsync(importPermission: true);
            var rows = await UsingDbContextAsync(async context =>
            {
                var shared = Membership.Create(null, "Synthetic shared AQGreen", "Fixture", MembershipType.AQGreen);
                var foreign = Membership.Create(fixture.ForeignTenantId, "Synthetic foreign AQGreen", "Fixture", MembershipType.AQGreen);
                context.Memberships.AddRange(shared, foreign);
                await context.SaveChangesAsync();

                var ids = new[] { fixture.LegacyOnly.CustomerId, fixture.Both.CustomerId,
                    fixture.Unversioned.CustomerId, fixture.OtherArea.CustomerId };
                var customers = await context.Customers.IgnoreQueryFilters().Where(c => ids.Contains(c.Id))
                    .ToDictionaryAsync(c => c.Id);
                customers[fixture.LegacyOnly.CustomerId].ChangeMembership(shared.Id);
                customers[fixture.Unversioned.CustomerId].ChangeMembership(foreign.Id);
                return ids.Select((id, index) => ExplicitRow($"000{index + 1}", customers[id].Email.Value,
                    customers[id].ClubMemberNumber)).ToArray();
            });
            await SignInAsync(fixture.Reviewer);
            var before = await BusinessSnapshotAsync();

            var result = await PostRosterAsync("example.csv", Csv(rows));

            result.GetProperty("acceptedRows").GetInt32().ShouldBe(0);
            result.GetProperty("ambiguousRows").GetInt32().ShouldBe(3);
            result.GetProperty("unmatchedRows").GetInt32().ShouldBe(1);
            var reported = result.GetProperty("rows").EnumerateArray().ToArray();
            string[] RosterIssues(int index) => reported[index].GetProperty("issues").EnumerateArray()
                .Select(issue => issue.GetString()).ToArray();
            reported[0].GetProperty("matchedCustomerId").GetInt32().ShouldBe(fixture.LegacyOnly.CustomerId);
            reported[0].GetProperty("existingParticipationId").ValueKind.ShouldBe(JsonValueKind.Null);
            RosterIssues(0).ShouldBe(new[] { "LegacyAQGreenAssignmentRequiresReconciliation" });
            reported[1].GetProperty("existingParticipationId").GetGuid().ShouldBe(fixture.Both.ParticipationId.Value);
            RosterIssues(1).ShouldBe(new[] { "LegacyAQGreenAssignmentRequiresReconciliation" });
            RosterIssues(2).ShouldBe(new[] { "MembershipAssignmentUnverifiable" });
            reported[2].TryGetProperty("legacyMembershipId", out _).ShouldBeFalse();
            result.GetRawText().ShouldNotContain("Synthetic foreign AQGreen");
            reported[3].GetProperty("matchedCustomerId").ValueKind.ShouldBe(JsonValueKind.Null);
            RosterIssues(3).ShouldBe(new[] { "NoVisibleAccountMatch" });
            (await BusinessSnapshotAsync()).ShouldBe(before);
        }

        [DiscoveryPostgreSqlFact]
        public async Task RosterPreview_ReportsAnActivePlacementGapAndDependentRowsWithoutWritingHistory()
        {
            var fixture = await SeedAsync(importPermission: true);
            var rows = await UsingDbContextAsync(async context =>
            {
                var subjects = new[] { fixture.Unversioned, fixture.Both, fixture.V2Only };
                var ids = subjects.Select(subject => subject.CustomerId).ToArray();
                var customers = await context.Customers.IgnoreQueryFilters().Where(c => ids.Contains(c.Id))
                    .ToDictionaryAsync(c => c.Id);
                foreach (var subject in new[] { fixture.Unversioned, fixture.V2Only })
                {
                    var entry = await context.EntryParticipations.IgnoreQueryFilters()
                        .SingleAsync(item => item.Id == subject.ParticipationId);
                    var payment = MemberPayment.CreatePending(1, subject.CustomerId, MemberPaymentPurpose.AQGreenJoining,
                        1200m, "Synthetic", Guid.NewGuid().ToString("N"), RecordedAt);
                    payment.Confirm(RecordedAt.AddMinutes(1));
                    context.MemberPayments.Add(payment);
                    entry.ApplyConfirmedJoiningPayment(payment);
                    entry.ApproveByAdministrator(fixture.Reviewer.Id, RecordedAt.AddMinutes(2));
                    if (subject == fixture.V2Only)
                    {
                        var scope = AQGreenPlacementTreeScope.Create(1);
                        context.AQGreenPlacementTreeScopes.Add(scope);
                        context.AQGreenNetworkPlacements.Add(AQGreenNetworkPlacement.CreateRoot(scope, entry.Id,
                            entry.ActivatedAt.Value, AQGreenPlacementRules.CurrentVersion));
                    }
                }
                var root = customers[fixture.Unversioned.CustomerId];
                var child = customers[fixture.Both.CustomerId];
                var recordedRoot = customers[fixture.V2Only.CustomerId];
                return new[] { ExplicitRow("0001", root.Email.Value, root.ClubMemberNumber),
                    $"0002,Synthetic,Child,{child.Email.Value},,,{child.ClubMemberNumber},AQGreen,Active,0001,0001,1,false",
                    ExplicitRow("0003", recordedRoot.Email.Value, recordedRoot.ClubMemberNumber) };
            });
            await SignInAsync(fixture.Reviewer);
            var before = await BusinessSnapshotAsync();

            var result = await PostRosterAsync("example.csv", Csv(rows));

            result.GetProperty("ambiguousRows").GetInt32().ShouldBe(2);
            result.GetProperty("acceptedRows").GetInt32().ShouldBe(1);
            var reported = result.GetProperty("rows").EnumerateArray().ToArray();
            reported[0].GetProperty("existingParticipationStatus").GetString().ShouldBe("Active");
            reported[0].GetProperty("issues").EnumerateArray().Select(issue => issue.GetString())
                .ShouldContain("ActiveParticipationMissingPlacement");
            reported[1].GetProperty("issues").EnumerateArray().Select(issue => issue.GetString())
                .ShouldContain("NetworkDependencyRequiresReview");
            reported[2].GetProperty("issues").GetArrayLength().ShouldBe(0);
            (await BusinessSnapshotAsync()).ShouldBe(before);
        }

        [DiscoveryPostgreSqlFact]
        public async Task RosterPreview_XlsxKeepsNetworkAssociationsAndUnknownsWithoutInferringTopology()
        {
            var fixture = await SeedAsync(importPermission: true);
            var email = await UsingDbContextAsync(async db => await db.Customers.IgnoreQueryFilters()
                .Where(c => c.Id == fixture.Both.CustomerId).Select(c => c.Email.Value).SingleAsync());
            using var workbook = new ExcelPackage();
            var sheet = workbook.Workbook.Worksheets.Add("Network");
            var values = new[] { AssociationHeaders.Split(','),
                new[] { "0007", "Synthetic member", email, "", "Synthetic owner", "0098", "Depth 2 / 17" },
                new[] { "0008", "Another synthetic member", "", "", "", "", "" } };
            for (var row = 0; row < values.Length; row++)
                for (var column = 0; column < values[row].Length; column++)
                    sheet.Cells[row + 1, column + 1].Value = values[row][column];
            await SignInAsync(fixture.Reviewer);
            var before = await BusinessSnapshotAsync();

            var result = await PostRosterAsync("example.xlsx", workbook.GetAsByteArray(), "NetworkAssociations");

            result.GetProperty("acceptedRows").GetInt32().ShouldBe(0);
            result.GetProperty("ambiguousRows").GetInt32().ShouldBe(2);
            var rows = result.GetProperty("rows").EnumerateArray().ToArray();
            rows[0].GetProperty("oldMemberId").GetString().ShouldBe("0007");
            rows[0].GetProperty("networkOwnerOldMemberId").GetString().ShouldBe("0098");
            rows[0].GetProperty("networkOwnerName").GetString().ShouldBe("Synthetic owner");
            rows[0].GetProperty("recordedNetworkPosition").GetString().ShouldBe("Depth 2 / 17");
            foreach (var row in rows)
            {
                foreach (var field in new[] { "networkParentOldMemberId", "sponsorOldMemberId", "placementSlot", "isRoot", "matchedCustomerId", "programme" })
                    row.GetProperty(field).ValueKind.ShouldBe(JsonValueKind.Null);
                row.GetProperty("issues").EnumerateArray().Select(issue => issue.GetString())
                    .ShouldContain("NetworkAssociationNeedsClarification");
            }
            rows[1].GetProperty("issues").EnumerateArray().Select(issue => issue.GetString())
                .ShouldContain("NetworkAssociationOwnerUnknown");
            (await BusinessSnapshotAsync()).ShouldBe(before);
        }

        [DiscoveryPostgreSqlTheory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public async Task RosterPreview_RequiresBothPermissionsOverHttp(bool reconciliation, bool importer)
        {
            var fixture = await SeedAsync(permission: reconciliation, importPermission: importer);
            await SignInAsync(fixture.Reviewer);
            await PostRosterAsync("example.csv", Csv(new[] { ExplicitRow("0001") }), expected: HttpStatusCode.Forbidden);
        }

        [DiscoveryPostgreSqlFact]
        public async Task RosterPreview_DeniesCrossTenantAndUnscopedHostRequests()
        {
            var tenant = await SeedAsync(importPermission: true);
            await SignInAsync(tenant.Reviewer);
            var bytes = Csv(new[] { ExplicitRow("0001") });
            await PostRosterAsync("example.csv", bytes, tenantId: tenant.ForeignTenantId, expected: HttpStatusCode.Forbidden);
            var host = await SeedAsync(central: true, importPermission: true);
            await SignInAsync(host.Reviewer);
            await PostRosterAsync("example.csv", bytes, expected: HttpStatusCode.Forbidden);
        }

        [DiscoveryPostgreSqlFact]
        public async Task RosterPreview_TransactionGuardEnablesPostgreSqlReadOnly()
        {
            using var uow = Resolve<IUnitOfWorkManager>().Begin(new UnitOfWorkOptions
            { IsTransactional = true, IsolationLevel = IsolationLevel.RepeatableRead });
            await Resolve<IAQGreenReconciliationEvidenceSnapshot>().RequireReadOnlyTransactionAsync();
            var db = await Resolve<IDbContextProvider<AqualLifeStyleDbContext>>().GetDbContextAsync();
            using var command = db.Database.GetDbConnection().CreateCommand();
            command.Transaction = db.Database.CurrentTransaction.GetDbTransaction();
            command.CommandText = "SELECT current_setting('transaction_isolation') || ':' || current_setting('transaction_read_only')";
            (await command.ExecuteScalarAsync()).ShouldBe("repeatable read:on");
        }

        private async Task<JsonElement> PostRosterAsync(string fileName, byte[] bytes, string layout = null,
            int tenantId = 1, HttpStatusCode expected = HttpStatusCode.OK)
        {
            using var response = await Client.PostAsJsonAsync(RosterEndpoint,
                new { tenantId, fileName, contentBase64 = Convert.ToBase64String(bytes), layout });
            var body = await response.Content.ReadAsStringAsync();
            response.StatusCode.ShouldBe(expected, body);
            if (expected != HttpStatusCode.OK) return default;
            using var json = JsonDocument.Parse(body);
            return json.RootElement.GetProperty("result").Clone();
        }

        private static string ExplicitRow(string id, string email = "", string number = "") =>
            $"{id},Synthetic,Member,{email},,,{number},AQGreen,Active,,,,true";

        private static byte[] Csv(string[] rows) => Encoding.UTF8.GetBytes(RosterHeaders + "\r\n" + string.Join("\r\n", rows) + "\r\n");
    }
}
