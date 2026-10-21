using System;
using System.Collections.Generic;
using Abp.Domain.Repositories;
using Abp.MultiTenancy;
using AqualLifeStyle.Migrator;
using AqualLifeStyle.MultiTenancy;
using NSubstitute;
using Shouldly;
using Xunit;
using Abp.Runtime.Security;
using Abp.Domain.Uow;

namespace AqualLifeStyle.Tests.Migrator
{
    public class MigrationFailureReleaseTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void MigrationFailure_ReturnsFailureAndStopsLaterDatabases(bool hostFails)
        {
            // Match application startup before encrypting fixtures. Parallel ABP modules
            // configure this process-wide default; encryption must not use its bootstrap value.
            SimpleStringCipher.DefaultPassPhrase = AqualLifeStyleConsts.DefaultPassPhrase;
            var resolver = Substitute.For<IDbPerTenantConnectionStringResolver>();
            resolver.GetNameOrConnectionString(Arg.Any<ConnectionStringResolveArgs>())
                .Returns("Host=localhost;Database=synthetic;Username=synthetic;Password=synthetic");
            var tenants = Substitute.For<IRepository<Tenant>>();
            tenants.GetAllList(Arg.Any<System.Linq.Expressions.Expression<Func<Tenant, bool>>>())
                .Returns(new List<Tenant> { new("one", "one") { ConnectionString = SimpleStringCipher.Instance.Encrypt("Host=localhost;Database=one;Password=synthetic") },
                    new("two", "two") { ConnectionString = SimpleStringCipher.Instance.Encrypt("Host=localhost;Database=two;Password=synthetic") } });
            var runner = new FailingRunner(tenants, resolver, hostFails);
            runner.Run(true).ShouldBeFalse();
            runner.HostCalls.ShouldBe(1); runner.TenantCalls.ShouldBe(hostFails ? 0 : 1);
        }

        private sealed class FailingRunner : MultiTenantMigrateExecuter
        {
            private readonly bool _hostFails;
            public int HostCalls { get; private set; }
            public int TenantCalls { get; private set; }
            public FailingRunner(IRepository<Tenant> tenants, IDbPerTenantConnectionStringResolver resolver, bool hostFails)
                : base(null, tenants, new Log(), resolver) => _hostFails = hostFails;
            protected override void MigrateHost()
            {
                HostCalls++;
                // Reproduce a parallel ABP fixture reapplying application startup configuration
                // between tenant-fixture encryption and the migrator's real decryption.
                SimpleStringCipher.DefaultPassPhrase = AqualLifeStyleConsts.DefaultPassPhrase;
                if (_hostFails) throw new InvalidOperationException("Synthetic migration failure");
            }
            protected override void MigrateTenant(Tenant tenant)
            { TenantCalls++; throw new InvalidOperationException("Synthetic migration failure"); }
        }
    }
}
