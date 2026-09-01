using System;
using System.Threading.Tasks;

namespace AqualLifeStyle.Domain.Onyx
{
    public enum AQGreenCommissionStructuralModel
    {
        LegacyV1 = 1,
        PlacementV2 = 2
    }

    public static class AQGreenCommissionDecisionRules
    {
        public const int MaximumVersionLength = 64;
        public const string CurrentVersion = "AQGreenWeeklyCommissionDecisionV1";
    }

    /// <summary>
    /// Explicit D10 cutover seam. The production implementation remains LegacyV1
    /// until an authorised effective boundary and scope exist.
    /// </summary>
    public interface IAQGreenCommissionStructuralModelSelector
    {
        Task<AQGreenCommissionStructuralModel> SelectAsync(
            int tenantId,
            DateTime commissionCutoffUtc);
    }
}
