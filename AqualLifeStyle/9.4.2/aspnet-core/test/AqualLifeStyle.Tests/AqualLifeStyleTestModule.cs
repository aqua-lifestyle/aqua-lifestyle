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

namespace AqualLifeStyle.Tests
{
    [DependsOn(
        typeof(AqualLifeStyleApplicationModule),
        typeof(AqualLifeStyleEntityFrameworkModule),
        typeof(AbpTestBaseModule)
        )]
    public class AqualLifeStyleTestModule : AbpModule
    {
        public AqualLifeStyleTestModule(AqualLifeStyleEntityFrameworkModule abpProjectNameEntityFrameworkModule)
        {
            abpProjectNameEntityFrameworkModule.SkipDbContextRegistration = true;
            abpProjectNameEntityFrameworkModule.SkipDbSeed = true;
        }

        public override void PreInitialize()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new[]
                {
                    new System.Collections.Generic.KeyValuePair<string, string>(
                        "App:ClientRootAddress",
                        "https://customers.example.test")
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
        }

        public override void Initialize()
        {
            ServiceCollectionRegistrar.Register(IocManager);

            // The default event bus resolves handlers from the global IocManager; register the
            // network handlers explicitly against the test's LocalIocManager so conversion/approval
            // side-effects fire within the integrated test's unit of work.
            Abp.Events.Bus.EventBus.Default.Register<AqualLifeStyle.Domain.Enquiries.EnquiryConvertedEvent>(e =>
                IocManager.Resolve<AqualLifeStyle.Application.Enquiries.EnquiryConvertedEventHandler>().HandleEventAsync(e).GetAwaiter().GetResult());
            Abp.Events.Bus.EventBus.Default.Register<AqualLifeStyle.Domain.AreaLeaders.AreaSpaceApprovedEvent>(e =>
                IocManager.Resolve<AqualLifeStyle.Application.AreaLeaders.AreaSpaceApprovedEventHandler>().HandleEventAsync(e).GetAwaiter().GetResult());
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
