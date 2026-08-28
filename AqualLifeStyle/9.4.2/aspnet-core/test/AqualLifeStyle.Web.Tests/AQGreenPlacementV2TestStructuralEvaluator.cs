using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Abp.Dependency;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.EntityFrameworkCore;

namespace AqualLifeStyle.Web.Tests
{
    public sealed class AQGreenPlacementV2TestStructuralEvaluator
        : IAQGreenStructuralCompletionEvaluator, ISingletonDependency
    {
        private readonly AQGreenStructuralCompletionEvaluator _realEvaluator;
        private readonly ConcurrentDictionary<Guid, Func<int, Guid, DateTime,
            Task<AQGreenStructuralCompletionResult>>> _handlers = new();
        private readonly ConcurrentDictionary<Guid, int> _calls = new();

        public AQGreenPlacementV2TestStructuralEvaluator(
            AQGreenStructuralCompletionEvaluator realEvaluator)
        {
            _realEvaluator = realEvaluator;
        }

        public Task<AQGreenStructuralCompletionResult> EvaluateAsync(
            int tenantId,
            Guid participantId,
            DateTime cutoff,
            CancellationToken cancellationToken = default)
        {
            _calls.AddOrUpdate(participantId, 1, (_, count) => count + 1);
            return _handlers.TryGetValue(participantId, out var handler)
                ? handler(tenantId, participantId, cutoff)
                : _realEvaluator.EvaluateAsync(
                    tenantId,
                    participantId,
                    cutoff,
                    cancellationToken);
        }

        public IDisposable Return(
            Guid participantId,
            AQGreenStructuralCompletionLevel level,
            int depth1,
            int depth2,
            int depth3)
        {
            _handlers[participantId] = (tenantId, requestedParticipantId, cutoff) =>
                Task.FromResult(new AQGreenStructuralCompletionResult(
                    requestedParticipantId,
                    Guid.NewGuid(),
                    level,
                    depth1,
                    depth2,
                    depth3,
                    cutoff,
                    AQGreenPlacementRules.CurrentVersion));
            _calls.TryRemove(participantId, out _);
            return new ResetScope(_handlers, _calls, participantId);
        }

        public IDisposable Fail(
            Guid participantId,
            Func<Guid, DateTime, Exception> exceptionFactory)
        {
            _handlers[participantId] = (_, requestedParticipantId, cutoff) =>
                Task.FromException<AQGreenStructuralCompletionResult>(
                    exceptionFactory(requestedParticipantId, cutoff));
            _calls.TryRemove(participantId, out _);
            return new ResetScope(_handlers, _calls, participantId);
        }

        public int GetCallCount(Guid participantId) =>
            _calls.TryGetValue(participantId, out var count) ? count : 0;

        private sealed class ResetScope : IDisposable
        {
            private readonly ConcurrentDictionary<Guid, Func<int, Guid, DateTime,
                Task<AQGreenStructuralCompletionResult>>> _handlers;
            private readonly ConcurrentDictionary<Guid, int> _calls;
            private readonly Guid _participantId;

            public ResetScope(
                ConcurrentDictionary<Guid, Func<int, Guid, DateTime,
                    Task<AQGreenStructuralCompletionResult>>> handlers,
                ConcurrentDictionary<Guid, int> calls,
                Guid participantId)
            {
                _handlers = handlers;
                _calls = calls;
                _participantId = participantId;
            }

            public void Dispose()
            {
                _handlers.TryRemove(_participantId, out _);
                _calls.TryRemove(_participantId, out _);
            }
        }
    }
}
