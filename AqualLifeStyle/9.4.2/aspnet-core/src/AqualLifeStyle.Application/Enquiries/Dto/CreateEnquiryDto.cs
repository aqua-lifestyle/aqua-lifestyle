namespace AqualLifeStyle.Application.Enquiries.Dto
{
    public class CreateEnquiryDto
    {
        public int CustomerId { get; set; }
        public int ProductId { get; set; }
        public string Message { get; set; }
    }

    public class RespondToEnquiryDto
    {
        public string Response { get; set; }
    }
}
