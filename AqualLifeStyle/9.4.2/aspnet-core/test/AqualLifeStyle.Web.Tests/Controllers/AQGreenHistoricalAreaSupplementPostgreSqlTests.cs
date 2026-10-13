using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AqualLifeStyle.Application.Admin.ProgrammeParticipations.Dto;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Web.Tests.Controllers
{
    public sealed partial class AQGreenExistingMemberDiscoveryPostgreSqlTests
    {
        [DiscoveryPostgreSqlFact]
        public async Task HistoricalAreaSupplement_RecordedRows_Baseline_Uncertainty_ScopeAndReplay_ArePreserved()
        {
            var fixture = await SeedAsync(exportPermission: true);
            var moved = await MoveSyntheticMemberAsync(fixture);
            var baselineId = await UsingDbContextAsync(async context =>
            {
                var row = await context.CustomerAreaAssignments.IgnoreQueryFilters().SingleAsync(a => a.CustomerId == fixture.LegacyOnly.CustomerId);
                context.Entry(row).Property(a => a.IsMigrationBaseline).CurrentValue = true;
                await context.SaveChangesAsync(); return row.Id;
            });
            await SignInAsync(fixture.Reviewer);
            var before = await BusinessSnapshotAsync();
            var source = await ExportAsync(); var replay = await ExportAsync(); source.Verify(); replay.Verify();
            source.Manifest.PackageVersion.ShouldBe(AQGreenReconciliationEvidenceManifest.Version);
            source.Manifest.SourceRecordCounts["CustomerAreaAssignments"].ShouldBe(6);
            source.RecordsBytes().ShouldBe(replay.RecordsBytes());
            source.Manifest.OverallSnapshotDigest.ShouldBe(replay.Manifest.OverallSnapshotDigest);
            var baseline = source.Records.Single(r => r.CustomerId == fixture.LegacyOnly.CustomerId);
            baseline.AreaAssignmentHistory.Single().SourceAssignmentId.ShouldBe(baselineId);
            baseline.AreaAssignmentHistory.Single().IsMigrationBaseline.ShouldBeTrue();
            baseline.AreaAssignmentHistory.Single().EffectiveFrom.ShouldBe(RecordedAt);
            baseline.AreaHistoryEvidenceIssues.ShouldContain("PRE_BASELINE_AREA_UNVERIFIABLE");
            baseline.AreaHistoryEvidenceIssues.ShouldContain("AREA_HISTORY_REQUIRES_AUDIT_AND_AUTHORIZATION_CORROBORATION");
            baseline.ParticipationId.ShouldBeNull(); baseline.RecordedActivatedAt.ShouldBeNull();
            var history = source.Records.Single(r => r.CustomerId == moved.CustomerId);
            history.AreaAssignmentHistory.Select(a => a.SourceAssignmentId).ShouldBe(moved.SourceIds);
            history.AreaAssignmentHistory[0].AreaId.ShouldBeNull();
            history.AreaAssignmentHistory[0].AreaIdentityEvidence.ShouldBe("WITHHELD_UNAUTHORIZED_AREA");
            history.AreaAssignmentHistory[0].EffectiveTo.ShouldBe(RecordedAt.AddHours(1));
            history.AreaAssignmentHistory[1].AreaId.ShouldBe(fixture.AreaId);
            history.AreaHistoryEvidenceIssues.ShouldContain("AREA_HISTORY_AREA_OUTSIDE_AUTHORIZED_SCOPE");
            Encoding.UTF8.GetString(AQGreenB6CanonicalEvidence.Bytes(history)).ShouldNotContain(moved.OldAreaId.ToString());
            var json = Encoding.UTF8.GetString(source.RecordsBytes());
            json.ShouldNotContain("Synthetic retained Area movement"); json.ShouldNotContain("creatorUserId"); json.ShouldNotContain("reason\"");
            source.Records.ShouldAllBe(r => r.TenantId == 1 && r.CurrentAreaId == fixture.AreaId);
            source.Records.ShouldNotContain(r => r.CustomerId == fixture.Foreign.CustomerId || r.CustomerId == fixture.OtherArea.CustomerId);
            await ExportAsync(new ExportLegacyAQGreenReconciliationEvidenceInput
                { TenantId = fixture.ForeignTenantId, AnalysisSnapshotCutoffUtc = RecordedAt.AddDays(1) }, HttpStatusCode.Forbidden);
            (await BusinessSnapshotAsync()).ShouldBe(before);
        }

        [DiscoveryPostgreSqlFact]
        public async Task HistoricalAreaSupplement_ConflictsAndTombstonesAreRetained_ChangedSourceRejectsPriorBindings()
        {
            var fixture = await SeedAsync(exportPermission: true); var moved = await MoveSyntheticMemberAsync(fixture);
            await SignInAsync(fixture.Reviewer);
            var original = await ExportAsync(); var inventory = ReviewInventory(original);
            var (decisions, authorities) = ReviewDecisions(original, inventory);
            await UsingDbContextAsync(async context =>
            {
                var row = await context.CustomerAreaAssignments.SingleAsync(a => a.Id == moved.SourceIds[0]);
                context.Entry(row).Property(a => a.EffectiveTo).CurrentValue = RecordedAt.AddHours(2);
            });
            var before = await BusinessSnapshotAsync(); var conflict = await ExportAsync();
            var record = conflict.Records.Single(r => r.CustomerId == moved.CustomerId);
            record.AreaHistoryEvidenceIssues.ShouldContain("AREA_HISTORY_INTERVALS_CONFLICT");
            record.HistoricalEvidenceIssues.ShouldContain("AREA_HISTORY_INTERVALS_CONFLICT");
            conflict.Manifest.OverallSnapshotDigest.ShouldNotBe(original.Manifest.OverallSnapshotDigest);
            record.RecordDigest.ShouldNotBe(original.Records.Single(r => r.CustomerId == moved.CustomerId).RecordDigest);
            Should.Throw<InvalidOperationException>(() => AQGreenFrozenReconciliationReview.Process(conflict, inventory, decisions, authorities));
            (await BusinessSnapshotAsync()).ShouldBe(before);
            await UsingDbContextAsync(async context =>
            {
                var row = await context.CustomerAreaAssignments.SingleAsync(a => a.Id == moved.SourceIds[0]);
                context.Entry(row).Property(a => a.IsDeleted).CurrentValue = true;
                context.Entry(row).Property(a => a.DeletionTime).CurrentValue = DateTime.UtcNow;
            });
            before = await BusinessSnapshotAsync(); var tombstone = await ExportAsync();
            record = tombstone.Records.Single(r => r.CustomerId == moved.CustomerId);
            record.AreaAssignmentHistory.Single(a => a.SourceAssignmentId == moved.SourceIds[0]).IsDeleted.ShouldBeTrue();
            record.AreaHistoryEvidenceIssues.ShouldContain("DELETED_AREA_HISTORY_REQUIRES_REVIEW");
            tombstone.Manifest.SourceRecordCounts["CustomerAreaAssignments"].ShouldBe(6);
            (await BusinessSnapshotAsync()).ShouldBe(before);
        }

        [DiscoveryPostgreSqlFact]
        public async Task HistoricalAreaSupplement_EmptyAuthorizedPopulationHasExplicitCoveredSourceCount()
        {
            var fixture = await SeedAsync(exportPermission: true);
            var areaId = await UsingDbContextAsync(async context =>
            {
                var area = AqualLifeStyle.Domain.Areas.Area.Create(1, "EMPTY" + Guid.NewGuid().ToString("N")[..8], "Synthetic empty scope");
                context.Areas.Add(area); await context.SaveChangesAsync();
                context.AreaAdminAssignments.Add(AqualLifeStyle.Domain.Areas.AreaAdminAssignment.Assign(area, fixture.Reviewer.Id, 1, RecordedAt));
                await context.SaveChangesAsync(); return area.Id;
            });
            await SignInAsync(fixture.Reviewer); var before = await BusinessSnapshotAsync();
            var source = await ExportAsync(new ExportLegacyAQGreenReconciliationEvidenceInput
                { TenantId = 1, AreaId = areaId, AnalysisSnapshotCutoffUtc = RecordedAt.AddDays(1) });
            source.Verify(); source.Records.ShouldBeEmpty(); source.Manifest.RecordCount.ShouldBe(0);
            source.Manifest.SourceRecordCounts["CustomerAreaAssignments"].ShouldBe(0);
            (await BusinessSnapshotAsync()).ShouldBe(before);
        }

        [DiscoveryPostgreSqlFact]
        public async Task HistoricalAreaSupplement_CorroboratingRowsCannotEstablishHistoricalAuthorityThroughAcceptance()
        {
            var source = await ReviewSourceAsync(); source.Records[0].HistoricalEvidenceIssues.ShouldBeEmpty();
            var inventory = ReviewInventory(source); var (decisions, authorities) = ReviewDecisions(source, inventory);
            var before = await BusinessSnapshotAsync();
            var result = AQGreenFrozenReconciliationReview.Process(source, inventory, decisions, authorities);
            result.ReviewCoverage.ShouldBe("COMPLETE"); result.PopulationApprovalState.ShouldBe("QUARANTINED");
            result.Components.Single().RequestedDisposition.ShouldBe("ACCEPTED");
            result.Components.Single().EffectiveDisposition.ShouldBe("QUARANTINED");
            result.Components.Single().HistoricalEvidenceIssues.ShouldContain(i => i.EndsWith(":AREA_HISTORY_REQUIRES_AUDIT_AND_AUTHORIZATION_CORROBORATION"));
            (await BusinessSnapshotAsync()).ShouldBe(before);
        }

        [DiscoveryPostgreSqlFact]
        public async Task HistoricalAreaSupplement_ExistingV1ArchiveRetainsOriginalCanonicalDigests()
        {
            // Use the retained synthetic package from the completed export task;
            // do not regenerate an alleged old-format package with current code.
            var path = Environment.GetEnvironmentVariable("AQGREEN_FROZEN_V1_ARCHIVE_DIRECTORY");
            path.ShouldNotBeNullOrWhiteSpace();
            var manifestBytes = await System.IO.File.ReadAllBytesAsync(System.IO.Path.Combine(path, "manifest.json"));
            var recordBytes = await System.IO.File.ReadAllBytesAsync(System.IO.Path.Combine(path, "records.json"));
            var source = new AQGreenReconciliationEvidencePackage
            {
                Manifest = JsonSerializer.Deserialize<AQGreenReconciliationEvidenceManifest>(manifestBytes, AQGreenB6CanonicalEvidence.Options),
                Records = JsonSerializer.Deserialize<System.Collections.Generic.List<AQGreenReconciliationEvidenceRecord>>(recordBytes, AQGreenB6CanonicalEvidence.Options),
                Sha256Sums = await System.IO.File.ReadAllTextAsync(System.IO.Path.Combine(path, "SHA256SUMS"))
            };
            source.Manifest.PackageVersion.ShouldBe(AQGreenReconciliationEvidenceManifest.LegacyVersion);
            source.Verify(); source.ManifestBytes().ShouldBe(manifestBytes); source.RecordsBytes().ShouldBe(recordBytes);
            source.Records.ShouldAllBe(r => r.AreaAssignmentHistory == null && r.AreaHistoryEvidenceIssues == null);
            source.Manifest.SourceRecordCounts.ContainsKey("CustomerAreaAssignments").ShouldBeFalse();
        }

        private async Task<(int CustomerId, Guid OldAreaId, Guid[] SourceIds)> MoveSyntheticMemberAsync(Fixture fixture)
        {
            return await UsingDbContextAsync(async context =>
            {
                var oldAreaId = (await context.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == fixture.OtherArea.CustomerId)).AreaId;
                var oldArea = await context.Areas.SingleAsync(a => a.Id == oldAreaId);
                var target = await context.Areas.SingleAsync(a => a.Id == fixture.AreaId);
                var membershipId = (await context.Customers.SingleAsync(c => c.Id == fixture.LegacyOnly.CustomerId)).MembershipId;
                var subject = await SubjectAsync(context, oldArea, membershipId, false, false, fixture.Reviewer.Id);
                var customer = await context.Customers.Include(c => c.AreaAssignments).SingleAsync(c => c.Id == subject.CustomerId);
                customer.MoveToArea(target, RecordedAt.AddHours(1), "Synthetic retained Area movement");
                // A preassigned GUID on a newly discovered navigation is otherwise
                // tracked as an existing row; fixtures must explicitly insert it.
                context.CustomerAreaAssignments.Add(customer.AreaAssignments.Single(a => !a.EffectiveTo.HasValue));
                await context.SaveChangesAsync();
                return (subject.CustomerId, oldArea.Id, customer.AreaAssignments.OrderBy(a => a.EffectiveFrom).Select(a => a.Id).ToArray());
            });
        }
    }
}
