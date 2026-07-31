using System.Threading.Tasks;

namespace AqualLifeStyle.Domain.Onyx
{
    public enum ProgrammeRecruiterNetwork
    {
        AQGreen = 1,
        Onyx = 2
    }

    public interface IProgrammeRecruiterCorrectionLock
    {
        Task AcquireAsync(ProgrammeRecruiterNetwork network);
    }
}
