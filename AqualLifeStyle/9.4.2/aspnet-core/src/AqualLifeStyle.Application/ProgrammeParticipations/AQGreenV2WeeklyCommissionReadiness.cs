using System.Threading.Tasks;
using AqualLifeStyle.Domain.AQGreen;

namespace AqualLifeStyle.Application.ProgrammeParticipations
{
    public interface IAQGreenV2WeeklyCommissionReadiness
    {
        Task<AQGreenD10Decision> RequireEligibleDecisionAsync();
    }

    /// <summary>
    /// Fails closed unless a current, selected D10 authority commissions the
    /// financial worker and the authorized tenant remains eligible for writes.
    /// A durable committed Normal receipt is checked by AQGreenD10Runtime and
    /// remains valid after the first-use authorization expires.
    /// </summary>
    public sealed class AQGreenV2WeeklyCommissionReadiness : IAQGreenV2WeeklyCommissionReadiness
    {
        private readonly IAQGreenD10CommissioningSource _source;
        private readonly AQGreenD10Runtime _runtime;

        public AQGreenV2WeeklyCommissionReadiness(
            IAQGreenD10CommissioningSource source,
            AQGreenD10Runtime runtime)
        {
            _source = source;
            _runtime = runtime;
        }

        public async Task<AQGreenD10Decision> RequireEligibleDecisionAsync()
        {
            var selected = _source.Read();
            AQGreenD10Commissioning.Require(selected != null, "D10_FINANCIAL_WORKER_AUTHORITY_REQUIRED");
            var selectedDigest = AQGreenB6CanonicalEvidence.Digest(selected.Decision);

            await _runtime.EnsureFinancialWorkersAsync();
            AQGreenD10Commissioning.Require(selected.FinancialWorkersCommissioned,
                "D10_FINANCIAL_WORKER_AUTHORITY_MISSING_OR_NOT_EFFECTIVE");
            AQGreenD10Commissioning.Require(await _runtime.ForTenantAsync(selected.Decision.TenantId, write: true),
                "D10_FINANCIAL_WORKER_TENANT_NOT_ELIGIBLE");

            var current = _source.Read();
            AQGreenD10Commissioning.Require(current != null && current.FinancialWorkersCommissioned &&
                current.Decision.TenantId == selected.Decision.TenantId &&
                AQGreenB6CanonicalEvidence.Digest(current.Decision) == selectedDigest,
                "D10_FINANCIAL_WORKER_AUTHORITY_CHANGED");
            return current.Decision;
        }
    }
}
