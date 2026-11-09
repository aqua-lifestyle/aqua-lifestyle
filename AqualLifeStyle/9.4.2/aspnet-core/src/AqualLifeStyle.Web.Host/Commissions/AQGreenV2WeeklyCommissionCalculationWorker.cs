using System;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Uow;
using Abp.Threading.BackgroundWorkers;
using Abp.Threading.Timers;
using AqualLifeStyle.Application.Admin.Commissions;
using AqualLifeStyle.Application.ProgrammeParticipations;
using AqualLifeStyle.Configuration;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AqualLifeStyle.Web.Host.Commissions
{
    /// <summary>
    /// AQGreen V2-only scheduled commission calculation. This periodic worker is
    /// registered independently of ABP's general BackgroundJobs switch. It uses
    /// the committed Yoco business lease and selected D10 worker authority before
    /// invoking the existing AQGreen calculator. It has no Onyx, travel-benefit,
    /// payout, or payment-provider dependency.
    /// </summary>
    public class AQGreenV2WeeklyCommissionCalculationWorker
        : AsyncPeriodicBackgroundWorkerBase, ISingletonDependency
    {
        private const int DefaultIntervalMinutes = 1440;
        private const string WeeklySetting = "App:WeeklyCommissions:Enabled";
        private const string BackgroundJobsSetting = "App:BackgroundJobs:Enabled";
        private const string FinancialOperationsSetting = "App:FinancialOperations:Enabled";

        private readonly IConfiguration _configuration;
        private readonly IUnitOfWorkManager _unitOfWorkManager;
        private readonly LatestClosedCommissionWeekResolver _closedWeekResolver;
        private readonly IWeeklyCommissionCalculator _commissionCalculator;
        private readonly IWeeklyCommissionCalculationLock _calculationLock;
        private readonly IYocoCutoverStore _cutoverStore;
        private readonly IAQGreenV2WeeklyCommissionReadiness _readiness;
        private readonly ILogger<AQGreenV2WeeklyCommissionCalculationWorker> _logger;

        public AQGreenV2WeeklyCommissionCalculationWorker(
            AbpAsyncTimer timer,
            IConfiguration configuration,
            IUnitOfWorkManager unitOfWorkManager,
            LatestClosedCommissionWeekResolver closedWeekResolver,
            IWeeklyCommissionCalculator commissionCalculator,
            IWeeklyCommissionCalculationLock calculationLock,
            IYocoCutoverStore cutoverStore,
            IAQGreenV2WeeklyCommissionReadiness readiness,
            ILogger<AQGreenV2WeeklyCommissionCalculationWorker> logger)
            : base(timer)
        {
            _configuration = configuration;
            _unitOfWorkManager = unitOfWorkManager;
            _closedWeekResolver = closedWeekResolver;
            _commissionCalculator = commissionCalculator;
            _calculationLock = calculationLock;
            _cutoverStore = cutoverStore;
            _readiness = readiness;
            _logger = logger;
            Timer.Period = checked(PositiveIntervalMinutes(
                configuration["App:WeeklyCommissions:IntervalMinutes"], DefaultIntervalMinutes) * 60 * 1000);
            Timer.RunOnStart = true;
        }

        protected override async Task DoWorkAsync()
        {
            if (!IsDedicatedRuntimeEnabled()) return;

            var nowUtc = DateTime.UtcNow;
            var closedWeek = _closedWeekResolver.Resolve(nowUtc);
            try
            {
                // This shared lease proves committed Normal and clean cutover
                // state, and prevents a phase transition during the write.
                var liveLease = await _cutoverStore.EnterBusinessAsync();
                try
                {
                    var decision = await _readiness.RequireEligibleDecisionAsync();
                    if (closedWeek.PeriodEndUtc < decision.V2EffectiveAtUtc)
                    {
                        _logger.LogInformation(
                            "ProgrammeEngineAlert {AlertType}: AQGreen V2 weekly calculation deferred until the first eligible closed week",
                            "aqgreen_v2_weekly_calculation_not_yet_effective");
                        return;
                    }

                    using (var uow = _unitOfWorkManager.Begin(new UnitOfWorkOptions { IsTransactional = true }))
                    {
                        await _calculationLock.AcquireAsync();
                        var result = await _commissionCalculator.CalculateEntryAsync(
                            decision.TenantId, closedWeek, nowUtc);
                        await uow.CompleteAsync();
                        _logger.LogInformation(
                            "ProgrammeEngineAlert {AlertType}: AQGreen V2 weekly calculation completed tenant={TenantId} period={PeriodStart}..{PeriodEnd} alreadyCalculated={AlreadyCalculated} evaluated={Evaluated} created={Created} held={Held}",
                            "aqgreen_v2_weekly_commission_calculation_completed",
                            decision.TenantId,
                            closedWeek.PeriodStartUtc,
                            closedWeek.PeriodEndUtc,
                            result.WasAlreadyCalculated,
                            result.EvaluatedCount,
                            result.RecordsCreated,
                            result.HeldCount);
                    }
                }
                finally
                {
                    await liveLease.DisposeAsync();
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                // Held, invalid, or failed attempts create no partial period;
                // the next periodic wake retries the same latest closed week.
                _logger.LogError(
                    "ProgrammeEngineAlert {AlertType}: AQGreen V2 weekly commission calculation was held or failed failureType={FailureType} period={PeriodStart}..{PeriodEnd}",
                    "aqgreen_v2_weekly_commission_calculation_failed",
                    exception.GetType().Name,
                    closedWeek.PeriodStartUtc,
                    closedWeek.PeriodEndUtc);
            }
        }

        private bool IsDedicatedRuntimeEnabled()
        {
            if (ApplicationRecoveryMode.IsEnabled(_configuration) ||
                !_configuration.GetValue<bool>(WeeklySetting) ||
                !bool.TryParse(_configuration["App:AQGreenV2:Commissioning:Enabled"], out var commissioningEnabled) ||
                !commissioningEnabled ||
                _configuration["App:AQGreenV2:Commissioning:AdmissionMode"] != "CLEAN_START")
                return false;

            // These are explicit profile requirements, not default-off guesses.
            if (!ExplicitlyFalse(BackgroundJobsSetting) ||
                !ExplicitlyFalse("App:EntryMonthlyObligations:Enabled") ||
                !ExplicitlyFalse("App:AutomaticPayouts:Enabled") ||
                !ExplicitlyFalse("AQGreenV2Demo:Enabled") ||
                !ExplicitlyFalse("AQGreenV2Pilot:Enabled") ||
                !ExplicitlyFalse("SEED_DEMO_DATA") ||
                _configuration["Yoco:Mode"] != "live" ||
                !bool.TryParse(_configuration["Bird:Enabled"], out var birdEnabled) ||
                !birdEnabled ||
                !bool.TryParse(_configuration[FinancialOperationsSetting], out var financialOperations) ||
                !financialOperations)
                return false;

            var cutover = new YocoCutoverOptions(_configuration);
            return cutover.Enabled && !cutover.CompatibilityReceiver;
        }

        private bool ExplicitlyFalse(string key) =>
            bool.TryParse(_configuration[key], out var value) && !value;

        private static int PositiveIntervalMinutes(string configuredValue, int defaultValue) =>
            int.TryParse(configuredValue, out var minutes) && minutes > 0 ? minutes : defaultValue;
    }
}
