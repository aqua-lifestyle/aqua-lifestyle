using System;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Uow;
using Abp.Threading.BackgroundWorkers;
using Abp.Threading.Timers;
using AqualLifeStyle.Application.EntryMonthlyObligations;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AqualLifeStyle.Web.Host.ProgrammeEngine
{
    /// <summary>
    /// Runs the recurring AQGreen R600 monthly obligations. Gated by
    /// <c>App:EntryMonthlyObligations:Enabled</c> (defaults to disabled): until
    /// operations arm it, no obligation is created, assessed, or settled by the
    /// worker. New obligations are only scheduled when exactly one valid,
    /// effective host due-policy version exists. Assessment runs for existing
    /// obligations; payment application requires a separate obligation-linked
    /// provider workflow and is never guessed by this worker.
    /// </summary>
    public class EntryMonthlyObligationWorker
        : AsyncPeriodicBackgroundWorkerBase, ISingletonDependency
    {
        private const int DefaultIntervalMinutes = 60;

        private readonly IConfiguration _configuration;
        private readonly IUnitOfWorkManager _unitOfWorkManager;
        private readonly IEntryMonthlyObligationSchedulingLock _schedulingLock;
        private readonly IEntryMonthlyObligationDueDatePolicy _dueDatePolicy;
        private readonly IEntryMonthlyObligationScheduler _scheduler;
        private readonly ILogger<EntryMonthlyObligationWorker> _logger;

        public EntryMonthlyObligationWorker(
            AbpAsyncTimer timer,
            IConfiguration configuration,
            IUnitOfWorkManager unitOfWorkManager,
            IEntryMonthlyObligationSchedulingLock schedulingLock,
            IEntryMonthlyObligationDueDatePolicy dueDatePolicy,
            IEntryMonthlyObligationScheduler scheduler,
            ILogger<EntryMonthlyObligationWorker> logger)
            : base(timer)
        {
            _configuration = configuration;
            _unitOfWorkManager = unitOfWorkManager;
            _schedulingLock = schedulingLock;
            _dueDatePolicy = dueDatePolicy;
            _scheduler = scheduler;
            _logger = logger;
            Timer.Period = checked(PositiveIntervalMinutes(
                configuration["App:EntryMonthlyObligations:IntervalMinutes"],
                DefaultIntervalMinutes) * 60 * 1000);
        }

        protected override async Task DoWorkAsync()
        {
            if (!_configuration.GetValue<bool>("App:EntryMonthlyObligations:Enabled"))
            {
                return;
            }

            var nowUtc = GetUtcNow();
            var currentJohannesburgMonth = EntryMonthlyObligationDuePolicy
                .JohannesburgMonth(nowUtc);
            var periodYear = currentJohannesburgMonth.Year;
            var periodMonth = currentJohannesburgMonth.Month;

            try
            {
                using (var unitOfWork = _unitOfWorkManager.Begin())
                {
                    await _schedulingLock.AcquireAsync();

                    var dueDateResolution = await _dueDatePolicy.ResolveDueDateAsync(
                        periodYear,
                        periodMonth);
                    var created = 0;
                    if (dueDateResolution.IsResolved)
                    {
                        created = await _scheduler.EnsureObligationsForPeriodAsync(
                            periodYear,
                            periodMonth,
                            dueDateResolution.DueAtUtc.Value,
                            dueDateResolution.PolicyVersion);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "ProgrammeEngineAlert {AlertType}: AQGreen due-policy resolution failed with {ResolutionStatus}, so recurring obligations are not being scheduled; assessment still runs",
                            "aqgreen_monthly_due_policy_unresolved",
                            dueDateResolution.Status);
                    }

                    var assessed = await _scheduler.AssessObligationsAsync(nowUtc);

                    await unitOfWork.CompleteAsync();

                    _logger.LogInformation(
                        "ProgrammeEngineAlert {AlertType}: AQGreen monthly obligations processed for {PeriodYear}-{PeriodMonth:00}; Created={Created}, Assessed={Assessed}",
                        "aqgreen_monthly_obligations_processed",
                        periodYear,
                        periodMonth,
                        created,
                        assessed);
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "ProgrammeEngineAlert {AlertType}: AQGreen monthly obligation processing failed",
                    "aqgreen_monthly_obligation_processing_failed");
                throw;
            }
        }

        private static int PositiveIntervalMinutes(string configuredValue, int defaultValue)
        {
            return int.TryParse(configuredValue, out var minutes) && minutes > 0
                ? minutes
                : defaultValue;
        }

        /// <summary>
        /// Supplies the current UTC instant for deterministic worker verification.
        /// </summary>
        protected virtual DateTime GetUtcNow() => DateTime.UtcNow;
    }
}
