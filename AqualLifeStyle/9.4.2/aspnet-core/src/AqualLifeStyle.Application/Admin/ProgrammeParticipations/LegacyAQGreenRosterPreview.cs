using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Abp.UI;
using AqualLifeStyle.Application.Admin.Import;
using AqualLifeStyle.Application.Admin.ProgrammeParticipations.Dto;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Onyx;

namespace AqualLifeStyle.Application.Admin.ProgrammeParticipations
{
    public static class LegacyAQGreenRosterPreview
    {
        private const long MaxExpandedWorkbookBytes = 32 * 1024 * 1024;

        public static async Task<IReadOnlyList<LegacyAQGreenRosterFileRow>> ReadRowsAsync(
            PreviewLegacyAQGreenRosterInput input)
        {
            return Key(input.Layout) switch
            {
                "" or "EXPLICITRELATIONSHIPS" => await ReadLayoutAsync<LegacyAQGreenRosterFileRow>(input),
                "NETWORKASSOCIATIONS" => (await ReadLayoutAsync<LegacyAQGreenAssociationFileRow>(input))
                    .Select(row => new LegacyAQGreenAssociationRow
                    {
                        OldMemberId = row.OldMemberId,
                        FirstName = row.MemberName,
                        Email = row.Email,
                        ContactNumber = row.ContactNumber,
                        OwnerName = row.NetworkOwnerName,
                        OwnerOldMemberId = row.NetworkOwnerOldMemberId,
                        Position = row.RecordedNetworkPosition
                    }).ToArray(),
                _ => throw new UserFriendlyException("Roster preview failed.", "Select a supported roster layout.")
            };
        }

        private static async Task<IReadOnlyList<T>> ReadLayoutAsync<T>(PreviewLegacyAQGreenRosterInput input) where T : class, new()
        {
            var result = await CustomerImportAppService.ReadFileAsync<T>(
                input.ContentBase64, Path.GetExtension(input.FileName ?? string.Empty).ToLowerInvariant(),
                InspectWorkbook);
            if (result.HasError || result.TemplateErrors?.Any() == true || result.RowErrors?.Any() == true)
                throw new UserFriendlyException("Roster preview failed.", "The file could not be read as the roster template. Check the headers and formatting.");

            var rows = result.Data?.ToList();
            if (rows == null || rows.Count == 0 || rows.Count > CustomerImportAppService.MaxRows)
                throw new UserFriendlyException("Roster preview failed.", "Supply between 1 and 1,000 roster rows.");
            return rows;
        }

        private static void InspectWorkbook(Stream stream)
        {
            // The shared single-sheet parser would otherwise silently ignore additional worksheets.
            try
            {
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
                var remaining = MaxExpandedWorkbookBytes;
                foreach (var entry in archive.Entries)
                {
                    if (entry.Length > remaining)
                        throw new UserFriendlyException("Roster preview failed.", "The expanded workbook exceeds 32 MB.");
                    remaining -= entry.Length;
                }

                var workbook = archive.GetEntry("xl/workbook.xml") ?? throw new InvalidDataException();
                using var xml = XmlReader.Create(workbook.Open(), new XmlReaderSettings
                {
                    CloseInput = true,
                    DtdProcessing = DtdProcessing.Prohibit,
                    MaxCharactersInDocument = MaxExpandedWorkbookBytes
                });
                XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                var sheets = XDocument.Load(xml).Root?.Element(spreadsheet + "sheets")?.Elements(spreadsheet + "sheet");
                if (sheets?.Take(2).Count() != 1)
                    throw new UserFriendlyException("Roster preview failed.", "Supply a workbook with exactly one worksheet.");
            }
            catch (Exception ex) when (ex is InvalidDataException or XmlException)
            {
                throw new UserFriendlyException("Roster preview failed.", "The workbook could not be read.");
            }
        }

        internal static string Key(string value) => value?.Trim().ToUpperInvariant() ?? string.Empty;

