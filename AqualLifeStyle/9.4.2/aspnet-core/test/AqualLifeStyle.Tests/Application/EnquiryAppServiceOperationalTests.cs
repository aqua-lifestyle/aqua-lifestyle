using System;
using System.Threading.Tasks;
using AqualLifeStyle.Application.Enquiries;
using AqualLifeStyle.Application.Enquiries.Dto;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enquiries;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Products;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class EnquiryAppServiceOperationalTests : AqualLifeStyleTestBase
    {
        private readonly IEnquiryAppService _enquiryAppService;

        public EnquiryAppServiceOperationalTests()
            => _enquiryAppService = Resolve<IEnquiryAppService>();

        [Fact]
        public async Task AreaAdministrator_CanManageAnotherCustomersEnquiry()
        {
            var enquiryId = await CreateCustomerEnquiryAsync();

            var response = await _enquiryAppService.RespondAsync(
                enquiryId,
                new RespondToEnquiryDto { Response = "Your club team has reviewed this enquiry." });
            response.Status.ShouldBe((int)EnquiryStatus.Responded);

            var closed = await _enquiryAppService.CloseAsync(enquiryId);
            closed.Status.ShouldBe((int)EnquiryStatus.Closed);

            var reopened = await _enquiryAppService.ReopenAsync(enquiryId);
            reopened.Status.ShouldBe((int)EnquiryStatus.Pending);
        }

        private async Task<int> CreateCustomerEnquiryAsync()
        {
            var userId = await CreateTestUserAsync(
                1,
                $"enquiry-user-{Guid.NewGuid():N}",
                $"enquiry-user-{Guid.NewGuid():N}@example.com");

            return await UsingDbContextAsync(1, async context =>
            {
                var product = Product.Create($"Enquiry test {Guid.NewGuid():N}", 100m);
                context.Products.Add(product);
                var customer = Customer.Create(
                    1,
                    userId,
                    "Operational enquiry customer",
                    new EmailAddress($"enquiry-{Guid.NewGuid():N}@example.com"));
                context.Customers.Add(customer);
                await context.SaveChangesAsync();

                var enquiry = Enquiry.Create(1, customer.Id, product.Id, "Please tell me more.");
                context.Enquiries.Add(enquiry);
                await context.SaveChangesAsync();
                return enquiry.Id;
            });
        }
    }
}
