using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AqualLifeStyle.Domain.Recruitment;

namespace AqualLifeStyle.Application.Recruitment.Dto
{
    public class ProgrammeInvitationDto
    {
        public string Code { get; set; }
        public string ProgrammeKey { get; set; }
        public string ProgrammeName { get; set; }
        public string ClubMemberNumber { get; set; }
    }

    public class MyProgrammeInvitationsDto
    {
        public IReadOnlyList<ProgrammeInvitationDto> Invitations { get; set; }
    }

    public class ProgrammeInvitationCodeInput
    {
        [Required]
        [StringLength(ProgrammeInvitation.CodeLength, MinimumLength = ProgrammeInvitation.CodeLength)]
        [RegularExpression("^[2-9A-HJ-NP-Za-hj-np-z]+$")]
        public string InviteCode { get; set; }
    }

    public class ProgrammeInvitationPreviewDto
    {
        public string InviteCode { get; set; }
        public string RecruiterName { get; set; }
        public string RecruiterClubMemberNumber { get; set; }
        public string ProgrammeKey { get; set; }
        public string ProgrammeName { get; set; }
        public bool RecruiterEligible { get; set; }
        public string AreaName { get; set; }
        public string TenancyName { get; set; }
    }
}
