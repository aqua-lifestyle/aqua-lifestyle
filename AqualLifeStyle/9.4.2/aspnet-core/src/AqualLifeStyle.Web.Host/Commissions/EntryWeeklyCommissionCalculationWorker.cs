using System;
using System.Linq;
using System.Threading.Tasks;
using Abp;
using Abp.Dependency;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.Threading.BackgroundWorkers;
using Abp.Threading.Timers;
using AqualLifeStyle.Application.Admin.Commissions;
using AqualLifeStyle.Application.Admin.Commissions.Dto;
using AqualLifeStyle.MultiTenancy;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AqualLifeStyle.Web.Host.Commissions
{
    /// <summary>
    /// Automatically calculates AQGreen weekly commissions for every tenant once
    /// a commission week (previous Monday 00:00 - Sunday 23:59 Africa/Johannesburg)
    /// has closed. Runs on a daily cadence (configurable) and resolves the latest
    /// closed week via <see cref="LatestClosedCommissionWeekResolver"/>, so each
    /// week's commissions are produced promptly and reliably (robust to host
    /// restarts on the nominal Monday slot) without ever computing the open
    /// current week.
    ///
    /// Behaviour:
    /// <list type="bullet">
    /// <item>Calculates <em>only</em> the latest closed week, creating Earned
    /// (or Held/NotEarned) commission records per the authoritative
    /// <see cref="EntryWeeklyCommissionCalculator"/> — it never releases and
    /// never marks commissions Paid.</item>
    /// <item>Idempotent per tenant+week: a tenant-week that already has a period
    /// row is skipped (the database unique constraint on
    /// <c>EntryCommissionPeriods(TenantId, PeriodStart, PeriodEnd)</c> is the
    /// authoritative guard against duplicate outcomes across concurrent
    /// attempts).</item>
    /// <item>Isolates tenant failures: one tenant's calculation error does not
    /// prevent the other tenants from being calculated.</item>
    /// <item>Single-instance across deployments via the advisory lock
    /// (<see cref="IEntryWeeklyCommissionCalculationLock"/>).</item>
    /// </list>
    ///
    /// Release and external payout remain manual Platform Administrator actions
    /// on Friday mornings, unchanged.
    ///
    /// Gated by <c>App:EntryWeeklyCommissions:Enabled</c> (defaults to disabled);
    /// see <c>docs/verification/release-report-gap-closure.md §17</c>.
    ///
    /// Onyx auto-calculation is intentionally out of scope: Onyx week calculation
    /// triggers travel-benefit subscription side effects, so Onyx remains the
    /// admin-triggered path until a business decision is recorded.
    /// </summary>
    public class EntryWeeklyCommissionCalculationWorker
        : AsyncPeriodicBackgroundWorkerBase, ISingletonDependency
    {
        private const int DefaultIntervalMinutes = 1440;

        private readonly IConfiguration _configuration;
        private readonly IUnitOfWorkManager _unitOfWorkManager;
        private readonly IRepository<Tenant, int> _tenantRepository;
        private readonly LatestClosedCommissionWeekResolver _closedWeekResolver;
        private readonly IWeeklyCommissionCalculator _commissionCalculator;
        private readonly IEntryWeeklyCommissionCalculationLock _calculationLock;
        private readonly ILogger<EntryWeeklyCommissionCalculationWorker> _logger;

        public EntryWeeklyCommissionCalculationWorker(
            AbpAsyncTimer timer,
            IConfiguration configuration,
            IUnitOfWorkManager unitOfWorkManager,
            IRepository<Tenant, int> tenantRepository,
            LatestClosedCommissionWeekResolver closedWeekResolver,
            IWeeklyCommissionCalculator commissionCalculator,
            IEntryWeeklyCommissionCalculationLock calculationLock,
            ILogger<EntryWeeklyCommissionCalculationWorker> logger)
            : base(timer)
        {
            _configuration = configuration;
            _unitOfWorkManager = unitOfWorkManager;
            _tenantRepository = tenantRepository;
            _closedWeekResolver = closedWeekResolver;
            _commissionCalculator = commissionCalculator;
            _calculationLock = calculationLock;
            _logger = logger;
            Timer.Period = checked(PositiveIntervalMinutes(
                configuration["App:EntryWeeklyCommissions:IntervalMinutes"],
                DefaultIntervalMinutes) * 60 * 1000);
        }

        protected override async Task DoWorkAsync()
        {
            if (!_configuration.GetValue<bool>("App:EntryWeeklyCommissions:Enabled"))
            {
                return;
            }

            var nowUtc = DateTime.UtcNow;
            var closedWeek = _closedWeekResolver.Resolve(nowUtc);
            var calculatedAt = nowUtc;
            var attempted = 0;
            var skipped = 0;
            var failed = 0;

            try
            {
                var tenantIds = await EnumerateActiveTenantIdsAsync();

                foreach (var tenantId in tenantIds)
                {
                    attempted++;
                    try
                    {
                        var result = await CalculateForTenantAsync(
                            tenantId,
                            closedWeek,
                            calculatedAt);
                        if (result.WasAlreadyCalculated)
                        {
                            skipped++;
                        }
                    }
                    catch (Exception exception)
                    {
                        failed++;
                        _logger.LogError(
                            exception,
                            "ProgrammeEngineAlert {AlertType}: AQGreen weekly commission calculation failed for tenant={TenantId} period={PeriodStart}..{PeriodEnd}",
                            "aqgreen_weekly_commission_calculation_failed",
                            tenantId,
                            closedWeek.PeriodStartUtc,
                            closedWeek.PeriodEndUtc);
                    }
                }

                _logger.LogInformation(
                    "ProgrammeEngineAlert {AlertType}: AQGreen weekly commissions calculated for period {PeriodStart}..{PeriodEnd}; tenants={Attempted}, skipped={Skipped}, failed={Failed}",
                    "aqgreen_weekly_commission_calculation_run",
                    closedWeek.PeriodStartUtc,
                    closedWeek.PeriodEndUtc,
                    attempted,
                    skipped,
                    failed);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "ProgrammeEngineAlert {AlertType}: AQGreen weekly commission calculation run failed",
                    "aqgreen_weekly_commission_calculation_run_failed");
                throw;
            }
        }

        private async Task<int[]> EnumerateActiveTenantIdsAsync()
        {
            using (var uow = _unitOfWorkManager.Begin())
            using (_unitOfWorkManager.Current.DisableFilter(
                AbpDataFilters.MayHaveTenant,
                AbpDataFilters.MustHaveTenant))
            {
                var tenantIds = (await _tenantRepository
                    .GetAllListAsync(tenant => tenant.IsActive))
                    .Select(tenant => tenant.Id)
                    .ToArray();
                await uow.CompleteAsync();
                return tenantIds;
            }
        }

        private async Task<CommissionCalculationResultDto> CalculateForTenantAsync(
            int tenantId,
            ClosedCommissionWeek closedWeek,
            DateTime calculatedAt)
        {
            using (var uow = _unitOfWorkManager.Begin(new UnitOfWorkOptions
            {
                IsTransactional = true
            }))
            using (_unitOfWorkManager.Current.DisableFilter(
                AbpDataFilters.MayHaveTenant,
                AbpDataFilters.MustHaveTenant))
            {
                await _calculationLock.AcquireAsync();

                var result = await _commissionCalculator.CalculateEntryAsync(
                    tenantId,
                    closedWeek,
                    calculatedAt);

                await uow.CompleteAsync();
                return result;
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
