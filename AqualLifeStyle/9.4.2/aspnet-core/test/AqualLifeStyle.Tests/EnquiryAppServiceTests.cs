using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Abp.Domain.Uow;
using Abp.Domain.Repositories;
using Abp.Runtime.Session;
using Moq;
using Abp.ObjectMapping;
using Shouldly;
using Xunit;
using AqualLifeStyle.Application.Enquiries;
using AqualLifeStyle.Application.Enquiries.Dto;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enquiries;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Email;

namespace AqualLifeStyle.Tests
{
    public class EnquiryAppServiceTests
    {
        private readonly Mock<IEnquiryRepository> _enquiryRepositoryMock;
        private readonly Mock<IObjectMapper> _objectMapperMock;
        private readonly Mock<ICustomerRepository> _customerRepositoryMock;
        private readonly EnquiryAppService _service;
        private readonly Mock<ITransactionalEmailOutbox> _emailOutboxMock;

        public EnquiryAppServiceTests()
        {
            _enquiryRepositoryMock = new Mock<IEnquiryRepository>();
            _objectMapperMock = new Mock<IObjectMapper>();
            _customerRepositoryMock = new Mock<ICustomerRepository>();
            _emailOutboxMock = new Mock<ITransactionalEmailOutbox>();
            _service = new TestableEnquiryAppService(_enquiryRepositoryMock.Object,
                _customerRepositoryMock.Object,
                _objectMapperMock.Object,
                _emailOutboxMock.Object)
            {
                AbpSession = Mock.Of<IAbpSession>(s => s.TenantId == 1 && s.UserId == 1)
            };

            _customerRepositoryMock
                .Setup(r => r.GetAsync(It.IsAny<int>()))
                .ReturnsAsync((int id) => { var c = Customer.Create(1, 1, "Test Customer", new EmailAddress("test@example.com")); c.Id = id; return c; });
        }

