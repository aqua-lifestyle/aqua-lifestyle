using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Abp.Domain.Repositories;

namespace AqualLifeStyle.Domain.Email
{
    public interface ITransactionalEmailOutboxRepository
        : IRepository<TransactionalEmailOutboxMessage, Guid>
    {
        Task<bool> InsertIfMissingAsync(TransactionalEmailOutboxMessage message);
        Task DeleteByIdempotencyKeyAsync(string idempotencyKey);
        Task<IReadOnlyList<Guid>> GetEligibleMessageIdsAsync(
            DateTime now,
            DateTime staleBefore,
            int maximumCount);
        Task<TransactionalEmailOutboxMessage> TryClaimAsync(
            Guid messageId,
            Guid processingToken,
            DateTime now,
            DateTime staleBefore);
    }
}
