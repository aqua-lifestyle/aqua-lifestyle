using System;
using System.Collections.Generic;
using System.Linq;
using Abp.Domain.Entities;
using AqualLifeStyle.Domain.Enums;

namespace AqualLifeStyle.Domain.Enquiries
{
    public class Enquiry : Entity<int>
    {
        public int CustomerId { get; private set; }
        public int ProductId { get; private set; }
        public string Message { get; private set; }
        public string Response { get; private set; }
        public EnquiryStatus Status { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public int? AssignedToMemberId { get; private set; }
        public int? ReferredByFacilitatorId { get; private set; }
        public bool IsConverted { get; private set; }
        public DateTime? ConvertedAt { get; private set; }

        // Follow-up workflow fields
        private readonly List<EnquiryFollowUp> _followUps = new();
        public IReadOnlyList<EnquiryFollowUp> FollowUps => _followUps.AsReadOnly();
        
        public decimal ConversionProbability { get; private set; } // Aggregate conversion probability (0-100)
        public DateTime? LastFollowUpDate { get; private set; }

        protected Enquiry() { }

        private Enquiry(int customerId, int productId, string message)
        {
            if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("Message is required.", nameof(message));
            CustomerId = customerId;
            ProductId = productId;
            Message = message.Trim();
            Status = EnquiryStatus.Pending;
            CreatedAt = DateTime.UtcNow;
            Response = string.Empty;
            IsConverted = false;
            ConversionProbability = 0m;
        }

        public static Enquiry Create(int customerId, int productId, string message)
            => new Enquiry(customerId, productId, message);

        public void Respond(string response)
        {
            if (Status != EnquiryStatus.Pending) throw new InvalidOperationException("Only pending enquiries can be responded to.");
            if (string.IsNullOrWhiteSpace(response)) throw new ArgumentException("Response is required.", nameof(response));
            Response = response.Trim();
            Status = EnquiryStatus.Responded;
        }

        public void Close()
        {
            if (Status == EnquiryStatus.Closed) return;
            Status = EnquiryStatus.Closed;
        }

        public void MarkAsResponded(string response)
        {
            Respond(response);
        }

        public void Reopen()
        {
            if (Status != EnquiryStatus.Closed) throw new InvalidOperationException("Only closed enquiries can be reopened.");
            Status = EnquiryStatus.Pending;
            Response = string.Empty;
        }

        public void AssignToMember(int memberId)
        {
            if (memberId <= 0) throw new ArgumentException("Member ID must be valid.", nameof(memberId));
            if (IsConverted) throw new InvalidOperationException("Converted enquiries cannot be re-assigned.");
            AssignedToMemberId = memberId;
        }

        /// <summary>
        /// Record the facilitator who sourced this lead (used for referral attribution on conversion).
        /// </summary>
        public void SetReferredByFacilitator(int facilitatorId)
        {
            if (facilitatorId <= 0) throw new ArgumentException("Facilitator ID must be valid.", nameof(facilitatorId));
            if (IsConverted) throw new InvalidOperationException("Converted enquiries cannot be re-linked.");
            ReferredByFacilitatorId = facilitatorId;
        }

        public void ConvertToCustomer()
            => ConvertToCustomer(ReferredByFacilitatorId);

        public void ConvertToCustomer(int? referredByFacilitatorId)
        {
            if (IsConverted) throw new InvalidOperationException("Enquiry has already been converted.");
            if (referredByFacilitatorId.HasValue && referredByFacilitatorId.Value <= 0)
            {
                throw new ArgumentException("Facilitator ID must be valid.", nameof(referredByFacilitatorId));
            }

            ReferredByFacilitatorId = referredByFacilitatorId;
            IsConverted = true;
            ConvertedAt = DateTime.UtcNow;
            Status = EnquiryStatus.Closed;
            ConversionProbability = 100m;
        }

        public void ClearAssignment()
        {
            if (IsConverted) throw new InvalidOperationException("Converted enquiries cannot be un-assigned.");
            AssignedToMemberId = null;
        }

        /// <summary>
        /// Record a follow-up attempt on this enquiry with outcome tracking and probability estimation.
        /// </summary>
        public void RecordFollowUp(int? memberId, string notes, EnquiryFollowUpOutcome outcome)
        {
            if (IsConverted)
            {
                throw new InvalidOperationException("Cannot record follow-ups on already-converted enquiries.");
            }

            var followUp = EnquiryFollowUp.Create(Id, memberId, notes, outcome);
            _followUps.Add(followUp);
            LastFollowUpDate = followUp.FollowUpDate;

            // Update conversion probability based on latest follow-up
            ConversionProbability = followUp.ConversionProbability;

            // Auto-convert if follow-up indicates conversion
            if (outcome == EnquiryFollowUpOutcome.Converted)
            {
                ConvertToCustomer();
            }
            // Mark as closed if lead is lost
            else if (outcome == EnquiryFollowUpOutcome.Lost && Status != EnquiryStatus.Closed)
            {
                Close();
            }
        }

        /// <summary>
        /// Get the most recent follow-up on this enquiry.
        /// </summary>
        public EnquiryFollowUp GetLatestFollowUp()
        {
            return _followUps.OrderByDescending(f => f.FollowUpDate).FirstOrDefault();
        }

        /// <summary>
        /// Get total number of follow-ups recorded for this enquiry.
        /// </summary>
        public int GetFollowUpCount()
        {
            return _followUps.Count;
        }

        /// <summary>
        /// Check if this enquiry is sales-ready based on follow-up engagement.
        /// </summary>
        public bool IsSalesReady()
        {
            return ConversionProbability >= 50m && Status == EnquiryStatus.Responded;
        }
    }
}
