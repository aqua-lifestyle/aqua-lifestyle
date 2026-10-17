using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Abp.Modules;
using Abp.Reflection.Extensions;
using Abp.Threading.BackgroundWorkers;
using AqualLifeStyle.Configuration;
using AqualLifeStyle.Web.Host.Payments.Yoco;
using Abp.Runtime.Caching.Redis;
using AqualLifeStyle.Web.Host.Email;
using AqualLifeStyle.Web.Host.ProgrammeEngine;
using AqualLifeStyle.Web.Host.Commissions;
using Abp.Dependency;
using Abp.Net.Mail;
using Abp.Configuration.Startup;
using AqualLifeStyle.Application.Admin.Commissions;
using AqualLifeStyle.Application.Admin.ProgrammeParticipations;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Web.Host.AQGreenV2Demo;
using AqualLifeStyle.Web.Host.AQGreenV2Pilot;
using Microsoft.Extensions.Hosting;
using Abp.Domain.Uow;
using Abp.EntityFrameworkCore.Uow;
using AqualLifeStyle.EntityFrameworkCore;
using AqualLifeStyle.Application.ProgrammeParticipations;

namespace AqualLifeStyle.Web.Host.Startup
{
    [DependsOn(
       typeof(AqualLifeStyleWebCoreModule),
       typeof(AbpRedisCacheModule))]
    public class AqualLifeStyleWebHostModule: AbpModule
    {
        private readonly IWebHostEnvironment _env;
        private readonly IConfigurationRoot _appConfiguration;

        public AqualLifeStyleWebHostModule(IWebHostEnvironment env)
        {
            _env = env;
            _appConfiguration = env.GetAppConfiguration();
        }

        public override void Initialize()
        {
            IocManager.RegisterAssemblyByConvention(typeof(AqualLifeStyleWebHostModule).GetAssembly());
        }

        public override void PreInitialize()
        {
            Configuration.BackgroundJobs.IsJobExecutionEnabled =
                RuntimeOperationGuards.CanRunBackgroundWork(_appConfiguration);
            var pilot = AQGreenV2PilotConfiguration.Validate(_env, _appConfiguration);
            var demo = AQGreenV2DemoConfiguration.Validate(_env, _appConfiguration);
            if (!pilot && !demo)
            {
                Configuration.ReplaceService<IAQGreenD10CommissioningSource, AQGreenD10OperatorEvidenceSource>(DependencyLifeStyle.Singleton);
                Configuration.ReplaceService<IAQGreenPlacementV2JoiningGate, AQGreenD10JoiningGate>(DependencyLifeStyle.Transient);
                Configuration.ReplaceService<IAQGreenPlacementV2ApprovalGate, AQGreenD10ApprovalGate>(DependencyLifeStyle.Transient);
                Configuration.ReplaceService<IAQGreenPlacementV2ProgressGate, AQGreenD10ProgressGate>(DependencyLifeStyle.Transient);
                Configuration.ReplaceService<IAQGreenWeeklySalesReviewGate, AQGreenD10SalesReviewGate>(DependencyLifeStyle.Transient);
                Configuration.ReplaceService<IAQGreenCommissionStructuralModelSelector, AQGreenD10CommissionSelector>(DependencyLifeStyle.Transient);
                Configuration.ReplaceService<IAQGreenGraduationStructuralModelSelector, AQGreenD10GraduationSelector>(DependencyLifeStyle.Transient);
            }
            if (pilot)
            {
                Configuration.ReplaceService<IAQGreenPilotMode, AQGreenV2PilotMode>(DependencyLifeStyle.Singleton);
                Configuration.ReplaceService<IAQGreenPlacementV2ApprovalGate, AQGreenV2PilotApprovalGate>(DependencyLifeStyle.Singleton);
                Configuration.ReplaceService<IAQGreenPlacementV2ProgressGate, AQGreenV2PilotProgressGate>(DependencyLifeStyle.Singleton);
                Configuration.ReplaceService<IAQGreenCommissionStructuralModelSelector, AQGreenV2PilotCommissionSelector>(DependencyLifeStyle.Singleton);
                Configuration.ReplaceService<IAQGreenWeeklySalesReviewGate, AQGreenV2PilotSalesReviewGate>(DependencyLifeStyle.Singleton);
                Configuration.BackgroundJobs.IsJobExecutionEnabled = false;
            }
            if (AQGreenV2DemoConfiguration.Validate(_env, _appConfiguration))
            {
                Configuration.ReplaceService<IAQGreenDemoCalculationMode,
                    AQGreenV2DemoCalculationMode>(DependencyLifeStyle.Singleton);
                Configuration.ReplaceService<
                    IAQGreenPlacementV2JoiningGate,
                    AQGreenV2DemoJoiningGate>(DependencyLifeStyle.Singleton);
                Configuration.ReplaceService<
                    IAQGreenPlacementV2ApprovalGate,
                    AQGreenV2DemoApprovalGate>(DependencyLifeStyle.Singleton);
                Configuration.ReplaceService<
                    IAQGreenPlacementV2ProgressGate,
                    AQGreenV2DemoProgressGate>(DependencyLifeStyle.Singleton);
                Configuration.ReplaceService<
                    IAQGreenCommissionStructuralModelSelector,
                    AQGreenV2DemoCommissionSelector>(DependencyLifeStyle.Singleton);
                Configuration.ReplaceService<
                    IAQGreenWeeklySalesReviewGate,
                    AQGreenV2DemoSalesReviewGate>(DependencyLifeStyle.Singleton);
            }

            if (pilot)
                Configuration.ReplaceService<IEmailSender, NullEmailSender>(DependencyLifeStyle.Transient);
            else
                Configuration.ReplaceService<IEmailSender, BirdAbpEmailSender>(DependencyLifeStyle.Transient);
            var redisConfiguration = _appConfiguration["Redis:Configuration"];
            if (string.IsNullOrWhiteSpace(redisConfiguration))
            {
                return;
            }

            Configuration.Caching.UseRedis(options =>
            {
                options.ConnectionString = NormalizeRedisConfiguration(redisConfiguration);
                options.DatabaseId = _appConfiguration.GetValue<int?>("Redis:DatabaseId") ?? 0;
            });
        }

