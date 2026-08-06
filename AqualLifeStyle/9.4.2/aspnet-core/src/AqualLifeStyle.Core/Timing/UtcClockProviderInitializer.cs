using System.Runtime.CompilerServices;
using Abp.Timing;

namespace AqualLifeStyle.Timing
{
    internal static class UtcClockProviderInitializer
    {
        [ModuleInitializer]
        public static void Initialize()
        {
            Clock.Provider = ClockProviders.Utc;
        }
    }
}
