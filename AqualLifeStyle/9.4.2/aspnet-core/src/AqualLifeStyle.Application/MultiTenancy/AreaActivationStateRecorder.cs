using System;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Repositories;
using Abp.Runtime.Session;
using Abp.Timing;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.MultiTenancy
{
    public interface IAreaActivationStateRecorder
    {
        Task<bool> HasHistoryAsync(int tenantId);

        Task<AreaActivationStateRecord> RecordAsync(
            int tenantId,
            bool isActive,
            AreaActivationStateRecordKind kind,
            string justification,
            DateTime? effectiveAtUtc = null);
    }

    public class AreaActivationStateRecorder
        : IAreaActivationStateRecorder, ITransientDependency
    {
        private readonly IRepository<AreaActivationStateRecord, Guid> _repository;
        private readonly IAbpSession _session;

        public AreaActivationStateRecorder(
            IRepository<AreaActivationStateRecord, Guid> repository,
            IAbpSession session)
        {
            _repository = repository;
            _session = session;
        }

        public Task<bool> HasHistoryAsync(int tenantId)
        {
            return _repository.GetAll().AnyAsync(record => record.TenantId == tenantId);
        }

        public async Task<AreaActivationStateRecord> RecordAsync(
            int tenantId,
            bool isActive,
            AreaActivationStateRecordKind kind,
            string justification,
            DateTime? effectiveAtUtc = null)
        {
            var recordedAt = effectiveAtUtc ?? Clock.Now.ToUniversalTime();
            var record = AreaActivationStateRecord.Record(
                Guid.NewGuid(),
                tenantId,
                isActive,
                recordedAt,
                recordedAt,
                _session.UserId,
                justification,
                kind);
            await _repository.InsertAsync(record);
            return record;
        }
    }
}