        public static LegacyAQGreenRosterPreviewDto Classify(int tenantId,
            IReadOnlyList<LegacyAQGreenRosterFileRow> rows,
            IReadOnlyCollection<LegacyAQGreenRosterAccount> accounts,
            IReadOnlyCollection<EntryParticipation> participations,
            IReadOnlyCollection<AQGreenNetworkPlacement> placements,
            IReadOnlyCollection<AQGreenRecruitmentAttribution> attributions)
        {
            var candidates = rows.Select((source, index) => new Row(source, index + 2)).ToArray();
            var byId = candidates.Where(row => row.Id.Length > 0).ToLookup(row => row.Id);
            foreach (var group in byId.Where(group => group.Count() > 1))
                foreach (var row in group) row.Duplicate("RepeatedOldMemberId");
            MarkSharedKey(candidates, row => Key(row.Source.Email), "RepeatedEmail");
            MarkSharedKey(candidates, row => Key(row.Source.ClubMemberNumber), "RepeatedClubMemberNumber");

            var tenantAccounts = accounts.Where(account => account.TenantId == tenantId).ToList();
            var byEmail = tenantAccounts.SelectMany(account => new[] { Key(account.CustomerEmail), Key(account.UserEmail) }
                .Where(key => key.Length > 0).Distinct().Select(key => (key, account)))
                .ToLookup(item => item.key, item => item.account);
            var byClubNumber = tenantAccounts.ToLookup(account => Key(account.ClubMemberNumber));
            var history = participations.Where(item => item.TenantId == tenantId).ToLookup(item => item.CustomerId);
            var topology = placements.Where(item => item.TenantId == tenantId).ToLookup(item => item.ParticipantId);
            var sponsorEvidence = attributions.Where(item => item.TenantId == tenantId).ToLookup(item => item.ParticipantId);

            foreach (var row in candidates)
            {
                if (row.Id.Length == 0) row.Issues.Add("OldMemberIdMissing");
                if (row.Association is { } association)
                {
                    row.Issues.Add("NetworkAssociationNeedsClarification");
                    if (Key(association.OwnerName).Length == 0 && Key(association.OwnerOldMemberId).Length == 0)
                        row.Issues.Add("NetworkAssociationOwnerUnknown");
                }
                if (!row.IsAQGreen) row.Issues.Add("ProgrammeRequiresReview");
                if (string.IsNullOrWhiteSpace(row.Source.MembershipStatus)) row.Issues.Add("MembershipStatusMissing");

                var email = Key(row.Source.Email);
                var clubNumber = Key(row.Source.ClubMemberNumber);
                var emailMatches = email.Length == 0 ? Array.Empty<LegacyAQGreenRosterAccount>() : byEmail[email].ToArray();
                var clubMatches = clubNumber.Length == 0 ? Array.Empty<LegacyAQGreenRosterAccount>() : byClubNumber[clubNumber].ToArray();
                if (emailMatches.Length == 0 && clubMatches.Length == 0)
                {
                    row.Unmatched = true;
                    row.Issues.Add("NoVisibleAccountMatch");
                }
                else if (emailMatches.Length != 1 || clubMatches.Length != 1 ||
                         emailMatches[0].CustomerId != clubMatches[0].CustomerId ||
                         Key(clubMatches[0].CustomerEmail) != email || Key(clubMatches[0].UserEmail) != email)
                    row.Issues.Add("AccountMatchRequiresCorroboration");
                else if (!clubMatches[0].AccountAvailable)
                    row.Issues.Add("AccountInactiveDeletedOrUnverified");
                else
                {
                    row.CustomerId = clubMatches[0].CustomerId;
                    if (row.IsAQGreen)
                    {
                        var existing = history[row.CustomerId.Value].ToArray();
                        if (existing.Any(item => item.IsDeleted)) row.Issues.Add("RetiredParticipationExists");
                        var live = existing.Where(item => !item.IsDeleted).ToArray();
                        if (live.Length > 1) row.Issues.Add("MultipleLiveParticipations");
                        else if (live.Length == 1) row.Participation = live[0];
                    }
                }
            }

            foreach (var row in candidates.Where(row => row.IsAQGreen))
            {
                var root = Key(row.Source.IsRoot);
                if (root.Length > 0 && root != "TRUE" && root != "FALSE") row.Issues.Add("InvalidRootMarker");
                if (root == "TRUE")
                {
                    if (row.ParentId.Length > 0 || row.SponsorId.Length > 0 || !string.IsNullOrWhiteSpace(row.Source.PlacementSlot))
                        row.Issues.Add("ConflictingRootFields");
                }
                else
                {
                    row.Parent = ResolveLink(row, row.ParentId, "PlacementParent", byId);
                    if (string.IsNullOrWhiteSpace(row.Source.PlacementSlot)) row.Issues.Add("PlacementSlotMissing");
                    else if (int.TryParse(row.Source.PlacementSlot.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var slot) &&
                             slot >= 1 && slot <= AQGreenPlacementRules.MaximumPlacementSlot)
                        row.Slot = slot;
                    else row.Issues.Add("InvalidPlacementSlot");
                    // A blank sponsor stays unknown, even if a placement parent is known.
                    row.Sponsor = ResolveLink(row, row.SponsorId, "CreditedSponsor", byId);
                }

                if (row.Participation != null)
                {
                    var existing = topology[row.Participation.Id].ToArray();
                    if (existing.Length > 1) row.Issues.Add("MultipleExistingPlacements");
                    else if (existing.Length == 1) row.ExistingPlacement = existing[0];
                }
            }

            foreach (var group in candidates.Where(row => row.Parent != null && row.Slot.HasValue)
                         .GroupBy(row => (row.Parent, row.Slot)).Where(group => group.Count() > 1))
                foreach (var row in group) row.Issues.Add("RepeatedPlacementSlot");

            var occupants = placements.Where(item => item.TenantId == tenantId && item.PlacementParentParticipantId.HasValue)
                .ToLookup(item => (item.PlacementParentParticipantId.Value, item.PlacementSlot));
            foreach (var row in candidates.Where(row => row.IsAQGreen))
            {
                if (HasCycle(row, item => item.Parent)) row.Issues.Add("PlacementCycle");
                if (HasCycle(row, item => item.Sponsor)) row.Issues.Add("SponsorCycle");
                if (row.Sponsor != null && RootOf(row) is Row root && RootOf(row.Sponsor) is Row sponsorRoot && root != sponsorRoot)
                    row.Issues.Add("CrossRootSponsor");

                if (row.Participation != null)
                {
                    var reportedSponsor = row.Sponsor?.Participation?.Id;
                    if (sponsorEvidence[row.Participation.Id].Any(item =>
                            (row.Sponsor != null && !reportedSponsor.HasValue) || item.CreditedSponsorParticipantId != reportedSponsor))
                        row.Issues.Add("ExistingCreditedSponsorConflict");
                }

                var parent = row.Parent?.Participation?.Id;
                if (row.ExistingPlacement is { } existing &&
                    (row.Parent != null && !parent.HasValue ||
                     parent != existing.PlacementParentParticipantId || row.Slot != existing.PlacementSlot ||
                     row.Parent?.ExistingPlacement is { } parentPlacement && parentPlacement.PlacementTreeScopeId != existing.PlacementTreeScopeId ||
                     row.Sponsor?.ExistingPlacement is { } sponsorPlacement && sponsorPlacement.PlacementTreeScopeId != existing.PlacementTreeScopeId))
                    row.Issues.Add("ExistingPlacementConflict");
                if (parent.HasValue && row.Slot.HasValue && occupants[(parent.Value, row.Slot)]
                        .Any(item => item.ParticipantId != row.Participation?.Id))
                    row.Issues.Add("ExistingPlacementSlotOccupied");
            }

            // An otherwise clean child cannot be accepted while its reported upline is unresolved.
            var requiresReview = candidates.Where(row => row.Issues.Count != 0 || row.IsDuplicate).ToHashSet();
            foreach (var row in candidates.Where(row => row.IsAQGreen))
            {
                var pending = new Stack<Row>();
                var seen = new HashSet<Row> { row };
                pending.Push(row);
                while (pending.TryPop(out var current))
                {
                    foreach (var dependency in new[] { current.Parent, current.Sponsor }.Where(dep => dep != null && seen.Add(dep)))
                    {
                        if (requiresReview.Contains(dependency)) row.Issues.Add("NetworkDependencyRequiresReview");
                        pending.Push(dependency);
                    }
                }
            }

            var preview = candidates.Select(row => row.ToDto()).ToArray();
            return new LegacyAQGreenRosterPreviewDto
            {
                TenantId = tenantId,
                AcceptedRows = preview.Count(row => row.Disposition == "Accepted"),
                DuplicateRows = preview.Count(row => row.Disposition == "Duplicate"),
                UnmatchedRows = preview.Count(row => row.Disposition == "Unmatched"),
                AmbiguousRows = preview.Count(row => row.Disposition == "Ambiguous"),
                Rows = preview
            };
        }

