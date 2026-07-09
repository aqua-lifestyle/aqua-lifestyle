using System.Threading.Tasks;
using Abp.Domain.Repositories;
using Moq;
using AqualLifeStyle.Application.Enquiries;
using AqualLifeStyle.Application.Enquiries.Dto;
using AqualLifeStyle.Domain.Enquiries;
using AqualLifeStyle.Domain.Enums;
using Xunit;

namespace AqualLifeStyle.Tests
{
    public class EnquiryAppServiceTests
    {
        private readonly Mock<IEnquiryRepository> _enquiryRepositoryMock;
        private readonly EnquiryAppService _service;

        public EnquiryAppServiceTests()
        {
            _enquiryRepositoryMock = new Mock<IEnquiryRepository>();
            _service = new EnquiryAppService(_enquiryRepositoryMock.Object);
        }

        [Fact]
        public async Task RespondAsync_WithValidResponse_UpdatesEnquiryStatus()
        {
            // Arrange
            var enquiry = Enquiry.Create(customerId: 1, productId: 5, message: "Question about product");
            _enquiryRepositoryMock.Setup(r => r.GetAsync(1)).ReturnsAsync(enquiry);

            // Act
            var result = await _service.RespondAsync(1, new RespondToEnquiryDto { Response = "Here is the answer" });

            // Assert
            Assert.Equal(EnquiryStatus.Responded, enquiry.Status);
            Assert.Equal("Here is the answer", enquiry.Response);
            _enquiryRepositoryMock.Verify(r => r.UpdateAsync(enquiry), Times.Once);
        }

        [Fact]
        public async Task CloseAsync_WithPendingEnquiry_ClosesSuccessfully()
        {
            // Arrange
            var enquiry = Enquiry.Create(customerId: 1, productId: 5, message: "Question");
            _enquiryRepositoryMock.Setup(r => r.GetAsync(2)).ReturnsAsync(enquiry);

            // Act
            await _service.CloseAsync(2);

            // Assert
            Assert.Equal(EnquiryStatus.Closed, enquiry.Status);
            _enquiryRepositoryMock.Verify(r => r.UpdateAsync(enquiry), Times.Once);
        }

        [Fact]
        public async Task ReopenAsync_WithClosedEnquiry_ReopensSuccessfully()
        {
            // Arrange
            var enquiry = Enquiry.Create(customerId: 1, productId: 5, message: "Question");
            enquiry.Close();
            _enquiryRepositoryMock.Setup(r => r.GetAsync(3)).ReturnsAsync(enquiry);

            // Act
            await _service.ReopenAsync(3);

            // Assert
            Assert.Equal(EnquiryStatus.Pending, enquiry.Status);
            Assert.Empty(enquiry.Response);
            _enquiryRepositoryMock.Verify(r => r.UpdateAsync(enquiry), Times.Once);
        }

        [Fact]
        public async Task AssignToMemberAsync_WithValidMemberId_AssignsSuccessfully()
        {
            // Arrange
            var enquiry = Enquiry.Create(customerId: 1, productId: 5, message: "Question");
            _enquiryRepositoryMock.Setup(r => r.GetAsync(4)).ReturnsAsync(enquiry);

            // Act
            var result = await _service.AssignToMemberAsync(4, new AssignEnquiryDto { MemberId = 10 });

            // Assert
            Assert.Equal(10, enquiry.AssignedToMemberId);
            Assert.False(enquiry.IsConverted);
            _enquiryRepositoryMock.Verify(r => r.UpdateAsync(enquiry), Times.Once);
        }

        [Fact]
        public async Task AssignToMemberAsync_WithInvalidMemberId_Throws()
        {
            // Arrange
            var enquiry = Enquiry.Create(customerId: 1, productId: 5, message: "Question");
            _enquiryRepositoryMock.Setup(r => r.GetAsync(5)).ReturnsAsync(enquiry);

            // Act & Assert
            await Assert.ThrowsAsync<AqualLifeStyle.Application.Exceptions.AqualLifeStyleValidationException>(() => 
                _service.AssignToMemberAsync(5, new AssignEnquiryDto { MemberId = 0 }));
        }

