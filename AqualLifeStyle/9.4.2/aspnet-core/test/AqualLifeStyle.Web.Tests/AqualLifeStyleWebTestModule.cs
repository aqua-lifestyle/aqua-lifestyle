using System;
using Abp.AspNetCore;
using Abp.AspNetCore.TestBase;
using Abp.Modules;
using Abp.Reflection.Extensions;
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
