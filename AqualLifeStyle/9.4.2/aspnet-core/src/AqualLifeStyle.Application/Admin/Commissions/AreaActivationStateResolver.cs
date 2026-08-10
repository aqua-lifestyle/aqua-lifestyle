using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.Admin.Commissions
{
    public enum AreaActivationStateResolutionStatus
    {
        Unknown = 0,
        Active = 1,
        Inactive = 2
    }

    public sealed class AreaActivationStateResolution
    {
        public AreaActivationStateResolutionStatus Status { get; }
        public DateTime? EffectiveAt { get; }

        private AreaActivationStateResolution(
            AreaActivationStateResolutionStatus status,
            DateTime? effectiveAt)
        {
            Status = status;
            EffectiveAt = effectiveAt;
        }

        public static AreaActivationStateResolution Unknown() =>
            new AreaActivationStateResolution(
                AreaActivationStateResolutionStatus.Unknown,
                null);

        public static AreaActivationStateResolution Resolved(
            bool isActive,
            DateTime effectiveAt) =>
            new AreaActivationStateResolution(
                isActive
                    ? AreaActivationStateResolutionStatus.Active
                    : AreaActivationStateResolutionStatus.Inactive,
                effectiveAt);
    }

    public interface IAreaActivationStateResolver
    {
        Task<AreaActivationStateResolution> ResolveAsync(
            int tenantId,
            DateTime cutoffUtc);

        Task EnsureActiveAsync(int tenantId, DateTime cutoffUtc);
    }

    public class AreaActivationStateResolver
        : IAreaActivationStateResolver, ITransientDependency
    {
        private readonly IRepository<MultiTenancy.AreaActivationStateRecord, Guid>
            _repository;

        public AreaActivationStateResolver(
            IRepository<MultiTenancy.AreaActivationStateRecord, Guid> repository)
        {
            _repository = repository;
        }

        [UnitOfWork]
        public virtual async Task<AreaActivationStateResolution> ResolveAsync(
            int tenantId,
            DateTime cutoffUtc)
        {
            if (tenantId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tenantId));
            }

            if (cutoffUtc == default || cutoffUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("Area cutoff must be UTC.", nameof(cutoffUtc));
            }

            var record = await _repository.GetAll()
                .Where(item =>
                    item.TenantId == tenantId &&
                    item.EffectiveAt <= cutoffUtc)
                .OrderByDescending(item => item.EffectiveAt)
                .FirstOrDefaultAsync();
            return record == null
                ? AreaActivationStateResolution.Unknown()
                : AreaActivationStateResolution.Resolved(
                    record.IsActive,
                    record.EffectiveAt);
        }

        [UnitOfWork]
        public virtual async Task EnsureActiveAsync(int tenantId, DateTime cutoffUtc)
        {
            var resolution = await ResolveAsync(tenantId, cutoffUtc);
            if (resolution.Status != AreaActivationStateResolutionStatus.Active)
            {
                throw new AreaActivationStateUnavailableException(
                    tenantId,
                    cutoffUtc,
                    resolution.Status);
            }
        }
    }

    public sealed class AreaActivationStateUnavailableException
        : InvalidOperationException
    {
        public int TenantId { get; }
        public DateTime CutoffUtc { get; }
        public AreaActivationStateResolutionStatus Status { get; }

        public AreaActivationStateUnavailableException(
            int tenantId,
            DateTime cutoffUtc,
            AreaActivationStateResolutionStatus status)
            : base(
                status == AreaActivationStateResolutionStatus.Inactive
                    ? $"Area {tenantId} was inactive at cutoff {cutoffUtc:O}."
                    : $"Area {tenantId} activation state cannot be proven at cutoff {cutoffUtc:O}.")
        {
            TenantId = tenantId;
            CutoffUtc = cutoffUtc;
            Status = status;
        }
    }
}
