using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Abp.Dependency;
using AqualLifeStyle.Domain.AQGreen;

namespace AqualLifeStyle.Web.Tests
{
    public sealed class AQGreenPlacementV2TestApprovalGate
        : IAQGreenPlacementV2ApprovalGate, ISingletonDependency
    {
        private readonly ConcurrentDictionary<Guid, byte> _enabled = new();

        public Task<bool> IsEnabledAsync(int? tenantId, Guid participantId) =>
            Task.FromResult(_enabled.ContainsKey(participantId));

        public IDisposable Enable(Guid participantId)
        {
            _enabled[participantId] = 0;
            return new DisableScope(_enabled, participantId);
        }

        private sealed class DisableScope : IDisposable
        {
            private readonly ConcurrentDictionary<Guid, byte> _enabled;
            private readonly Guid _participantId;

            public DisableScope(
                ConcurrentDictionary<Guid, byte> enabled,
                Guid participantId)
            {
                _enabled = enabled;
                _participantId = participantId;
            }

            public void Dispose() => _enabled.TryRemove(_participantId, out _);
        }
    }
}
