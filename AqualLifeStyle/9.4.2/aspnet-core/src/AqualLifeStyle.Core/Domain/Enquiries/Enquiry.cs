using System;
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
        public bool IsConverted { get; private set; }
        public DateTime? ConvertedAt { get; private set; }

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

        public void ConvertToCustomer()
        {
            if (IsConverted) throw new InvalidOperationException("Enquiry has already been converted.");
            IsConverted = true;
            ConvertedAt = DateTime.UtcNow;
            Status = EnquiryStatus.Closed;
        }

        public void ClearAssignment()
        {
            if (IsConverted) throw new InvalidOperationException("Converted enquiries cannot be un-assigned.");
            AssignedToMemberId = null;
        }
    }
}