        public override void PostInitialize()
        {
            if (_env.IsProduction() || ApplicationRecoveryMode.IsEnabled(_appConfiguration))
            {
                var manager = IocManager.Resolve<IUnitOfWorkManager>();
                using (var uow = manager.Begin())
                {
                    DatabaseReleasePreflight.Verify(manager.Current.GetDbContext<AqualLifeStyleDbContext>());
                    uow.Complete();
                }
                if (!ApplicationRecoveryMode.IsEnabled(_appConfiguration))
                    IocManager.Resolve<AQGreenD10Runtime>().VerifyStartupAsync().GetAwaiter().GetResult();
            }
            // PostInitialize is called after ABP modules are started. Resolve the
            // ErrorInfoBuilder here and add our converter so ABP will build
            // descriptive ErrorInfo instances for known exceptions.
            try
            {
                var errorInfoBuilder = IocManager.Resolve<Abp.Web.Models.IErrorInfoBuilder>();
                // Register converter in IoC so it can receive IHttpContextAccessor and environment
                IocManager.Register<CustomExceptionToErrorInfoConverter>();
                var converter = IocManager.Resolve<CustomExceptionToErrorInfoConverter>();
                errorInfoBuilder.AddExceptionConverter(converter);
            }
            catch (Exception)
            {
                // Swallow any errors here to avoid startup crash; ABP will fall back
                // to default behavior if the ErrorInfoBuilder is not available.
            }

            if (ApplicationRecoveryMode.IsEnabled(_appConfiguration) ||
                AQGreenV2PilotConfiguration.Validate(_env, _appConfiguration))
                return; // Recovery and pilot never register provider or financial workers.

            // ABP's job-execution switch does not govern independently registered
            // periodic workers. Check the same holds before resolving each worker;
            // their bodies also enforce them for direct calls and later suspension.
            if (RuntimeOperationGuards.CanRunBackgroundWork(_appConfiguration))
                IocManager.Resolve<IBackgroundWorkerManager>().Add(
                    IocManager.Resolve<YocoPaymentOperationsMonitor>());
            if (RuntimeOperationGuards.CanDispatchEmail(_appConfiguration))
                IocManager.Resolve<IBackgroundWorkerManager>().Add(
                    IocManager.Resolve<TransactionalEmailOutboxWorker>());
            if (RuntimeOperationGuards.CanRunFinancialWorker(_appConfiguration, "App:EntryMonthlyObligations:Enabled"))
                IocManager.Resolve<IBackgroundWorkerManager>().Add(
                    IocManager.Resolve<EntryMonthlyObligationWorker>());
            if (RuntimeOperationGuards.CanRunFinancialWorker(_appConfiguration, "App:WeeklyCommissions:Enabled"))
                IocManager.Resolve<IBackgroundWorkerManager>().Add(
                    IocManager.Resolve<WeeklyCommissionCalculationWorker>());

            if (AQGreenV2DemoConfiguration.Validate(_env, _appConfiguration))
            {
                IocManager.Resolve<AQGreenV2DemoFixtureSeeder>().Seed();
            }
        }

        private static string NormalizeRedisConfiguration(string configuration)
        {
            Uri uri;
            if (!Uri.TryCreate(configuration, UriKind.Absolute, out uri) ||
                (uri.Scheme != "redis" && uri.Scheme != "rediss"))
            {
                return configuration;
            }

            var parts = new System.Collections.Generic.List<string>
            {
                uri.Host + ":" + uri.Port,
                "abortConnect=false"
            };
            if (uri.Scheme == "rediss")
            {
                parts.Add("ssl=true");
            }
            if (!string.IsNullOrWhiteSpace(uri.UserInfo))
            {
                var userInfo = uri.UserInfo.Split(new[] { ':' }, 2);
                if (userInfo.Length == 2)
                {
                    if (!string.IsNullOrWhiteSpace(userInfo[0]))
                    {
                        parts.Add("user=" + Uri.UnescapeDataString(userInfo[0]));
                    }
                    parts.Add("password=" + Uri.UnescapeDataString(userInfo[1]));
                }
            }
            return string.Join(",", parts);
        }
    }
}
