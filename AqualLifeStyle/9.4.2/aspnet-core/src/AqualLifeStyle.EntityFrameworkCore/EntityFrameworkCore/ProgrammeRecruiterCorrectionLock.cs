using System;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.EntityFrameworkCore;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.EntityFrameworkCore
{
    public sealed class ProgrammeRecruiterCorrectionLock
        : IProgrammeRecruiterCorrectionLock, ITransientDependency
    {
        private const long AQGreenLockKey = 0x4151475245454E;
        private const long OnyxLockKey = 0x4F4E5958;
        private readonly IDbContextProvider<AqualLifeStyleDbContext> _dbContextProvider;

        public ProgrammeRecruiterCorrectionLock(
            IDbContextProvider<AqualLifeStyleDbContext> dbContextProvider)
        {
            _dbContextProvider = dbContextProvider;
        }

        public async Task AcquireAsync(ProgrammeRecruiterNetwork network)
        {
            var context = _dbContextProvider.GetDbContext();
            var resource = network switch
            {
                ProgrammeRecruiterNetwork.AQGreen => "programme-recruiter-aqgreen",
                ProgrammeRecruiterNetwork.Onyx => "programme-recruiter-onyx",
                _ => throw new ArgumentOutOfRangeException(nameof(network), network, "Unknown programme network.")
            };

            if (context.Database.IsNpgsql())
            {
                var key = network == ProgrammeRecruiterNetwork.AQGreen
                    ? AQGreenLockKey
                    : OnyxLockKey;
                await context.Database.ExecuteSqlRawAsync(
                    "SELECT pg_advisory_xact_lock({0})",
                    key);
                return;
            }

            if (context.Database.IsSqlServer())
            {
                await context.Database.ExecuteSqlRawAsync(
                    "DECLARE @result int; " +
                    "EXEC @result = sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', " +
                    "@LockOwner = 'Transaction', @LockTimeout = 10000; " +
                    "IF @result < 0 THROW 51000, 'Unable to lock programme recruiter corrections.', 1;",
                    resource);
            }
        }
    }
}
