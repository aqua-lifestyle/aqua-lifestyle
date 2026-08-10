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
using AqualLifeStyle.Application.ProgrammeParticipations;
using AqualLifeStyle.MultiTenancy;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AqualLifeStyle.Web.Host.Commissions
{
    /// <summary>
    /// Automatically calculates AQGreen and Onyx commissions for every active
    /// tenant after a Friday-through-Thursday Africa/Johannesburg cycle closes.
    /// Each tenant and programme has its own transaction and failure boundary.
    ///
    /// Behaviour:
    /// <list type="bullet">
    /// <item>Calculates the latest closed cycle only; it never releases or marks
    /// commissions Paid.</item>
    /// <item>Retries are idempotent through programme period and commission
    /// uniqueness constraints.</item>
    /// <item>A tenant/programme failure does not suppress the other programme or
    /// later tenants.</item>
    /// <item>Travel eligibility is synchronized independently so it cannot roll
    /// back, or be rolled back by, Onyx commission calculation.</item>
    /// <item>Single-instance across deployments via the advisory lock
    /// (<see cref="IWeeklyCommissionCalculationLock"/>).</item>
    /// </list>
    ///
    /// Release and external payout remain manual Platform Administrator actions
    /// on Friday mornings, unchanged.
    ///
    /// Gated by <c>App:WeeklyCommissions:Enabled</c> (defaults to disabled);
    /// see <c>docs/verification/release-report-gap-closure.md §17</c>.
    /// </summary>
    public class WeeklyCommissionCalculationWorker
        : AsyncPeriodicBackgroundWorkerBase, ISingletonDependency
    {
        private enum CommissionProgramme
        {
            AQGreen,
            Onyx
        }

        private const int DefaultIntervalMinutes = 1440;

        private readonly IConfiguration _configuration;
        private readonly IUnitOfWorkManager _unitOfWorkManager;
        private readonly IRepository<Tenant, int> _tenantRepository;
        private readonly LatestClosedCommissionWeekResolver _closedWeekResolver;
        private readonly IWeeklyCommissionCalculator _commissionCalculator;
        private readonly IOnyxTravelBenefitSynchronizer _travelBenefitSynchronizer;
        private readonly IAreaActivationStateResolver _areaActivationStateResolver;
        private readonly IWeeklyCommissionCalculationLock _calculationLock;
        private readonly ILogger<WeeklyCommissionCalculationWorker> _logger;

        public WeeklyCommissionCalculationWorker(
            AbpAsyncTimer timer,
            IConfiguration configuration,
            IUnitOfWorkManager unitOfWorkManager,
            IRepository<Tenant, int> tenantRepository,
            LatestClosedCommissionWeekResolver closedWeekResolver,
            IWeeklyCommissionCalculator commissionCalculator,
            IOnyxTravelBenefitSynchronizer travelBenefitSynchronizer,
            IAreaActivationStateResolver areaActivationStateResolver,
            IWeeklyCommissionCalculationLock calculationLock,
            ILogger<WeeklyCommissionCalculationWorker> logger)
            : base(timer)
        {
            _configuration = configuration;
            _unitOfWorkManager = unitOfWorkManager;
            _tenantRepository = tenantRepository;
            _closedWeekResolver = closedWeekResolver;
            _commissionCalculator = commissionCalculator;
            _travelBenefitSynchronizer = travelBenefitSynchronizer;
            _areaActivationStateResolver = areaActivationStateResolver;
            _calculationLock = calculationLock;
            _logger = logger;
            Timer.Period = checked(PositiveIntervalMinutes(
                configuration["App:WeeklyCommissions:IntervalMinutes"],
                DefaultIntervalMinutes) * 60 * 1000);
            Timer.RunOnStart = true;
        }

        protected override async Task DoWorkAsync()
        {
            if (!_configuration.GetValue<bool>("App:WeeklyCommissions:Enabled"))
            {
                return;
            }

            var nowUtc = DateTime.UtcNow;
            var closedWeek = _closedWeekResolver.Resolve(nowUtc);
            var calculatedAt = nowUtc;
            var attempted = 0;
            var skipped = 0;
            var failed = 0;
            var travelGranted = 0;
            var travelActivated = 0;
            var travelFailed = 0;
            var inactiveAreas = 0;
            var unknownAreas = 0;

            try
            {
                var tenantIds = await EnumerateTenantIdsAsync();

                foreach (var tenantId in tenantIds)
                {
                    var areaState = await _areaActivationStateResolver.ResolveAsync(
                        tenantId,
                        closedWeek.PeriodEndUtc);
                    if (areaState.Status == AreaActivationStateResolutionStatus.Inactive)
                    {
                        inactiveAreas++;
                        continue;
                    }

                    if (areaState.Status == AreaActivationStateResolutionStatus.Unknown)
                    {
                        unknownAreas++;
                        _logger.LogError(
                            "ProgrammeEngineAlert {AlertType}: Area activation state is unknown for tenant={TenantId} periodEnd={PeriodEnd}",
                            "weekly_commission_area_state_unknown",
                            tenantId,
                            closedWeek.PeriodEndUtc);
                        continue;
                    }

                    foreach (var programme in new[]
                    {
                        CommissionProgramme.AQGreen,
                        CommissionProgramme.Onyx
                    })
                    {
                        attempted++;
                        try
                        {
                            var result = await CalculateForTenantAsync(
                                tenantId,
                                programme,
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
                                "ProgrammeEngineAlert {AlertType}: weekly commission calculation failed for tenant={TenantId} programme={Programme} period={PeriodStart}..{PeriodEnd}",
                                "weekly_commission_calculation_failed",
                                tenantId,
                                programme,
                                closedWeek.PeriodStartUtc,
                                closedWeek.PeriodEndUtc);
                        }
                    }

                    try
                    {
                        var travelResult = await SynchronizeTravelBenefitAsync(
                            tenantId,
                            closedWeek,
                            calculatedAt);
                        travelGranted += travelResult.GrantedCount;
                        travelActivated += travelResult.ActivatedCount;
                    }
                    catch (Exception exception)
                    {
                        travelFailed++;
                        _logger.LogError(
                            exception,
                            "ProgrammeEngineAlert {AlertType}: Onyx travel benefit synchronization failed for tenant={TenantId} period={PeriodStart}..{PeriodEnd}",
                            "onyx_travel_benefit_synchronization_failed",
                            tenantId,
                            closedWeek.PeriodStartUtc,
                            closedWeek.PeriodEndUtc);
                    }
                }

                _logger.LogInformation(
                    "ProgrammeEngineAlert {AlertType}: weekly commission engine processed period {PeriodStart}..{PeriodEnd}; programmeAttempts={Attempted}, skipped={Skipped}, failed={Failed}, inactiveAreas={InactiveAreas}, unknownAreas={UnknownAreas}, travelGranted={TravelGranted}, travelActivated={TravelActivated}, travelFailed={TravelFailed}",
                    "weekly_commission_calculation_run",
                    closedWeek.PeriodStartUtc,
                    closedWeek.PeriodEndUtc,
                    attempted,
                    skipped,
                    failed,
                    inactiveAreas,
                    unknownAreas,
                    travelGranted,
                    travelActivated,
                    travelFailed);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "ProgrammeEngineAlert {AlertType}: weekly commission engine run failed",
                    "weekly_commission_calculation_run_failed");
                throw;
            }
        }

        private async Task<int[]> EnumerateTenantIdsAsync()
        {
            using (var uow = _unitOfWorkManager.Begin())
            using (_unitOfWorkManager.Current.DisableFilter(
                AbpDataFilters.MayHaveTenant,
                AbpDataFilters.MustHaveTenant))
            {
                var tenantIds = (await _tenantRepository.GetAllListAsync())
                    .Select(tenant => tenant.Id)
                    .ToArray();
                await uow.CompleteAsync();
                return tenantIds;
            }
        }

        private async Task<CommissionCalculationResultDto> CalculateForTenantAsync(
            int tenantId,
            CommissionProgramme programme,
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

                var result = programme == CommissionProgramme.Onyx
                    ? await _commissionCalculator.CalculateOnyxAsync(
                        tenantId,
                        closedWeek,
                        calculatedAt)
                    : await _commissionCalculator.CalculateEntryAsync(
                        tenantId,
                        closedWeek,
                        calculatedAt);

                await uow.CompleteAsync();
                return result;
            }
        }

        private async Task<OnyxTravelBenefitEligibilityResult>
            SynchronizeTravelBenefitAsync(
                int tenantId,
                ClosedCommissionWeek closedWeek,
                DateTime processedAt)
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
                var result = await _travelBenefitSynchronizer.SynchronizeAsync(
                    tenantId,
                    closedWeek,
                    processedAt);
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
