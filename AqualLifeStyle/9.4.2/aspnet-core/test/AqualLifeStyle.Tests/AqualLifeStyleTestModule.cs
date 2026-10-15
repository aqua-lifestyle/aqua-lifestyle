using System;
using Castle.MicroKernel.Registration;
using NSubstitute;
using Abp.AutoMapper;
using Abp.Dependency;
using Abp.Modules;
using Abp.Configuration.Startup;
using Abp.Net.Mail;
using Abp.TestBase;
using Abp.Zero.Configuration;
using Abp.Zero.EntityFrameworkCore;
using AqualLifeStyle.EntityFrameworkCore;
using AqualLifeStyle.Tests.DependencyInjection;
using Microsoft.Extensions.Configuration;
using AqualLifeStyle.Payments.Yoco;
using AqualLifeStyle.Tests.Payments;
using AqualLifeStyle.Email;

namespace AqualLifeStyle.Tests
{
    [DependsOn(
        typeof(AqualLifeStyleApplicationModule),
        typeof(AqualLifeStyleEntityFrameworkModule),
        typeof(AbpTestBaseModule)
        )]
    public class AqualLifeStyleTestModule : AbpModule
    {
        private IDisposable _enquiryConvertedRegistration;
        private IDisposable _areaSpaceApprovedRegistration;

        public AqualLifeStyleTestModule(AqualLifeStyleEntityFrameworkModule abpProjectNameEntityFrameworkModule)
        {
            abpProjectNameEntityFrameworkModule.SkipDbContextRegistration = true;
            abpProjectNameEntityFrameworkModule.SkipDbSeed = true;
        }

        public override void PreInitialize()
        {
            AdministratorBootstrapTestEnvironment.Configure();

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new[]
                {
                    new System.Collections.Generic.KeyValuePair<string, string>(
                        "App:ClientRootAddress",
                        "https://customers.example.test"),
                    new System.Collections.Generic.KeyValuePair<string, string>(
                        "App:DefaultTenantName",
                        "Default"),
                    new System.Collections.Generic.KeyValuePair<string, string>(
                        "Yoco:Mode",
                        "test"),
                    // Synthetic financial fixtures explicitly exercise commissioned operations.
                    new System.Collections.Generic.KeyValuePair<string, string>(
                        "App:FinancialOperations:Enabled", "true"),
                    new System.Collections.Generic.KeyValuePair<string, string>(
                        "Bird:DeliveryNotBeforeUtc", "1970-01-01T00:00:00Z")
                })
                .Build();
            IocManager.IocContainer.Register(Component.For<IConfiguration>().Instance(configuration));

            Configuration.UnitOfWork.Timeout = TimeSpan.FromMinutes(30);
            Configuration.UnitOfWork.IsTransactional = false;

            // Disable static mapper usage since it breaks unit tests (see https://github.com/aspnetboilerplate/aspnetboilerplate/issues/2052)
            Configuration.Modules.AbpAutoMapper().UseStaticMapper = false;

            Configuration.BackgroundJobs.IsJobExecutionEnabled = false;

            // Use database for language management
            Configuration.Modules.Zero().LanguageManagement.EnableDbLocalization();

            RegisterFakeService<AbpZeroDbMigrator<AqualLifeStyleDbContext>>();

            Configuration.ReplaceService<IEmailSender, NullEmailSender>(DependencyLifeStyle.Transient);
            RegisterFakeService<ITransactionalEmailDeliveryGateway>();
            IocManager.IocContainer.Register(
                Component.For<IYocoCheckoutGateway>()
                    .ImplementedBy<FakeYocoCheckoutGateway>()
                    .LifestyleSingleton());
        }

        public override void Initialize()
        {
            ServiceCollectionRegistrar.Register(IocManager);

            // The default event bus resolves handlers from the global IocManager; register the
            // network handlers explicitly against the test's LocalIocManager so conversion/approval
            // side-effects fire within the integrated test's unit of work.
            _enquiryConvertedRegistration = Abp.Events.Bus.EventBus.Default.Register<AqualLifeStyle.Domain.Enquiries.EnquiryConvertedEvent>(e =>
                IocManager.Resolve<AqualLifeStyle.Application.Enquiries.EnquiryConvertedEventHandler>().HandleEventAsync(e).GetAwaiter().GetResult());
            _areaSpaceApprovedRegistration = Abp.Events.Bus.EventBus.Default.Register<AqualLifeStyle.Domain.AreaLeaders.AreaSpaceApprovedEvent>(e =>
                IocManager.Resolve<AqualLifeStyle.Application.AreaLeaders.AreaSpaceApprovedEventHandler>().HandleEventAsync(e).GetAwaiter().GetResult());
        }

        public override void Shutdown()
        {
            // Global callbacks must not retain a disposed test's IoC scope and EF caches.
            _enquiryConvertedRegistration?.Dispose();
            _areaSpaceApprovedRegistration?.Dispose();
            _enquiryConvertedRegistration = null;
            _areaSpaceApprovedRegistration = null;
        }

        private void RegisterFakeService<TService>() where TService : class
        {
            IocManager.IocContainer.Register(
                Component.For<TService>()
                    .UsingFactoryMethod(() => Substitute.For<TService>())
                    .LifestyleSingleton()
            );
        }
    }
}
