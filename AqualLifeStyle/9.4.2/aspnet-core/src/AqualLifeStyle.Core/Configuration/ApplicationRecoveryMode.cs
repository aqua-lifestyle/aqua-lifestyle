using System;
using Microsoft.Extensions.Configuration;

namespace AqualLifeStyle.Configuration
{
    /// <summary>Operational suspension for controlled API recovery; never V2 cutover authority.</summary>
    public static class ApplicationRecoveryMode
    {
        public const string EnabledSetting = "App:Recovery:Enabled";

        public static bool IsEnabled(IConfiguration configuration) =>
            configuration.GetValue<bool>(EnabledSetting);

        public static void DenyBusinessOperation(IConfiguration configuration)
        {
            if (IsEnabled(configuration))
                throw new InvalidOperationException("Business processing is suspended during API recovery.");
        }
    }
}
