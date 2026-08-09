using System.Threading.Tasks;

namespace AqualLifeStyle.Domain.Onyx
{
    /// <summary>
    /// Serialises AQGreen and Onyx weekly commission calculation across host
    /// instances. Only calculation is serialised; release and payout remain
    /// manual Platform Administrator actions.
    /// </summary>
    public interface IWeeklyCommissionCalculationLock
    {
        Task AcquireAsync();
    }
}
