using AqualLifeStyle.Domain.Enquiries;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Domain
{
    public class EnquiryReferralTests
    {
        [Fact]
        public void SetReferredByFacilitator_RecordsFacilitator()
        {
            var enquiry = Enquiry.Create(customerId: 1, productId: 2, "Interested");
            enquiry.SetReferredByFacilitator(42);
            enquiry.ReferredByFacilitatorId.ShouldBe(42);
        }

        [Fact]
        public void SetReferredByFacilitator_InvalidId_Throws()
        {
            var enquiry = Enquiry.Create(customerId: 1, productId: 2, "Interested");
            Should.Throw<System.ArgumentException>(() => enquiry.SetReferredByFacilitator(0));
        }

        [Fact]
        public void ConvertToCustomer_WithFacilitator_SetsReferralAndConverts()
        {
            var enquiry = Enquiry.Create(customerId: 1, productId: 2, "Interested");
            enquiry.SetReferredByFacilitator(42);

            enquiry.ConvertToCustomer();

            enquiry.IsConverted.ShouldBeTrue();
            enquiry.ReferredByFacilitatorId.ShouldBe(42);
            enquiry.ConvertedAt.ShouldNotBeNull();
        }

        [Fact]
        public void ConvertToCustomer_Default_KeepsNoReferral()
        {
            var enquiry = Enquiry.Create(customerId: 1, productId: 2, "Interested");
            enquiry.ConvertToCustomer();
            enquiry.ReferredByFacilitatorId.ShouldBeNull();
        }
    }
}
