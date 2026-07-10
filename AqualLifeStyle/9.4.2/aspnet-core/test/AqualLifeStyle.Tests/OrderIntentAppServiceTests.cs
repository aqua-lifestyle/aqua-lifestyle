using System.Collections.Generic;
using System.Threading.Tasks;
using AqualLifeStyle.Application.Exceptions;
using AqualLifeStyle.Application.Orders;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enquiries;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using AqualLifeStyle.Domain.Orders;
using AqualLifeStyle.Domain.Products;
using Moq;
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
        private readonly OrderIntentAppService _service;

        public OrderIntentAppServiceTests()
        {
            _orderIntentRepositoryMock = new Mock<IOrderIntentRepository>();
            _enquiryRepositoryMock = new Mock<IEnquiryRepository>();
            _customerRepositoryMock = new Mock<ICustomerRepository>();
            _productRepositoryMock = new Mock<IProductRepository>();
            _membershipRepositoryMock = new Mock<IMembershipRepository>();

            _service = new OrderIntentAppService(
                _orderIntentRepositoryMock.Object,
                _enquiryRepositoryMock.Object,
                _customerRepositoryMock.Object,
                _productRepositoryMock.Object,
                _membershipRepositoryMock.Object);
        }

        [Fact]
        public async Task CreateFromEnquiryAsync_WithConvertedEnquiry_CreatesReservedOrderIntent()
        {
            var enquiry = CreateConvertedEnquiry();
            var customer = CreateCustomer(membershipId: 1);
            var product = Product.Create("Jasper Bundle", 100m, membershipId: 1);
            product.Id = 2;
            var membership = Membership.Create("Jasper", "Entry tier", MembershipType.Jasper);
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
            var enquiry = Enquiry.Create(1, 2, "Question about bundle");
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
            var membership = Membership.Create("Jasper", "Entry tier", MembershipType.Jasper);
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
        public async Task CancelAsync_WithReservedOrderIntent_CancelsSuccessfully()
        {
            var orderIntent = OrderIntent.CreateReserved(1, 2, 3, 100m, 95m, System.DateTime.UtcNow);
            _orderIntentRepositoryMock.Setup(r => r.GetAsync(10)).ReturnsAsync(orderIntent);

            var result = await _service.CancelAsync(10);

            Assert.Equal((int)OrderIntentStatus.Cancelled, result.Status);
            _orderIntentRepositoryMock.Verify(r => r.UpdateAsync(orderIntent), Times.Once);
        }

        private static Enquiry CreateConvertedEnquiry()
        {
            var enquiry = Enquiry.Create(1, 2, "Question about bundle");
            enquiry.Id = 3;
            enquiry.ConvertToCustomer(null);
            return enquiry;
        }

        private static Customer CreateCustomer(int membershipId)
        {
            var customer = Customer.Create("Jane Doe", new EmailAddress("jane@example.com"), membershipId);
            customer.Id = 1;
            return customer;
        }
    }
}
