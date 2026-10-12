using System.Threading.Tasks;

namespace AqualLifeStyle.Domain.AQGreen
{
    /// <summary>
    /// Selects prospective invitation joining only in isolated verification.
    /// Production stays disabled pending separately authorized cutover.
    /// </summary>
    public interface IAQGreenPlacementV2JoiningGate
    {
        Task<bool> IsEnabledAsync(int tenantId);
    }
}
