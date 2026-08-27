using System;
using System.Threading.Tasks;
using Abp.Dependency;
using AqualLifeStyle.Domain.AQGreen;

namespace AqualLifeStyle.Application.Admin.ProgrammeParticipations
{
    public sealed class DisabledAQGreenPlacementV2ApprovalGate
        : IAQGreenPlacementV2ApprovalGate, ISingletonDependency
    {
        public Task<bool> IsEnabledAsync(int? tenantId, Guid participantId) =>
            Task.FromResult(false);
    }
}
