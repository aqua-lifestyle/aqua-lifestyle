using Abp.AutoMapper;
using Abp.Dependency;
using Abp.Modules;
using Abp.Reflection.Extensions;
using AqualLifeStyle.Application.Admin.ProgrammeParticipations;
using AqualLifeStyle.Application.Recruitment;
using AqualLifeStyle.Authorization;
using Castle.MicroKernel.Resolvers.SpecializedResolvers;

namespace AqualLifeStyle
{
    [DependsOn(
        typeof(AqualLifeStyleCoreModule), 
        typeof(AbpAutoMapperModule))]
    public class AqualLifeStyleApplicationModule : AbpModule
    {
        public override void PreInitialize()
        {
            IocManager.IocContainer.Kernel.ComponentRegistered += (key, handler) =>
            {
                if (typeof(Abp.Application.Services.IApplicationService).IsAssignableFrom(
                        handler.ComponentModel.Implementation))
                    handler.ComponentModel.Interceptors.Add(new Castle.Core.InterceptorReference(
                        typeof(AqualLifeStyle.Application.ProgrammeParticipations.PilotApplicationBoundaryInterceptor)));
            };
            IocManager.IocContainer.Kernel.Resolver.AddSubResolver(
                new CollectionResolver(IocManager.IocContainer.Kernel, true));
            IocManager.IocContainer.Kernel.Resolver.AddSubResolver(
                new ArrayResolver(IocManager.IocContainer.Kernel, true));
            Configuration.Authorization.Providers.Add<AqualLifeStyleAuthorizationProvider>();
        }

        public override void Initialize()
        {
            var thisAssembly = typeof(AqualLifeStyleApplicationModule).GetAssembly();

            IocManager.RegisterAssemblyByConvention(thisAssembly);

            IocManager.Register<IProgrammeRecruitmentPolicy, AQGreenRecruitmentPolicy>(
                DependencyLifeStyle.Transient);
            IocManager.Register<IProgrammeRecruitmentPolicy, OnyxRecruitmentPolicy>(
                DependencyLifeStyle.Transient);
            IocManager.Register<IProgrammeRecruiterCorrectionPolicy, AQGreenRecruiterCorrectionPolicy>(
                DependencyLifeStyle.Transient);
            IocManager.Register<IProgrammeRecruiterCorrectionPolicy, OnyxRecruiterCorrectionPolicy>(
                DependencyLifeStyle.Transient);

            Configuration.Modules.AbpAutoMapper().Configurators.Add(
                cfg => cfg.AddMaps(thisAssembly)
            );
        }
    }
}
