using Abp.Dependency;
using Abp.TestBase;
using AqualLifeStyle.Application.ProgrammeParticipations;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Web.Host.Commissions;
using Castle.MicroKernel.Registration;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Commissions
{
    public sealed class AQGreenV2WeeklyCommissionReadinessRegistrationTests
        : AbpIntegratedTestBase<AqualLifeStyleTestModule>
    {
        [Fact]
        public void Application_convention_registers_weekly_commission_readiness_and_worker_graph_resolves()
        {
            LocalIocManager.IocContainer.Register(
                Component.For<IAQGreenD10CommissioningSource>()
                    .Instance(UncommissionedAQGreenD10Source.Instance)
                    .Named("weekly-commission-readiness-test")
                    .IsDefault());

            var readiness = LocalIocManager.Resolve<IAQGreenV2WeeklyCommissionReadiness>();
            readiness.ShouldBeOfType<AQGreenV2WeeklyCommissionReadiness>();

            LocalIocManager.IocContainer.Register(
                Component.For<AQGreenV2WeeklyCommissionCalculationWorker>()
                    .LifestyleSingleton());

            LocalIocManager.Resolve<AQGreenV2WeeklyCommissionCalculationWorker>().ShouldNotBeNull();
        }
    }
}