        [Fact]
        public async Task ConvertToCustomerAsync_WithPendingEnquiry_ConvertsSuccessfully()
        {
            // Arrange
            var enquiry = Enquiry.Create(customerId: 1, productId: 5, message: "Question");
            _enquiryRepositoryMock.Setup(r => r.GetAsync(6)).ReturnsAsync(enquiry);

            // Act
            var result = await _service.ConvertToCustomerAsync(6, new ConvertEnquiryToCustomerDto());

            // Assert
            Assert.True(enquiry.IsConverted);
            Assert.Equal(EnquiryStatus.Closed, enquiry.Status);
            Assert.NotNull(enquiry.ConvertedAt);
            _enquiryRepositoryMock.Verify(r => r.UpdateAsync(enquiry), Times.Once);
        }

        [Fact]
        public async Task ConvertToCustomerAsync_WithAlreadyConvertedEnquiry_Throws()
        {
            // Arrange
            var enquiry = Enquiry.Create(customerId: 1, productId: 5, message: "Question");
            enquiry.ConvertToCustomer();
            _enquiryRepositoryMock.Setup(r => r.GetAsync(7)).ReturnsAsync(enquiry);

            // Act & Assert
            await Assert.ThrowsAsync<AqualLifeStyle.Application.Exceptions.AqualLifeStyleBusinessRuleException>(() => 
                _service.ConvertToCustomerAsync(7, new ConvertEnquiryToCustomerDto()));
        }

        [Fact]
        public async Task ConvertToCustomerAsync_WhenDomainRejects_PreservesInnerException()
        {
            // Arrange
            var enquiry = Enquiry.Create(customerId: 1, productId: 5, message: "Question");
            enquiry.ConvertToCustomer();
            _enquiryRepositoryMock.Setup(r => r.GetAsync(11)).ReturnsAsync(enquiry);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AqualLifeStyle.Application.Exceptions.AqualLifeStyleBusinessRuleException>(() =>
                _service.ConvertToCustomerAsync(11, new ConvertEnquiryToCustomerDto()));

            Assert.IsType<System.InvalidOperationException>(ex.InnerException);
        }

        [Fact]
        public async Task ClearAssignmentAsync_WithAssignedEnquiry_ClearsSuccessfully()
        {
            // Arrange
            var enquiry = Enquiry.Create(customerId: 1, productId: 5, message: "Question");
            enquiry.AssignToMember(10);
            _enquiryRepositoryMock.Setup(r => r.GetAsync(8)).ReturnsAsync(enquiry);

            // Act
            await _service.ClearAssignmentAsync(8, new ClearAssignmentDto());

            // Assert
            Assert.Null(enquiry.AssignedToMemberId);
            _enquiryRepositoryMock.Verify(r => r.UpdateAsync(enquiry), Times.Once);
        }

        [Fact]
        public async Task ClearAssignmentAsync_WithConvertedEnquiry_Throws()
        {
            // Arrange
            var enquiry = Enquiry.Create(customerId: 1, productId: 5, message: "Question");
            enquiry.AssignToMember(10);
            enquiry.ConvertToCustomer();
            _enquiryRepositoryMock.Setup(r => r.GetAsync(9)).ReturnsAsync(enquiry);

            // Act & Assert
            await Assert.ThrowsAsync<AqualLifeStyle.Application.Exceptions.AqualLifeStyleBusinessRuleException>(() => 
                _service.ClearAssignmentAsync(9, new ClearAssignmentDto()));
        }

        [Fact]
        public async Task AssignToMemberAsync_AfterConversion_Throws()
        {
            // Arrange
            var enquiry = Enquiry.Create(customerId: 1, productId: 5, message: "Question");
            enquiry.ConvertToCustomer();
            _enquiryRepositoryMock.Setup(r => r.GetAsync(10)).ReturnsAsync(enquiry);

            // Act & Assert
            await Assert.ThrowsAsync<AqualLifeStyle.Application.Exceptions.AqualLifeStyleBusinessRuleException>(() => 
                _service.AssignToMemberAsync(10, new AssignEnquiryDto { MemberId = 20 }));
        }
    }
}
