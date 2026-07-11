using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Abp.Domain.Uow;
using Abp.Domain.Repositories;
using Abp.Events.Bus;
using Abp.Runtime.Session;
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
            _service = new TestableEnquiryAppService(_enquiryRepositoryMock.Object, null)
            {
                // The service now scopes every enquiry to the current tenant, so unit tests
                // must supply a tenant context.
                AbpSession = Mock.Of<IAbpSession>(s => s.TenantId == 1)
            };
        }

        private void SetupEnquiry(Enquiry enquiry)
        {
            // The app service looks up the enquiry via FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId)
            // now that tenant isolation is enforced.
            _enquiryRepositoryMock
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Enquiry, bool>>>()))
                .ReturnsAsync(enquiry);
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
        public async Task ConvertToCustomerAsync_RegistersSingleCompletionCallback_AndTriggersEventOnCompletion()
        {
            // Arrange
            var enquiry = Enquiry.Create(tenantId: 1, customerId: 11, productId: 5, message: "Question");
            SetupEnquiry(enquiry);
            var eventBusMock = new Mock<IEventBus>();

            var unitOfWorkManagerMock = new Mock<IUnitOfWorkManager>();
            var activeUnitOfWorkMock = new Mock<IActiveUnitOfWork>();
            EventHandler completionHandlers = null;
            EventHandler removedHandler = null;
            var service = new TestableEnquiryAppService(_enquiryRepositoryMock.Object, eventBusMock.Object)
            {
                AbpSession = Mock.Of<IAbpSession>(s => s.TenantId == 1)
            };

            activeUnitOfWorkMock
                .SetupAdd(m => m.Completed += It.IsAny<EventHandler>())
                .Callback((EventHandler h) => completionHandlers += h);
            activeUnitOfWorkMock
                .SetupRemove(m => m.Completed -= It.IsAny<EventHandler>())
                .Callback((EventHandler h) =>
                {
                    removedHandler = h;
                    completionHandlers -= h;
                });
            unitOfWorkManagerMock
                .Setup(m => m.Current)
                .Returns(activeUnitOfWorkMock.Object);

            service.SetUnitOfWorkManager(unitOfWorkManagerMock.Object);

            // Act
            await service.ConvertToCustomerAsync(6, new ConvertEnquiryToCustomerDto());

            // Assert
            activeUnitOfWorkMock.VerifyAdd(m => m.Completed += It.IsAny<EventHandler>(), Times.Once);
            eventBusMock.Verify(b => b.Trigger(It.IsAny<EnquiryConvertedEvent>()), Times.Never);

            Assert.NotNull(completionHandlers);
            completionHandlers(activeUnitOfWorkMock.Object, EventArgs.Empty);

            activeUnitOfWorkMock.VerifyRemove(m => m.Completed -= It.IsAny<EventHandler>(), Times.Once);
            Assert.Null(completionHandlers);
            Assert.NotNull(removedHandler);
            eventBusMock.Verify(b => b.Trigger(It.Is<EnquiryConvertedEvent>(evt =>
                evt.EnquiryId == enquiry.Id &&
                evt.CustomerId == enquiry.CustomerId &&
                evt.ProductId == enquiry.ProductId &&
                evt.ReferredByFacilitatorId == enquiry.ReferredByFacilitatorId &&
                evt.TenantId == enquiry.TenantId)), Times.Once);

            eventBusMock.Verify(b => b.Trigger(It.IsAny<EnquiryConvertedEvent>()), Times.Once);
        }

        [Fact]
        public async Task ConvertToCustomerAsync_WithAlreadyConvertedEnquiry_Throws()
        {
            // Arrange
            var enquiry = Enquiry.Create(tenantId: 1, customerId: 1, productId: 5, message: "Question");
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
            enquiry.ConvertToCustomer(null);
            SetupEnquiry(enquiry);

            // Act & Assert
            await Assert.ThrowsAsync<AqualLifeStyle.Application.Exceptions.AqualLifeStyleBusinessRuleException>(() =>
                _service.AssignToMemberAsync(10, new AssignEnquiryDto { MemberId = 20 }));
        }

        private sealed class TestableEnquiryAppService : EnquiryAppService
        {
            public TestableEnquiryAppService(IEnquiryRepository enquiryRepository, IEventBus eventBus)
                : base(enquiryRepository, eventBus)
            {
            }

            public void SetUnitOfWorkManager(IUnitOfWorkManager unitOfWorkManager)
            {
                UnitOfWorkManager = unitOfWorkManager;
            }
        }
    }
}
