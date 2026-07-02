namespace AqualLifeStyle.Application.Enquiries.Dto
{
    public class EnquiryDto
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public int ProductId { get; set; }
        public string Message { get; set; }
        public string Response { get; set; }
        public int Status { get; set; }
        public string CreatedAt { get; set; }
        public bool IsClosed { get; set; }
        public bool IsPending { get; set; }
    }
}
