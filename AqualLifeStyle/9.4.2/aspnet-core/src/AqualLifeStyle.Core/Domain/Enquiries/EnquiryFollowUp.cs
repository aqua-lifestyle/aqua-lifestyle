using System;

namespace AqualLifeStyle.Domain.Enquiries
{
    /// <summary>
    /// Tracks a single follow-up attempt on an enquiry with outcome and conversion probability.
    /// </summary>
    public class EnquiryFollowUp
    {
        public int Id { get; private set; }
        public int EnquiryId { get; private set; }
        public DateTime FollowUpDate { get; private set; }
        public int? FollowUpByMemberId { get; private set; }
        public string FollowUpNotes { get; private set; }
        public EnquiryFollowUpOutcome Outcome { get; private set; }
        public decimal ConversionProbability { get; private set; } // 0-100
        public bool IsResolved { get; private set; }

        protected EnquiryFollowUp() { }

        private EnquiryFollowUp(int enquiryId, int? memberId, string notes, EnquiryFollowUpOutcome outcome)
        {
            if (string.IsNullOrWhiteSpace(notes)) throw new ArgumentException("Follow-up notes are required.", nameof(notes));
            
            EnquiryId = enquiryId;
            FollowUpByMemberId = memberId;
            FollowUpNotes = notes.Trim();
            Outcome = outcome;
            FollowUpDate = DateTime.UtcNow;
            ConversionProbability = EstimateConversionProbability(outcome);
            IsResolved = outcome == EnquiryFollowUpOutcome.Converted || outcome == EnquiryFollowUpOutcome.Lost;
        }

        public static EnquiryFollowUp Create(int enquiryId, int? memberId, string notes, EnquiryFollowUpOutcome outcome)
            => new EnquiryFollowUp(enquiryId, memberId, notes, outcome);

        public void UpdateOutcome(EnquiryFollowUpOutcome newOutcome)
        {
            Outcome = newOutcome;
            ConversionProbability = EstimateConversionProbability(newOutcome);
            IsResolved = newOutcome == EnquiryFollowUpOutcome.Converted || newOutcome == EnquiryFollowUpOutcome.Lost;
        }

        private static decimal EstimateConversionProbability(EnquiryFollowUpOutcome outcome) => outcome switch
        {
            EnquiryFollowUpOutcome.Interested => 75m,        // High probability
            EnquiryFollowUpOutcome.Considering => 50m,       // Medium probability
            EnquiryFollowUpOutcome.NotInterested => 10m,     // Low probability
            EnquiryFollowUpOutcome.Converted => 100m,        // Already converted
            EnquiryFollowUpOutcome.Lost => 0m,               // Lost the deal
            _ => 0m
        };
    }

    /// <summary>
    /// Outcome of an enquiry follow-up attempt.
    /// </summary>
    public enum EnquiryFollowUpOutcome
    {
        Interested = 0,      // Customer shows interest in the product/service
        Considering = 1,     // Customer is considering the offer
        NotInterested = 2,   // Customer declined interest
        Converted = 3,       // Customer converted to purchase/membership
        Lost = 4            // Lead lost, no further action planned
    }
}
