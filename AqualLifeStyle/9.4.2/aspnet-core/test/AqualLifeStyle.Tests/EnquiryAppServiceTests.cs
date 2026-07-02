using System.Threading.Tasks;
using Moq;
using Xunit;
using AqualLifeStyle.Application.Enquiries;
using AqualLifeStyle.Application.Enquiries.Dto;
using AqualLifeStyle.Domain.Enquiries;

namespace AqualLifeStyle.Tests
{
    public class EnquiryAppServiceTests
    {
        [Fact]
        public async Task RespondAsync_SetsResponseAndStatus()
        {
            var enquiry = Enquiry.Create(1, 2, "Initial message");

            var repo = new Mock<IEnquiryRepository>();
            repo.Setup(r => r.GetAsync(10)).ReturnsAsync(enquiry);
            repo.Setup(r => r.UpdateAsync(enquiry)).ReturnsAsync(enquiry);

            var svc = new EnquiryAppService(repo.Object);

            var result = await svc.RespondAsync(10, new RespondToEnquiryDto { Response = "Thanks" });

            Assert.Equal("Thanks", result.Response);
            Assert.Equal((int)EnquiryStatus.Responded, result.Status);
        }

        [Fact]
        public async Task CloseAsync_SetsClosedStatus()
        {
            var enquiry = Enquiry.Create(1, 2, "Question");

            var repo = new Mock<IEnquiryRepository>();
            repo.Setup(r => r.GetAsync(11)).ReturnsAsync(enquiry);
            repo.Setup(r => r.UpdateAsync(enquiry)).ReturnsAsync(enquiry);

            var svc = new EnquiryAppService(repo.Object);

            var result = await svc.CloseAsync(11);

            Assert.True(result.IsClosed);
            Assert.Equal((int)EnquiryStatus.Closed, result.Status);
        }

        [Fact]
        public async Task ReopenAsync_ReopensClosedEnquiry()
        {
            var enquiry = Enquiry.Create(1, 2, "Question");
            enquiry.MarkAsResponded("ok");
            enquiry.Close();

            var repo = new Mock<IEnquiryRepository>();
            repo.Setup(r => r.GetAsync(12)).ReturnsAsync(enquiry);
            repo.Setup(r => r.UpdateAsync(enquiry)).ReturnsAsync(enquiry);

            var svc = new EnquiryAppService(repo.Object);

            var result = await svc.ReopenAsync(12);

            Assert.True(result.IsPending);
            Assert.Equal((int)EnquiryStatus.Pending, result.Status);
            Assert.Equal(string.Empty, result.Response);
        }
    }
}
