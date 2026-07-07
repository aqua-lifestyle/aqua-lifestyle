using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Abp.Modules;
using Abp.Reflection.Extensions;
using AqualLifeStyle.Configuration;

namespace AqualLifeStyle.Web.Host.Startup
{
    [DependsOn(
       typeof(AqualLifeStyleWebCoreModule))]
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

        public override void PostInitialize()
        {
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
        }
    }
}
