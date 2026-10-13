using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AqualLifeStyle.Application.Admin.ProgrammeParticipations.Dto;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Areas;
using AqualLifeStyle.Domain.Recruitment;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Web.Tests.Controllers
{
    public sealed partial class AQGreenExistingMemberDiscoveryPostgreSqlTests
    {
        private const string ExportEndpoint = "/api/services/app/AdminProgrammeParticipation/ExportLegacyAQGreenReconciliationEvidence";

        [DiscoveryPostgreSqlFact]
        public async Task Export_CoversPaginationAndOverlaps_RepeatsCanonicalContent_WithoutWritesOrSensitiveFields()
        {
            var fixture = await SeedAsync(exportPermission: true);
            var additional = await UsingDbContextAsync(async context =>
            {
                var area = await context.Areas.SingleAsync(a => a.Id == fixture.AreaId);
                var membershipId = (await context.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == fixture.LegacyOnly.CustomerId)).MembershipId;
                var ids = new int[101];
                for (var i = 0; i < ids.Length; i++)
                    ids[i] = (await SubjectAsync(context, area, membershipId, false, false, fixture.Reviewer.Id)).CustomerId;
                return ids;
            });
            await SignInAsync(fixture.Reviewer);
            var before = await BusinessSnapshotAsync();
            var first = await ExportAsync();
            var second = await ExportAsync();
            first.Verify(); second.Verify();
            first.Manifest.RecordCount.ShouldBe(105);
            first.Records.Select(r => r.CustomerId).ShouldBe(new[] { fixture.LegacyOnly.CustomerId, fixture.Both.CustomerId,
                fixture.V2Only.CustomerId, fixture.Unversioned.CustomerId }.Concat(additional).OrderBy(id => id));
            first.Manifest.ClassificationCounts["LegacyAssignmentWithoutV2Participation"].ShouldBe(102);
            first.Records.Single(r => r.CustomerId == fixture.Both.CustomerId).DiscoveryKind.ShouldBe("LegacyAssignmentWithV2Participation");
            first.Records.Single(r => r.CustomerId == fixture.V2Only.CustomerId).ParticipationId.ShouldBe(fixture.V2Only.ParticipationId);
            var legacy = first.Records.Single(r => r.CustomerId == fixture.LegacyOnly.CustomerId);
            legacy.ParticipationId.ShouldBeNull(); legacy.RecordedActivatedAt.ShouldBeNull(); legacy.Attribution.ShouldBeNull();
            legacy.HistoricalEvidenceIssues.ShouldContain("SPONSOR_HISTORY_UNVERIFIABLE");
            first.Records.Single(r => r.CustomerId == fixture.Unversioned.CustomerId).ParticipationVersionEvidence.ShouldBe("VersionUnverifiable");
            var both = first.Records.Single(r => r.CustomerId == fixture.Both.CustomerId);
            both.JoiningPayments.Count.ShouldBe(1); both.JoiningPaymentId.ShouldBe(fixture.JoiningPaymentId);
            both.JoiningPayments.Single().ConfirmedAt.ShouldNotBeNull(); both.CheckoutAttempts.Count.ShouldBe(1);
            first.RecordsBytes().ShouldBe(second.RecordsBytes());
            first.Manifest.OverallSnapshotDigest.ShouldBe(second.Manifest.OverallSnapshotDigest);
            first.Manifest.CandidatePopulationDigest.ShouldBe(second.Manifest.CandidatePopulationDigest);
            first.Manifest.SourceBoundary.TransactionMode.ShouldBe("REPEATABLE READ READ ONLY");
            first.Manifest.SourceBoundary.SourcePostgreSqlVersion.ShouldStartWith("16.");
            var json = Encoding.UTF8.GetString(AQGreenB6CanonicalEvidence.Bytes(first));
            foreach (var secret in new[] { "example.test", "Synthetic discovery member", "clubMemberNumber", "providerReference",
                "providerCheckoutId", "assignmentReason", "administratorUserId", "discovery-joining-", "monthlySubscription" })
                json.ShouldNotContain(secret);
            (await BusinessSnapshotAsync()).ShouldBe(before);
            // Offline consumer validates a round trip without resolving any DB service.
            var offline = JsonSerializer.Deserialize<AQGreenReconciliationEvidencePackage>(json, AQGreenB6CanonicalEvidence.Options);
            offline.Verify();
            Should.Throw<InvalidOperationException>(() => offline.SimulateWholeScope(Guid.NewGuid(), Array.Empty<AQGreenReconciliationSimulationInput>()));
        }

        [DiscoveryPostgreSqlTheory]
        [InlineData(false, true, false, false, false)] // viewer alone cannot bulk export
        [InlineData(true, false, false, false, false)] // exporter alone cannot view evidence
        [InlineData(true, true, false, false, true)] // cross Tenant
        [InlineData(true, true, true, false, false)] // host lacks explicit AllTenants
        [InlineData(true, true, false, true, false)] // Tenant operator cannot claim whole Tenant
        public async Task Export_DeniesMissingPermissionsAndUnauthorisedScope(
            bool exportPermission, bool viewPermission, bool central, bool wholeTenant, bool foreignTenant)
        {
            var fixture = await SeedAsync(permission: viewPermission, central: central, exportPermission: exportPermission);
            await SignInAsync(fixture.Reviewer);
            var before = await BusinessSnapshotAsync();
            await ExportAsync(new ExportLegacyAQGreenReconciliationEvidenceInput
            { TenantId = foreignTenant ? fixture.ForeignTenantId : 1, WholeTenant = wholeTenant,
                AnalysisSnapshotCutoffUtc = RecordedAt.AddDays(1) }, HttpStatusCode.Forbidden);
            (await BusinessSnapshotAsync()).ShouldBe(before);
        }

        [DiscoveryPostgreSqlFact]
        public async Task Export_DeniesUnassignedAreaAndRevokedAuthority()
        {
            var fixture = await SeedAsync(exportPermission: true);
            var otherArea = await UsingDbContextAsync(async context =>
                (await context.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == fixture.OtherArea.CustomerId)).AreaId.Value);
            await SignInAsync(fixture.Reviewer);
            await ExportAsync(new ExportLegacyAQGreenReconciliationEvidenceInput
            { TenantId = 1, AreaId = otherArea, AnalysisSnapshotCutoffUtc = RecordedAt.AddDays(1) }, HttpStatusCode.Forbidden);
            await UsingDbContextAsync(async context =>
            {
                var assignment = await context.AreaAdminAssignments.SingleAsync(a => a.Id == fixture.AssignmentId);
                assignment.Revoke(RecordedAt.AddDays(1)); await context.SaveChangesAsync();
            });
            await ExportAsync(expected: HttpStatusCode.Forbidden);
        }

        [DiscoveryPostgreSqlFact]
        public async Task Export_PreservesConflictsAndWithholdsForeignSponsorReferences()
        {
            var fixture = await SeedAsync(exportPermission: true);
            var sponsored = await UsingDbContextAsync(async context =>
            {
                // Reuse discovery's historical-owner-conflict fixture. The payment
                // reference exists; PostgreSQL constraints remain fully enforced.
                await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"MemberPayments\" SET \"CustomerId\" = {fixture.OtherArea.CustomerId} WHERE \"Id\" = {fixture.JoiningPaymentId}");
                var attribution = await context.AQGreenRecruitmentAttributions.IgnoreQueryFilters().SingleAsync(a => a.ParticipantId == fixture.Both.ParticipationId);
                context.AQGreenRecruitmentAttributionConfirmations.Add(AQGreenRecruitmentAttributionConfirmation.Confirm(
                    attribution, RecordedAt.AddMinutes(1), fixture.Reviewer.Id,
                    AQGreenAttributionConfirmationMethod.AuthorisedProspectiveRootConfirmation, Guid.NewGuid(), AQGreenRecruitmentAttributionRules.CurrentVersion));
                var area = await context.Areas.SingleAsync(a => a.Id == fixture.AreaId);
                var subject = await SubjectAsync(context, area, null, true, false, fixture.Reviewer.Id);
                var invitation = ProgrammeInvitation.Create(1, "AQGREEN", fixture.OtherAreaV2.ParticipationId.Value);
                context.ProgrammeInvitations.Add(invitation);
                await context.SaveChangesAsync();
                context.AQGreenRecruitmentAttributions.Add(AQGreenRecruitmentAttribution.Create(1, subject.ParticipationId.Value,
                    fixture.OtherAreaV2.ParticipationId, AQGreenRecruitmentAttributionKind.SponsoredParticipant,
                    AQGreenAcquisitionSource.MemberInvitation, invitation.Id, RecordedAt, null, null, AQGreenRecruitmentAttributionRules.CurrentVersion));
                await context.SaveChangesAsync();
                return subject;
            });
            await SignInAsync(fixture.Reviewer);
            var package = await ExportAsync();
            var both = package.Records.Single(r => r.CustomerId == fixture.Both.CustomerId);
            both.HistoricalEvidenceIssues.ShouldContain("LINKED_JOINING_PAYMENT_UNVERIFIABLE");
            both.HistoricalEvidenceIssues.ShouldContain("ATTRIBUTION_CONFIRMATION_CONFLICT");
            both.AttributionConfirmation.ShouldNotBeNull(); both.JoiningPaymentId.ShouldBeNull();
            var sponsoredEvidence = package.Records.Single(r => r.CustomerId == sponsored.CustomerId);
            sponsoredEvidence.HistoricalEvidenceIssues.ShouldContain("SPONSOR_OUTSIDE_EXPORT_POPULATION");
            sponsoredEvidence.Attribution.CreditedSponsorParticipantId.ShouldBeNull();
            Encoding.UTF8.GetString(package.RecordsBytes()).ShouldNotContain(fixture.OtherAreaV2.ParticipationId.ToString());
        }

        [DiscoveryPostgreSqlFact]
        public async Task Export_WholeTenantInventory_SupportsOfflineCompleteScopeSimulationAndRejectsTampering()
        {
            var fixture = await SeedAsync(central: true, allTenants: true, exportPermission: true);
            await UsingDbContextAsync(async context =>
            {
                var areaId = (await context.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == fixture.Foreign.CustomerId)).AreaId;
                var area = await context.Areas.IgnoreQueryFilters().SingleAsync(a => a.Id == areaId);
                var membershipId = (await context.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == fixture.Foreign.CustomerId)).MembershipId;
                for (var i = 0; i < 6; i++) await SubjectAsync(context, area, membershipId, false, false, fixture.Reviewer.Id);
            });
            await SignInAsync(fixture.Reviewer);
            var before = await BusinessSnapshotAsync();
            // The distinct synthetic Tenant contains seven legacy-only customers.
            var package = await ExportAsync(new ExportLegacyAQGreenReconciliationEvidenceInput
            { TenantId = fixture.ForeignTenantId, WholeTenant = true, AnalysisSnapshotCutoffUtc = RecordedAt.AddDays(1) });
            package.Manifest.WholeTenant.ShouldBeTrue(); package.Records.Count.ShouldBe(7);
            package.Manifest.SourceRecordCounts["LegacyAQGreenAssignments"].ShouldBe(7);
            var source = package.Records.First(); source.CustomerId.ShouldBe(fixture.Foreign.CustomerId);
            var artifactPath = Environment.GetEnvironmentVariable("AQGREEN_SYNTHETIC_EXPORT_ARTIFACTS");
            if (!string.IsNullOrEmpty(artifactPath))
            {
                System.IO.Directory.CreateDirectory(artifactPath);
                await System.IO.File.WriteAllBytesAsync(System.IO.Path.Combine(artifactPath, "records.json"), package.RecordsBytes());
                await System.IO.File.WriteAllBytesAsync(System.IO.Path.Combine(artifactPath, "manifest.json"), package.ManifestBytes());
                await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(artifactPath, "SHA256SUMS"), package.Sha256Sums, new UTF8Encoding(false));
            }
            // Round-trip package bytes; the following work has no DB service dependency.
            package = JsonSerializer.Deserialize<AQGreenReconciliationEvidencePackage>(
                AQGreenB6CanonicalEvidence.Bytes(package), AQGreenB6CanonicalEvidence.Options);
            var keys = package.Records.Select(r => Guid.NewGuid()).ToArray(); var scope = Guid.NewGuid();
            AQGreenReconciliationSimulationInput[] Review(AQGreenLegacyRootEvidenceStatus root) => package.Records.Select((record, index) => new AQGreenReconciliationSimulationInput
            {
                DiscoveryId = record.DiscoveryId,
                Candidate = new AQGreenLegacyCanonicalisationCandidate
                {
                    TenantSnapshotKey = fixture.ForeignTenantId, ParticipantSnapshotKey = keys[index],
                    SourceEvidenceDigest = record.RecordDigest, LegacyOrderingTieBreakOrdinal = index + 1,
                    QualifiedUnderRecruiterAtUtc = index == 0 ? null : RecordedAt,
                    AcceptedLegacySponsorSnapshotKey = index == 0 ? null : keys[0],
                    RootEvidenceStatus = index == 0 ? root : AQGreenLegacyRootEvidenceStatus.NotRoot,
                    SponsorEvidenceStatus = index == 0 ? AQGreenLegacySponsorEvidenceStatus.NotApplicableToRoot : AQGreenLegacySponsorEvidenceStatus.AcceptedSponsor,
                    AcquisitionProvenance = AQGreenLegacyAcquisitionProvenance.LegacyUnknownAcquisition
                }
            }).ToArray();
            var missing = package.SimulateWholeScope(scope, Review(AQGreenLegacyRootEvidenceStatus.MissingRootEvidence));
            missing.IsCanonicalisable.ShouldBeFalse(); missing.Placements.ShouldBeEmpty();
            missing.Anomalies.ShouldContain(a => a.Code == AQGreenLegacyCanonicalisationAnomalyCode.MissingRootEvidence);
            // Explicit synthetic review hypothesis, never inferred from a null recruiter.
            var review = Review(AQGreenLegacyRootEvidenceStatus.AcceptedRoot);
            var result = package.SimulateWholeScope(scope, review);
            result.IsCanonicalisable.ShouldBeTrue(); result.Placements.Count.ShouldBe(7);
            result.Placements.Last().PlacementParentSnapshotKey.ShouldBe(keys[1]); // sixth direct recruit spills into slot 1 of first child
            result.StructuralImpacts.ShouldAllBe(i => i.Classification == AQGreenLegacyStructuralImpactClassification.Ambiguous);
            result.ManifestDigest.ShouldBe(package.SimulateWholeScope(scope, review.Reverse().ToArray()).ManifestDigest);
            Should.Throw<InvalidOperationException>(() => package.SimulateWholeScope(scope, review.Skip(1).ToArray()));
            review[0].DiscoveryId = review[1].DiscoveryId;
            Should.Throw<InvalidOperationException>(() => package.SimulateWholeScope(scope, review));
            (await BusinessSnapshotAsync()).ShouldBe(before);
            var original = package.Manifest.RecordCount; package.Manifest.RecordCount++;
            Should.Throw<InvalidOperationException>(package.Verify); package.Manifest.RecordCount = original;
            source = package.Records.First();
            var originalDigest = source.RecordDigest; source.RecordDigest = new string('0', 64);
            Should.Throw<InvalidOperationException>(package.Verify); source.RecordDigest = originalDigest;
            package.Records.Reverse(); Should.Throw<InvalidOperationException>(package.Verify); package.Records.Reverse();
            package.Sha256Sums += "tampered"; Should.Throw<InvalidOperationException>(package.Verify);
        }

        [DiscoveryPostgreSqlFact]
        public async Task Export_PreservesSupportedApprovalAndImmutableSourceReferences()
        {
            var fixture = await SeedAsync(exportPermission: true);
            var evidence = await UsingDbContextAsync(async context =>
            {
                var entry = await context.EntryParticipations.IgnoreQueryFilters().SingleAsync(e => e.Id == fixture.Both.ParticipationId);
                entry.ApproveByAdministrator(fixture.Reviewer.Id, RecordedAt.AddMinutes(2));
                var attribution = await context.AQGreenRecruitmentAttributions.IgnoreQueryFilters().SingleAsync(a => a.ParticipantId == entry.Id);
                var confirmation = AQGreenRecruitmentAttributionConfirmation.Confirm(attribution, RecordedAt, fixture.Reviewer.Id,
                    AQGreenAttributionConfirmationMethod.AuthorisedProspectiveRootConfirmation, attribution.SourceReferenceId,
                    AQGreenRecruitmentAttributionRules.CurrentVersion);
                context.AQGreenRecruitmentAttributionConfirmations.Add(confirmation);
                var scope = AQGreenPlacementTreeScope.Create(1);
                context.AQGreenPlacementTreeScopes.Add(scope);
                var placement = AQGreenNetworkPlacement.CreateRoot(scope, entry.Id, entry.ActivatedAt.Value, AQGreenPlacementRules.CurrentVersion);
                context.AQGreenNetworkPlacements.Add(placement);
                await context.SaveChangesAsync(); // ABP assigns approval-decision IDs on persistence.
                return (ApprovalId: entry.ApprovalDecisions.Single().Id, attribution.Id, attribution.SourceReferenceId,
                    ConfirmationId: confirmation.Id, PlacementId: placement.Id, ScopeId: scope.Id);
            });
            await SignInAsync(fixture.Reviewer);
            var before = await BusinessSnapshotAsync();
            var package = await ExportAsync();
            var record = package.Records.Single(r => r.CustomerId == fixture.Both.CustomerId);
            record.RecordedActivatedAt.ShouldBe(RecordedAt.AddMinutes(2));
            record.ApprovalDecisions.Single().DecisionId.ShouldBe(evidence.ApprovalId);
            record.ApprovalDecisions.Single().Approved.ShouldBeTrue();
            record.Attribution.AttributionId.ShouldBe(evidence.Id); record.Attribution.SourceReferenceId.ShouldBe(evidence.SourceReferenceId);
            record.AttributionConfirmation.ConfirmationId.ShouldBe(evidence.ConfirmationId);
            record.Placement.PlacementId.ShouldBe(evidence.PlacementId); record.Placement.PlacementTreeScopeId.ShouldBe(evidence.ScopeId);
            record.HistoricalEvidenceIssues.ShouldNotContain("ACTIVATION_HISTORY_UNVERIFIABLE");
            record.HistoricalEvidenceIssues.ShouldContain("HISTORICAL_MEMBERSHIP_PROVENANCE_UNVERIFIABLE");
            (await BusinessSnapshotAsync()).ShouldBe(before);
        }

        [DiscoveryPostgreSqlFact]
        public async Task Export_PermissionIsNotAutomaticallySeededToAdministrators()
        {
            await UsingDbContextAsync(async context =>
            {
                var seededRoles = context.Roles.IgnoreQueryFilters().Where(r => r.Name == "Admin" || r.Name == "SystemAdmin").Select(r => r.Id);
                (await context.Permissions.IgnoreQueryFilters().OfType<Abp.Authorization.Roles.RolePermissionSetting>().AnyAsync(p =>
                    seededRoles.Contains(p.RoleId) && p.Name == AquaPermissions.Admin.ProgrammeParticipations.ExportLegacyReconciliationEvidence)).ShouldBeFalse();
            });
        }

        private async Task<AQGreenReconciliationEvidencePackage> ExportAsync(
            ExportLegacyAQGreenReconciliationEvidenceInput input = null, HttpStatusCode expected = HttpStatusCode.OK)
        {
            input ??= new ExportLegacyAQGreenReconciliationEvidenceInput { TenantId = 1, AnalysisSnapshotCutoffUtc = RecordedAt.AddDays(1) };
            using var response = await Client.PostAsync(ExportEndpoint,
                new StringContent(JsonSerializer.Serialize(input, AQGreenB6CanonicalEvidence.Options), Encoding.UTF8, "application/json"));
            response.StatusCode.ShouldBe(expected);
            if (expected != HttpStatusCode.OK) return null;
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return document.RootElement.GetProperty("result").Deserialize<AQGreenReconciliationEvidencePackage>(AQGreenB6CanonicalEvidence.Options);
        }
    }
}
