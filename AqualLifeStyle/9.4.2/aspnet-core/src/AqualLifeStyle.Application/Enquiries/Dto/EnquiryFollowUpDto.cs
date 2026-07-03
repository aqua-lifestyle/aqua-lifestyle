using System;

namespace AqualLifeStyle.Application.Enquiries.Dto
{
    public class EnquiryFollowUpDto
    {
        public int Id { get; set; }
        public int EnquiryId { get; set; }
        public DateTime FollowUpDate { get; set; }
        public int? FollowUpByMemberId { get; set; }
        public string FollowUpNotes { get; set; }
        public int Outcome { get; set; } // EnquiryFollowUpOutcome
        public string OutcomeText { get; set; }
        public decimal ConversionProbability { get; set; }
        public bool IsResolved { get; set; }
    }

    public class CreateEnquiryFollowUpDto
    {
        public int? FollowUpByMemberId { get; set; }
        public string FollowUpNotes { get; set; }
        public int Outcome { get; set; } // EnquiryFollowUpOutcome
    }

    public class UpdateEnquiryFollowUpOutcomeDto
    {
        public int Outcome { get; set; } // EnquiryFollowUpOutcome
        public string UpdatedNotes { get; set; }
    }
}
