using System;
using System.Runtime.CompilerServices;
using Abp.TestBase;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests
{
    public class TestHostLifetimeTests
    {
        [Fact]
        public void DisposedTestHost_DoesNotRemainRootedByGlobalEventRegistrations()
        {
            var manager = CreateDisposedHost();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            manager.IsAlive.ShouldBeFalse(
                "A disposed test host must not retain its IoC scope through global event callbacks.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference CreateDisposedHost()
        {
            WeakReference manager;
            using (var host = new TestHost())
                manager = host.ManagerReference;

            // ABP's mapper retains its latest localization context. Initialize
            // another host so this assertion measures unbounded old-host retention,
            // rather than that framework-owned reference to the latest host.
            using var nextHost = new TestHost();
            return manager;
        }

        private sealed class TestHost : AbpIntegratedTestBase<AqualLifeStyleTestModule>
        {
            public WeakReference ManagerReference => new(LocalIocManager);
        }
    }
}
