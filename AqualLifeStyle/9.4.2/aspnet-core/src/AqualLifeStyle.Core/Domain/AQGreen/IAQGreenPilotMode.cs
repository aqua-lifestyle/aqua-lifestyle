using System;
using Abp.Dependency;

namespace AqualLifeStyle.Domain.AQGreen
{
    // This is an environment boundary, never production cutover authority.
    public interface IAQGreenPilotMode
    {
        bool IsEnabled { get; }
        void DenyOutsidePilot(string capability);
        void RequirePilot();
    }

    public sealed class DisabledAQGreenPilotMode : IAQGreenPilotMode, ISingletonDependency
    {
        public static readonly DisabledAQGreenPilotMode Instance = new();
        public bool IsEnabled => false;
        public void DenyOutsidePilot(string capability) { }
        public void RequirePilot() => throw new InvalidOperationException(
            "This operation requires the isolated non-production AQGreen pilot.");
    }
}
