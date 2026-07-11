using Abp.AutoMapper;
using Abp.Modules;
using Abp.Reflection.Extensions;
using AqualLifeStyle.Application.AreaLeaders.Dto;
using AqualLifeStyle.Application.Customers.Dto;
using AqualLifeStyle.Application.Enquiries.Dto;
using AqualLifeStyle.Application.Facilitators.Dto;
using AqualLifeStyle.Application.Memberships.Dto;
using AqualLifeStyle.Application.Orders.Dto;
using AqualLifeStyle.Application.Referrals.Dto;
using AqualLifeStyle.Authorization;

namespace AqualLifeStyle
{
    [DependsOn(
        typeof(AqualLifeStyleCoreModule), 
        typeof(AbpAutoMapperModule))]
    public class AqualLifeStyleApplicationModule : AbpModule
    {
        public override void PreInitialize()
        {
            Configuration.Authorization.Providers.Add<AqualLifeStyleAuthorizationProvider>();
        }

        public override void Initialize()
        {
            var thisAssembly = typeof(AqualLifeStyleApplicationModule).GetAssembly();

            IocManager.RegisterAssemblyByConvention(thisAssembly);

            Configuration.Modules.AbpAutoMapper().Configurators.Add(
                cfg =>
                {
                    cfg.AddMaps(thisAssembly);
                    cfg.AddProfile<CustomerMapProfile>();
                    cfg.AddProfile<MembershipMapProfile>();
                    cfg.AddProfile<EnquiryMapProfile>();
                    cfg.AddProfile<FacilitatorMapProfile>();
                    cfg.AddProfile<AreaLeaderMapProfile>();
                    cfg.AddProfile<OrderIntentMapProfile>();
                    cfg.AddProfile<ReferralMapProfile>();
                }
            );
        }
    }
}
