using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Abp.Auditing;
using Magicodes.ExporterAndImporter.Excel;

namespace AqualLifeStyle.Application.Admin.ProgrammeParticipations.Dto
{
    public class PreviewLegacyAQGreenRosterInput
    {
        [Required, StringLength(255), DisableAuditing]
        public string FileName { get; set; }

        [Required, DisableAuditing]
        public string ContentBase64 { get; set; }

        [Range(1, int.MaxValue)]
        public int TenantId { get; set; }

        public string Layout { get; set; }
    }

    public class LegacyAQGreenRosterPreviewDto
    {
        public int TenantId { get; set; }
        public int AcceptedRows { get; set; }
        public int DuplicateRows { get; set; }
        public int UnmatchedRows { get; set; }
        public int AmbiguousRows { get; set; }
        public IReadOnlyList<LegacyAQGreenRosterPreviewRowDto> Rows { get; set; }
    }

    public class LegacyAQGreenRosterPreviewRowDto
    {
        public int RowNumber { get; set; }
        public string OldMemberId { get; set; }
        public string Programme { get; set; }
        public string MembershipStatus { get; set; }
        public string NetworkParentOldMemberId { get; set; }
        public string SponsorOldMemberId { get; set; }
        public string PlacementSlot { get; set; }
        public string IsRoot { get; set; }
        public string NetworkOwnerName { get; set; }
        public string NetworkOwnerOldMemberId { get; set; }
        public string RecordedNetworkPosition { get; set; }
        public int? MatchedCustomerId { get; set; }
        public Guid? ExistingParticipationId { get; set; }
        public string ExistingParticipationStatus { get; set; }
        public string Disposition { get; set; }
        public IReadOnlyList<string> Issues { get; set; }
    }

    [ExcelImporter(IsIgnoreColumnCase = true)]
    public class LegacyAQGreenRosterFileRow
    {
        public string OldMemberId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string ContactNumber { get; set; }
        public string HomeAddress { get; set; }
        public string ClubMemberNumber { get; set; }
        public string Programme { get; set; }
        public string MembershipStatus { get; set; }
        public string NetworkParentOldMemberId { get; set; }
        public string SponsorOldMemberId { get; set; }
        public string PlacementSlot { get; set; }
        public string IsRoot { get; set; }
    }

    [ExcelImporter(IsIgnoreColumnCase = true)]
    public class LegacyAQGreenAssociationFileRow
    {
        public string OldMemberId { get; set; }
        public string MemberName { get; set; }
        public string Email { get; set; }
        public string ContactNumber { get; set; }
        public string NetworkOwnerName { get; set; }
        public string NetworkOwnerOldMemberId { get; set; }
        public string RecordedNetworkPosition { get; set; }
    }

    internal sealed class LegacyAQGreenAssociationRow : LegacyAQGreenRosterFileRow
    {
        public string OwnerName { get; set; }
        public string OwnerOldMemberId { get; set; }
        public string Position { get; set; }
    }
}
