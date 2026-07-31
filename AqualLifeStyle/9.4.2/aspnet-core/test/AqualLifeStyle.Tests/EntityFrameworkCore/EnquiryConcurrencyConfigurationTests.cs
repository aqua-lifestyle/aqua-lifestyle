using System.Threading.Tasks;
using AqualLifeStyle.Domain.Enquiries;
using AqualLifeStyle.Domain.Enums;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.EntityFrameworkCore
{
    public class EnquiryConcurrencyConfigurationTests : AqualLifeStyleTestBase
    {
        [Fact]
        public async Task ResponseVersion_IsAnOptimisticConcurrencyToken()
        {
            await UsingDbContextAsync(context =>
            {
                var property = context.Model.FindEntityType(typeof(Enquiry))
                    .FindProperty(nameof(Enquiry.ResponseVersion));
                property.IsConcurrencyToken.ShouldBeTrue();
                return Task.CompletedTask;
            });
        }

        [Fact]
        public void ResponseVersion_AdvancesForEveryAggregateMutation()
        {
            var enquiry = Enquiry.Create(1, 1, 1, "Question");

            enquiry.AssignToMember(2);
            enquiry.ResponseVersion.ShouldBe(1);
            enquiry.ClearAssignment();
            enquiry.ResponseVersion.ShouldBe(2);
            enquiry.SetReferredByFacilitator(3);
            enquiry.ResponseVersion.ShouldBe(3);
            enquiry.Respond("Answer");
            enquiry.ResponseVersion.ShouldBe(4);
            enquiry.Close();
            enquiry.ResponseVersion.ShouldBe(5);
            enquiry.Reopen();
            enquiry.ResponseVersion.ShouldBe(6);
            enquiry.RecordFollowUp(2, "Followed up", EnquiryFollowUpOutcome.Interested);
            enquiry.ResponseVersion.ShouldBe(7);

            enquiry.Id = 1;
            enquiry.ConvertToCustomer();
            enquiry.ResponseVersion.ShouldBe(8);
        }
    }
}
