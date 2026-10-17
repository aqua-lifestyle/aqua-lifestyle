using Abp.Dependency;

namespace AqualLifeStyle.Domain.AQGreen
{
    // Composition boundary for disposable demo calculation, not financial authority.
    public interface IAQGreenDemoCalculationMode
    {
        bool IsEnabled { get; }
    }

    public sealed class DisabledAQGreenDemoCalculationMode : IAQGreenDemoCalculationMode, ISingletonDependency
    {
        public static readonly DisabledAQGreenDemoCalculationMode Instance = new();
        public bool IsEnabled => false;
    }
}
