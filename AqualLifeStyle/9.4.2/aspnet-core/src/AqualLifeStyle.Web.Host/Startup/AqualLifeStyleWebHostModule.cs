using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Abp.Modules;
using Abp.Reflection.Extensions;
using Abp.Threading.BackgroundWorkers;
using AqualLifeStyle.Configuration;
using AqualLifeStyle.Web.Host.Payments.Yoco;
using Abp.Runtime.Caching.Redis;

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

            IocManager.Resolve<IBackgroundWorkerManager>().Add(
                IocManager.Resolve<YocoPaymentOperationsMonitor>());
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
