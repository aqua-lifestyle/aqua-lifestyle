using System;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace AqualLifeStyle.Web.Tests
{
    public class TestHostLifetimeTests
    {
        [Fact]
        public async Task Dispose_ReleasesClientAndServerAndIsIdempotent()
        {
            var host = new TestHost();
            var lifetime = Assert.IsAssignableFrom<IDisposable>(host);

            lifetime.Dispose();
            lifetime.Dispose();

            using var serverClient = host.CreateClient();
            await Assert.ThrowsAsync<ObjectDisposedException>(
                () => serverClient.GetAsync("/"));
            await Assert.ThrowsAsync<ObjectDisposedException>(
                () => host.Client.GetAsync("/"));
        }

        private sealed class TestHost : AqualLifeStyleWebTestBase
        {
            public HttpClient CreateClient() => Server.CreateClient();
        }
    }
}
