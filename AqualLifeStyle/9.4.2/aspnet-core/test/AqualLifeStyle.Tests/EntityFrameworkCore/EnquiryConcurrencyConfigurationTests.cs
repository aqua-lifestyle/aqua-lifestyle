using System.Threading.Tasks;
using AqualLifeStyle.Domain.Enquiries;
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
    }
}
