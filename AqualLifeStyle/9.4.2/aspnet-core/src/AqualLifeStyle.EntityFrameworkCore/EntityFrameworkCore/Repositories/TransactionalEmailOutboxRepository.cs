using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.EntityFrameworkCore;
using AqualLifeStyle.Domain.Email;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.EntityFrameworkCore.Repositories
{
    public class TransactionalEmailOutboxRepository
        : AqualLifeStyleRepositoryBase<TransactionalEmailOutboxMessage, Guid>,
          ITransactionalEmailOutboxRepository
    {
        private readonly IDbContextProvider<AqualLifeStyleDbContext> _dbContextProvider;

        public TransactionalEmailOutboxRepository(
            IDbContextProvider<AqualLifeStyleDbContext> dbContextProvider)
            : base(dbContextProvider)
        {
            _dbContextProvider = dbContextProvider;
        }

        public async Task<bool> InsertIfMissingAsync(TransactionalEmailOutboxMessage message)
        {
            var context = _dbContextProvider.GetDbContext();
            await InsertAsync(message);
            try
            {
                await context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException exception) when (DatabaseUniqueConstraintDetector.Matches(
                exception,
                "IX_TransactionalEmailOutboxMessages_IdempotencyKey",
                "TransactionalEmailOutboxMessages.IdempotencyKey"))
            {
                context.Entry(message).State = EntityState.Detached;
                return false;
            }
        }

        public async Task DeleteByIdempotencyKeyAsync(string idempotencyKey)
            => await GetAll()
                .Where(candidate => candidate.IdempotencyKey == idempotencyKey)
                .ExecuteDeleteAsync();

        public async Task<IReadOnlyList<Guid>> GetEligibleMessageIdsAsync(
            DateTime now,
            DateTime staleBefore,
            int maximumCount)
        {
            return await GetAll()
                .Where(message =>
                    (message.Status == TransactionalEmailStatus.Pending && message.NextAttemptAt <= now) ||
                    (message.Status == TransactionalEmailStatus.Processing && message.ProcessingStartedAt < staleBefore))
                .OrderBy(message => message.NextAttemptAt)
                .Select(message => message.Id)
                .Take(maximumCount)
                .ToListAsync();
        }

        public async Task<TransactionalEmailOutboxMessage> TryClaimAsync(
            Guid messageId,
            Guid processingToken,
            DateTime now,
            DateTime staleBefore)
        {
            var affected = await GetAll()
                .Where(message =>
                    message.Id == messageId &&
                    ((message.Status == TransactionalEmailStatus.Pending && message.NextAttemptAt <= now) ||
                     (message.Status == TransactionalEmailStatus.Processing && message.ProcessingStartedAt < staleBefore)))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(message => message.Status, TransactionalEmailStatus.Processing)
                    .SetProperty(message => message.ProcessingStartedAt, now)
                    .SetProperty(message => message.ProcessingToken, processingToken)
                    .SetProperty(message => message.AttemptCount, message => message.AttemptCount + 1));

            return affected == 1
                ? await GetAll().AsNoTracking().SingleAsync(message => message.Id == messageId)
                : null;
        }
    }
}
