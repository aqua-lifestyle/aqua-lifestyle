using System;
using System.Threading;
using System.Threading.Tasks;
using Abp.Dependency;
using AqualLifeStyle.Domain.AQGreen;

namespace AqualLifeStyle.Web.Tests
{
    public sealed class AQGreenV2TestJoiningGate
        : IAQGreenPlacementV2JoiningGate, ISingletonDependency
    {
        private readonly AsyncLocal<int?> _tenant = new();
        public Task<bool> IsEnabledAsync(int tenantId) =>
            Task.FromResult(_tenant.Value == tenantId);
        public IDisposable Enable(int tenantId)
        {
            var previous = _tenant.Value;
            _tenant.Value = tenantId;
            return new ResetScope(() => _tenant.Value = previous);
        }
    }
}