        private static void MarkSharedKey(IEnumerable<Row> rows, Func<Row, string> key, string issue)
        {
            foreach (var group in rows.GroupBy(key).Where(group => group.Key.Length > 0 && group.Count() > 1))
                foreach (var row in group) row.Duplicate(issue);
        }

        private static Row ResolveLink(Row row, string key, string relationship, ILookup<string, Row> byId)
        {
            var matches = byId[key].ToArray();
            if (key.Length == 0) row.Issues.Add(relationship + "Unknown");
            else if (matches.Length != 1 || !matches[0].IsAQGreen) row.Issues.Add(relationship + "NotResolved");
            else if (matches[0] == row) row.Issues.Add(relationship + "SelfReference");
            else return matches[0];
            return null;
        }

        private static bool HasCycle(Row row, Func<Row, Row> next)
        {
            var seen = new HashSet<Row>();
            for (var current = row; current != null; current = next(current))
                if (!seen.Add(current)) return true;
            return false;
        }

        private static Row RootOf(Row row)
        {
            var seen = new HashSet<Row>();
            for (var current = row; current != null && seen.Add(current); current = current.Parent)
                if (Key(current.Source.IsRoot) == "TRUE") return current;
            return null;
        }

        private sealed class Row(LegacyAQGreenRosterFileRow source, int rowNumber)
        {
            public LegacyAQGreenRosterFileRow Source => source;
            public LegacyAQGreenAssociationRow Association => source as LegacyAQGreenAssociationRow;
            public string Id => Key(source.OldMemberId);
            public string ParentId => Key(source.NetworkParentOldMemberId);
            public string SponsorId => Key(source.SponsorOldMemberId);
            public bool IsAQGreen => Key(source.Programme) == "AQGREEN";
            public HashSet<string> Issues { get; } = new();
            public bool IsDuplicate { get; private set; }
            public bool Unmatched { get; set; }
            public int? CustomerId { get; set; }
            public EntryParticipation Participation { get; set; }
            public AQGreenNetworkPlacement ExistingPlacement { get; set; }
            public Row Parent { get; set; }
            public Row Sponsor { get; set; }
            public int? Slot { get; set; }

