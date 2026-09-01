using System;
using System.Threading.Tasks;
using Abp.Dependency;
using AqualLifeStyle.Domain.Onyx;

namespace AqualLifeStyle.Application.Admin.Commissions
{
    public sealed class LegacyV1AQGreenCommissionStructuralModelSelector
        : IAQGreenCommissionStructuralModelSelector, ISingletonDependency
    {
        public Task<AQGreenCommissionStructuralModel> SelectAsync(
            int tenantId,
            DateTime commissionCutoffUtc) =>
            Task.FromResult(AQGreenCommissionStructuralModel.LegacyV1);
    }
}
