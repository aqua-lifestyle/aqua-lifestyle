using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Abp.UI;
using AqualLifeStyle.Application.Admin.ProgrammeParticipations;
using AqualLifeStyle.Application.Admin.ProgrammeParticipations.Dto;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Onyx;
using OfficeOpenXml;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class LegacyAQGreenRosterPreviewTests
    {
        private const string Headers = "OldMemberId,FirstName,LastName,Email,ContactNumber,HomeAddress,ClubMemberNumber,Programme,MembershipStatus,NetworkParentOldMemberId,SponsorOldMemberId,PlacementSlot,IsRoot";
        private static readonly DateTime Timestamp = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        [Theory]
        [InlineData("csv")]
        [InlineData("xlsx")]
        public async Task FilePreservesTextMemberNumberAndSeparateParentAndSponsor(string extension)
        {
            const string row = "0007,Test,Person,person@example.test,,,CLB-0007,AQGreen,Active,0002,0001,5,false";
            var bytes = extension == "csv" ? Encoding.UTF8.GetBytes(Headers + "\r\n" + row + "\r\n") : Workbook(row);
            var rows = await LegacyAQGreenRosterPreview.ReadRowsAsync(new PreviewLegacyAQGreenRosterInput
            {
                FileName = "example." + extension,
                ContentBase64 = Convert.ToBase64String(bytes)
            });

            var parsed = rows.ShouldHaveSingleItem();
            parsed.OldMemberId.ShouldBe("0007");
            parsed.NetworkParentOldMemberId.ShouldBe("0002");
            parsed.SponsorOldMemberId.ShouldBe("0001");
            parsed.PlacementSlot.ShouldBe("5");
        }

        [Fact]
        public async Task DifferentHeadingsDoNotSilentlyUseTheInitialTemplate()
        {
            await Should.ThrowAsync<UserFriendlyException>(() => LegacyAQGreenRosterPreview.ReadRowsAsync(new PreviewLegacyAQGreenRosterInput
            {
                FileName = "example.csv",
                ContentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("OldMemberId,Programme,MembershipStatus,IsRoot\r\n0007,AQGreen,Active,true\r\n"))
            }));
        }

        [Fact]
        public async Task MultipleWorksheetsAreNotSilentlyTruncated()
        {
            var exception = await Should.ThrowAsync<UserFriendlyException>(() => LegacyAQGreenRosterPreview.ReadRowsAsync(
                new PreviewLegacyAQGreenRosterInput
                {
                    FileName = "example.xlsx",
                    ContentBase64 = Convert.ToBase64String(Workbook("0007,Test,Person,,,,,AQGreen,Active,,,,true", multipleSheets: true))
                }));

            exception.Details.ShouldBe("Supply a workbook with exactly one worksheet.");
        }

        [Fact]
        public async Task RepeatedSourceRowsReachDuplicateClassification()
        {
            const string row = "0007,Test,Person,,,,,AQGreen,Active,,,,true";
            var rows = await LegacyAQGreenRosterPreview.ReadRowsAsync(new PreviewLegacyAQGreenRosterInput
            {
                FileName = "example.csv",
                ContentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(Headers + "\r\n" + row + "\r\n" + row + "\r\n"))
            });

            Preview(rows.ToArray(), Array.Empty<LegacyAQGreenRosterAccount>()).DuplicateRows.ShouldBe(2);
        }

        [Theory]
        [InlineData("csv", "AA==")]
        [InlineData("xls", "YWJj")]
        [InlineData("xlsx", "YWJj")]
        public async Task UsesExistingFileValidation(string extension, string payload)
        {
            await Should.ThrowAsync<UserFriendlyException>(() => LegacyAQGreenRosterPreview.ReadRowsAsync(
                new PreviewLegacyAQGreenRosterInput { FileName = "example." + extension, ContentBase64 = payload }));
        }

        [Fact]
        public void SeparatesCorroboratedAccountMatchesDuplicatesUnmatchedAndAmbiguous()
        {
            var wrongProgramme = Root("07");
            wrongProgramme.Programme = "Onyx";
            var emailOnly = Root("04");
            emailOnly.ClubMemberNumber = null;
            var splitIdentity = Root("08");
            splitIdentity.Email = "10@example.test";
            var result = Preview(new[] { Root("01"), Root(" 02 "), Root("02"), Root("03"), emailOnly, Root("05"), wrongProgramme, splitIdentity },
                new[] { Account("01", 1), Account("03", 3) with { TenantId = 2 }, Account("04", 4),
                    Account("05", 5) with { AccountAvailable = false }, Account("07", 7), Account("08", 8), Account("10", 10) });

            result.AcceptedRows.ShouldBe(1);
            result.DuplicateRows.ShouldBe(2);
            result.UnmatchedRows.ShouldBe(1);
            result.AmbiguousRows.ShouldBe(4);
            result.Rows[0].MatchedCustomerId.ShouldBe(1);
            result.Rows[1].Issues.ShouldContain("RepeatedOldMemberId");
            result.Rows[3].MatchedCustomerId.ShouldBeNull();
            result.Rows[4].Issues.ShouldContain("AccountMatchRequiresCorroboration");
            result.Rows[5].Issues.ShouldContain("AccountInactiveDeletedOrUnverified");
            result.Rows[6].Issues.ShouldContain("ProgrammeRequiresReview");
            result.Rows[7].Issues.ShouldContain("AccountMatchRequiresCorroboration");
        }

        [Fact]
        public void SpilloverKeepsRecordedParentAndSponsorSeparate()
        {
            var result = Preview(new[] { Root("01"), Child("02", "01", "01", "1"), Child("03", "02", "01", "5") },
                new[] { Account("01", 1), Account("02", 2), Account("03", 3) });

            result.AcceptedRows.ShouldBe(3);
            result.Rows[2].NetworkParentOldMemberId.ShouldBe("02");
            result.Rows[2].SponsorOldMemberId.ShouldBe("01");
            result.Rows[2].PlacementSlot.ShouldBe("5");
        }

        [Fact]
        public void MissingLinksAreNeverGuessedAndTheirDescendantsRequireReview()
        {
            var result = Preview(new[] { Child("01", null, null, null), Child("02", "01", "01", "1"), Child("03", "99", "01", "2") },
                new[] { Account("01", 1), Account("02", 2), Account("03", 3) });

            result.AmbiguousRows.ShouldBe(3);
            result.Rows[0].Issues.ShouldContain("PlacementParentUnknown");
            result.Rows[0].Issues.ShouldContain("CreditedSponsorUnknown");
            result.Rows[0].SponsorOldMemberId.ShouldBeNull();
            result.Rows[1].Issues.ShouldContain("NetworkDependencyRequiresReview");
            result.Rows[2].Issues.ShouldContain("PlacementParentNotResolved");
        }

        [Fact]
        public void CyclesSlotCollisionsAndCrossRootReferencesAreHeld()
        {
            var rows = new[] { Root("01"), Root("02"), Child("03", "01", "02", "1"), Child("04", "01", "01", "1"),
                Child("05", "06", "06", "2"), Child("06", "05", "05", "3"), Child("07", "01", "01", "6") };
            var result = Preview(rows, rows.Select((row, i) => Account(row.OldMemberId, i + 1)).ToArray());

            result.Rows[2].Issues.ShouldContain("CrossRootSponsor");
            result.Rows[2].Issues.ShouldContain("RepeatedPlacementSlot");
            result.Rows[3].Issues.ShouldContain("RepeatedPlacementSlot");
            result.Rows[4].Issues.ShouldContain("PlacementCycle");
            result.Rows[5].Issues.ShouldContain("SponsorCycle");
            result.Rows[6].Issues.ShouldContain("InvalidPlacementSlot");
        }

        [Fact]
        public void RetiredParticipationAndOccupiedLiveSlotAreReportedWithoutMovingTheMember()
        {
            var root = Participation(1);
            var history = Participation(2);
            history.IsDeleted = true;
            var current = Participation(2);
            var other = Participation(3);
            var rootPlacement = AQGreenNetworkPlacement.CreateRoot(AQGreenPlacementTreeScope.Create(1), root.Id, Timestamp, AQGreenPlacementRules.CurrentVersion);
            var currentPlacement = AQGreenNetworkPlacement.CreateChild(rootPlacement, current.Id, 1, Timestamp, AQGreenPlacementRules.CurrentVersion);
            var otherPlacement = AQGreenNetworkPlacement.CreateChild(rootPlacement, other.Id, 2, Timestamp, AQGreenPlacementRules.CurrentVersion);

            var result = LegacyAQGreenRosterPreview.Classify(1, new[] { Root("01"), Child("02", "01", "01", "2") },
                new[] { Account("01", 1), Account("02", 2) }, new[] { root, history, current, other },
                new[] { rootPlacement, currentPlacement, otherPlacement }, new[]
                {
                    AQGreenRecruitmentAttribution.Create(1, current.Id, other.Id,
                        AQGreenRecruitmentAttributionKind.SponsoredParticipant, AQGreenAcquisitionSource.MemberInvitation,
                        Guid.NewGuid(), Timestamp, null, null, AQGreenRecruitmentAttributionRules.CurrentVersion)
                });

            result.Rows[1].ExistingParticipationId.ShouldBe(current.Id);
            result.Rows[1].Issues.ShouldContain("RetiredParticipationExists");
            result.Rows[1].Issues.ShouldContain("ExistingPlacementConflict");
            result.Rows[1].Issues.ShouldContain("ExistingPlacementSlotOccupied");
            result.Rows[1].Issues.ShouldContain("ExistingCreditedSponsorConflict");
            result.Rows[1].PlacementSlot.ShouldBe("2");
            currentPlacement.PlacementSlot.ShouldBe(1);
        }

        private static byte[] Workbook(string row, bool multipleSheets = false)
        {
            using var package = new ExcelPackage();
            var sheet = package.Workbook.Worksheets.Add("Roster");
            var headings = Headers.Split(',');
            var values = row.Split(',');
            for (var i = 0; i < headings.Length; i++)
            {
                sheet.Cells[1, i + 1].Value = headings[i];
                sheet.Cells[2, i + 1].Value = values[i];
            }
            if (multipleSheets) package.Workbook.Worksheets.Add("Network").Cells[1, 1].Value = "Extra records";
            return package.GetAsByteArray();
        }

        private static EntryParticipation Participation(int customerId) =>
            EntryParticipation.StartIndependently(1, customerId,
                EntryProgrammeTerms.CreateFlexibleJoiningPayment("test", Timestamp, 1200, 600, 600, 7), Timestamp);

        private static LegacyAQGreenRosterFileRow Root(string id) => Child(id, null, null, null, "true");

        private static LegacyAQGreenRosterFileRow Child(string id, string parent, string sponsor, string slot, string root = "false") =>
            new() { OldMemberId = id, Email = id.Trim() + "@example.test", ClubMemberNumber = "CLB-" + id.Trim(),
                Programme = "AQGreen", MembershipStatus = "Active", NetworkParentOldMemberId = parent,
                SponsorOldMemberId = sponsor, PlacementSlot = slot, IsRoot = root };

        private static LegacyAQGreenRosterAccount Account(string id, int customerId) =>
            new(1, customerId, "CLB-" + id, id + "@example.test", id + "@example.test", true);

        private static LegacyAQGreenRosterPreviewDto Preview(LegacyAQGreenRosterFileRow[] rows, LegacyAQGreenRosterAccount[] accounts) =>
            LegacyAQGreenRosterPreview.Classify(1, rows, accounts, Array.Empty<EntryParticipation>(),
                Array.Empty<AQGreenNetworkPlacement>(), Array.Empty<AQGreenRecruitmentAttribution>());
    }
}
