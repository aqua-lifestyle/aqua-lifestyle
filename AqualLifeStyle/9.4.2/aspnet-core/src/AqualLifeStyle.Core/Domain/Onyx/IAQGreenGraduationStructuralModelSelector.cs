using System;
using System.Threading.Tasks;

namespace AqualLifeStyle.Domain.Onyx
{
    /// <summary>
    /// Explicit integration seam for the separately authorized D10 cutover.
    /// The production implementation remains LegacyV1 until that decision is made.
    /// </summary>
    public interface IAQGreenGraduationStructuralModelSelector
    {
        Task<AQGreenGraduationStructuralModel> SelectAsync(
            int tenantId,
            Guid entryParticipationId);
    }
}
