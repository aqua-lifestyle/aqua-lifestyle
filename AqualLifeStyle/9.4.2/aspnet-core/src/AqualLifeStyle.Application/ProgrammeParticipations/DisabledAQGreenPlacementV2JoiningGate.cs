using System;
using System.Threading.Tasks;
using Abp.Dependency;
using AqualLifeStyle.Domain.AQGreen;

namespace AqualLifeStyle.Application.ProgrammeParticipations
{
    public sealed class DisabledAQGreenPlacementV2JoiningGate
        : IAQGreenPlacementV2JoiningGate, ISingletonDependency
    {
        public Task<bool> IsEnabledAsync(int tenantId) => Task.FromResult(false);
        public Task ValidateDestinationAsync(int tenantId, Guid destinationAreaId) => Task.CompletedTask;
    }
}
