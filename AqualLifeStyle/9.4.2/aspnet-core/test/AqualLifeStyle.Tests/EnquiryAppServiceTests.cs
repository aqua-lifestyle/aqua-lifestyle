using System.Threading.Tasks;
using NSubstitute;
using Xunit;
using AqualLifeStyle.Application.Enquiries;
using AqualLifeStyle.Application.Enquiries.Dto;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Enquiries;

namespace AqualLifeStyle.Tests
{
    public class EnquiryAppServiceTests
    {
        [Fact]
        public async Task RespondAsync_SetsResponseAndStatus()
        {
            var enquiry = Enquiry.Create(1, 2, "Initial message");

            var repo = Substitute.For<IEnquiryRepository>();
            repo.GetAsync(10).Returns(enquiry);
            repo.UpdateAsync(enquiry).Returns(enquiry);

            var svc = new EnquiryAppService(repo);

            var result = await svc.RespondAsync(10, new RespondToEnquiryDto { Response = "Thanks" });

            Assert.Equal("Thanks", result.Response);
            Assert.Equal((int)EnquiryStatus.Responded, result.Status);
        }

        [Fact]
        public async Task CloseAsync_SetsClosedStatus()
        {
            var enquiry = Enquiry.Create(1, 2, "Question");

            var repo = Substitute.For<IEnquiryRepository>();
            repo.GetAsync(11).Returns(enquiry);
            repo.UpdateAsync(enquiry).Returns(enquiry);

            var svc = new EnquiryAppService(repo);

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

            var repo = Substitute.For<IEnquiryRepository>();
            repo.GetAsync(12).Returns(enquiry);
            repo.UpdateAsync(enquiry).Returns(enquiry);

            var svc = new EnquiryAppService(repo);

            var result = await svc.ReopenAsync(12);

            Assert.True(result.IsPending);
            Assert.Equal((int)EnquiryStatus.Pending, result.Status);
            Assert.Equal(string.Empty, result.Response);
        }

        [Fact]
        public async Task AssignToMemberAsync_WithValidMemberId_AssignsSuccessfully()
        {
            var enquiry = Enquiry.Create(1, 5, "Question");

            var repo = Substitute.For<IEnquiryRepository>();
            repo.GetAsync(14).Returns(enquiry);
            repo.UpdateAsync(enquiry).Returns(enquiry);

            var svc = new EnquiryAppService(repo);

            var result = await svc.AssignToMemberAsync(14, new AssignEnquiryDto { MemberId = 10 });

            Assert.Equal(10, enquiry.AssignedToMemberId);
            Assert.False(enquiry.IsConverted);
            Assert.Equal(10, result.AssignedToMemberId);
            await repo.Received(1).UpdateAsync(enquiry);
        }

        [Fact]
        public async Task AssignToMemberAsync_WithInvalidMemberId_Throws()
        {
            var enquiry = Enquiry.Create(1, 5, "Question");

            var repo = Substitute.For<IEnquiryRepository>();
            repo.GetAsync(15).Returns(enquiry);

            var svc = new EnquiryAppService(repo);

            await Assert.ThrowsAsync<AqualLifeStyle.Application.Exceptions.AqualLifeStyleValidationException>(
                () => svc.AssignToMemberAsync(15, new AssignEnquiryDto { MemberId = 0 }));
        }

        [Fact]
        public async Task ConvertToCustomerAsync_WithPendingEnquiry_ConvertsSuccessfully()
        {
            var enquiry = Enquiry.Create(1, 5, "Question");

            var repo = Substitute.For<IEnquiryRepository>();
            repo.GetAsync(16).Returns(enquiry);
            repo.UpdateAsync(enquiry).Returns(enquiry);

            var svc = new EnquiryAppService(repo);

            var result = await svc.ConvertToCustomerAsync(16, new ConvertEnquiryToCustomerDto());

            Assert.True(enquiry.IsConverted);
            Assert.Equal(EnquiryStatus.Closed, enquiry.Status);
            Assert.NotNull(enquiry.ConvertedAt);
            Assert.True(result.IsConverted);
            await repo.Received(1).UpdateAsync(enquiry);
        }

        [Fact]
        public async Task ConvertToCustomerAsync_WithAlreadyConvertedEnquiry_Throws()
        {
            var enquiry = Enquiry.Create(1, 5, "Question");
            enquiry.ConvertToCustomer();

            var repo = Substitute.For<IEnquiryRepository>();
            repo.GetAsync(17).Returns(enquiry);

            var svc = new EnquiryAppService(repo);

            await Assert.ThrowsAsync<AqualLifeStyle.Application.Exceptions.AqualLifeStyleBusinessRuleException>(
                () => svc.ConvertToCustomerAsync(17, new ConvertEnquiryToCustomerDto()));
        }

        [Fact]
        public async Task ClearAssignmentAsync_WithAssignedEnquiry_ClearsSuccessfully()
        {
            var enquiry = Enquiry.Create(1, 5, "Question");
            enquiry.AssignToMember(10);

            var repo = Substitute.For<IEnquiryRepository>();
            repo.GetAsync(18).Returns(enquiry);
            repo.UpdateAsync(enquiry).Returns(enquiry);

            var svc = new EnquiryAppService(repo);

            var result = await svc.ClearAssignmentAsync(18, new ClearAssignmentDto());

            Assert.Null(enquiry.AssignedToMemberId);
            Assert.Null(result.AssignedToMemberId);
            await repo.Received(1).UpdateAsync(enquiry);
        }

        [Fact]
        public async Task ClearAssignmentAsync_WithConvertedEnquiry_Throws()
        {
            var enquiry = Enquiry.Create(1, 5, "Question");
            enquiry.AssignToMember(10);
            enquiry.ConvertToCustomer();

            var repo = Substitute.For<IEnquiryRepository>();
            repo.GetAsync(19).Returns(enquiry);

            var svc = new EnquiryAppService(repo);

            await Assert.ThrowsAsync<AqualLifeStyle.Application.Exceptions.AqualLifeStyleBusinessRuleException>(
                () => svc.ClearAssignmentAsync(19, new ClearAssignmentDto()));
        }

        [Fact]
        public async Task AssignToMemberAsync_AfterConversion_Throws()
        {
            var enquiry = Enquiry.Create(1, 5, "Question");
            enquiry.ConvertToCustomer();

            var repo = Substitute.For<IEnquiryRepository>();
            repo.GetAsync(20).Returns(enquiry);

            var svc = new EnquiryAppService(repo);

            await Assert.ThrowsAsync<AqualLifeStyle.Application.Exceptions.AqualLifeStyleBusinessRuleException>(
                () => svc.AssignToMemberAsync(20, new AssignEnquiryDto { MemberId = 20 }));
        }

        [Fact]
        public async Task RespondAsync_ClosedEnquiry_ThrowsInvalidStateException()
        {
            var enquiry = Enquiry.Create(1, 2, "Question");
            enquiry.MarkAsResponded("ok");
            enquiry.Close();

            var repo = Substitute.For<IEnquiryRepository>();
            repo.GetAsync(13).Returns(enquiry);

            var svc = new EnquiryAppService(repo);

            var exception = await Assert.ThrowsAsync<AqualLifeStyle.Application.Exceptions.AqualLifeStyleInvalidStateException>(
                () => svc.RespondAsync(13, new RespondToEnquiryDto { Response = "Thank you" }));

            Assert.Equal("Enquiry in state 'Closed' cannot respond.", exception.Message);
            Assert.Equal(400, exception.StatusCode);
            Assert.Equal("INVALID_STATE", exception.ErrorCode);
        }
    }
}