            public void Duplicate(string issue)
            {
                IsDuplicate = true;
                Issues.Add(issue);
            }

            public LegacyAQGreenRosterPreviewRowDto ToDto() => new()
            {
                RowNumber = rowNumber,
                OldMemberId = source.OldMemberId?.Trim(),
                Programme = source.Programme?.Trim(),
                MembershipStatus = source.MembershipStatus?.Trim(),
                NetworkParentOldMemberId = source.NetworkParentOldMemberId?.Trim(),
                SponsorOldMemberId = source.SponsorOldMemberId?.Trim(),
                PlacementSlot = source.PlacementSlot?.Trim(),
                IsRoot = source.IsRoot?.Trim(),
                NetworkOwnerName = Association?.OwnerName?.Trim(),
                NetworkOwnerOldMemberId = Association?.OwnerOldMemberId?.Trim(),
                RecordedNetworkPosition = Association?.Position?.Trim(),
                MatchedCustomerId = CustomerId,
                ExistingParticipationId = Participation?.Id,
                ExistingParticipationStatus = Participation?.Status.ToString(),
                Disposition = IsDuplicate ? "Duplicate" : Unmatched && Issues.Count == 1 ? "Unmatched" :
                    Issues.Count > 0 ? "Ambiguous" : "Accepted",
                Issues = Issues.OrderBy(issue => issue, StringComparer.Ordinal).ToArray()
            };
        }
    }

    public sealed record LegacyAQGreenRosterAccount(int TenantId, int CustomerId, string ClubMemberNumber,
        string CustomerEmail, string UserEmail, bool AccountAvailable);
}
