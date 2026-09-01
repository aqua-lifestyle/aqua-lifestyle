using System;
using System.Collections.Generic;
using System.Linq;
using System.Transactions;
using System.Threading.Tasks;
using Abp.Application.Services.Dto;
using Abp.Auditing;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.Runtime.Session;
using Abp.Timing;
using AqualLifeStyle.Application.Admin.Commissions.Dto;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.Admin.Commissions
{
    [Audited]
    public class AdminCommissionAppService
        : AdminAppServiceBase, IAdminCommissionAppService
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly IRepository<EntryCommissionPeriod, Guid> _entryPeriodRepository;
        private readonly IRepository<OnyxCommissionPeriod, Guid> _onyxPeriodRepository;
        private readonly IRepository<EntryWeeklyCommission, Guid> _entryCommissionRepository;
        private readonly IRepository<OnyxWeeklyCommission, Guid> _onyxCommissionRepository;
        private readonly LatestClosedCommissionWeekResolver _closedWeekResolver;
        private readonly IWeeklyCommissionCalculator _commissionCalculator;
        private readonly IWeeklyCommissionCalculationLock _calculationLock;
        private readonly IWeeklyCommissionPayoutMutationLock _payoutMutationLock;
        private readonly IRepository<Tenant, int> _tenantRepository;
        private readonly IUnitOfWorkManager _unitOfWorkManager;

        public AdminCommissionAppService(
            ICustomerRepository customerRepository,
            IRepository<EntryCommissionPeriod, Guid> entryPeriodRepository,
            IRepository<OnyxCommissionPeriod, Guid> onyxPeriodRepository,
            IRepository<EntryWeeklyCommission, Guid> entryCommissionRepository,
            IRepository<OnyxWeeklyCommission, Guid> onyxCommissionRepository,
            LatestClosedCommissionWeekResolver closedWeekResolver,
            IWeeklyCommissionCalculator commissionCalculator,
            IWeeklyCommissionCalculationLock calculationLock,
            IWeeklyCommissionPayoutMutationLock payoutMutationLock,
            IRepository<Tenant, int> tenantRepository,
            IUnitOfWorkManager unitOfWorkManager)
        {
            _customerRepository = customerRepository;
            _entryPeriodRepository = entryPeriodRepository;
            _onyxPeriodRepository = onyxPeriodRepository;
            _entryCommissionRepository = entryCommissionRepository;
            _onyxCommissionRepository = onyxCommissionRepository;
            _closedWeekResolver = closedWeekResolver;
            _commissionCalculator = commissionCalculator;
            _calculationLock = calculationLock;
            _payoutMutationLock = payoutMutationLock;
            _tenantRepository = tenantRepository;
            _unitOfWorkManager = unitOfWorkManager;
        }

        [AbpAuthorize(AquaPermissions.Admin.Commissions.View)]
        public async Task<PagedResultDto<AdminWeeklyCommissionDto>> GetAllAsync(
            AdminCommissionListInput input)
        {
            input ??= new AdminCommissionListInput();
            ValidateRequestedTenant(input.TenantId, "Commission");
            if (!AbpSession.TenantId.HasValue &&
                !await PermissionChecker.IsGrantedAsync(AquaPermissions.Admin.AllTenants))
            {
                throw new AbpAuthorizationException(
                    "Host-wide commission access requires permission to view all Areas.");
            }

            using (DisableAllTenantDataFiltersForHost())
            {
                return input.Programme == AdminCommissionProgramme.Onyx
                    ? await GetOnyxCommissionsAsync(input)
                    : await GetEntryCommissionsAsync(input);
            }
        }

        [UnitOfWork]
        [AbpAuthorize(
            AquaPermissions.Admin.Commissions.Calculate,
            AquaPermissions.Admin.AllTenants,
            RequireAllPermissions = true)]
        public async Task<CommissionCalculationResultDto>
            CalculateLatestClosedWeekAsync(
                CalculateLatestClosedCommissionWeekInput input)
        {
            if (input == null)
            {
                throw Failed(
                    "Commission calculation",
                    "Calculation details are required.");
            }

            var tenantId = ResolveTargetTenant(
                input.TenantId,
                "Commission",
                "calculation");
            var calculatedAt = Clock.Now.ToUniversalTime();
            var closedWeek = _closedWeekResolver.Resolve(calculatedAt);

            using (CurrentUnitOfWork.DisableFilter(
                       AbpDataFilters.MayHaveTenant,
                       AbpDataFilters.MustHaveTenant))
            {
                await _calculationLock.AcquireAsync();
                var result = input.Programme == AdminCommissionProgramme.Onyx
                    ? await _commissionCalculator.CalculateOnyxAsync(tenantId, closedWeek, calculatedAt)
                    : await _commissionCalculator.CalculateEntryAsync(tenantId, closedWeek, calculatedAt);
                Logger.Info(
                    $"Admin commission calculation actor={AbpSession.GetUserId()} " +
                    $"tenant={tenantId} programme={result.ProgrammeName} " +
                    $"period={result.PeriodStart:O}..{result.PeriodEnd:O} " +
                    $"alreadyCalculated={result.WasAlreadyCalculated} " +
                    $"recordsCreated={result.RecordsCreated}");
                return result;
            }
        }

        [AbpAuthorize(
            AquaPermissions.Admin.Commissions.Calculate,
            AquaPermissions.Admin.AllTenants,
            RequireAllPermissions = true)]
        public async Task<CommissionPeriodInventoryOutput> GetPeriodInventoryAsync(
            GetCommissionPeriodInventoryInput input)
        {
            RequireHostCommissionInventoryAccess();
            input = input ?? new GetCommissionPeriodInventoryInput();
            if (input.TenantId.HasValue)
            {
                ResolveTargetTenant(input.TenantId.Value, "Commission", "inventory");
            }

            ValidateInventoryProgramme(input.Programme);
            using (CurrentUnitOfWork.DisableFilter(
                       AbpDataFilters.MayHaveTenant,
                       AbpDataFilters.MustHaveTenant,
                       AbpDataFilters.SoftDelete))
            {
                var tenants = await _tenantRepository.GetAllListAsync(tenant =>
                    !input.TenantId.HasValue || tenant.Id == input.TenantId.Value);
                var tenantNames = tenants.ToDictionary(
                    tenant => tenant.Id,
                    tenant => tenant.Name);
                var periods = new List<CommissionPeriodInventoryDto>();

                if (input.Programme != CommissionInventoryProgramme.Onyx)
                {
                    await AddEntryInventoryAsync(input.TenantId, tenantNames, periods);
                }

                if (input.Programme != CommissionInventoryProgramme.AQGreen)
                {
                    await AddOnyxInventoryAsync(input.TenantId, tenantNames, periods);
                }

                MarkExactBoundaryDuplicates(periods);
                var latestClosedWeek = _closedWeekResolver.Resolve(
                    Clock.Now.ToUniversalTime());
                return new CommissionPeriodInventoryOutput
                {
                    LatestClosedCycleStartUtc = latestClosedWeek.PeriodStartUtc,
                    LatestClosedCycleEndUtc = latestClosedWeek.PeriodEndUtc,
                    Periods = periods
                        .OrderBy(item => item.TenantId)
                        .ThenBy(item => item.ProgrammeName)
                        .ThenBy(item => item.PeriodStart)
                        .ToList(),
                    ProgrammeBoundaries = BuildPeriodBoundaries(
                        tenants,
                        input.Programme,
                        periods,
                        latestClosedWeek)
                };
            }
        }

        [UnitOfWork(IsDisabled = true)]
        [AbpAuthorize(
            AquaPermissions.Admin.Commissions.Release,
            AquaPermissions.Admin.AllTenants,
            RequireAllPermissions = true)]
        public async Task ReleaseAsync(ReleaseWeeklyEarningInput input)
        {
            ValidateMutationInput(
                input?.Id ?? Guid.Empty,
                input?.Justification,
                "Weekly earnings release");
            using (var uow = _unitOfWorkManager.Begin(new UnitOfWorkOptions
                   {
                       IsTransactional = true,
                       IsolationLevel = IsolationLevel.ReadCommitted
                   }))
            using (CurrentUnitOfWork.DisableFilter(
                       AbpDataFilters.MayHaveTenant,
                       AbpDataFilters.MustHaveTenant))
            {
                await AcquirePayoutMutationLockAsync(input.Programme, input.Id);
                var releasedAt = Clock.Now.ToUniversalTime();
                if (input.Programme == AdminCommissionProgramme.Onyx)
                {
                    var commission = await GetOnyxCommissionAsync(
                        input.Id,
                        "Weekly earnings release");
                    if (commission.PayoutStatus !=
                        WeeklyCommissionPayoutStatus.Released &&
                        commission.PayoutStatus !=
                        WeeklyCommissionPayoutStatus.Paid)
                    {
                        TryMutation(
                            () => commission.ReleaseEligiblePayout(releasedAt),
                            "Weekly earnings release");
                        LogMutation(
                            commission.TenantId,
                            commission.Id,
                            "Onyx",
                            "released for payment",
                            input.Justification);
                    }
                }
                else
                {
                    var commission = await GetEntryCommissionAsync(
                        input.Id,
                        "Weekly earnings release");
                    if (commission.PayoutStatus !=
                        WeeklyCommissionPayoutStatus.Released &&
                        commission.PayoutStatus !=
                        WeeklyCommissionPayoutStatus.Paid)
                    {
                        TryMutation(
                            () => commission.ReleaseEligiblePayout(releasedAt),
                            "Weekly earnings release");
                        LogMutation(
                            commission.TenantId,
                            commission.Id,
                            "AQGreen",
                            "released for payment",
                            input.Justification);
                    }
                }

                await CurrentUnitOfWork.SaveChangesAsync();
                await uow.CompleteAsync();
            }
        }

        [UnitOfWork(IsDisabled = true)]
        [AbpAuthorize(
            AquaPermissions.Admin.Commissions.RecordPayment,
            AquaPermissions.Admin.AllTenants,
            RequireAllPermissions = true)]
        public async Task RecordPaymentAsync(
            RecordWeeklyEarningPaymentInput input)
        {
            ValidateMutationInput(
                input?.Id ?? Guid.Empty,
                input?.Justification,
                "Weekly earnings payment");
            if (string.IsNullOrWhiteSpace(input.PaymentReference) ||
                input.PaymentReference.Trim().Length > 128)
            {
                throw Failed(
                    "Weekly earnings payment",
                    "A valid external payment reference is required.");
            }

            var paymentReference = input.PaymentReference.Trim();
            using (var uow = _unitOfWorkManager.Begin(new UnitOfWorkOptions
                   {
                       IsTransactional = true,
                       IsolationLevel = IsolationLevel.ReadCommitted
                   }))
            using (CurrentUnitOfWork.DisableFilter(
                       AbpDataFilters.MayHaveTenant,
                       AbpDataFilters.MustHaveTenant))
            {
                await AcquirePayoutMutationLockAsync(input.Programme, input.Id);
                var paidAt = Clock.Now.ToUniversalTime();
                if (input.Programme == AdminCommissionProgramme.Onyx)
                {
                    var commission = await GetOnyxCommissionAsync(
                        input.Id,
                        "Weekly earnings payment");
                    RecordPayment(
                        commission.PayoutStatus,
                        commission.PaymentReference,
                        paymentReference,
                        () => commission.MarkPaid(paidAt, paymentReference));
                    LogMutation(
                        commission.TenantId,
                        commission.Id,
                        "Onyx",
                        $"external payment recorded reference={SanitizeJustification(paymentReference)}",
                        input.Justification);
                }
                else
                {
                    var commission = await GetEntryCommissionAsync(
                        input.Id,
                        "Weekly earnings payment");
                    RecordPayment(
                        commission.PayoutStatus,
                        commission.PaymentReference,
                        paymentReference,
                        () => commission.MarkPaid(paidAt, paymentReference));
                    LogMutation(
                        commission.TenantId,
                        commission.Id,
                        "AQGreen",
                        $"external payment recorded reference={SanitizeJustification(paymentReference)}",
                        input.Justification);
                }

                await CurrentUnitOfWork.SaveChangesAsync();
                await uow.CompleteAsync();
            }
        }

        private Task AcquirePayoutMutationLockAsync(
            AdminCommissionProgramme programme,
            Guid commissionId)
        {
            return programme == AdminCommissionProgramme.Onyx
                ? _payoutMutationLock.AcquireOnyxAsync(commissionId)
                : _payoutMutationLock.AcquireEntryAsync(commissionId);
        }

        private async Task<PagedResultDto<AdminWeeklyCommissionDto>>
            GetEntryCommissionsAsync(AdminCommissionListInput input)
        {
            var query = ApplyScope(
                _entryCommissionRepository.GetAllIncluding(
                    commission => commission.Components),
                input.TenantId);
            var total = await query.CountAsync();
            var commissions = await query
                .OrderByDescending(commission => commission.CalculatedAt)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount)
                .ToListAsync();
            var periods = await LoadEntryPeriodsAsync(
                commissions.Select(commission => commission.CommissionPeriodId));
            var customers = await LoadCustomersAsync(
                commissions.Select(commission => commission.CustomerId));

            return new PagedResultDto<AdminWeeklyCommissionDto>(
                total,
                commissions.Select(commission => Map(
                    commission,
                    periods[commission.CommissionPeriodId],
                    customers[commission.CustomerId])).ToList());
        }

        private async Task<PagedResultDto<AdminWeeklyCommissionDto>>
            GetOnyxCommissionsAsync(AdminCommissionListInput input)
        {
            var query = ApplyScope(
                _onyxCommissionRepository.GetAllIncluding(
                    commission => commission.Components),
                input.TenantId);
            var total = await query.CountAsync();
            var commissions = await query
                .OrderByDescending(commission => commission.CalculatedAt)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount)
                .ToListAsync();
            var periods = await LoadOnyxPeriodsAsync(
                commissions.Select(commission => commission.CommissionPeriodId));
            var customers = await LoadCustomersAsync(
                commissions.Select(commission => commission.CustomerId));

            return new PagedResultDto<AdminWeeklyCommissionDto>(
                total,
                commissions.Select(commission => Map(
                    commission,
                    periods[commission.CommissionPeriodId],
                    customers[commission.CustomerId])).ToList());
        }

        private IQueryable<TCommission> ApplyScope<TCommission>(
            IQueryable<TCommission> query,
            int? requestedTenantId)
            where TCommission : class, Abp.Domain.Entities.IMustHaveTenant
        {
            if (AbpSession.TenantId.HasValue)
            {
                var tenantId = AbpSession.TenantId.Value;
                return query.Where(commission => commission.TenantId == tenantId);
            }

            return requestedTenantId.HasValue
                ? query.Where(commission =>
                    commission.TenantId == requestedTenantId.Value)
                : query;
        }

        private async Task<Dictionary<Guid, EntryCommissionPeriod>>
            LoadEntryPeriodsAsync(IEnumerable<Guid> periodIds)
        {
            var ids = periodIds.Distinct().ToList();
            return await _entryPeriodRepository.GetAll()
                .Where(period => ids.Contains(period.Id))
                .ToDictionaryAsync(period => period.Id);
        }

        private async Task<Dictionary<Guid, OnyxCommissionPeriod>>
            LoadOnyxPeriodsAsync(IEnumerable<Guid> periodIds)
        {
            var ids = periodIds.Distinct().ToList();
            return await _onyxPeriodRepository.GetAll()
                .Where(period => ids.Contains(period.Id))
                .ToDictionaryAsync(period => period.Id);
        }

        private async Task<Dictionary<int, Customer>> LoadCustomersAsync(
            IEnumerable<int> customerIds)
        {
            var ids = customerIds.Distinct().ToList();
            return await _customerRepository.GetAllIncluding(customer => customer.User)
                .Where(customer => ids.Contains(customer.Id))
                .ToDictionaryAsync(customer => customer.Id);
        }

        private static AdminWeeklyCommissionDto Map(
            EntryWeeklyCommission commission,
            EntryCommissionPeriod period,
            Customer customer) =>
            new AdminWeeklyCommissionDto
            {
                Id = commission.Id,
                TenantId = commission.TenantId,
                CustomerId = commission.CustomerId,
                CustomerName = customer.Name,
                Email = customer.User.EmailAddress,
                ProgrammeName = "AQGreen",
                PeriodStart = period.PeriodStart,
                PeriodEnd = period.PeriodEnd,
                TimeZoneId = period.TimeZoneId,
                HighestQualifiedLevel = commission.HighestQualifiedNetworkLevel,
                HighestCommissionedLevel = commission.HighestCommissionedLevel,
                StructuralModel = commission.StructuralModel.ToString(),
                CommissionDecisionRulesVersion =
                    commission.CommissionDecisionRulesVersion,
                TotalAmount = commission.TotalAmount,
                Currency = commission.Currency,
                Status = CommissionPayoutStatusPresenter.ToBusinessLabel(
                    commission.PayoutStatus),
                HoldReason = commission.HoldReason,
                ReleasedAt = commission.ReleasedAt,
                ReleaseReason = commission.ReleaseReason,
                PaidAt = commission.PaidAt,
                PaymentReference = commission.PaymentReference,
                CalculatedAt = commission.CalculatedAt,
                RulesVersion = commission.RulesVersion,
                Components = commission.Components
                    .OrderBy(component => component.Level)
                    .Select(component => new AdminCommissionComponentDto
                    {
                        Level = component.Level,
                        Amount = component.Amount
                    })
                    .ToList()
            };

        private static AdminWeeklyCommissionDto Map(
            OnyxWeeklyCommission commission,
            OnyxCommissionPeriod period,
            Customer customer) =>
            new AdminWeeklyCommissionDto
            {
                Id = commission.Id,
                TenantId = commission.TenantId,
                CustomerId = commission.CustomerId,
                CustomerName = customer.Name,
                Email = customer.User.EmailAddress,
                ProgrammeName = "Onyx",
                PeriodStart = period.PeriodStart,
                PeriodEnd = period.PeriodEnd,
                TimeZoneId = period.TimeZoneId,
                HighestQualifiedLevel = commission.HighestQualifiedNetworkLevel,
                HighestCommissionedLevel = commission.HighestCommissionedLevel,
                TotalAmount = commission.TotalAmount,
                Currency = commission.Currency,
                Status = CommissionPayoutStatusPresenter.ToBusinessLabel(
                    commission.PayoutStatus),
                ReleasedAt = commission.ReleasedAt,
                ReleaseReason = commission.ReleaseReason,
                PaidAt = commission.PaidAt,
                PaymentReference = commission.PaymentReference,
                CalculatedAt = commission.CalculatedAt,
                RulesVersion = commission.RulesVersion,
                Components = commission.Components
                    .OrderBy(component => component.Level)
                    .Select(component => new AdminCommissionComponentDto
                    {
                        Level = component.Level,
                        Amount = component.Amount
                    })
                    .ToList()
            };

        private async Task AddEntryInventoryAsync(
            int? tenantId,
            IReadOnlyDictionary<int, string> tenantNames,
            ICollection<CommissionPeriodInventoryDto> output)
        {
            var periods = await _entryPeriodRepository.GetAll()
                .Where(period => !tenantId.HasValue || period.TenantId == tenantId.Value)
                .ToListAsync();
            var byPeriod = new Dictionary<Guid, InventoryCommissionSummary>();
            var commissionRows = _entryCommissionRepository.GetAll()
                .AsNoTracking()
                .Where(commission =>
                    !tenantId.HasValue || commission.TenantId == tenantId.Value)
                .Select(commission => new InventoryCommissionRow
                {
                    PeriodId = commission.CommissionPeriodId,
                    Amount = commission.TotalAmount,
                    Status = commission.PayoutStatus,
                    IsDeleted = commission.IsDeleted
                });
            await foreach (var row in commissionRows.AsAsyncEnumerable())
            {
                if (!byPeriod.TryGetValue(row.PeriodId, out var summary))
                {
                    summary = new InventoryCommissionSummary();
                    byPeriod.Add(row.PeriodId, summary);
                }

                summary.Add(row.Amount, row.Status, row.IsDeleted);
            }

            foreach (var period in periods)
            {
                byPeriod.TryGetValue(period.Id, out var summary);
                output.Add(CreateInventoryItem(
                    period.TenantId,
                    tenantNames,
                    "AQGreen",
                    period.Id,
                    period.PeriodStart,
                    period.PeriodEnd,
                    period.TimeZoneId,
                    period.RulesVersion,
                    period.CalculatedAt,
                    period.IsDeleted,
                    period.DeletionTime,
                    summary ?? InventoryCommissionSummary.Empty));
            }
        }

        private async Task AddOnyxInventoryAsync(
            int? tenantId,
            IReadOnlyDictionary<int, string> tenantNames,
            ICollection<CommissionPeriodInventoryDto> output)
        {
            var periods = await _onyxPeriodRepository.GetAll()
                .Where(period => !tenantId.HasValue || period.TenantId == tenantId.Value)
                .ToListAsync();
            var byPeriod = new Dictionary<Guid, InventoryCommissionSummary>();
            var commissionRows = _onyxCommissionRepository.GetAll()
                .AsNoTracking()
                .Where(commission =>
                    !tenantId.HasValue || commission.TenantId == tenantId.Value)
                .Select(commission => new InventoryCommissionRow
                {
                    PeriodId = commission.CommissionPeriodId,
                    Amount = commission.TotalAmount,
                    Status = commission.PayoutStatus,
                    IsDeleted = commission.IsDeleted
                });
            await foreach (var row in commissionRows.AsAsyncEnumerable())
            {
                if (!byPeriod.TryGetValue(row.PeriodId, out var summary))
                {
                    summary = new InventoryCommissionSummary();
                    byPeriod.Add(row.PeriodId, summary);
                }

                summary.Add(row.Amount, row.Status, row.IsDeleted);
            }

            foreach (var period in periods)
            {
                byPeriod.TryGetValue(period.Id, out var summary);
                output.Add(CreateInventoryItem(
                    period.TenantId,
                    tenantNames,
                    "Onyx",
                    period.Id,
                    period.PeriodStart,
                    period.PeriodEnd,
                    period.TimeZoneId,
                    period.RulesVersion,
                    period.CalculatedAt,
                    period.IsDeleted,
                    period.DeletionTime,
                    summary ?? InventoryCommissionSummary.Empty));
            }
        }

        private CommissionPeriodInventoryDto CreateInventoryItem(
            int tenantId,
            IReadOnlyDictionary<int, string> tenantNames,
            string programmeName,
            Guid periodId,
            DateTime periodStart,
            DateTime periodEnd,
            string timeZoneId,
            string rulesVersion,
            DateTime calculatedAt,
            bool isDeleted,
            DateTime? deletionTime,
            InventoryCommissionSummary summary)
        {
            var classification = _closedWeekResolver.IsCanonicalCycle(
                    periodStart,
                    periodEnd,
                    timeZoneId)
                ? CommissionPeriodClassification.FridayToThursday
                : _closedWeekResolver.IsLegacyMondayToSundayCycle(
                    periodStart,
                    periodEnd,
                    timeZoneId)
                    ? CommissionPeriodClassification.LegacyMondayToSunday
                    : CommissionPeriodClassification.Malformed;

            return new CommissionPeriodInventoryDto
            {
                TenantId = tenantId,
                TenantName = tenantNames.TryGetValue(tenantId, out var name)
                    ? name
                    : $"Unknown tenant {tenantId}",
                ProgrammeName = programmeName,
                PeriodId = periodId,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                TimeZoneId = timeZoneId,
                RulesVersion = rulesVersion,
                CalculatedAt = calculatedAt,
                CommissionCount = summary.CommissionCount,
                NotEarnedCount = summary.NotEarnedCount,
                EarnedCount = summary.EarnedCount,
                HeldCount = summary.HeldCount,
                ReleasedCount = summary.ReleasedCount,
                PaidCount = summary.PaidCount,
                DeletedCommissionCount = summary.DeletedCommissionCount,
                TotalAmount = summary.TotalAmount,
                EarnedTotal = summary.EarnedTotal,
                HeldTotal = summary.HeldTotal,
                ReleasedTotal = summary.ReleasedTotal,
                PaidTotal = summary.PaidTotal,
                DeletedCommissionTotal = summary.DeletedCommissionTotal,
                Classification = classification,
                OverlapsFridayToThursdayCycle =
                    classification != CommissionPeriodClassification.FridayToThursday &&
                    _closedWeekResolver.OverlapsCanonicalCycle(periodStart, periodEnd),
                IsDeleted = isDeleted,
                DeletionTime = deletionTime
            };
        }

        private static void MarkExactBoundaryDuplicates(
            IEnumerable<CommissionPeriodInventoryDto> periods)
        {
            foreach (var duplicateGroup in periods
                .GroupBy(period => new
                {
                    period.TenantId,
                    period.ProgrammeName,
                    period.PeriodStart,
                    period.PeriodEnd
                })
                .Where(group => group.Count() > 1))
            {
                foreach (var period in duplicateGroup)
                {
                    period.HasExactBoundaryDuplicate = true;
                }
            }
        }

        private IReadOnlyList<CommissionPeriodBoundaryDto>
            BuildPeriodBoundaries(
                IEnumerable<Tenant> tenants,
                CommissionInventoryProgramme requestedProgramme,
                IReadOnlyCollection<CommissionPeriodInventoryDto> periods,
                ClosedCommissionWeek latestClosedWeek)
        {
            var programmes = requestedProgramme == CommissionInventoryProgramme.Both
                ? new[] { "AQGreen", "Onyx" }
                : new[] { requestedProgramme.ToString() };
            var result = new List<CommissionPeriodBoundaryDto>();

            foreach (var tenant in tenants)
            {
                foreach (var programme in programmes)
                {
                    var programmePeriods = periods
                        .Where(period =>
                            period.TenantId == tenant.Id &&
                            string.Equals(
                                period.ProgrammeName,
                                programme,
                                StringComparison.Ordinal))
                        .ToList();
                    var firstSafeStart = programmePeriods.Count == 0
                        ? latestClosedWeek.PeriodStartUtc
                        : _closedWeekResolver.ResolveFirstCycleStartAfter(
                            programmePeriods.Max(period => period.PeriodEnd));
                    var canonicalStarts = programmePeriods
                        .Where(period =>
                            period.Classification ==
                                CommissionPeriodClassification.FridayToThursday)
                        .Select(period => period.PeriodStart)
                        .ToHashSet();
                    var missingStarts = new List<DateTime>();
                    if (canonicalStarts.Count > 0 &&
                        canonicalStarts.Min() <= latestClosedWeek.PeriodStartUtc)
                    {
                        for (var cycleStart = canonicalStarts.Min();
                             cycleStart <= latestClosedWeek.PeriodStartUtc;
                             cycleStart = cycleStart.AddDays(7))
                        {
                            if (!canonicalStarts.Contains(cycleStart))
                            {
                                missingStarts.Add(cycleStart);
                            }
                        }
                    }
                    else
                    {
                        missingStarts.Add(latestClosedWeek.PeriodStartUtc);
                    }

                    result.Add(new CommissionPeriodBoundaryDto
                    {
                        TenantId = tenant.Id,
                        TenantName = tenant.Name,
                        ProgrammeName = programme,
                        FirstNonOverlappingCycleStartUtc = firstSafeStart,
                        MissingCanonicalCycles = missingStarts
                            .Select(cycleStart => new MissingCommissionCycleDto
                            {
                                CycleStartUtc = cycleStart,
                                IsLatestClosedCycle =
                                    cycleStart == latestClosedWeek.PeriodStartUtc,
                                Disposition = cycleStart == latestClosedWeek.PeriodStartUtc
                                    ? MissingCommissionCycleDisposition.PendingCalculation
                                    : MissingCommissionCycleDisposition
                                        .ManualFinancialReconciliationRequired,
                                Message = cycleStart == latestClosedWeek.PeriodStartUtc
                                    ? "Automatic calculation is pending for the latest closed cycle. It is generated deterministically by the automatic calculation pipeline at the cycle cutoff once calculation is enabled; it is not reconstructible from current state. Commission is only classified after verified payment."
                                    : "Historical calculation is unavailable because period-effective network, qualification, eligibility, and terms cannot be reconstructed reliably. Manual financial reconciliation required."
                            })
                            .ToList()
                    });
                }
            }

            return result;
        }

        private static void ValidateInventoryProgramme(
            CommissionInventoryProgramme programme)
        {
            if (programme != CommissionInventoryProgramme.AQGreen &&
                programme != CommissionInventoryProgramme.Onyx &&
                programme != CommissionInventoryProgramme.Both)
            {
                throw Failed(
                    "Commission period inventory",
                    "Select AQGreen, Onyx, or both programmes.");
            }
        }

        private void RequireHostCommissionInventoryAccess()
        {
            if (AbpSession.TenantId.HasValue)
            {
                throw new AbpAuthorizationException(
                    "Commission period inventory is restricted to the host.");
            }
        }

        private sealed class InventoryCommissionSummary
        {
            public static readonly InventoryCommissionSummary Empty =
                new InventoryCommissionSummary();

            public Guid PeriodId { get; set; }
            public int CommissionCount { get; set; }
            public int NotEarnedCount { get; set; }
            public int EarnedCount { get; set; }
            public int HeldCount { get; set; }
            public int ReleasedCount { get; set; }
            public int PaidCount { get; set; }
            public int DeletedCommissionCount { get; set; }
            public decimal TotalAmount { get; set; }
            public decimal EarnedTotal { get; set; }
            public decimal HeldTotal { get; set; }
            public decimal ReleasedTotal { get; set; }
            public decimal PaidTotal { get; set; }
            public decimal DeletedCommissionTotal { get; set; }

            public void Add(
                decimal amount,
                WeeklyCommissionPayoutStatus status,
                bool isDeleted)
            {
                CommissionCount++;
                TotalAmount += amount;
                if (isDeleted)
                {
                    DeletedCommissionCount++;
                    DeletedCommissionTotal += amount;
                }

                switch (status)
                {
                    case WeeklyCommissionPayoutStatus.NotEarned:
                        NotEarnedCount++;
                        break;
                    case WeeklyCommissionPayoutStatus.Earned:
                        EarnedCount++;
                        EarnedTotal += amount;
                        break;
                    case WeeklyCommissionPayoutStatus.Held:
                        HeldCount++;
                        HeldTotal += amount;
                        break;
                    case WeeklyCommissionPayoutStatus.Released:
                        ReleasedCount++;
                        ReleasedTotal += amount;
                        break;
                    case WeeklyCommissionPayoutStatus.Paid:
                        PaidCount++;
                        PaidTotal += amount;
                        break;
                }
            }
        }

        private sealed class InventoryCommissionRow
        {
            public Guid PeriodId { get; set; }
            public decimal Amount { get; set; }
            public WeeklyCommissionPayoutStatus Status { get; set; }
            public bool IsDeleted { get; set; }
        }


        private async Task<EntryWeeklyCommission> GetEntryCommissionAsync(
            Guid id,
            string operation)
        {
            var commission =
                await _entryCommissionRepository.FirstOrDefaultAsync(id);
            if (commission == null)
            {
                throw Failed(operation, "The AQGreen earning record was not found.");
            }

            return commission;
        }

        private async Task<OnyxWeeklyCommission> GetOnyxCommissionAsync(
            Guid id,
            string operation)
        {
            var commission =
                await _onyxCommissionRepository.FirstOrDefaultAsync(id);
            if (commission == null)
            {
                throw Failed(operation, "The Onyx earning record was not found.");
            }

            return commission;
        }

        private static void ValidateMutationInput(
            Guid id,
            string justification,
            string operation)
        {
            if (id == Guid.Empty)
            {
                throw Failed(operation, "A valid weekly earning record is required.");
            }

            if (string.IsNullOrWhiteSpace(justification) ||
                justification.Trim().Length < 3 ||
                justification.Trim().Length > 500)
            {
                throw Failed(
                    operation,
                    "A clear reason for the administrator action is required.");
            }
        }

        private static void RecordPayment(
            WeeklyCommissionPayoutStatus status,
            string existingReference,
            string requestedReference,
            Action markPaid)
        {
            if (status == WeeklyCommissionPayoutStatus.Paid)
            {
                if (!string.Equals(
                        existingReference,
                        requestedReference,
                        StringComparison.Ordinal))
                {
                    throw Failed(
                        "Weekly earnings payment",
                        "This earning was already recorded with a different payment reference.");
                }

                return;
            }

            TryMutation(markPaid, "Weekly earnings payment");
        }

        private static void TryMutation(Action mutation, string operation)
        {
            try
            {
                mutation();
            }
            catch (ArgumentException exception)
            {
                throw Failed(operation, exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                throw Failed(operation, exception.Message);
            }
        }

        private void LogMutation(
            int tenantId,
            Guid commissionId,
            string programme,
            string action,
            string justification)
        {
            Logger.Info(
                $"Admin weekly earnings {action} actor={AbpSession.GetUserId()} " +
                $"tenant={tenantId} programme={programme} commission={commissionId} " +
                $"justification={SanitizeJustification(justification)}");
        }
    }
}
