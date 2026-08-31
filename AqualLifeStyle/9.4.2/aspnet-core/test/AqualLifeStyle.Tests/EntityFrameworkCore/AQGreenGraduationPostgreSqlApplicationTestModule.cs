using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abp.AutoMapper;
using Abp.Authorization;
using Abp.Configuration.Startup;
using Abp.Dependency;
using Abp.EntityFrameworkCore.Configuration;
using Abp.Modules;
using Abp.Net.Mail;
using Abp.TestBase;
using Abp.Zero.Configuration;
using Abp.Zero.EntityFrameworkCore;
using AqualLifeStyle.Application.Admin.ProgrammeParticipations;
using AqualLifeStyle.Application.Admin.Commissions;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Email;
using AqualLifeStyle.EntityFrameworkCore;
using AqualLifeStyle.Identity;
using AqualLifeStyle.Payments.Yoco;
using AqualLifeStyle.Tests.Payments;
using AqualLifeStyle.Tests.Application;
using Castle.MicroKernel.Registration;
using Castle.Windsor.MsDependencyInjection;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace AqualLifeStyle.Tests.EntityFrameworkCore
{
    [DependsOn(
        typeof(AqualLifeStyleApplicationModule),
        typeof(AqualLifeStyleEntityFrameworkModule),
        typeof(AbpTestBaseModule))]
    public sealed class AQGreenGraduationPostgreSqlApplicationTestModule : AbpModule
    {
        private AQGreenPlacementAllocatorPostgreSqlFixture.DatabaseLease _database;
        internal static AQGreenPlacementAllocatorPostgreSqlFixture.DatabaseLease
            CurrentDatabase { get; private set; }

        public AQGreenGraduationPostgreSqlApplicationTestModule(
            AqualLifeStyleEntityFrameworkModule entityFrameworkModule)
        {
            entityFrameworkModule.SkipDbContextRegistration = true;
            entityFrameworkModule.SkipDbSeed = true;
        }

        public override void PreInitialize()
        {
            var server = AQGreenPlacementAllocatorPostgreSqlFixture.Current ??
                throw new InvalidOperationException(
                    "The PostgreSQL collection fixture must be initialized first.");
            _database = server.CreateDatabaseAsync().GetAwaiter().GetResult();
            CurrentDatabase = _database;
            AQGreenGraduationPostgreSqlFailureState.Shared.Reset(
                _database.ConnectionString("b52-application"));

            Configuration.DefaultNameOrConnectionString =
                AQGreenGraduationPostgreSqlFailureState.Shared.ConnectionString;
            Configuration.UnitOfWork.Timeout = TimeSpan.FromMinutes(5);
            Configuration.UnitOfWork.IsTransactional = false;
            Configuration.Authorization.IsEnabled = false;
            Configuration.BackgroundJobs.IsJobExecutionEnabled = false;
            Configuration.Modules.AbpAutoMapper().UseStaticMapper = false;
            Configuration.Modules.Zero().LanguageManagement.EnableDbLocalization();
            Configuration.Modules.AbpEfCore().AddDbContext<AqualLifeStyleDbContext>(options =>
            {
                options.DbContextOptions
                    .UseNpgsql(AQGreenGraduationPostgreSqlFailureState.Shared.ConnectionString)
                    .AddInterceptors(
                        AQGreenGraduationPostgreSqlSaveChangesInterceptor.Shared,
                        AQGreenGraduationPostgreSqlCommandInterceptor.Shared,
                        AQGreenGraduationPostgreSqlTransactionInterceptor.Shared,
                        AQGreenGraduationPostgreSqlConnectionInterceptor.Shared);
            });

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new[]
                {
                    new KeyValuePair<string, string>(
                        "App:ClientRootAddress",
                        "https://customers.example.test"),
                    new KeyValuePair<string, string>(
                        "App:DefaultTenantName",
                        "Default"),
                    new KeyValuePair<string, string>("Yoco:Mode", "test")
                })
                .Build();
            IocManager.IocContainer.Register(
                Component.For<IConfiguration>().Instance(configuration));

            RegisterFakeService<AbpZeroDbMigrator<AqualLifeStyleDbContext>>();
            Configuration.ReplaceService<IEmailSender, NullEmailSender>(
                DependencyLifeStyle.Transient);
            RegisterFakeService<ITransactionalEmailDeliveryGateway>();
        }

        public override void Initialize()
        {
            var services = new ServiceCollection();
            IdentityRegistrar.Register(services);
            services.AddLogging();
            services.AddDataProtection()
                .SetApplicationName("AqualLifeStyle.PostgreSqlGraduationTests");
            WindsorRegistrationHelper.CreateServiceProvider(
                IocManager.IocContainer,
                services);

            var permissionChecker = Substitute.For<IPermissionChecker>();
            permissionChecker.IsGrantedAsync(Arg.Any<string>())
                .Returns(Task.FromResult(true));

            IocManager.IocContainer.Register(
                Component.For<IPermissionChecker>()
                    .Instance(permissionChecker)
                    .Named("b53-postgresql-permission-checker")
                    .IsDefault(),
                Component.For<IYocoCheckoutGateway>()
                    .ImplementedBy<FakeYocoCheckoutGateway>()
                    .LifestyleSingleton(),
                Component.For<IAQGreenGraduationStructuralModelSelector>()
                    .Instance(AQGreenGraduationPostgreSqlSelector.Shared)
                    .Named("b52-postgresql-selector")
                    .IsDefault(),
                Component.For<IAQGreenGraduationStructuralEvidenceEvaluator>()
                    .Instance(AQGreenGraduationPostgreSqlEvaluator.Shared)
                    .Named("b52-postgresql-evaluator")
                    .IsDefault(),
                Component.For<IAQGreenWeeklySalesReviewGate>()
                    .Instance(new AQGreenWeeklySalesReviewTestGate())
                    .Named("b53-postgresql-review-gate")
                    .IsDefault());
        }

        public override void Shutdown()
        {
            _database?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _database = null;
            CurrentDatabase = null;
        }

        private void RegisterFakeService<TService>() where TService : class
        {
            IocManager.IocContainer.Register(
                Component.For<TService>()
                    .UsingFactoryMethod(() => Substitute.For<TService>())
                    .LifestyleSingleton());
        }
    }

    internal sealed class AQGreenGraduationPostgreSqlSelector
        : IAQGreenGraduationStructuralModelSelector
    {
        public static readonly AQGreenGraduationPostgreSqlSelector Shared = new();

        public AQGreenGraduationStructuralModel Model { get; set; } =
            AQGreenGraduationStructuralModel.PlacementV2;

        public Task<AQGreenGraduationStructuralModel> SelectAsync(
            int tenantId,
            Guid entryParticipationId) =>
            Task.FromResult(Model);
    }

    internal sealed class AQGreenGraduationPostgreSqlEvaluator
        : IAQGreenGraduationStructuralEvidenceEvaluator
    {
        public static readonly AQGreenGraduationPostgreSqlEvaluator Shared = new();
        private int _callCount;

        public Func<int, Guid, DateTime, AQGreenGraduationStructuralEvidenceResult>
            ResultFactory { get; set; }
        public Exception Failure { get; set; }
        public AQGreenGraduationStructuralEvidenceResult LastResult { get; private set; }
        public int CallCount => Volatile.Read(ref _callCount);

        public void Reset()
        {
            ResultFactory = null;
            Failure = null;
            LastResult = null;
            Interlocked.Exchange(ref _callCount, 0);
        }

        public Task<AQGreenGraduationStructuralEvidenceResult> EvaluateAsync(
            int tenantId,
            Guid participantId,
            DateTime cutoff,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            if (Failure != null) throw Failure;
            LastResult = ResultFactory?.Invoke(tenantId, participantId, cutoff) ??
                throw new InvalidOperationException(
                    "The PostgreSQL graduation evaluator was not configured.");
            return Task.FromResult(LastResult);
        }
    }
}
