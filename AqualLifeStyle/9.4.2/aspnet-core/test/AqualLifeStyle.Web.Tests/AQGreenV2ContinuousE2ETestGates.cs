using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Abp.Dependency;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.EntityFrameworkCore;

namespace AqualLifeStyle.Web.Tests
{
    /// <summary>
    /// Test-only dormant-path selectors. Every selector preserves the production
    /// LegacyV1/disabled result unless an individual continuous E2E scope enables
    /// the exact participant, period cutoff, or Tenant under test.
    /// </summary>
    public sealed class AQGreenV2ContinuousGraduationSelector
        : IAQGreenGraduationStructuralModelSelector, ISingletonDependency
    {
        private readonly ConcurrentDictionary<Guid, byte> _enabled = new();

        public Task<AQGreenGraduationStructuralModel> SelectAsync(
            int tenantId,
            Guid entryParticipationId) =>
            Task.FromResult(tenantId > 0 && _enabled.ContainsKey(entryParticipationId)
                ? AQGreenGraduationStructuralModel.PlacementV2
                : AQGreenGraduationStructuralModel.LegacyV1);

        public IDisposable Enable(Guid entryParticipationId)
        {
            _enabled[entryParticipationId] = 0;
            return new ResetScope(() => _enabled.TryRemove(entryParticipationId, out _));
        }
    }

    public sealed class AQGreenV2ContinuousCommissionSelector
        : IAQGreenCommissionStructuralModelSelector, ISingletonDependency
    {
        private readonly ConcurrentDictionary<(int TenantId, long CutoffTicks), byte>
            _enabled = new();

        public Task<AQGreenCommissionStructuralModel> SelectAsync(
            int tenantId,
            DateTime commissionCutoffUtc) =>
            Task.FromResult(_enabled.ContainsKey((tenantId, commissionCutoffUtc.Ticks))
                ? AQGreenCommissionStructuralModel.PlacementV2
                : AQGreenCommissionStructuralModel.LegacyV1);

        public IDisposable Enable(int tenantId, DateTime commissionCutoffUtc)
        {
            var key = (tenantId, commissionCutoffUtc.Ticks);
            _enabled[key] = 0;
            return new ResetScope(() => _enabled.TryRemove(key, out _));
        }
    }

    public sealed class AQGreenV2ContinuousSalesReviewGate
        : IAQGreenWeeklySalesReviewGate, ISingletonDependency
    {
        private readonly ConcurrentDictionary<int, byte> _enabled = new();

        public Task<bool> IsEnabledAsync(int tenantId) =>
            Task.FromResult(_enabled.ContainsKey(tenantId));

        public IDisposable Enable(int tenantId)
        {
            _enabled[tenantId] = 0;
            return new ResetScope(() => _enabled.TryRemove(tenantId, out _));
        }
    }

    public sealed class AQGreenV2ContinuousSalesClock
        : IAQGreenWeeklySalesEligibilityClock, ISingletonDependency
    {
        private readonly AQGreenWeeklySalesEligibilityClock _productionClock;
        private readonly AsyncLocal<DateTime?> _testUtcNow = new();

        public AQGreenV2ContinuousSalesClock(
            AQGreenWeeklySalesEligibilityClock productionClock)
        {
            _productionClock = productionClock;
        }

        public Task<DateTime> GetUtcNowAsync(
            CancellationToken cancellationToken = default) =>
            _testUtcNow.Value.HasValue
                ? Task.FromResult(_testUtcNow.Value.Value)
                : _productionClock.GetUtcNowAsync(cancellationToken);

        public IDisposable Set(DateTime utcNow)
        {
            if (utcNow == default || utcNow.Kind != DateTimeKind.Utc)
                throw new ArgumentException("A UTC test clock value is required.", nameof(utcNow));
            var previous = _testUtcNow.Value;
            _testUtcNow.Value = utcNow;
            return new ResetScope(() => _testUtcNow.Value = previous);
        }
    }

    internal sealed class ResetScope : IDisposable
    {
        private Action _reset;

        public ResetScope(Action reset)
        {
            _reset = reset ?? throw new ArgumentNullException(nameof(reset));
        }

        public void Dispose() => Interlocked.Exchange(ref _reset, null)?.Invoke();
    }
}
