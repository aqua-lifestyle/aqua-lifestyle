using System.Threading.Tasks;

namespace AqualLifeStyle.Domain.Onyx
{
    /// <summary>
    /// Serialises weekly commission calculation across host instances so that
    /// two instances can never calculate the same tenant+week at the same time.
    /// Only calculation (Earned) is serialised; release and payout remain
    /// manual Platform Administrator actions.
    /// </summary>
    public interface IEntryWeeklyCommissionCalculationLock
    {
        Task AcquireAsync();
    }
}
