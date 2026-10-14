using System.Linq;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests
{
    public class SqliteTestHostIsolationTests
    {
        [Fact]
        public void TestHosts_ReuseEfMetadataWithoutSharingDatabaseState()
        {
            using var first = new TestHost();
            using var second = new TestHost();
            var secondTenantName = second.TenantName;

            first.SetTenantName("isolated-first-test-host");

            first.TenantName.ShouldBe("isolated-first-test-host");
            second.TenantName.ShouldBe(secondTenantName);
            first.Model.ShouldBeSameAs(second.Model);
        }

        private sealed class TestHost : AqualLifeStyleTestBase
        {
            public IModel Model => UsingDbContext(context => context.Model);
            public string TenantName => UsingDbContext(
                context => context.Tenants.Single(tenant => tenant.Id == 1).Name);

            public void SetTenantName(string name) => UsingDbContext(context =>
            {
                context.Tenants.Single(tenant => tenant.Id == 1).Name = name;
            });
        }
    }
}
