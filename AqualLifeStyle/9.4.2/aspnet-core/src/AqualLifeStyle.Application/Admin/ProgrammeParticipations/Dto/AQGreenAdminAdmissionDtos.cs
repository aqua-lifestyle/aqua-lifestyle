using System;

namespace AqualLifeStyle.Application.Admin.ProgrammeParticipations.Dto
{
    public sealed class AQGreenAdmissionTreeDto
    {
        public Guid ScopeId { get; set; }
        public string RootMemberNumber { get; set; }
        public bool HistoricalHold { get; set; }
    }

    public sealed class RegisterAQGreenCompanyAdmissionInput
    {
        public int TenantId { get; set; }
        public int CustomerId { get; set; }
        public Guid ScopeId { get; set; }
        public string AcquisitionEvidence { get; set; }
    }

    public sealed class OpenAQGreenHistoricalReviewInput
    {
        public int TenantId { get; set; }
        public Guid ScopeId { get; set; }
        public string EvidenceReference { get; set; }
        public int ExpectedMembers { get; set; }
    }

    public sealed class CompleteAQGreenHistoricalReviewInput
    {
        public int TenantId { get; set; }
        public Guid ScopeId { get; set; }
        public string EvidenceReference { get; set; }
        public AQGreenHistoricalPlacementInput[] Members { get; set; }
    }

    public sealed class AQGreenHistoricalPlacementInput
    {
        public string OriginalReference { get; set; }
        public Guid ParticipantId { get; set; }
        public int CustomerId { get; set; }
        public Guid? ParentParticipantId { get; set; }
        public int? Slot { get; set; }
        public DateTime StartedAtUtc { get; set; }
        public DateTime ActivatedAtUtc { get; set; }
        public DateTime PlacedAtUtc { get; set; }
    }
}
