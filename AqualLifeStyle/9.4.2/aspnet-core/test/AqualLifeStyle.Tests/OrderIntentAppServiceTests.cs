using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AqualLifeStyle.Application.Exceptions;
using AqualLifeStyle.Application.Orders;
using AqualLifeStyle.Application.Orders.Dto;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enquiries;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using AqualLifeStyle.Domain.Orders;
using AqualLifeStyle.Domain.Products;
using Moq;
using Abp.ObjectMapping;
using Abp.Runtime.Session;
using Xunit;

namespace AqualLifeStyle.Tests
{
    public class OrderIntentAppServiceTests
    {
        private readonly Mock<IOrderIntentRepository> _orderIntentRepositoryMock;
        private readonly Mock<IEnquiryRepository> _enquiryRepositoryMock;
        private readonly Mock<ICustomerRepository> _customerRepositoryMock;
        private readonly Mock<IProductRepository> _productRepositoryMock;
        private readonly Mock<IMembershipRepository> _membershipRepositoryMock;
        private readonly Mock<IObjectMapper> _objectMapperMock;
        private readonly OrderIntentAppService _service;

        public OrderIntentAppServiceTests()
        {
            _orderIntentRepositoryMock = new Mock<IOrderIntentRepository>();
            _enquiryRepositoryMock = new Mock<IEnquiryRepository>();
            _customerRepositoryMock = new Mock<ICustomerRepository>();
            _productRepositoryMock = new Mock<IProductRepository>();
            _membershipRepositoryMock = new Mock<IMembershipRepository>();
            _objectMapperMock = new Mock<IObjectMapper>();
            _objectMapperMock
                .Setup(m => m.Map<OrderIntentDto>(It.IsAny<OrderIntent>()))
                .Returns((OrderIntent oi) => new OrderIntentDto
                {
                    Id = oi.Id,
                    CustomerId = oi.CustomerId,
                    ProductId = oi.ProductId,
                    EnquiryId = oi.EnquiryId,
                    UnitPrice = oi.UnitPrice,
                    ReservedPrice = oi.ReservedPrice,
                    Status = (int)oi.Status,
                    StatusText = oi.Status.ToString(),
                    CreatedAt = oi.CreatedAt,
                    ReservedAt = oi.ReservedAt,
                    CancelledAt = oi.CancelledAt,
                    CompletedAt = oi.CompletedAt
                });

            _service = new DeterministicOrderIntentAppService(
                _orderIntentRepositoryMock.Object,
                _enquiryRepositoryMock.Object,
                _customerRepositoryMock.Object,
                _productRepositoryMock.Object,
                _membershipRepositoryMock.Object,
                _objectMapperMock.Object)
            {
                AbpSession = Mock.Of<IAbpSession>(s => s.TenantId == 1 && s.UserId == 43)
            };
        }

        [Fact]
        public async Task GetMineAsync_FiltersOrdersByTheAuthenticatedCustomer()
        {
            var customer = CreateCustomer(membershipId: 1);
            Expression<System.Func<OrderIntent, bool>> appliedFilter = null;
            _customerRepositoryMock
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<System.Func<Customer, bool>>>() ))
                .ReturnsAsync(customer);
            _orderIntentRepositoryMock
                .Setup(r => r.GetAllListAsync(It.IsAny<Expression<System.Func<OrderIntent, bool>>>() ))
                .Callback((Expression<System.Func<OrderIntent, bool>> filter) => appliedFilter = filter)
                .ReturnsAsync(new List<OrderIntent>());
            _objectMapperMock
                .Setup(m => m.Map<List<OrderIntentDto>>(It.IsAny<List<OrderIntent>>()))
                .Returns(new List<OrderIntentDto>());

            await _service.GetMineAsync();

