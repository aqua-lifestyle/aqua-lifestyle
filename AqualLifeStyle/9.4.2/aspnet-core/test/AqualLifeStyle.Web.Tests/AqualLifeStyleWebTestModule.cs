using System;
using Abp.AspNetCore;
using Abp.AspNetCore.TestBase;
using Abp.Configuration.Startup;
using Abp.Modules;
using Abp.Reflection.Extensions;
using Abp.Dependency;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.EntityFrameworkCore;
using AqualLifeStyle.Tests;
using AqualLifeStyle.Web.Startup;
using Microsoft.AspNetCore.Mvc.ApplicationParts;

namespace AqualLifeStyle.Web.Tests
{
    [DependsOn(
        typeof(AqualLifeStyleWebMvcModule),
        typeof(AbpAspNetCoreTestBaseModule)
    )]
    public class AqualLifeStyleWebTestModule : AbpModule
    {
        public AqualLifeStyleWebTestModule(AqualLifeStyleEntityFrameworkModule abpProjectNameEntityFrameworkModule)
        {
            abpProjectNameEntityFrameworkModule.SkipDbContextRegistration = true;
        } 
        
        public override void PreInitialize()
        {
            AdministratorBootstrapTestEnvironment.Configure();
            Configuration.ReplaceService<
                IAQGreenPlacementV2ApprovalGate,
                AQGreenPlacementV2TestApprovalGate>(DependencyLifeStyle.Singleton);
            Configuration.ReplaceService<
                IAQGreenPlacementV2ProgressGate,
                AQGreenPlacementV2TestProgressGate>(DependencyLifeStyle.Singleton);
            Configuration.ReplaceService<
                IAQGreenStructuralCompletionEvaluator,
                AQGreenPlacementV2TestStructuralEvaluator>(DependencyLifeStyle.Singleton);
            // Allow enabling transactional UnitOfWork for reproduction via environment variable to avoid impacting other tests.
            var enableTransactional = string.Equals(Environment.GetEnvironmentVariable("REPRO_TRANSACTIONAL"), "true", StringComparison.OrdinalIgnoreCase);
            Configuration.UnitOfWork.IsTransactional = enableTransactional;
        }

        public override void Initialize()
        {
            IocManager.RegisterAssemblyByConvention(typeof(AqualLifeStyleWebTestModule).GetAssembly());
        }
        
        public override void PostInitialize()
        {
            IocManager.Resolve<ApplicationPartManager>()
                .AddApplicationPartsIfNotAddedBefore(typeof(AqualLifeStyleWebMvcModule).Assembly);
        }
    }
}
