using System;
using System.Collections.Generic;
using System.Linq;
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
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.Admin.Commissions
{
    [Audited]
    public class AdminCommissionAppService
        : AdminAppServiceBase, IAdminCommissionAppService
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly IRepository<EntryParticipation, Guid> _entryParticipationRepository;
        private readonly IRepository<OnyxParticipation, Guid> _onyxParticipationRepository;
        private readonly IRepository<EntryMonthlyObligation, Guid> _entryObligationRepository;
        private readonly IRepository<OnyxLoanAgreement, Guid> _loanAgreementRepository;
        private readonly IRepository<EntryCommissionPeriod, Guid> _entryPeriodRepository;
        private readonly IRepository<OnyxCommissionPeriod, Guid> _onyxPeriodRepository;
        private readonly IRepository<EntryWeeklyCommission, Guid> _entryCommissionRepository;
        private readonly IRepository<OnyxWeeklyCommission, Guid> _onyxCommissionRepository;
        private readonly ICurrentCommissionTermsProvider _termsProvider;
        private readonly LatestClosedCommissionWeekResolver _closedWeekResolver;

        public AdminCommissionAppService(
            ICustomerRepository customerRepository,
            IRepository<EntryParticipation, Guid> entryParticipationRepository,
            IRepository<OnyxParticipation, Guid> onyxParticipationRepository,
            IRepository<EntryMonthlyObligation, Guid> entryObligationRepository,
            IRepository<OnyxLoanAgreement, Guid> loanAgreementRepository,
            IRepository<EntryCommissionPeriod, Guid> entryPeriodRepository,
            IRepository<OnyxCommissionPeriod, Guid> onyxPeriodRepository,
            IRepository<EntryWeeklyCommission, Guid> entryCommissionRepository,
            IRepository<OnyxWeeklyCommission, Guid> onyxCommissionRepository,
            ICurrentCommissionTermsProvider termsProvider,
            LatestClosedCommissionWeekResolver closedWeekResolver)
        {
            _customerRepository = customerRepository;
            _entryParticipationRepository = entryParticipationRepository;
            _onyxParticipationRepository = onyxParticipationRepository;
            _entryObligationRepository = entryObligationRepository;
            _loanAgreementRepository = loanAgreementRepository;
            _entryPeriodRepository = entryPeriodRepository;
            _onyxPeriodRepository = onyxPeriodRepository;
            _entryCommissionRepository = entryCommissionRepository;
            _onyxCommissionRepository = onyxCommissionRepository;
            _termsProvider = termsProvider;
            _closedWeekResolver = closedWeekResolver;
        }

        [AbpAuthorize(AquaPermissions.Admin.Commissions.View)]
        public async Task<PagedResultDto<AdminWeeklyCommissionDto>> GetAllAsync(
            AdminCommissionListInput input)
        {
            input ??= new AdminCommissionListInput();
            ValidateRequestedTenant(input.TenantId, "Commission");
            if (!AbpSession.TenantId.HasValue &&
                !input.TenantId.HasValue &&
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
                var result = input.Programme == AdminCommissionProgramme.Onyx
                    ? await CalculateOnyxAsync(tenantId, closedWeek, calculatedAt)
                    : await CalculateEntryAsync(tenantId, closedWeek, calculatedAt);
                Logger.Info(
                    $"Admin commission calculation actor={AbpSession.GetUserId()} " +
                    $"tenant={tenantId} programme={result.ProgrammeName} " +
                    $"period={result.PeriodStart:O}..{result.PeriodEnd:O} " +
                    $"alreadyCalculated={result.WasAlreadyCalculated} " +
                    $"recordsCreated={result.RecordsCreated}");
                return result;
            }
        }

        [UnitOfWork]
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
            var releasedAt = Clock.Now.ToUniversalTime();

            using (CurrentUnitOfWork.DisableFilter(
                       AbpDataFilters.MayHaveTenant,
                       AbpDataFilters.MustHaveTenant))
            {
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
                    }

                    LogMutation(
                        commission.TenantId,
                        commission.Id,
                        "Onyx",
                        "released for payment",
                        input.Justification);
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
                    }

                    LogMutation(
                        commission.TenantId,
                        commission.Id,
                        "Entry",
                        "released for payment",
                        input.Justification);
                }

                await CurrentUnitOfWork.SaveChangesAsync();
            }
        }

        [UnitOfWork]
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
            var paidAt = Clock.Now.ToUniversalTime();
            using (CurrentUnitOfWork.DisableFilter(
                       AbpDataFilters.MayHaveTenant,
                       AbpDataFilters.MustHaveTenant))
            {
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
                        "Entry",
                        $"external payment recorded reference={SanitizeJustification(paymentReference)}",
                        input.Justification);
                }

                await CurrentUnitOfWork.SaveChangesAsync();
            }
        }

        private async Task<CommissionCalculationResultDto> CalculateEntryAsync(
            int tenantId,
            ClosedCommissionWeek closedWeek,
            DateTime calculatedAt)
        {
            var terms = _termsProvider.GetEntryTerms();
            var existingPeriod = await _entryPeriodRepository.FirstOrDefaultAsync(period =>
                period.TenantId == tenantId &&
                period.PeriodStart == closedWeek.PeriodStartUtc &&
                period.PeriodEnd == closedWeek.PeriodEndUtc);
            if (existingPeriod != null)
            {
                var existingCommissions = await _entryCommissionRepository.GetAll()
                    .Where(commission =>
                        commission.CommissionPeriodId == existingPeriod.Id)
                    .ToListAsync();
                return BuildResult(
                    existingPeriod.Id,
                    "Entry",
                    existingPeriod.PeriodStart,
                    existingPeriod.PeriodEnd,
                    existingPeriod.TimeZoneId,
                    existingCommissions.Select(commission =>
                        new CommissionSummaryRow(
                            commission.TotalAmount,
                            commission.PayoutStatus)),
                    terms.Currency,
                    wasAlreadyCalculated: true,
                    recordsCreated: 0);
            }

            var networkParticipations = await _entryParticipationRepository.GetAll()
                .Where(participation =>
                    participation.Status == EntryParticipationStatus.Active &&
                    participation.ActivatedAt <= closedWeek.PeriodEndUtc)
                .ToListAsync();
            var targetParticipations = networkParticipations
                .Where(participation => participation.TenantId == tenantId)
                .ToList();
            var targetParticipationIds = targetParticipations
                .Select(participation => participation.Id)
                .ToList();
            var obligations = await _entryObligationRepository.GetAll()
                .Where(obligation =>
                    targetParticipationIds.Contains(obligation.EntryParticipationId))
                .ToListAsync();
            var loanAgreements = await _loanAgreementRepository
                .GetAllIncluding(agreement => agreement.WeeklyRequirements)
                .Where(agreement =>
                    targetParticipationIds.Contains(agreement.EntryParticipationId))
                .ToListAsync();

            var period = EntryCommissionPeriod.CreateClosedPeriod(
                tenantId,
                closedWeek.PeriodStartUtc,
                closedWeek.PeriodEndUtc,
                closedWeek.TimeZoneId,
                calculatedAt,
                terms);
            await _entryPeriodRepository.InsertAsync(period);
            var calculator = new EntryWeeklyCommissionCalculator(
                new EntryNetworkQualificationEvaluator());
            var commissions = targetParticipations
                .Select(participation => calculator.Calculate(
                    participation,
                    period,
                    terms,
                    networkParticipations,
                    obligations,
                    loanAgreements))
                .ToList();
            foreach (var commission in commissions)
            {
                await _entryCommissionRepository.InsertAsync(commission);
            }

            await CurrentUnitOfWork.SaveChangesAsync();
            return BuildResult(
                period.Id,
                "Entry",
                period.PeriodStart,
                period.PeriodEnd,
                period.TimeZoneId,
                commissions.Select(commission =>
                    new CommissionSummaryRow(
                        commission.TotalAmount,
                        commission.PayoutStatus)),
                terms.Currency,
                wasAlreadyCalculated: false,
                recordsCreated: commissions.Count);
        }

        private async Task<CommissionCalculationResultDto> CalculateOnyxAsync(
            int tenantId,
            ClosedCommissionWeek closedWeek,
            DateTime calculatedAt)
        {
            var terms = _termsProvider.GetOnyxTerms();
            var existingPeriod = await _onyxPeriodRepository.FirstOrDefaultAsync(period =>
                period.TenantId == tenantId &&
                period.PeriodStart == closedWeek.PeriodStartUtc &&
                period.PeriodEnd == closedWeek.PeriodEndUtc);
            if (existingPeriod != null)
            {
                var existingCommissions = await _onyxCommissionRepository.GetAll()
                    .Where(commission =>
                        commission.CommissionPeriodId == existingPeriod.Id)
                    .ToListAsync();
                return BuildResult(
                    existingPeriod.Id,
                    "Onyx",
                    existingPeriod.PeriodStart,
                    existingPeriod.PeriodEnd,
                    existingPeriod.TimeZoneId,
                    existingCommissions.Select(commission =>
                        new CommissionSummaryRow(
                            commission.TotalAmount,
                            commission.PayoutStatus)),
                    terms.Currency,
                    wasAlreadyCalculated: true,
                    recordsCreated: 0);
            }

            var networkParticipations = await _onyxParticipationRepository.GetAll()
                .Where(participation =>
                    participation.Status == OnyxParticipationStatus.Active &&
                    participation.ActivatedAt <= closedWeek.PeriodEndUtc)
                .ToListAsync();
            var targetParticipations = networkParticipations
                .Where(participation => participation.TenantId == tenantId)
                .ToList();
            var period = OnyxCommissionPeriod.CreateClosedPeriod(
                tenantId,
                closedWeek.PeriodStartUtc,
                closedWeek.PeriodEndUtc,
                closedWeek.TimeZoneId,
                calculatedAt,
                terms);
            await _onyxPeriodRepository.InsertAsync(period);
            var calculator = new OnyxWeeklyCommissionCalculator(
                new OnyxNetworkQualificationEvaluator());
            var commissions = targetParticipations
                .Select(participation => calculator.Calculate(
                    participation,
                    period,
                    terms,
                    networkParticipations))
                .ToList();
            foreach (var commission in commissions)
            {
                await _onyxCommissionRepository.InsertAsync(commission);
            }

            await CurrentUnitOfWork.SaveChangesAsync();
            return BuildResult(
                period.Id,
                "Onyx",
                period.PeriodStart,
                period.PeriodEnd,
                period.TimeZoneId,
                commissions.Select(commission =>
                    new CommissionSummaryRow(
                        commission.TotalAmount,
                        commission.PayoutStatus)),
                terms.Currency,
                wasAlreadyCalculated: false,
                recordsCreated: commissions.Count);
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
                ProgrammeName = "Entry",
                PeriodStart = period.PeriodStart,
                PeriodEnd = period.PeriodEnd,
                TimeZoneId = period.TimeZoneId,
                HighestQualifiedLevel = commission.HighestCompletedLevel,
                HighestCommissionedLevel = commission.HighestCompletedLevel,
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

        private static CommissionCalculationResultDto BuildResult(
            Guid periodId,
            string programmeName,
            DateTime periodStart,
            DateTime periodEnd,
            string timeZoneId,
            IEnumerable<CommissionSummaryRow> commissions,
            string currency,
            bool wasAlreadyCalculated,
            int recordsCreated)
        {
            var rows = commissions.ToList();
            return new CommissionCalculationResultDto
            {
                PeriodId = periodId,
                ProgrammeName = programmeName,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                TimeZoneId = timeZoneId,
                WasAlreadyCalculated = wasAlreadyCalculated,
                RecordsCreated = recordsCreated,
                EarnedCount = rows.Count(row =>
                    row.Amount > 0m),
                HeldCount = rows.Count(row =>
                    row.Status == WeeklyCommissionPayoutStatus.Held),
                TotalEarnedAmount = rows.Sum(row => row.Amount),
                Currency = currency
            };
        }

        private sealed class CommissionSummaryRow
        {
            public decimal Amount { get; }
            public WeeklyCommissionPayoutStatus Status { get; }

            public CommissionSummaryRow(
                decimal amount,
                WeeklyCommissionPayoutStatus status)
            {
                Amount = amount;
                Status = status;
            }
        }

        private async Task<EntryWeeklyCommission> GetEntryCommissionAsync(
            Guid id,
            string operation)
        {
            var commission =
                await _entryCommissionRepository.FirstOrDefaultAsync(id);
            if (commission == null)
            {
                throw Failed(operation, "The Entry earning record was not found.");
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
