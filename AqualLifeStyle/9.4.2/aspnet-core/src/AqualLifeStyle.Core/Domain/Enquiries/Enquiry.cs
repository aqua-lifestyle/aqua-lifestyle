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
    }
}
