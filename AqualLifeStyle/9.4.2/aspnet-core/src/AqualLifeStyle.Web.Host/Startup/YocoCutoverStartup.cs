using System;
using System.Linq;
using System.Threading.Tasks;
using AqualLifeStyle.Configuration;
using AqualLifeStyle.Payments.Yoco;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

#if YOCO_CUTOVER_COMPAT
[assembly: YocoReceiverArtifact]
#endif

namespace AqualLifeStyle.Web.Host.Startup
{
    public static class YocoCutoverStartup
    {
        public static async Task ValidateAsync(IServiceProvider services, IWebHostEnvironment environment, IConfiguration configuration)
        {
            var options = new YocoCutoverOptions(configuration);
            if (!environment.IsProduction() && !options.Enabled) return;
            if (options.Enabled && !services.GetRequiredService<IYocoWebhookSignatureVerifier>().IsConfigured)
                throw new InvalidOperationException("YOCO_RECEIVER_SIGNATURE_CONFIGURATION_REQUIRED");
            await services.GetRequiredService<IYocoCutoverStore>().TransactAsync(s =>
            {
                if (!options.CompatibilityReceiver && !s.HasMigration55 && !ApplicationRecoveryMode.IsEnabled(configuration))
                    throw new InvalidOperationException("FULL_V2_REQUIRES_MIGRATION_55_USE_COMPAT_RECEIVER");
                if (!options.Enabled && (s.Events.Any(YocoCutoverService.Unfinished) || s.Phase != "Uninitialized"))
                    throw new InvalidOperationException("YOCO_CUTOVER_CANNOT_DISABLE_WITH_PENDING_FENCE_OR_EVENTS");
                return Task.FromResult(true);
            });
        }
    }
}