            Assert.NotNull(appliedFilter);
            var predicate = appliedFilter.Compile();
            Assert.True(predicate(OrderIntent.CreateReserved(1, 2, null, 100m, 90m, System.DateTime.UtcNow)));
            Assert.False(predicate(OrderIntent.CreateReserved(2, 2, null, 100m, 90m, System.DateTime.UtcNow)));
        }

        [Fact]
        public async Task CreateForCurrentCustomerAsync_UsesAuthenticatedCustomerAndTierPricing()
        {
            var customer = CreateCustomer(membershipId: 1);
            var product = Product.Create("Jasper Bundle", 100m, membershipId: 1);
            product.Id = 2;
            var membership = Membership.Create(1, "Jasper", "Entry tier", MembershipType.Jasper);
            membership.Id = 1;

            _customerRepositoryMock
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<System.Func<Customer, bool>>>() ))
                .ReturnsAsync(customer);
            _productRepositoryMock.Setup(r => r.GetAsync(2)).ReturnsAsync(product);
            _membershipRepositoryMock.Setup(r => r.GetAsync(1)).ReturnsAsync(membership);
            _orderIntentRepositoryMock.Setup(r => r.CountOpenForCustomerAsync(1)).ReturnsAsync(0);
            _orderIntentRepositoryMock.Setup(r => r.InsertAndGetIdAsync(It.IsAny<OrderIntent>()))
                .ReturnsAsync(12);

            var result = await _service.CreateForCurrentCustomerAsync(2);

            Assert.Equal(1, result.CustomerId);
            Assert.Equal(2, result.ProductId);
            Assert.Null(result.EnquiryId);
            Assert.Equal(100m, result.UnitPrice);
            Assert.Equal(95m, result.ReservedPrice);
            Assert.Equal((int)OrderIntentStatus.Reserved, result.Status);
        }

        [Fact]
        public async Task CreateForCurrentCustomerAsync_WhenNoProfileIsLinked_ThrowsWithoutCreatingOrder()
        {
            _customerRepositoryMock
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<System.Func<Customer, bool>>>() ))
                .ReturnsAsync(default(Customer)!);

            await Assert.ThrowsAsync<Abp.UI.UserFriendlyException>(() =>
                _service.CreateForCurrentCustomerAsync(2));

            _orderIntentRepositoryMock.Verify(
                r => r.InsertAndGetIdAsync(It.IsAny<OrderIntent>()),
                Times.Never);
        }

        [Fact]
        public async Task CreateForCurrentCustomerAsync_WithoutMembership_ThrowsWithoutCreatingOrder()
        {
            var customer = CreateCustomer(membershipId: null);
            var product = Product.Create("General Bundle", 100m, membershipId: null);
            product.Id = 2;

            _customerRepositoryMock
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<System.Func<Customer, bool>>>() ))
                .ReturnsAsync(customer);
            _productRepositoryMock.Setup(r => r.GetAsync(2)).ReturnsAsync(product);

            await Assert.ThrowsAsync<AqualLifeStyleBusinessRuleException>(() =>
                _service.CreateForCurrentCustomerAsync(2));

            _orderIntentRepositoryMock.Verify(
                r => r.InsertAndGetIdAsync(It.IsAny<OrderIntent>()),
                Times.Never);
        }

        [Fact]
        public async Task CreateFromEnquiryAsync_WithConvertedEnquiry_CreatesReservedOrderIntent()
        {
            var enquiry = CreateConvertedEnquiry();
            var customer = CreateCustomer(membershipId: 1);
            var product = Product.Create("Jasper Bundle", 100m, membershipId: 1);
            product.Id = 2;
            var membership = Membership.Create(1, "Jasper", "Entry tier", MembershipType.Jasper);
            membership.Id = 1;

            _enquiryRepositoryMock.Setup(r => r.GetAsync(3)).ReturnsAsync(enquiry);
            _orderIntentRepositoryMock.Setup(r => r.GetByEnquiryIdAsync(3)).ReturnsAsync(default(OrderIntent)!);
            _orderIntentRepositoryMock.Setup(r => r.CountOpenForCustomerAsync(1)).ReturnsAsync(0);
            _orderIntentRepositoryMock.Setup(r => r.InsertAndGetIdAsync(It.IsAny<OrderIntent>()))
                .ReturnsAsync(11);
            _customerRepositoryMock.Setup(r => r.GetAsync(1)).ReturnsAsync(customer);
            _productRepositoryMock.Setup(r => r.GetAsync(2)).ReturnsAsync(product);
            _membershipRepositoryMock.Setup(r => r.GetAsync(1)).ReturnsAsync(membership);

            var result = await _service.CreateFromEnquiryAsync(3);

            Assert.Equal(1, result.CustomerId);
            Assert.Equal(2, result.ProductId);
            Assert.Equal(3, result.EnquiryId);
            Assert.Equal(100m, result.UnitPrice);
            Assert.Equal(95m, result.ReservedPrice);
            Assert.Equal((int)OrderIntentStatus.Reserved, result.Status);
            _orderIntentRepositoryMock.Verify(r => r.InsertAndGetIdAsync(It.Is<OrderIntent>(orderIntent =>
                orderIntent.CustomerId == 1 &&
                orderIntent.ProductId == 2 &&
                orderIntent.EnquiryId == 3 &&
                orderIntent.Status == OrderIntentStatus.Reserved)), Times.Once);
        }

        [Fact]
        public async Task CreateFromEnquiryAsync_WithUnconvertedEnquiry_ThrowsBusinessRuleException()
        {
            var enquiry = Enquiry.Create(1, 1, 2, "Question about bundle");
            enquiry.Id = 3;
            _enquiryRepositoryMock.Setup(r => r.GetAsync(3)).ReturnsAsync(enquiry);

            await Assert.ThrowsAsync<AqualLifeStyleBusinessRuleException>(() => _service.CreateFromEnquiryAsync(3));
        }

        [Fact]
        public async Task CreateFromEnquiryAsync_WithExistingOrderIntent_ThrowsDuplicateException()
        {
            var enquiry = CreateConvertedEnquiry();
            var existingOrderIntent = OrderIntent.CreateReserved(1, 2, 3, 100m, 95m, System.DateTime.UtcNow);
            _enquiryRepositoryMock.Setup(r => r.GetAsync(3)).ReturnsAsync(enquiry);
            _orderIntentRepositoryMock.Setup(r => r.GetByEnquiryIdAsync(3)).ReturnsAsync(existingOrderIntent);

            await Assert.ThrowsAsync<AqualLifeStyleDuplicateException>(() => _service.CreateFromEnquiryAsync(3));
        }

        [Fact]
        public async Task CreateFromEnquiryAsync_WhenTierOrderLimitReached_ThrowsBusinessRuleException()
        {
            var enquiry = CreateConvertedEnquiry();
            var customer = CreateCustomer(membershipId: 1);
            var product = Product.Create("Jasper Bundle", 100m, membershipId: 1);
            product.Id = 2;
            var membership = Membership.Create(1, "Jasper", "Entry tier", MembershipType.Jasper);
            membership.Id = 1;

            _enquiryRepositoryMock.Setup(r => r.GetAsync(3)).ReturnsAsync(enquiry);
            _orderIntentRepositoryMock.Setup(r => r.GetByEnquiryIdAsync(3)).ReturnsAsync(default(OrderIntent)!);
            _orderIntentRepositoryMock.Setup(r => r.CountOpenForCustomerAsync(1)).ReturnsAsync(1);
            _customerRepositoryMock.Setup(r => r.GetAsync(1)).ReturnsAsync(customer);
            _productRepositoryMock.Setup(r => r.GetAsync(2)).ReturnsAsync(product);
            _membershipRepositoryMock.Setup(r => r.GetAsync(1)).ReturnsAsync(membership);

            await Assert.ThrowsAsync<AqualLifeStyleBusinessRuleException>(() => _service.CreateFromEnquiryAsync(3));
        }

        [Fact]
        public async Task CancelAsync_WithOperationalPermission_DoesNotRequireCustomerOwnership()
        {
            var orderIntent = OrderIntent.CreateReserved(1, 2, 3, 100m, 95m, System.DateTime.UtcNow);
            var customer = CreateCustomer(membershipId: 1, userId: 99);
            _orderIntentRepositoryMock.Setup(r => r.GetAsync(10)).ReturnsAsync(orderIntent);
            _customerRepositoryMock.Setup(r => r.FirstOrDefaultAsync(1)).ReturnsAsync(customer);

            var abpSessionMock = new Mock<IAbpSession>();
            abpSessionMock.Setup(s => s.TenantId).Returns(1);
            abpSessionMock.Setup(s => s.UserId).Returns((long?)43);
            _service.AbpSession = abpSessionMock.Object;

            var result = await _service.CancelAsync(10);

            Assert.Equal((int)OrderIntentStatus.Cancelled, result.Status);
            _orderIntentRepositoryMock.Verify(r => r.UpdateAsync(orderIntent), Times.Once);
        }

        [Fact]
        public async Task CompleteAsync_WithOperationalPermission_DoesNotRequireCustomerOwnership()
        {
            var orderIntent = OrderIntent.CreateReserved(1, 2, 3, 100m, 95m, System.DateTime.UtcNow);
            var customer = CreateCustomer(membershipId: 1, userId: 99);
            _orderIntentRepositoryMock.Setup(r => r.GetAsync(10)).ReturnsAsync(orderIntent);
            _customerRepositoryMock.Setup(r => r.FirstOrDefaultAsync(1)).ReturnsAsync(customer);

            var result = await _service.CompleteAsync(10);

            Assert.Equal((int)OrderIntentStatus.Completed, result.Status);
            _orderIntentRepositoryMock.Verify(r => r.UpdateAsync(orderIntent), Times.Once);
        }

        [Fact]
        public async Task CancelAsync_WhenOrderBelongsToAnotherArea_RejectsWithoutMutation()
        {
            var orderIntent = OrderIntent.CreateReserved(1, 2, 3, 100m, 95m, System.DateTime.UtcNow);
            var customer = CreateCustomer(membershipId: 1, userId: 99, tenantId: 2);
            _orderIntentRepositoryMock.Setup(r => r.GetAsync(10)).ReturnsAsync(orderIntent);
            _customerRepositoryMock.Setup(r => r.FirstOrDefaultAsync(1)).ReturnsAsync(customer);

            await Assert.ThrowsAsync<AqualLifeStyleNotFoundException>(() => _service.CancelAsync(10));

            Assert.Equal(OrderIntentStatus.Reserved, orderIntent.Status);
            _orderIntentRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<OrderIntent>()), Times.Never);
        }

        private static Enquiry CreateConvertedEnquiry()
        {
            var enquiry = Enquiry.Create(1, 1, 2, "Question about bundle");
            enquiry.Id = 3;
            enquiry.ConvertToCustomer(null);
            return enquiry;
        }

        private static Customer CreateCustomer(int? membershipId, long userId = 43, int tenantId = 1)
        {
            var customer = Customer.Create(tenantId, userId, "Jane Doe", new EmailAddress("jane@example.com"), membershipId);
            customer.Id = 1;
            return customer;
        }

        private sealed class DeterministicOrderIntentAppService : OrderIntentAppService
        {
            public DeterministicOrderIntentAppService(
                IOrderIntentRepository orderIntentRepository,
                IEnquiryRepository enquiryRepository,
                ICustomerRepository customerRepository,
                IProductRepository productRepository,
                IMembershipRepository membershipRepository,
                IObjectMapper objectMapper)
                : base(orderIntentRepository, enquiryRepository, customerRepository, productRepository, membershipRepository, objectMapper)
            {
            }

            protected override System.DateTime UtcNow => new System.DateTime(2026, 7, 10, 12, 0, 0, System.DateTimeKind.Utc);
        }
    }
}
