using System;
using System.Threading.Tasks;
using Abp.Dependency;
using AqualLifeStyle.Domain.Onyx;

namespace AqualLifeStyle.Application.Admin.ProgrammeParticipations
{
    public sealed class LegacyV1AQGreenGraduationStructuralModelSelector
        : IAQGreenGraduationStructuralModelSelector, ISingletonDependency
    {
        public Task<AQGreenGraduationStructuralModel> SelectAsync(
            int tenantId,
            Guid entryParticipationId)
            => Task.FromResult(AQGreenGraduationStructuralModel.LegacyV1);
    }
}