        private void SetupEnquiry(Enquiry enquiry)
        {
            if (enquiry.Id == 0)
            {
                enquiry.Id = 1;
            }

            _enquiryRepositoryMock
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Enquiry, bool>>>()))
                .ReturnsAsync(enquiry);
        }

        [Fact]
        public async Task GetMineAsync_FiltersEnquiriesByAreaAndAuthenticatedCustomer()
        {
            var customer = Customer.Create(1, 1, "Test Customer", new EmailAddress("test@example.com"));
            customer.Id = 7;
            Expression<Func<Enquiry, bool>> appliedFilter = null;
            _customerRepositoryMock
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Customer, bool>>>() ))
                .ReturnsAsync(customer);
            _enquiryRepositoryMock
                .Setup(r => r.GetAllListAsync(It.IsAny<Expression<Func<Enquiry, bool>>>() ))
                .Callback((Expression<Func<Enquiry, bool>> filter) => appliedFilter = filter)
                .ReturnsAsync(new System.Collections.Generic.List<Enquiry>());
            _objectMapperMock
                .Setup(m => m.Map<System.Collections.Generic.List<EnquiryDto>>(
                    It.IsAny<System.Collections.Generic.List<Enquiry>>()))
                .Returns(new System.Collections.Generic.List<EnquiryDto>());

            await _service.GetMineAsync();

            appliedFilter.ShouldNotBeNull();
            var predicate = appliedFilter.Compile();
            predicate(Enquiry.Create(1, 7, 5, "Mine")).ShouldBeTrue();
            predicate(Enquiry.Create(1, 8, 5, "Another customer")).ShouldBeFalse();
            predicate(Enquiry.Create(2, 7, 5, "Another Area")).ShouldBeFalse();
        }

        [Fact]
        public async Task RespondAsync_WithValidResponse_UpdatesEnquiryStatus()
        {
            // Arrange
            var enquiry = Enquiry.Create(tenantId: 1, customerId: 1, productId: 5, message: "Question about product");
            SetupEnquiry(enquiry);

            // Act
            var result = await _service.RespondAsync(1, new RespondToEnquiryDto { Response = "Here is the answer" });

            // Assert
            Assert.Equal(EnquiryStatus.Responded, enquiry.Status);
            Assert.Equal("Here is the answer", enquiry.Response);
            _enquiryRepositoryMock.Verify(r => r.UpdateAsync(enquiry), Times.Once);
            _emailOutboxMock.Verify(outbox => outbox.EnqueueAsync(
                1, "EnquiryResponse", "enquiry-response:1:1", It.Is<TransactionalEmail>(email =>
                    email.HtmlBody.Contains("Here is the answer"))), Times.Once);
        }

        [Fact]
        public async Task RespondAsync_AfterReopen_UsesANewNotificationKey()
        {
            var enquiry = Enquiry.Create(1, 1, 5, "Question about product");
            SetupEnquiry(enquiry);

            await _service.RespondAsync(1, new RespondToEnquiryDto { Response = "First answer" });
            enquiry.Close();
            enquiry.Reopen();
            await _service.RespondAsync(1, new RespondToEnquiryDto { Response = "Updated answer" });

            enquiry.ResponseVersion.ShouldBe(4);
            _emailOutboxMock.Verify(outbox => outbox.EnqueueAsync(
                1, "EnquiryResponse", "enquiry-response:1:1", It.IsAny<TransactionalEmail>()), Times.Once);
            _emailOutboxMock.Verify(outbox => outbox.EnqueueAsync(
                1, "EnquiryResponse", "enquiry-response:1:4", It.IsAny<TransactionalEmail>()), Times.Once);
        }

        [Fact]
        public async Task CloseAsync_WithPendingEnquiry_ClosesSuccessfully()
        {
            // Arrange
            var enquiry = Enquiry.Create(tenantId: 1, customerId: 1, productId: 5, message: "Question");
            SetupEnquiry(enquiry);

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
            var enquiry = Enquiry.Create(tenantId: 1, customerId: 1, productId: 5, message: "Question");
            enquiry.Close();
            SetupEnquiry(enquiry);

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
            var enquiry = Enquiry.Create(tenantId: 1, customerId: 1, productId: 5, message: "Question");
            SetupEnquiry(enquiry);

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
            var enquiry = Enquiry.Create(tenantId: 1, customerId: 1, productId: 5, message: "Question");
            SetupEnquiry(enquiry);

            // Act & Assert
            await Assert.ThrowsAsync<AqualLifeStyle.Application.Exceptions.AqualLifeStyleValidationException>(() =>
                _service.AssignToMemberAsync(5, new AssignEnquiryDto { MemberId = 0 }));
        }

        [Fact]
        public async Task ConvertToCustomerAsync_WithPendingEnquiry_ConvertsSuccessfully()
        {
            // Arrange
            var enquiry = Enquiry.Create(tenantId: 1, customerId: 1, productId: 5, message: "Question");
            enquiry.Id = 1;
            SetupEnquiry(enquiry);

            // Act
            var result = await _service.ConvertToCustomerAsync(6, new ConvertEnquiryToCustomerDto());

            // Assert
            Assert.True(enquiry.IsConverted);
            Assert.Equal(EnquiryStatus.Closed, enquiry.Status);
            Assert.NotNull(enquiry.ConvertedAt);
            _enquiryRepositoryMock.Verify(r => r.UpdateAsync(enquiry), Times.Once);
        }

        [Fact]
        public async Task ConvertToCustomerAsync_PreservesExistingReferralWhenNoNewReferralIsProvided()
        {
            // Arrange
            var enquiry = Enquiry.Create(tenantId: 1, customerId: 1, productId: 5, message: "Question");
            enquiry.Id = 1;
            enquiry.SetReferredByFacilitator(42);
            SetupEnquiry(enquiry);

            // Act
            await _service.ConvertToCustomerAsync(6, new ConvertEnquiryToCustomerDto());

            // Assert
            Assert.True(enquiry.IsConverted);
            Assert.Equal(42, enquiry.ReferredByFacilitatorId);
            _enquiryRepositoryMock.Verify(r => r.UpdateAsync(enquiry), Times.Once);
        }

        [Fact]
        public async Task ConvertToCustomerAsync_AddsEnquiryConvertedEventToDomainEvents()
        {
            // Arrange
            var enquiry = Enquiry.Create(tenantId: 1, customerId: 11, productId: 5, message: "Question");
            enquiry.Id = 7;
            SetupEnquiry(enquiry);

            // Act
            await _service.ConvertToCustomerAsync(7, new ConvertEnquiryToCustomerDto());

            // Assert
            var domainEvent = enquiry.DomainEvents.OfType<EnquiryConvertedEvent>().SingleOrDefault();
            domainEvent.ShouldNotBeNull();
            domainEvent.EnquiryId.ShouldBe(7);
            domainEvent.CustomerId.ShouldBe(11);
            domainEvent.ProductId.ShouldBe(5);
            domainEvent.TenantId.ShouldBe(1);
        }

        [Fact]
        public async Task ConvertToCustomerAsync_WithoutAmbientUnitOfWork_StillQueuesDeferredConversionEvent()
        {
            // Arrange: this unit test does not provide an ambient ABP UoW, so conversion must rely
            // on the aggregate's domain event rather than CurrentUnitOfWork callbacks.
            var enquiry = Enquiry.Create(tenantId: 1, customerId: 12, productId: 6, message: "Question");
            enquiry.Id = 8;
            enquiry.SetReferredByFacilitator(42);
            SetupEnquiry(enquiry);

            // Act
            await _service.ConvertToCustomerAsync(8, new ConvertEnquiryToCustomerDto());

            // Assert
            var domainEvent = enquiry.DomainEvents.OfType<EnquiryConvertedEvent>().SingleOrDefault();
            domainEvent.ShouldNotBeNull();
            domainEvent.ReferredByFacilitatorId.ShouldBe(42);
            domainEvent.ConvertedAt.ShouldBe(enquiry.ConvertedAt!.Value);
            _enquiryRepositoryMock.Verify(r => r.UpdateAsync(enquiry), Times.Once);
        }

        [Fact]
        public async Task ConvertToCustomerAsync_WithAlreadyConvertedEnquiry_Throws()
        {
            // Arrange
            var enquiry = Enquiry.Create(tenantId: 1, customerId: 1, productId: 5, message: "Question");
            enquiry.Id = 1;
            enquiry.ConvertToCustomer(null);
            SetupEnquiry(enquiry);

            // Act & Assert
            await Assert.ThrowsAsync<AqualLifeStyle.Application.Exceptions.AqualLifeStyleBusinessRuleException>(() =>
                _service.ConvertToCustomerAsync(7, new ConvertEnquiryToCustomerDto()));
        }

        [Fact]
        public async Task ClearAssignmentAsync_WithAssignedEnquiry_ClearsSuccessfully()
        {
            // Arrange
            var enquiry = Enquiry.Create(tenantId: 1, customerId: 1, productId: 5, message: "Question");
            enquiry.AssignToMember(10);
            SetupEnquiry(enquiry);

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
            var enquiry = Enquiry.Create(tenantId: 1, customerId: 1, productId: 5, message: "Question");
            enquiry.Id = 1;
            enquiry.AssignToMember(10);
            enquiry.ConvertToCustomer(null);
            SetupEnquiry(enquiry);

            // Act & Assert
            await Assert.ThrowsAsync<AqualLifeStyle.Application.Exceptions.AqualLifeStyleBusinessRuleException>(() =>
                _service.ClearAssignmentAsync(9, new ClearAssignmentDto()));
        }

        [Fact]
        public async Task AssignToMemberAsync_AfterConversion_Throws()
        {
            // Arrange
            var enquiry = Enquiry.Create(tenantId: 1, customerId: 1, productId: 5, message: "Question");
            enquiry.Id = 1;
            enquiry.ConvertToCustomer(null);
            SetupEnquiry(enquiry);

            // Act & Assert
            await Assert.ThrowsAsync<AqualLifeStyle.Application.Exceptions.AqualLifeStyleBusinessRuleException>(() =>
                _service.AssignToMemberAsync(10, new AssignEnquiryDto { MemberId = 20 }));
        }

        private sealed class TestableEnquiryAppService : EnquiryAppService
        {
            public TestableEnquiryAppService(
                IEnquiryRepository enquiryRepository,
                ICustomerRepository customerRepository,
                IObjectMapper objectMapper,
                ITransactionalEmailOutbox emailOutbox)
                : base(enquiryRepository, customerRepository, objectMapper, emailOutbox, new TransactionalEmailTemplateBuilder())
            {
            }
        }
    }
}
