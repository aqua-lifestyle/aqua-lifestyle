using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Auditing;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using AqualLifeStyle.Application.Admin.Commissions.Dto;
using AqualLifeStyle.Application.EntryMonthlyObligations;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AqualLifeStyle.Application.Admin.Commissions
{
    /// <summary>
    /// Reviewed, idempotent, host-only bootstrap and preflight for the
    /// authorised clean operational cutover:
    ///
    /// <list type="bullet">
    /// <item>initial Entry and Onyx commission terms effective
    /// 2026-08-14 00:00 Africa/Johannesburg;</item>
    /// <item>a September 2026 AQGreen due-policy row that cannot be created
    /// until the business-authorised due day is supplied;</item>
    /// <item>read-only first-run preflight projections for the first automated
    /// weekly cycle (14–20 August 2026) and month (September 2026).</item>
    /// </list>
    ///
    /// No worker configuration is ever changed here. Inserted rows remain
    /// protected by the existing append-only guarantees.
    /// </summary>
    [Audited]
    public class AdminCommissionBootstrapAppService
        : AdminAppServiceBase, IAdminCommissionBootstrapAppService
    {
        public const string InitialEntryTermsVersion = "2026-08-14-entry-initial";
        public const string InitialOnyxTermsVersion = "2026-08-14-onyx-initial";
        public const string SeptemberPolicyVersion = "2026-09-aqgreen-monthly-initial";

        public const decimal EntryLevelOneAmount = 150m;
        public const decimal EntryLevelTwoAmount = 250m;
        public const decimal EntryLevelThreeAmount = 1250m;
        public const decimal OnyxLevelOneRate = 50m;
        public const decimal OnyxLevelTwoRate = 20m;
        public const decimal OnyxLevelThreeRate = 12.62m;
        public const decimal OnyxLevelFourRate = 5m;
        public const decimal OnyxLevelFiveRate = 4m;
        public const string Currency = "ZAR";

        private const int FirstAutomatedCycleYear = 2026;
        private const int FirstAutomatedCycleMonth = 8;
        private const int FirstAutomatedCycleDay = 14;
        private const int SeptemberPeriodYear = 2026;
        private const int SeptemberPeriodMonth = 9;

        private readonly IRepository<EntryCommissionTermsVersion, Guid>
            _entryTermsRepository;
        private readonly IRepository<OnyxCommissionTermsVersion, Guid>
            _onyxTermsRepository;
        private readonly IRepository<EntryMonthlyObligationDuePolicy, Guid>
            _duePolicyRepository;
        private readonly IRepository<EntryMonthlyObligation, Guid>
            _obligationRepository;
        private readonly IRepository<EntryParticipation, Guid>
            _entryParticipationRepository;
        private readonly IRepository<OnyxParticipation, Guid>
            _onyxParticipationRepository;
        private readonly IRepository<EntryCommissionPeriod, Guid>
            _entryPeriodRepository;
        private readonly IRepository<OnyxCommissionPeriod, Guid>
            _onyxPeriodRepository;
        private readonly IRepository<OnyxLoanAgreement, Guid>
            _loanAgreementRepository;
        private readonly IRepository<Tenant, int> _tenantRepository;
        private readonly IRepository<AreaActivationStateRecord, Guid>
            _areaActivationStateRepository;
        private readonly IEntryMonthlyObligationDueDatePolicy _dueDatePolicy;
        private readonly LatestClosedCommissionWeekResolver _cycleResolver;
        private readonly IConfiguration _configuration;
        private readonly IUnitOfWorkManager _unitOfWorkManager;

        public AdminCommissionBootstrapAppService(
            IRepository<EntryCommissionTermsVersion, Guid> entryTermsRepository,
            IRepository<OnyxCommissionTermsVersion, Guid> onyxTermsRepository,
            IRepository<EntryMonthlyObligationDuePolicy, Guid> duePolicyRepository,
            IRepository<EntryMonthlyObligation, Guid> obligationRepository,
            IRepository<EntryParticipation, Guid> entryParticipationRepository,
            IRepository<OnyxParticipation, Guid> onyxParticipationRepository,
            IRepository<EntryCommissionPeriod, Guid> entryPeriodRepository,
            IRepository<OnyxCommissionPeriod, Guid> onyxPeriodRepository,
            IRepository<OnyxLoanAgreement, Guid> loanAgreementRepository,
            IRepository<Tenant, int> tenantRepository,
            IRepository<AreaActivationStateRecord, Guid>
                areaActivationStateRepository,
            IEntryMonthlyObligationDueDatePolicy dueDatePolicy,
            LatestClosedCommissionWeekResolver cycleResolver,
            IConfiguration configuration,
            IUnitOfWorkManager unitOfWorkManager)
        {
            _entryTermsRepository = entryTermsRepository;
            _onyxTermsRepository = onyxTermsRepository;
            _duePolicyRepository = duePolicyRepository;
            _obligationRepository = obligationRepository;
            _entryParticipationRepository = entryParticipationRepository;
            _onyxParticipationRepository = onyxParticipationRepository;
            _entryPeriodRepository = entryPeriodRepository;
            _onyxPeriodRepository = onyxPeriodRepository;
            _loanAgreementRepository = loanAgreementRepository;
            _tenantRepository = tenantRepository;
            _areaActivationStateRepository = areaActivationStateRepository;
            _dueDatePolicy = dueDatePolicy;
            _cycleResolver = cycleResolver;
            _configuration = configuration;
            _unitOfWorkManager = unitOfWorkManager;
        }

        /// <inheritdoc />
        [AbpAuthorize(AquaPermissions.Admin.Commissions.Release)]
        public virtual async Task<CommissionTermsBootstrapResult>
            BootstrapInitialCommissionTermsAsync(
                BootstrapInitialCommissionTermsInput input)
        {
            RequireHostAccess("commission terms bootstrap");
            input ??= new BootstrapInitialCommissionTermsInput();

            var effectiveAt = FirstAutomatedCycleBoundaryUtc();
            var result = new CommissionTermsBootstrapResult
            {
                DryRun = input.DryRun
            };

            await ValidateAndStageEntryAsync(effectiveAt, result);
            await ValidateAndStageOnyxAsync(effectiveAt, result);

            if (result.AnyConflict)
            {
                throw new InvalidOperationException(
                    "Commission terms bootstrap conflicts: " +
                    string.Join("; ", result.Conflicts));
            }

            if (input.DryRun)
            {
                return result;
            }

            foreach (var row in result.Rows)
            {
                if (row.Status == CommissionTermsBootstrapRowStatus.WouldInsert)
                {
                    if (row.Programme == "Entry")
                    {
                        await _entryTermsRepository.InsertAsync(
                            EntryCommissionTermsVersion.Create(
                                row.Version,
                                row.EffectiveAtUtc,
                                EntryLevelOneAmount,
                                EntryLevelTwoAmount,
                                EntryLevelThreeAmount,
                                Currency));
                    }
                    else
                    {
                        await _onyxTermsRepository.InsertAsync(
                            OnyxCommissionTermsVersion.Create(
                                row.Version,
                                row.EffectiveAtUtc,
                                OnyxLevelOneRate,
                                OnyxLevelTwoRate,
                                OnyxLevelThreeRate,
                                OnyxLevelFourRate,
                                OnyxLevelFiveRate,
                                Currency));
                    }

                    row.Status = CommissionTermsBootstrapRowStatus.Inserted;
                }
            }

            await CurrentUnitOfWork.SaveChangesAsync();
            return result;
        }

        /// <inheritdoc />
        [AbpAuthorize(AquaPermissions.Admin.Commissions.View)]
        public virtual async Task<WeeklyEnablementPreflightOutput>
            GetWeeklyEnablementPreflightAsync()
        {
            RequireHostAccess("weekly enablement preflight");

            var periodStart = FirstAutomatedCycleBoundaryUtc();
            var periodEnd = periodStart.AddDays(7).AddTicks(-1);
            var boundary = _cycleResolver.ResolveFirstCycleStartAfter(periodEnd);

            var output = new WeeklyEnablementPreflightOutput
            {
                TargetPeriodStartUtc = periodStart,
                TargetPeriodEndUtc = periodEnd,
                TimeZoneId = LatestClosedCommissionWeekResolver.CommissionTimeZoneId,
                WorkerEnabled = _configuration.GetValue<bool>(
                    "App:WeeklyCommissions:Enabled"),
                TopologyStatus = "NOT CONFIRMED",
                TopologyDetail = "Repository code assumes all programme Areas share the host database for cross-Area network queries (tenant filters disabled during calculation). Confirm from actual deployment/database configuration that every programme Area uses this host database before enabling."
            };

            output.Terms.Add(StageEntryTermsPreflight(periodStart, boundary));
            output.Terms.Add(StageOnyxTermsPreflight(periodStart, boundary));

            output.Areas = await BuildAreaBaselineRowsAsync(periodEnd);

            using (_unitOfWorkManager.Current.DisableFilter(
                AbpDataFilters.MayHaveTenant,
                AbpDataFilters.MustHaveTenant))
            {
                output.ExistingTargetEntryPeriods =
                    await _entryPeriodRepository.GetAll()
                        .CountAsync(period =>
                            period.PeriodStart == periodStart &&
                            period.PeriodEnd == periodEnd);
                output.ExistingTargetOnyxPeriods =
                    await _onyxPeriodRepository.GetAll()
                        .CountAsync(period =>
                            period.PeriodStart == periodStart &&
                            period.PeriodEnd == periodEnd);
            }

            output.Projection = await BuildWeeklyProjectionAsync(
                periodStart,
                periodEnd,
                output.Areas);

            return output;
        }

        /// <inheritdoc />
        [AbpAuthorize(AquaPermissions.Admin.EntryMonthlyObligations.View)]
        public virtual async Task<MonthlyEnablementPreflightOutput>
            GetMonthlyEnablementPreflightAsync()
        {
            RequireHostAccess("monthly enablement preflight");

            var output = new MonthlyEnablementPreflightOutput
            {
                PeriodYear = SeptemberPeriodYear,
                PeriodMonth = SeptemberPeriodMonth,
                PeriodName = "September 2026",
                WorkerEnabled = _configuration.GetValue<bool>(
                    "App:EntryMonthlyObligations:Enabled")
            };

            var resolution = await _dueDatePolicy.ResolveDueDateAsync(
                SeptemberPeriodYear,
                SeptemberPeriodMonth);
            output.DuePolicyStatus = resolution.Status.ToString();
            if (resolution.IsResolved)
            {
                output.ResolvedDueDayOfMonth =
                    ConvertDueDay(resolution.DueAtUtc.Value);
                output.ResolvedPolicyVersion = resolution.PolicyVersion;
                output.DuePolicyDetail =
                    $"Due day {output.ResolvedDueDayOfMonth} from policy version {resolution.PolicyVersion}.";
            }
            else
            {
                output.DuePolicyDetail =
                    "No authorised September 2026 due policy exists yet. The AQGreen DueDayOfMonth is UNRESOLVED; the immutable policy row must not be created until the business authorises the due day (1-28).";
            }

            using (_unitOfWorkManager.Current.DisableFilter(
                AbpDataFilters.MayHaveTenant,
                AbpDataFilters.MustHaveTenant))
            {
                output.ExistingTargetPeriodObligations =
                    await _obligationRepository.GetAll()
                        .CountAsync(obligation =>
                            obligation.PeriodYear == SeptemberPeriodYear &&
                            obligation.PeriodMonth == SeptemberPeriodMonth);
                output.ExistingAugustObligations =
                    await _obligationRepository.GetAll()
                        .CountAsync(obligation =>
                            obligation.PeriodYear == 2026 &&
                            obligation.PeriodMonth == 8);
            }

            await BuildSeptemberProjectionAsync(output);
            return output;
        }

        /// <inheritdoc />
        [AbpAuthorize(AquaPermissions.Admin.EntryMonthlyObligations.View)]
        public virtual async Task<SeptemberDueDatePolicyBootstrapResult>
            BootstrapSeptemberDueDatePolicyAsync(
                BootstrapSeptemberDueDatePolicyInput input)
        {
            RequireHostAccess("September due-policy bootstrap");
            input ??= new BootstrapSeptemberDueDatePolicyInput();

            if (input.DueDayOfMonth < 1 || input.DueDayOfMonth > 28)
            {
                throw new InvalidOperationException(
                    "The AQGreen DueDayOfMonth is UNRESOLVED. The business must authorise a due day between 1 and 28 before the immutable September 2026 policy can be created.");
            }

            var version = input.Version?.Trim();
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new InvalidOperationException(
                    "A due-policy version identifier is required.");
            }

            var effectiveFrom = EntryMonthlyObligationDuePolicy
                .JohannesburgMonthStartUtc(
                    SeptemberPeriodYear,
                    SeptemberPeriodMonth);

            var result = new SeptemberDueDatePolicyBootstrapResult
            {
                DryRun = input.DryRun,
                Version = version,
                EffectiveFromUtc = effectiveFrom,
                DueDayOfMonth = input.DueDayOfMonth,
                Status = "WouldInsert"
            };

            using (_unitOfWorkManager.Current.DisableFilter(
                AbpDataFilters.MayHaveTenant,
                AbpDataFilters.MustHaveTenant))
            {
                var existing = await _duePolicyRepository.GetAll().ToListAsync();

                var sameVersion = existing
                    .Where(policy =>
                        string.Equals(
                            policy.Version,
                            version,
                            StringComparison.Ordinal))
                    .ToList();
                if (sameVersion.Count > 0)
                {
                    var matches = sameVersion.Any(policy =>
                        policy.DueDayOfMonth == input.DueDayOfMonth &&
                        policy.EffectiveFrom == effectiveFrom);
                    if (matches)
                    {
                        result.Status = "AlreadyPresent";
                        return result;
                    }

                    result.Conflicts.Add(
                        $"Version '{version}' already exists with a different due day or effective instant.");
                }

                var sameBoundary = existing
                    .Where(policy => policy.EffectiveFrom == effectiveFrom)
                    .ToList();
                if (sameBoundary.Count > 0 &&
                    sameBoundary.All(policy =>
                        !string.Equals(
                            policy.Version,
                            version,
                            StringComparison.Ordinal)))
                {
                    result.Conflicts.Add(
                        "Another due policy already occupies the 1 September 2026 effective boundary.");
                }
            }

            if (result.Conflicts.Count > 0)
            {
                throw new InvalidOperationException(
                    "September due-policy bootstrap conflicts: " +
                    string.Join("; ", result.Conflicts));
            }

            if (input.DryRun)
            {
                return result;
            }

            await _duePolicyRepository.InsertAsync(
                EntryMonthlyObligationDuePolicy.Create(
                    version,
                    input.DueDayOfMonth,
                    effectiveFrom));
            await CurrentUnitOfWork.SaveChangesAsync();
            result.Status = "Inserted";
            return result;
        }

        private async Task ValidateAndStageEntryAsync(
            DateTime effectiveAt,
            CommissionTermsBootstrapResult result)
        {
            var existing = await _entryTermsRepository.GetAll().ToListAsync();

            var sameVersion = existing
                .Where(version =>
                    string.Equals(
                        version.Version,
                        InitialEntryTermsVersion,
                        StringComparison.Ordinal))
                .ToList();
            if (sameVersion.Count > 0)
            {
                var matches = sameVersion.Any(version =>
                    version.EffectiveAt == effectiveAt &&
                    version.LevelOneComponentAmount == EntryLevelOneAmount &&
                    version.LevelTwoComponentAmount == EntryLevelTwoAmount &&
                    version.LevelThreeComponentAmount == EntryLevelThreeAmount &&
                    string.Equals(
                        version.Currency,
                        Currency,
                        StringComparison.Ordinal));
                if (matches)
                {
                    result.Rows.Add(StagedRow(
                        "Entry",
                        InitialEntryTermsVersion,
                        effectiveAt,
                        CommissionTermsBootstrapRowStatus.AlreadyPresent));
                    return;
                }

                result.Conflicts.Add(
                    $"Entry version '{InitialEntryTermsVersion}' already exists with different rates, currency, or effective boundary.");
                return;
            }

            var sameBoundary = existing
                .Where(version => version.EffectiveAt == effectiveAt)
                .ToList();
            if (sameBoundary.Count > 0)
            {
                result.Conflicts.Add(
                    $"Another Entry terms version already occupies the authorised effective boundary {effectiveAt:O}.");
                return;
            }

            result.Rows.Add(StagedRow(
                "Entry",
                InitialEntryTermsVersion,
                effectiveAt,
                CommissionTermsBootstrapRowStatus.WouldInsert));
        }

        private async Task ValidateAndStageOnyxAsync(
            DateTime effectiveAt,
            CommissionTermsBootstrapResult result)
        {
            var existing = await _onyxTermsRepository.GetAll().ToListAsync();

            var sameVersion = existing
                .Where(version =>
                    string.Equals(
                        version.Version,
                        InitialOnyxTermsVersion,
                        StringComparison.Ordinal))
                .ToList();
            if (sameVersion.Count > 0)
            {
                var matches = sameVersion.Any(version =>
                    version.EffectiveAt == effectiveAt &&
                    version.LevelOnePerPersonRate == OnyxLevelOneRate &&
                    version.LevelTwoPerPersonRate == OnyxLevelTwoRate &&
                    version.LevelThreePerPersonRate == OnyxLevelThreeRate &&
                    version.LevelFourPerPersonRate == OnyxLevelFourRate &&
                    version.LevelFivePerPersonRate == OnyxLevelFiveRate &&
                    string.Equals(
                        version.Currency,
                        Currency,
                        StringComparison.Ordinal));
                if (matches)
                {
                    result.Rows.Add(StagedRow(
                        "Onyx",
                        InitialOnyxTermsVersion,
                        effectiveAt,
                        CommissionTermsBootstrapRowStatus.AlreadyPresent));
                    return;
                }

                result.Conflicts.Add(
                    $"Onyx version '{InitialOnyxTermsVersion}' already exists with different rates, currency, or effective boundary.");
                return;
            }

            var sameBoundary = existing
                .Where(version => version.EffectiveAt == effectiveAt)
                .ToList();
            if (sameBoundary.Count > 0)
            {
                result.Conflicts.Add(
                    $"Another Onyx terms version already occupies the authorised effective boundary {effectiveAt:O}.");
                return;
            }

            result.Rows.Add(StagedRow(
                "Onyx",
                InitialOnyxTermsVersion,
                effectiveAt,
                CommissionTermsBootstrapRowStatus.WouldInsert));
        }

        private static CommissionTermsBootstrapRow StagedRow(
            string programme,
            string version,
            DateTime effectiveAt,
            CommissionTermsBootstrapRowStatus status)
        {
            return new CommissionTermsBootstrapRow
            {
                Programme = programme,
                Version = version,
                EffectiveAtUtc = effectiveAt,
                Status = status
            };
        }

        private CommissionTermsPreflightRow StageEntryTermsPreflight(
            DateTime expectedEffectiveAt,
            DateTime boundary)
        {
            var all = _entryTermsRepository.GetAll().ToList();
            var sameVersion = all
                .Where(version => version.Version == InitialEntryTermsVersion)
                .ToList();
            if (sameVersion.Count > 0)
            {
                return new CommissionTermsPreflightRow
                {
                    Programme = "Entry",
                    ExpectedVersion = InitialEntryTermsVersion,
                    ExpectedEffectiveAtUtc = expectedEffectiveAt,
                    Status = sameVersion[0].EffectiveAt == expectedEffectiveAt
                        ? CommissionTermsPreflightStatus.Present
                        : CommissionTermsPreflightStatus.Conflicting,
                    Detail = sameVersion[0].EffectiveAt == expectedEffectiveAt
                        ? "Authorised initial terms present."
                        : "The expected version exists with a different effective boundary."
                };
            }

            var applicable = all
                .Where(version => version.EffectiveAt <= boundary)
                .OrderByDescending(version => version.EffectiveAt)
                .FirstOrDefault();
            return new CommissionTermsPreflightRow
            {
                Programme = "Entry",
                ExpectedVersion = InitialEntryTermsVersion,
                ExpectedEffectiveAtUtc = expectedEffectiveAt,
                Status = applicable == null
                    ? CommissionTermsPreflightStatus.Missing
                    : CommissionTermsPreflightStatus.Conflicting,
                Detail = applicable == null
                    ? $"No Entry terms version is effective for the target cycle (boundary {boundary:O})."
                    : "A different Entry terms version is effective for the target cycle; the authorised initial version is absent."
            };
        }

        private CommissionTermsPreflightRow StageOnyxTermsPreflight(
            DateTime expectedEffectiveAt,
            DateTime boundary)
        {
            var all = _onyxTermsRepository.GetAll().ToList();
            var sameVersion = all
                .Where(version => version.Version == InitialOnyxTermsVersion)
                .ToList();
            if (sameVersion.Count > 0)
            {
                return new CommissionTermsPreflightRow
                {
                    Programme = "Onyx",
                    ExpectedVersion = InitialOnyxTermsVersion,
                    ExpectedEffectiveAtUtc = expectedEffectiveAt,
                    Status = sameVersion[0].EffectiveAt == expectedEffectiveAt
                        ? CommissionTermsPreflightStatus.Present
                        : CommissionTermsPreflightStatus.Conflicting,
                    Detail = sameVersion[0].EffectiveAt == expectedEffectiveAt
                        ? "Authorised initial terms present."
                        : "The expected version exists with a different effective boundary."
                };
            }

            var applicable = all
                .Where(version => version.EffectiveAt <= boundary)
                .OrderByDescending(version => version.EffectiveAt)
                .FirstOrDefault();
            return new CommissionTermsPreflightRow
            {
                Programme = "Onyx",
                ExpectedVersion = InitialOnyxTermsVersion,
                ExpectedEffectiveAtUtc = expectedEffectiveAt,
                Status = applicable == null
                    ? CommissionTermsPreflightStatus.Missing
                    : CommissionTermsPreflightStatus.Conflicting,
                Detail = applicable == null
                    ? $"No Onyx terms version is effective for the target cycle (boundary {boundary:O})."
                    : "A different Onyx terms version is effective for the target cycle; the authorised initial version is absent."
            };
        }

        private async Task<List<AreaBaselinePreflightRow>>
            BuildAreaBaselineRowsAsync(DateTime targetPeriodEndUtc)
        {
            var tenants = await _tenantRepository.GetAll()
                .OrderBy(tenant => tenant.Id)
                .ToListAsync();
            var records = await _areaActivationStateRepository.GetAll()
                .ToListAsync();

            var rows = new List<AreaBaselinePreflightRow>();
            foreach (var tenant in tenants)
            {
                var applicable = records
                    .Where(record => record.TenantId == tenant.Id)
                    .OrderByDescending(record => record.EffectiveAt)
                    .ToList();
                var latest = applicable.FirstOrDefault();

                AreaBaselinePreflightStatus status;
                if (latest == null)
                {
                    status = AreaBaselinePreflightStatus.Missing;
                }
                else if (latest.EffectiveAt <= targetPeriodEndUtc)
                {
                    status = AreaBaselinePreflightStatus.Sufficient;
                }
                else
                {
                    status = AreaBaselinePreflightStatus.RecordedAfterTargetCutoff;
                }

                rows.Add(new AreaBaselinePreflightRow
                {
                    TenantId = tenant.Id,
                    TenantName = tenant.Name,
                    IsActive = tenant.IsActive,
                    BaselineStatus = status,
                    BaselineEffectiveAtUtc = latest?.EffectiveAt,
                    WorkerWouldSkipAtCutoff =
                        latest == null ||
                        latest.EffectiveAt > targetPeriodEndUtc ||
                        !latest.IsActive
                });
            }

            return rows;
        }

        private async Task<WeeklyFirstRunProjection>
            BuildWeeklyProjectionAsync(
                DateTime periodStart,
                DateTime periodEnd,
                List<AreaBaselinePreflightRow> areaRows)
        {
            var projection = new WeeklyFirstRunProjection
            {
                PeriodStartUtc = periodStart,
                PeriodEndUtc = periodEnd
            };

            using (_unitOfWorkManager.Current.DisableFilter(
                AbpDataFilters.SoftDelete))
            {
                var entryParticipations = await _entryParticipationRepository
                    .GetAll()
                    .Where(participation =>
                        participation.Status == EntryParticipationStatus.Active &&
                        (!participation.ActivatedAt.HasValue ||
                         participation.ActivatedAt <= periodEnd))
                    .ToListAsync();
                var onyxParticipations = await _onyxParticipationRepository
                    .GetAll()
                    .Where(participation =>
                        participation.Status == OnyxParticipationStatus.Active &&
                        (!participation.ActivatedAt.HasValue ||
                         participation.ActivatedAt <= periodEnd))
                    .ToListAsync();

                var entryIds = entryParticipations
                    .Select(participation => participation.Id)
                    .ToList();
                var obligations = await _obligationRepository.GetAll()
                    .Where(obligation =>
                        entryIds.Contains(obligation.EntryParticipationId))
                    .ToListAsync();
                var loans = await _loanAgreementRepository
                    .GetAllIncluding(agreement => agreement.WeeklyRequirements)
                    .Where(agreement =>
                        entryIds.Contains(agreement.EntryParticipationId))
                    .ToListAsync();

                projection.ActiveEntryParticipations = entryParticipations.Count;
                projection.ActiveOnyxParticipations = onyxParticipations.Count;
                projection.EntryOverdueObligationHolds = obligations
                    .Count(obligation => obligation.WasOverdueAt(periodEnd));
                projection.EntryLoanHolds = loans
                    .Count(agreement =>
                        agreement.WasRequiringPayoutHoldAt(periodEnd));

                foreach (var area in areaRows)
                {
                    var entryIdsInArea = entryParticipations
                        .Where(participation =>
                            participation.TenantId == area.TenantId)
                        .Select(participation => participation.Id)
                        .ToHashSet();
                    var onyxInArea = onyxParticipations
                        .Count(participation =>
                            participation.TenantId == area.TenantId);
                    projection.Tenants.Add(new WeeklyFirstRunTenantProjection
                    {
                        TenantId = area.TenantId,
                        TenantName = area.TenantName,
                        AreaBaselineStatus = area.BaselineStatus,
                        ActiveEntryParticipations = entryIdsInArea.Count,
                        ActiveOnyxParticipations = onyxInArea,
                        EntryOverdueObligationHolds = obligations
                            .Count(obligation =>
                                entryIdsInArea.Contains(
                                    obligation.EntryParticipationId) &&
                                obligation.WasOverdueAt(periodEnd)),
                        EntryLoanHolds = loans.Count(agreement =>
                            entryIdsInArea.Contains(
                                agreement.EntryParticipationId) &&
                            agreement.WasRequiringPayoutHoldAt(periodEnd))
                    });
                }
            }

            return projection;
        }

        private async Task BuildSeptemberProjectionAsync(
            MonthlyEnablementPreflightOutput output)
        {
            using (_unitOfWorkManager.Current.DisableFilter(
                AbpDataFilters.SoftDelete))
            {
                var active = await _entryParticipationRepository.GetAll()
                    .Where(participation =>
                        participation.Status == EntryParticipationStatus.Active)
                    .ToListAsync();

                var eligible = 0;
                var activationMonthExcluded = 0;
                var withoutActivatedAt = 0;
                foreach (var participation in active)
                {
                    if (!participation.ActivatedAt.HasValue)
                    {
                        withoutActivatedAt++;
                        continue;
                    }

                    var activationMonth = EntryMonthlyObligationDuePolicy
                        .JohannesburgMonth(participation.ActivatedAt.Value);
                    var activationNumber =
                        activationMonth.Year * 12 + activationMonth.Month;
                    var septemberNumber =
                        SeptemberPeriodYear * 12 + SeptemberPeriodMonth;
                    if (activationNumber >= septemberNumber)
                    {
                        activationMonthExcluded++;
                    }
                    else
                    {
                        eligible++;
                    }
                }

                output.EligibleActiveParticipations = eligible;
                output.ExcludedActivationMonthParticipations =
                    activationMonthExcluded;
                output.ExcludedWithoutActivatedAt = withoutActivatedAt;
            }
        }

        private static int ConvertDueDay(DateTime dueAtUtc)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(
                    dueAtUtc,
                    TimeZoneInfo.FindSystemTimeZoneById(
                        CommissionCycleBoundary.TimeZoneId))
                .Day;
        }

        private static DateTime FirstAutomatedCycleBoundaryUtc()
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(
                CommissionCycleBoundary.TimeZoneId);
            var localBoundary = new DateTime(
                FirstAutomatedCycleYear,
                FirstAutomatedCycleMonth,
                FirstAutomatedCycleDay,
                0,
                0,
                0,
                DateTimeKind.Unspecified);
            var utc = TimeZoneInfo.ConvertTimeToUtc(localBoundary, timeZone);
            if (!CommissionCycleBoundary.IsCanonicalCycleBoundary(utc))
            {
                throw new InvalidOperationException(
                    "The authorised first automated cycle boundary is not a canonical Friday 00:00 Africa/Johannesburg cycle start.");
            }

            return utc;
        }

        private void RequireHostAccess(string operation)
        {
            if (AbpSession.TenantId.HasValue)
            {
                throw new AbpAuthorizationException(
                    $"Host access is required for {operation}.");
            }
        }
    }
}
