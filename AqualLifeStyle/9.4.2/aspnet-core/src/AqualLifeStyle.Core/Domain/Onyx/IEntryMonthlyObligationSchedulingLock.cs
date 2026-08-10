using System.Threading.Tasks;

namespace AqualLifeStyle.Domain.Onyx
{
    /// <summary>
    /// Serialises AQGreen monthly-obligation scheduling across host instances so
    /// that two instances can never create obligations or apply payments for the
    /// same member at the same time.
    /// </summary>
    public interface IEntryMonthlyObligationSchedulingLock
    {
        Task AcquireAsync();
    }
}
