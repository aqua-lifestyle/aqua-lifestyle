using Abp.EntityFrameworkCore.Configuration;
using Abp.Modules;
using Abp.Reflection.Extensions;
using Abp.Zero.EntityFrameworkCore;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.EntityFrameworkCore.Seed;
using Castle.MicroKernel.Registration;

namespace AqualLifeStyle.EntityFrameworkCore
{
    [DependsOn(
        typeof(AqualLifeStyleCoreModule), 
        typeof(AbpZeroCoreEntityFrameworkCoreModule))]
    public class AqualLifeStyleEntityFrameworkModule : AbpModule
    {
        /* Used it tests to skip dbcontext registration, in order to use in-memory database of EF Core */
        public bool SkipDbContextRegistration { get; set; }

        public bool SkipDbSeed { get; set; }

        public override void PreInitialize()
        {
            if (!SkipDbContextRegistration)
            {
                Configuration.Modules.AbpEfCore().AddDbContext<AqualLifeStyleDbContext>(options =>
                {
                    if (options.ExistingConnection != null)
                    {
                        AqualLifeStyleDbContextConfigurer.Configure(options.DbContextOptions, options.ExistingConnection);
                    }
                    else
                    {
                        AqualLifeStyleDbContextConfigurer.Configure(options.DbContextOptions, options.ConnectionString);
                    }
                });
            }
        }

        public override void Initialize()
        {
            IocManager.RegisterAssemblyByConvention(typeof(AqualLifeStyleEntityFrameworkModule).GetAssembly());
            IocManager.IocContainer.Register(
                Component.For<IAQGreenGraduationStructuralEvidenceEvaluator>()
                    .ImplementedBy<AQGreenStructuralCompletionEvaluator>()
                    .Named("AQGreenGraduationStructuralEvidenceEvaluator")
                    .LifestyleTransient());
            IocManager.IocContainer.Register(
                Component.For<IAQGreenCommissionStructuralEvidenceEvaluator>()
                    .ImplementedBy<AQGreenStructuralCompletionEvaluator>()
                    .Named("AQGreenCommissionStructuralEvidenceEvaluator")
                    .LifestyleTransient());
        }

        public override void PostInitialize()
        {
            if (!SkipDbSeed)
            {
                SeedHelper.SeedHostDb(IocManager);
            }
        }
    }
}
