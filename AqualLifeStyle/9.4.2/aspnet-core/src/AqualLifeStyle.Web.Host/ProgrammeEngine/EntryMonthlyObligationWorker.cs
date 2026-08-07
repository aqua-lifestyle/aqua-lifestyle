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
    /// worker. New obligations are only scheduled once the due-date policy
    /// (PD-07) is defined via
    /// <c>App:EntryMonthlyObligations:DueDayOfMonth</c>. Assessment and payment
    /// allocation run regardless so that manually created obligations and
    /// confirmed monthly payments stay consistent.
    /// </summary>
    public sealed class EntryMonthlyObligationWorker
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

            var nowUtc = DateTime.UtcNow;
            var periodYear = nowUtc.Year;
            var periodMonth = nowUtc.Month;

            try
            {
                using (var unitOfWork = _unitOfWorkManager.Begin())
                {
                    await _schedulingLock.AcquireAsync();

                    var dueAt = _dueDatePolicy.ResolveDueDate(periodYear, periodMonth);
                    var created = 0;
                    if (dueAt.HasValue)
                    {
                        created = await _scheduler.EnsureObligationsForPeriodAsync(
                            periodYear,
                            periodMonth,
                            dueAt.Value);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "ProgrammeEngineAlert {AlertType}: the AQGreen monthly due-date policy (PD-07) is undefined, so recurring obligations are not being scheduled; assessment and payment allocation still run",
                            "aqgreen_monthly_due_date_undefined");
                    }

                    var assessed = await _scheduler.AssessObligationsAsync(nowUtc);
                    var allocated = await _scheduler.AllocateConfirmedMonthlyPaymentsAsync();

                    await unitOfWork.CompleteAsync();

                    _logger.LogInformation(
                        "ProgrammeEngineAlert {AlertType}: AQGreen monthly obligations processed for {PeriodYear}-{PeriodMonth:00}; Created={Created}, Assessed={Assessed}, Allocated={Allocated}",
                        "aqgreen_monthly_obligations_processed",
                        periodYear,
                        periodMonth,
                        created,
                        assessed,
                        allocated);
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
    }
}
