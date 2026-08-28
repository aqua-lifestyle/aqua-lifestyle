using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Abp.Dependency;
using AqualLifeStyle.Domain.AQGreen;

namespace AqualLifeStyle.Web.Tests
{
    public sealed class AQGreenPlacementV2TestProgressGate
        : IAQGreenPlacementV2ProgressGate, ISingletonDependency
    {
        private readonly ConcurrentDictionary<Guid, byte> _enabled = new();
        private readonly ConcurrentDictionary<Guid, int> _checks = new();

        public Task<bool> IsEnabledAsync(int? tenantId, Guid participantId)
        {
            _checks.AddOrUpdate(participantId, 1, (_, count) => count + 1);
            return Task.FromResult(_enabled.ContainsKey(participantId));
        }

        public int GetCheckCount(Guid participantId) =>
            _checks.TryGetValue(participantId, out var count) ? count : 0;

        public IDisposable Enable(Guid participantId)
        {
            _enabled[participantId] = 0;
            _checks.TryRemove(participantId, out _);
            return new DisableScope(_enabled, _checks, participantId);
        }

        private sealed class DisableScope : IDisposable
        {
            private readonly ConcurrentDictionary<Guid, byte> _enabled;
            private readonly ConcurrentDictionary<Guid, int> _checks;
            private readonly Guid _participantId;

            public DisableScope(
                ConcurrentDictionary<Guid, byte> enabled,
                ConcurrentDictionary<Guid, int> checks,
                Guid participantId)
            {
                _enabled = enabled;
                _checks = checks;
                _participantId = participantId;
            }

            public void Dispose()
            {
                _enabled.TryRemove(_participantId, out _);
                _checks.TryRemove(_participantId, out _);
            }
        }
    }
}
