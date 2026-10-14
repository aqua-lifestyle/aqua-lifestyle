using System;
using Microsoft.Extensions.Configuration;
using System.Runtime.CompilerServices;

namespace AqualLifeStyle.Configuration
{
    /// <summary>Operational suspension for controlled API recovery; never V2 cutover authority.</summary>
    public static class ApplicationRecoveryMode
    {
        public const string EnabledSetting = "App:Recovery:Enabled";

        private sealed class Suspension { public bool Enabled; }
        private static readonly ConditionalWeakTable<IConfiguration, Suspension> Suspensions = new();

        public static bool IsEnabled(IConfiguration configuration)
        {
            var state = Suspensions.GetValue(configuration, _ => new Suspension());
            lock (state)
            {
                state.Enabled |= configuration.GetValue<bool>(EnabledSetting);
                return state.Enabled; // Recovery cannot be disarmed by configuration reload.
            }
        }

        public static void Latch(IConfiguration configuration)
        {
            var state = Suspensions.GetValue(configuration, _ => new Suspension());
            lock (state) state.Enabled = true;
        }

        public static void DenyBusinessOperation(IConfiguration configuration)
        {
            if (IsEnabled(configuration))
                throw new InvalidOperationException("Business processing is suspended during API recovery.");
        }
    }
}
