using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Auditing;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.Runtime.Session;
using Abp.UI;
using AqualLifeStyle.Application.Admin.Commissions;
using AqualLifeStyle.Application.ProgrammeParticipations.Dto;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.ProgrammeParticipations
{
    /// <summary>
    /// Member-facing view that explains the Club Member's AQGreen network
    /// position, weekly earnings, monthly subscription, and funeral-cover
    /// inclusion, together with educational content about the programme. Read
    /// only: no member action can change programme state through this service.
    /// </summary>
    [Audited]
    [AbpAuthorize]
    public class ClubMemberProgrammeProgressAppService
        : AqualLifeStyleAppServiceBase,
            IClubMemberProgrammeProgressAppService
    {
        private const int MaxRecentEarnings = 12;

        private readonly ICustomerRepository _customerRepository;
        private readonly IRepository<EntryParticipation, Guid>
            _entryParticipationRepository;
        private readonly IRepository<AQGreenRecruitmentAttribution, Guid>
            _attributionRepository;
        private readonly IRepository<EntryWeeklyCommission, Guid>
            _entryCommissionRepository;
        private readonly IRepository<EntryCommissionPeriod, Guid>
            _entryPeriodRepository;
        private readonly IRepository<EntryMonthlyObligation, Guid>
            _obligationRepository;
        private readonly IRepository<AQGreenFuneralCoverEntitlement, Guid>
            _funeralCoverRepository;
        private readonly IRepository<OnyxParticipation, Guid>
            _onyxParticipationRepository;
        private readonly IRepository<OnyxWeeklyCommission, Guid>
            _onyxCommissionRepository;
        private readonly IRepository<OnyxCommissionPeriod, Guid>
            _onyxPeriodRepository;
        private readonly IRepository<OnyxTravelBenefitEntitlement, Guid>
            _travelBenefitRepository;
        private readonly ICurrentProgrammeTermsProvider _programmeTermsProvider;
        private readonly ICurrentCommissionTermsProvider _commissionTermsProvider;
        private readonly ICurrentAQGreenFuneralCoverTermsProvider
            _funeralCoverTermsProvider;
        private readonly IAQGreenPlacementV2ProgressGate _v2ProgressGate;
        private readonly IAQGreenStructuralCompletionEvaluator _v2StructuralEvaluator;

        protected virtual DateTime UtcNow => DateTime.UtcNow;

        public ClubMemberProgrammeProgressAppService(
            ICustomerRepository customerRepository,
            IRepository<EntryParticipation, Guid> entryParticipationRepository,
            IRepository<AQGreenRecruitmentAttribution, Guid> attributionRepository,
            IRepository<EntryWeeklyCommission, Guid> entryCommissionRepository,
            IRepository<EntryCommissionPeriod, Guid> entryPeriodRepository,
            IRepository<EntryMonthlyObligation, Guid> obligationRepository,
            IRepository<AQGreenFuneralCoverEntitlement, Guid> funeralCoverRepository,
            IRepository<OnyxParticipation, Guid> onyxParticipationRepository,
            IRepository<OnyxWeeklyCommission, Guid> onyxCommissionRepository,
            IRepository<OnyxCommissionPeriod, Guid> onyxPeriodRepository,
            IRepository<OnyxTravelBenefitEntitlement, Guid> travelBenefitRepository,
            ICurrentProgrammeTermsProvider programmeTermsProvider,
            ICurrentCommissionTermsProvider commissionTermsProvider,
            ICurrentAQGreenFuneralCoverTermsProvider funeralCoverTermsProvider,
            IAQGreenPlacementV2ProgressGate v2ProgressGate,
            IAQGreenStructuralCompletionEvaluator v2StructuralEvaluator)
        {
            _customerRepository = customerRepository;
            _entryParticipationRepository = entryParticipationRepository;
            _attributionRepository = attributionRepository;
            _entryCommissionRepository = entryCommissionRepository;
            _entryPeriodRepository = entryPeriodRepository;
            _obligationRepository = obligationRepository;
            _funeralCoverRepository = funeralCoverRepository;
            _onyxParticipationRepository = onyxParticipationRepository;
            _onyxCommissionRepository = onyxCommissionRepository;
            _onyxPeriodRepository = onyxPeriodRepository;
            _travelBenefitRepository = travelBenefitRepository;
            _programmeTermsProvider = programmeTermsProvider;
            _commissionTermsProvider = commissionTermsProvider;
            _funeralCoverTermsProvider = funeralCoverTermsProvider;
            _v2ProgressGate = v2ProgressGate;
            _v2StructuralEvaluator = v2StructuralEvaluator;
        }

        [AbpAuthorize(AquaPermissions.ProgrammeParticipations.ViewSelf)]
        public async Task<MyProgrammeJourneyDto> GetMyJourneyAsync()
        {
            var tenantId = GetRequiredTenantId(
                "Your programme journey is unavailable.");
            var customer = await _customerRepository.FirstOrDefaultAsync(item =>
                item.TenantId == tenantId &&
                item.UserId == AbpSession.GetUserId());
            if (customer == null || !customer.IsActive)
            {
                throw new UserFriendlyException(
                    "Your programme journey is unavailable.",
                    "An active Club Member account is required.");
            }

            var projectedAt = UtcNow;
            var entryParticipation = await _entryParticipationRepository
                .GetAllIncluding(item => item.ApprovalDecisions)
                .FirstOrDefaultAsync(item =>
                    item.TenantId == tenantId && item.CustomerId == customer.Id);
            var onyxParticipation = await _onyxParticipationRepository
                .GetAllIncluding(item => item.ApprovalDecisions)
                .FirstOrDefaultAsync(item =>
                    item.TenantId == tenantId && item.CustomerId == customer.Id);

            var entryNetworkRows = await _entryParticipationRepository
                .GetAllIncluding(item => item.RecruiterCorrections)
                .Where(item =>
                    item.TenantId == tenantId &&
                    item.Status == EntryParticipationStatus.Active &&
                    item.ActivatedAt <= projectedAt)
                .ToListAsync();
            var onyxNetworkRows = await _onyxParticipationRepository
                .GetAllIncluding(item => item.RecruiterCorrections)
                .Where(item =>
                    item.TenantId == tenantId &&
                    item.Status == OnyxParticipationStatus.Active &&
                    item.ActivatedAt <= projectedAt)
                .ToListAsync();

            var entryNetwork = EffectiveProgrammeNetwork.BuildAQGreen(
                tenantId,
                entryNetworkRows,
                projectedAt);
            var onyxNetwork = EffectiveProgrammeNetwork.BuildOnyx(
                tenantId,
                onyxNetworkRows,
                projectedAt);

            var v2Enabled = entryParticipation != null
                && await _v2ProgressGate.IsEnabledAsync(tenantId, entryParticipation.Id);
            var v2StructuralResult = v2Enabled
                ? await EvaluateV2StructuralProgressAsync(
                    tenantId,
                    entryParticipation.Id,
                    projectedAt)
                : null;
            var entryLevel = v2StructuralResult == null
                ? entryParticipation?.Status == EntryParticipationStatus.Active
                    ? new EntryNetworkQualificationEvaluator().Evaluate(
                        customer.Id,
                        entryNetwork)
                    : EntryNetworkLevel.None
                : MapV2StructuralLevel(
                    v2StructuralResult.StructuralCompletionLevel);
            var onyxLevel = onyxParticipation?.Status == OnyxParticipationStatus.Active
                ? new OnyxNetworkQualificationEvaluator().Evaluate(customer.Id, onyxNetwork)
                : OnyxNetworkLevel.None;

            var entryCommissions = await _entryCommissionRepository
                .GetAllIncluding(item => item.Components)
                .Where(item => item.CustomerId == customer.Id)
                .OrderByDescending(item => item.CalculatedAt)
                .ToListAsync();
            var onyxCommissions = await _onyxCommissionRepository
                .GetAllIncluding(item => item.Components)
                .Where(item => item.CustomerId == customer.Id)
                .OrderByDescending(item => item.CalculatedAt)
                .ToListAsync();
            var entryPeriods = await LoadPeriodsAsync(
                entryCommissions.Select(item => item.CommissionPeriodId));
            var onyxPeriods = await LoadOnyxPeriodsAsync(
                onyxCommissions.Select(item => item.CommissionPeriodId));
            var obligations = await _obligationRepository.GetAll()
                .Where(item => item.CustomerId == customer.Id)
                .OrderBy(item => item.PeriodYear)
                .ThenBy(item => item.PeriodMonth)
                .ToListAsync();
            var funeralCover = await _funeralCoverRepository.FirstOrDefaultAsync(
                item => item.CustomerId == customer.Id);
            var travelBenefit = await _travelBenefitRepository.FirstOrDefaultAsync(
                item => item.CustomerId == customer.Id);

            return new MyProgrammeJourneyDto
            {
                ProjectedAt = projectedAt,
                Programmes = new[]
                {
                    BuildAQGreenJourney(
                        entryParticipation,
                        entryNetwork,
                        entryLevel,
                        entryCommissions,
                        entryPeriods,
                        obligations,
                        funeralCover,
                        v2StructuralResult),
                    BuildOnyxJourney(
                        onyxParticipation,
                        onyxNetwork,
                        onyxLevel,
                        onyxCommissions,
                        onyxPeriods,
                        travelBenefit)
                }
            };
        }

        private MemberProgrammeJourneyDto BuildAQGreenJourney(
            EntryParticipation participation,
            EffectiveProgrammeNetwork network,
            EntryNetworkLevel qualifiedLevel,
            IReadOnlyCollection<EntryWeeklyCommission> commissions,
            IReadOnlyDictionary<Guid, EntryCommissionPeriod> periods,
            IReadOnlyList<EntryMonthlyObligation> obligations,
            AQGreenFuneralCoverEntitlement funeralCover,
            AQGreenStructuralCompletionResult v2StructuralResult)
        {
            var programmeTerms = _programmeTermsProvider.GetEntryTerms();
            var commissionTerms = _commissionTermsProvider.GetEntryTerms();
            var funeralCoverTerms = _funeralCoverTermsProvider.GetTerms();
            var status = participation == null
                ? null
                : ProgrammeParticipationStatusPresenter.Describe(participation);
            var joiningRequired = participation == null
                ? programmeTerms.JoiningPaymentAmount
                : GetAQGreenJoiningRequiredAmount(participation);
            var joiningPaid = participation == null
                ? 0m
                : GetAQGreenJoiningPaidAmount(participation);
            var joiningComplete = joiningRequired > 0m && joiningPaid >= joiningRequired;
            var openObligation = obligations.FirstOrDefault(item =>
                item.Status != EntryMonthlyObligationStatus.Paid);
            var levelNumber = (int)qualifiedLevel;
            var benefits = new List<MemberProgrammeBenefitDto>();
            if (funeralCover != null)
            {
                benefits.Add(new MemberProgrammeBenefitDto
                {
                    Code = "AQGREEN_FUNERAL_COVER",
                    Name = "Funeral-cover inclusion",
                    State = "Included",
                    Description = "The internal funeral-cover inclusion record is present following confirmed completion of the AQGreen joining requirement. This does not confirm insurer activation, enrolment, policy acceptance, or active cover.",
                    Amount = funeralCover.FuneralCoverAmount,
                    Currency = funeralCover.Currency,
                    UnlockedAt = funeralCover.IncludedAt
                });
            }
            else
            {
                benefits.Add(new MemberProgrammeBenefitDto
                {
                    Code = "AQGREEN_FUNERAL_COVER",
                    Name = "Funeral-cover inclusion",
                    State = joiningComplete
                        ? "Pending record"
                        : "Locked",
                    Description = joiningComplete
                        ? "Your joining payment is complete, but no funeral-cover inclusion record is available yet."
                        : "An internal inclusion record can be created after the full AQGreen joining payment is confirmed.",
                    Amount = funeralCoverTerms.FuneralCoverAmount,
                    Currency = programmeTerms.Currency
                });
            }

            var nextAction = AQGreenNextAction(
                participation,
                openObligation,
                levelNumber,
                joiningComplete);
            return new MemberProgrammeJourneyDto
            {
                ProgrammeCode = "AQGREEN",
                ProgrammeName = "AQGreen",
                HasParticipation = participation != null,
                ParticipationStatus = status?.Status ?? "Not joined",
                DecisionReason = participation?.ApprovalDecisions
                    .OrderByDescending(item => item.DecidedAt)
                    .FirstOrDefault()?.Reason,
                IsActive = status?.IsActive ?? false,
                StartedAt = participation?.StartedAt,
                ActivatedAt = participation?.ActivatedAt,
                Currency = participation?.Currency ?? programmeTerms.Currency,
                QualifiedLevel = levelNumber,
                MaximumLevel = EntryNetworkQualificationEvaluator.MaximumLevel,
                ActivationSteps = BuildActivationSteps(
                    participation != null,
                    joiningComplete,
                    participation?.Status == EntryParticipationStatus.PaymentConfirmedAwaitingApproval,
                    participation?.Status == EntryParticipationStatus.Active,
                    participation?.Status == EntryParticipationStatus.Rejected),
                Levels = BuildLevels(
                    network,
                    participation?.CustomerId,
                    levelNumber,
                    EntryNetworkQualificationEvaluator.MaximumLevel,
                    status?.IsActive ?? false,
                    level => AQGreenStructuralCompletionCalculator
                        .GetRequiredPopulation(level),
                    level => null,
                    level => $"R{Enumerable.Range(1, level).Sum(commissionTerms.GetComponentAmount):0.00} cumulative weekly commission",
                    level => commissionTerms.GetComponentAmount(level),
                    v2StructuralResult),
                Joining = new MemberJoiningProgressDto
                {
                    Kind = "One-time AQGreen joining requirement",
                    RequiredAmount = joiningRequired,
                    PaidAmount = joiningPaid,
                    RemainingAmount = Math.Max(0m, joiningRequired - joiningPaid),
                    ProgressPercent = Percent(joiningPaid, joiningRequired),
                    ScheduleLabel = participation?.JoiningPaymentSchedule switch
                    {
                        AQGreenJoiningPaymentSchedule.Full => "One payment",
                        AQGreenJoiningPaymentSchedule.TwoInstallments => "Two instalments",
                        null when participation != null && participation.JoiningPaymentAmount <= 0m => "Historical two-stage payment",
                        _ => "Choose one payment or two instalments"
                    },
                    IsComplete = joiningComplete,
                    CompletedAt = null
                },
                MonthlySubscription = new MemberMonthlyObligationSummaryDto
                {
                    Status = openObligation == null
                        ? obligations.Count > 0 ? "No recorded amount outstanding" : "No obligation recorded"
                        : ObligationStatusLabel(openObligation.Status),
                    MonthlyAmount = participation?.MonthlyCommitmentAmount ?? programmeTerms.MonthlyCommitmentAmount,
                    OutstandingAmount = openObligation?.OutstandingAmount,
                    DueAt = openObligation?.DueAt,
                    Explanation = openObligation == null
                        ? "This is a recurring monthly subscription, separate from the one-time joining payment."
                        : NextActionFor(openObligation),
                    RequiresAction = openObligation != null
                },
                Earnings = BuildAQGreenEarnings(commissions, periods, programmeTerms.Currency),
                Benefits = benefits,
                NextActionCode = nextAction.Code,
                NextActionTitle = nextAction.Title,
                NextActionBody = nextAction.Body
            };
        }

        private MemberProgrammeJourneyDto BuildOnyxJourney(
            OnyxParticipation participation,
            EffectiveProgrammeNetwork network,
            OnyxNetworkLevel qualifiedLevel,
            IReadOnlyCollection<OnyxWeeklyCommission> commissions,
            IReadOnlyDictionary<Guid, OnyxCommissionPeriod> periods,
            OnyxTravelBenefitEntitlement travelBenefit)
        {
            var programmeTerms = _programmeTermsProvider.GetDirectOnyxTerms();
            var commissionTerms = _commissionTermsProvider.GetOnyxTerms();
            var status = participation == null
                ? null
                : ProgrammeParticipationStatusPresenter.Describe(participation);
            var levelNumber = (int)qualifiedLevel;
            var benefits = new List<MemberProgrammeBenefitDto>();
            if (travelBenefit != null)
            {
                benefits.Add(new MemberProgrammeBenefitDto
                {
                    Code = "ONYX_TRAVEL",
                    Name = "Travel benefit",
                    State = travelBenefit.Status == OnyxTravelBenefitStatus.Active
                        ? "Available"
                        : "Waiting period",
                    Description = $"Earned after completing Onyx Level 3. You contribute {travelBenefit.MemberTripContributionPercent:0.##}% when a future trip is arranged.",
                    UnlockedAt = travelBenefit.EligibleAt,
                    AvailableAt = travelBenefit.ActivatedAt ?? travelBenefit.WaitingPeriodEndsAt
                });
            }
            else
            {
                benefits.Add(new MemberProgrammeBenefitDto
                {
                    Code = "ONYX_TRAVEL",
                    Name = "Travel benefit",
                    State = levelNumber < 3 ? "Locked" : "Pending record",
                    Description = levelNumber < 3
                        ? "Unlocks after completing Onyx Level 3."
                        : "Level 3 is complete, but no travel-benefit entitlement record is available yet."
                });
            }

            var nextAction = ProgrammeNextAction(
                participation != null,
                status?.IsActive ?? false,
                status?.Status,
                levelNumber,
                OnyxNetworkQualificationEvaluator.HighestConfirmedStructuralLevel,
                "Onyx");
            var isGraduation = participation?.AdmissionRoute == OnyxAdmissionRoute.EntryGraduation;
            var required = isGraduation
                ? 0m
                : participation?.DirectEntryAmount ?? programmeTerms.DirectEntryAmount;
            var paid = participation?.DirectEntryPaymentId.HasValue == true
                ? required
                : 0m;
            var joiningComplete = isGraduation || paid == required && required > 0m;
            return new MemberProgrammeJourneyDto
            {
                ProgrammeCode = "ONYX",
                ProgrammeName = "Onyx",
                HasParticipation = participation != null,
                ParticipationStatus = status?.Status ?? "Not joined",
                DecisionReason = participation?.ApprovalDecisions
                    .OrderByDescending(item => item.DecidedAt)
                    .FirstOrDefault()?.Reason,
                IsActive = status?.IsActive ?? false,
                StartedAt = participation?.StartedAt,
                ActivatedAt = participation?.ActivatedAt,
                Currency = participation?.Currency ?? programmeTerms.Currency,
                QualifiedLevel = levelNumber,
                MaximumLevel = OnyxNetworkQualificationEvaluator.HighestConfirmedStructuralLevel,
                ActivationSteps = BuildActivationSteps(
                    participation != null,
                    joiningComplete,
                    participation?.Status == OnyxParticipationStatus.PaymentConfirmedAwaitingApproval,
                    participation?.Status == OnyxParticipationStatus.Active,
                    participation?.Status == OnyxParticipationStatus.Rejected,
                    isGraduation ? "Loan-backed admission" : "Joining payment",
                    isGraduation
                        ? "The approved Onyx loan-backed admission is confirmed."
                        : "The full joining requirement is confirmed."),
                Levels = BuildLevels(
                    network,
                    participation?.CustomerId,
                    levelNumber,
                    OnyxNetworkQualificationEvaluator.HighestConfirmedStructuralLevel,
                    status?.IsActive ?? false,
                    level => OnyxNetworkQualificationEvaluator.GetRequiredPopulation((OnyxNetworkLevel)level),
                    level => commissionTerms.GetPerPersonRate((OnyxNetworkLevel)level),
                    _ => "per qualifying person",
                    level => commissionTerms.GetLevelComponentAmount((OnyxNetworkLevel)level)),
                Joining = new MemberJoiningProgressDto
                {
                    Kind = isGraduation
                        ? "AQGreen graduation with an Onyx loan"
                        : "One-time direct Onyx joining requirement",
                    RequiredAmount = required,
                    PaidAmount = paid,
                    RemainingAmount = Math.Max(0m, required - paid),
                    ProgressPercent = Percent(paid, required),
                    ScheduleLabel = isGraduation
                        ? "Loan-backed admission"
                        : "One full payment only",
                    IsComplete = joiningComplete
                },
                MonthlySubscription = null,
                Earnings = BuildOnyxEarnings(commissions, periods, programmeTerms.Currency),
                Benefits = benefits,
                NextActionCode = nextAction.Code,
                NextActionTitle = nextAction.Title,
                NextActionBody = nextAction.Body
            };
        }

        private static IReadOnlyList<MemberActivationStepDto> BuildActivationSteps(
            bool started,
            bool paymentComplete,
            bool awaitingApproval,
            bool active,
            bool declined,
            string joiningLabel = "Joining payment",
            string joiningCompleteExplanation = "The full joining requirement is confirmed.")
        {
            return new[]
            {
                new MemberActivationStepDto
                {
                    Code = "Started",
                    Label = "Joining started",
                    State = started ? "Complete" : "Current",
                    Explanation = started ? "Your programme place has been created." : "Choose a programme to begin."
                },
                new MemberActivationStepDto
                {
                    Code = "Payment",
                    Label = joiningLabel,
                    State = paymentComplete ? "Complete" : started ? "Current" : "Upcoming",
                    Explanation = paymentComplete ? joiningCompleteExplanation : "Complete the joining requirement."
                },
                new MemberActivationStepDto
                {
                    Code = "Approval",
                    Label = "Area approval",
                    State = active ? "Complete" : declined ? "Declined" : awaitingApproval ? "Current" : "Upcoming",
                    Explanation = active ? "Area approval is complete." : declined ? "Area approval was declined. Review the recorded decision reason." : awaitingApproval ? "The Area team must approve activation next." : "Available after payment is complete."
                },
                new MemberActivationStepDto
                {
                    Code = "Active",
                    Label = "Programme active",
                    State = active ? "Complete" : declined ? "Declined" : "Upcoming",
                    Explanation = active ? "Your programme is active." : declined ? "Activation did not occur." : "Network progression begins after activation."
                }
            };
        }

        private static IReadOnlyList<MemberLevelProgressDto> BuildLevels(
            EffectiveProgrammeNetwork network,
            int? customerId,
            int qualifiedLevel,
            int maximumLevel,
            bool progressionActive,
            Func<int, int> requiredCount,
            Func<int, decimal?> commissionRate,
            Func<int, string> commissionRateLabel,
            Func<int, decimal> componentAmount,
            AQGreenStructuralCompletionResult v2StructuralResult = null)
        {
            return Enumerable.Range(1, maximumLevel)
                .Select(level =>
                {
                    var required = requiredCount(level);
                    var achieved = v2StructuralResult != null
                        ? v2StructuralResult.GetQualifyingCountAtRelativeDepth(level)
                        : customerId.HasValue
                            ? network.CountSelectedParticipantsAtDepth(
                                customerId.Value,
                                level)
                            : 0;
                    return new MemberLevelProgressDto
                    {
                        Level = level,
                        Label = $"Level {level}",
                        State = progressionActive
                            ? LevelState(level, qualifiedLevel, maximumLevel)
                            : "Locked",
                        MeasureLabel = v2StructuralResult != null
                            ? "Qualifying placement occupants"
                            : level == 1
                                ? "Direct recruits"
                                : "Qualifying network members",
                        AchievedCount = achieved,
                        RequiredCount = required,
                        RemainingCount = Math.Max(0, required - achieved),
                        ProgressPercent = Percent(achieved, required),
                        IsStructurallyComplete = level <= qualifiedLevel,
                        CommissionRate = commissionRate(level),
                        CommissionRateLabel = commissionRateLabel(level),
                        CommissionComponentAmount = componentAmount(level)
                    };
                })
                .ToList();
        }

        private static string LevelState(int level, int qualifiedLevel, int maximumLevel)
        {
            if (level <= qualifiedLevel) return "Complete";
            if (level == qualifiedLevel + 1) return "Current";
            if (level == qualifiedLevel + 2 && level <= maximumLevel) return "Next";
            return "Locked";
        }

        private static int Percent(decimal achieved, decimal required) =>
            required <= 0m
                ? 0
                : Math.Min(100, (int)Math.Round(achieved * 100m / required));

        private static MemberProgrammeEarningsDto BuildAQGreenEarnings(
            IReadOnlyCollection<EntryWeeklyCommission> commissions,
            IReadOnlyDictionary<Guid, EntryCommissionPeriod> periods,
            string defaultCurrency)
        {
            var recent = commissions
                .Where(item => periods.ContainsKey(item.CommissionPeriodId))
                .Select(item => MapJourneyEarning(item, periods[item.CommissionPeriodId]))
                .OrderByDescending(item => item.PeriodEnd)
                .Take(MaxRecentEarnings)
                .ToList();
            return new MemberProgrammeEarningsDto
            {
                Currency = commissions.Select(item => item.Currency).FirstOrDefault() ?? defaultCurrency,
                TotalEarned = commissions
                    .Where(item => item.PayoutStatus != WeeklyCommissionPayoutStatus.NotEarned)
                    .Sum(item => item.TotalAmount),
                EarnedAwaitingRelease = commissions
                    .Where(item => item.PayoutStatus == WeeklyCommissionPayoutStatus.Earned)
                    .Sum(item => item.TotalAmount),
                OnHold = commissions
                    .Where(item => item.PayoutStatus == WeeklyCommissionPayoutStatus.Held)
                    .Sum(item => item.TotalAmount),
                ReleasedAwaitingPayment = commissions
                    .Where(item => item.PayoutStatus == WeeklyCommissionPayoutStatus.Released)
                    .Sum(item => item.TotalAmount),
                RecordedAsPaid = commissions
                    .Where(item => item.PayoutStatus == WeeklyCommissionPayoutStatus.Paid)
                    .Sum(item => item.TotalAmount),
                LatestRecordedWeek = recent.FirstOrDefault(),
                RecentWeeks = recent
            };
        }

        private static MemberProgrammeEarningsDto BuildOnyxEarnings(
            IReadOnlyCollection<OnyxWeeklyCommission> commissions,
            IReadOnlyDictionary<Guid, OnyxCommissionPeriod> periods,
            string defaultCurrency)
        {
            var recent = commissions
                .Where(item => periods.ContainsKey(item.CommissionPeriodId))
                .Select(item => MapJourneyEarning(item, periods[item.CommissionPeriodId]))
                .OrderByDescending(item => item.PeriodEnd)
                .Take(MaxRecentEarnings)
                .ToList();
            return new MemberProgrammeEarningsDto
            {
                Currency = commissions.Select(item => item.Currency).FirstOrDefault() ?? defaultCurrency,
                TotalEarned = commissions
                    .Where(item => item.PayoutStatus != WeeklyCommissionPayoutStatus.NotEarned)
                    .Sum(item => item.TotalAmount),
                EarnedAwaitingRelease = commissions
                    .Where(item => item.PayoutStatus == WeeklyCommissionPayoutStatus.Earned)
                    .Sum(item => item.TotalAmount),
                OnHold = commissions
                    .Where(item => item.PayoutStatus == WeeklyCommissionPayoutStatus.Held)
                    .Sum(item => item.TotalAmount),
                ReleasedAwaitingPayment = commissions
                    .Where(item => item.PayoutStatus == WeeklyCommissionPayoutStatus.Released)
                    .Sum(item => item.TotalAmount),
                RecordedAsPaid = commissions
                    .Where(item => item.PayoutStatus == WeeklyCommissionPayoutStatus.Paid)
                    .Sum(item => item.TotalAmount),
                LatestRecordedWeek = recent.FirstOrDefault(),
                RecentWeeks = recent
            };
        }

        private static MemberProgrammeCycleEarningDto MapJourneyEarning(
            EntryWeeklyCommission commission,
            EntryCommissionPeriod period) =>
            new()
            {
                PeriodStart = period.PeriodStart,
                PeriodEnd = period.PeriodEnd,
                TotalAmount = commission.TotalAmount,
                Status = CommissionPayoutStatusPresenter.ToBusinessLabel(commission.PayoutStatus),
                HoldReason = commission.HoldReason,
                ZeroReason = commission.PayoutStatus == WeeklyCommissionPayoutStatus.NotEarned
                    ? "No complete network level was achieved when this week closed."
                    : null,
                QualifiedLevel = commission.HighestQualifiedNetworkLevel,
                CommissionedLevel = commission.HighestCommissionedLevel,
                Components = commission.Components
                    .OrderBy(item => item.Level)
                    .Select(item => new MemberEarningComponentDto
                    {
                        Level = item.Level,
                        Amount = item.Amount
                    })
                    .ToList()
            };

        private static MemberProgrammeCycleEarningDto MapJourneyEarning(
            OnyxWeeklyCommission commission,
            OnyxCommissionPeriod period) =>
            new()
            {
                PeriodStart = period.PeriodStart,
                PeriodEnd = period.PeriodEnd,
                TotalAmount = commission.TotalAmount,
                Status = CommissionPayoutStatusPresenter.ToBusinessLabel(commission.PayoutStatus),
                ZeroReason = commission.PayoutStatus == WeeklyCommissionPayoutStatus.NotEarned
                    ? "No complete network level was achieved when this week closed."
                    : null,
                QualifiedLevel = commission.HighestQualifiedNetworkLevel,
                CommissionedLevel = commission.HighestCommissionedLevel,
                Components = commission.Components
                    .OrderBy(item => item.Level)
                    .Select(item => new MemberEarningComponentDto
                    {
                        Level = item.Level,
                        Amount = item.Amount
                    })
                    .ToList()
            };

        private static JourneyNextAction AQGreenNextAction(
            EntryParticipation participation,
            EntryMonthlyObligation openObligation,
            int qualifiedLevel,
            bool joiningComplete)
        {
            if (participation == null)
            {
                return new JourneyNextAction(
                    "JoinProgramme",
                    "Start your AQGreen journey",
                    "Choose a joining payment option to begin.");
            }

            if (!joiningComplete)
            {
                return new JourneyNextAction(
                    "CompleteJoiningPayment",
                    "Complete your one-time joining payment",
                    $"{Math.Max(0m, GetAQGreenJoiningRequiredAmount(participation) - GetAQGreenJoiningPaidAmount(participation)):0.00} {participation.Currency} remains.");
            }

            if (participation.Status == EntryParticipationStatus.PaymentConfirmedAwaitingApproval)
            {
                return new JourneyNextAction(
                    "AwaitApproval",
                    "Await Area approval",
                    "Your joining payment is complete. Do not pay it again; the Area team must approve activation next.");
            }

            if (participation.Status != EntryParticipationStatus.Active)
            {
                return new JourneyNextAction(
                    "ReviewParticipation",
                    "Review your participation status",
                    ProgrammeParticipationStatusPresenter.Describe(participation).Status);
            }

            if (openObligation != null)
            {
                return new JourneyNextAction(
                    "ResolveMonthlySubscription",
                    "Review your monthly subscription",
                    NextActionFor(openObligation));
            }

            return ProgrammeNextAction(
                true,
                participation.Status == EntryParticipationStatus.Active,
                ProgrammeParticipationStatusPresenter.Describe(participation).Status,
                qualifiedLevel,
                EntryNetworkQualificationEvaluator.MaximumLevel,
                "AQGreen");
        }

        private static JourneyNextAction ProgrammeNextAction(
            bool hasParticipation,
            bool isActive,
            string status,
            int qualifiedLevel,
            int maximumLevel,
            string programmeName)
        {
            if (!hasParticipation)
            {
                return new JourneyNextAction(
                    "JoinProgramme",
                    $"Explore {programmeName}",
                    $"Review the {programmeName} joining requirement and begin when ready.");
            }

            if (!isActive)
            {
                return status == "Awaiting Area approval"
                    ? new JourneyNextAction(
                        "AwaitApproval",
                        "Await Area approval",
                        "Payment is complete. Programme activation is the next step.")
                    : new JourneyNextAction(
                        "ReviewParticipation",
                        "Review your participation status",
                        status ?? "Your participation is not active yet.");
            }

            if (qualifiedLevel >= maximumLevel)
            {
                return new JourneyNextAction(
                    "ReviewEarnings",
                    "Review your weekly earnings",
                    $"You have completed every confirmed {programmeName} structural level.");
            }

            return new JourneyNextAction(
                "InviteMembers",
                qualifiedLevel == 0 ? "Invite your first member" : $"Build toward Level {qualifiedLevel + 1}",
                "Share your programme invitation to grow your qualifying network.");
        }

        private sealed record JourneyNextAction(string Code, string Title, string Body);

        private static decimal GetAQGreenJoiningRequiredAmount(
            EntryParticipation participation) =>
            participation.JoiningPaymentAmount > 0m
                ? participation.JoiningPaymentAmount
                : participation.RegistrationPaymentAmount +
                  participation.ActivationPaymentAmount;

        private static decimal GetAQGreenJoiningPaidAmount(
            EntryParticipation participation)
        {
            if (participation.JoiningPaymentSchedule.HasValue)
            {
                return participation.GetConfirmedJoiningAmount();
            }

            return (participation.JoiningPaymentId.HasValue
                ? participation.JoiningPaymentAmount
                : 0m) +
            (participation.RegistrationPaymentId.HasValue
                ? participation.RegistrationPaymentAmount
                : 0m) +
            (participation.ActivationPaymentId.HasValue
                ? participation.ActivationPaymentAmount
                : 0m);
        }

        [AbpAuthorize(AquaPermissions.ProgrammeParticipations.ViewSelf)]
        public async Task<MyProgrammeProgressDto> GetMyProgressAsync()
        {
            var tenantId = GetRequiredTenantId(
                "Your AQGreen programme progress is unavailable.");
            var customer = await _customerRepository.FirstOrDefaultAsync(
                item =>
                    item.TenantId == tenantId &&
                    item.UserId == AbpSession.GetUserId());
            if (customer == null || !customer.IsActive)
            {
                throw new UserFriendlyException(
                    "Your AQGreen programme progress is unavailable.",
                    "An active Club Member account is required.");
            }

            var participation = await _entryParticipationRepository
                .FirstOrDefaultAsync(item => item.CustomerId == customer.Id);
            var activeParticipations = participation == null
                ? new List<EntryParticipation>()
                : await _entryParticipationRepository
                    .GetAllIncluding(item => item.RecruiterCorrections)
                    .Where(item =>
                        item.TenantId == tenantId &&
                        item.Status == EntryParticipationStatus.Active)
                    .ToListAsync();
            var commissions = await _entryCommissionRepository
                .GetAllIncluding(commission => commission.Components)
                .Where(commission => commission.CustomerId == customer.Id)
                .OrderByDescending(commission => commission.CalculatedAt)
                .ToListAsync();
            var periods = await LoadPeriodsAsync(
                commissions.Select(commission => commission.CommissionPeriodId));
            var obligations = await _obligationRepository.GetAll()
                .Where(obligation => obligation.CustomerId == customer.Id)
                .OrderByDescending(obligation => obligation.PeriodYear)
                .ThenByDescending(obligation => obligation.PeriodMonth)
                .ToListAsync();
            var funeralCover = await _funeralCoverRepository.FirstOrDefaultAsync(
                entitlement => entitlement.CustomerId == customer.Id);

            var programmeTerms = _programmeTermsProvider.GetEntryTerms();
            var commissionTerms = _commissionTermsProvider.GetEntryTerms();
            var v2Enabled = participation != null
                && await _v2ProgressGate.IsEnabledAsync(tenantId, participation.Id);
            AQGreenStructuralCompletionResult v2StructuralResult = null;
            EntryNetworkLevel entryLevel;
            int? v2CreditedRecruitCount = null;
            if (v2Enabled)
            {
                v2StructuralResult = await EvaluateV2StructuralProgressAsync(
                    tenantId,
                    participation.Id,
                    UtcNow);
                entryLevel = MapV2StructuralLevel(
                    v2StructuralResult.StructuralCompletionLevel);
                v2CreditedRecruitCount = await (
                        from attribution in _attributionRepository.GetAll()
                        join recruit in _entryParticipationRepository.GetAll()
                            on attribution.ParticipantId equals recruit.Id
                        where attribution.TenantId == tenantId &&
                              attribution.CreditedSponsorParticipantId == participation.Id &&
                              attribution.AttributionKind ==
                                  AQGreenRecruitmentAttributionKind.SponsoredParticipant &&
                              recruit.TenantId == tenantId &&
                              recruit.Status == EntryParticipationStatus.Active
                        select attribution.Id)
                    .CountAsync();
            }
            else
            {
                // This is the exact legacy V1 projection boundary. V2's current
                // cutoff must not change the disabled-gate read path.
                entryLevel = participation == null
                    ? EntryNetworkLevel.None
                    : new EntryNetworkQualificationEvaluator().Evaluate(
                        customer.Id,
                        EffectiveProgrammeNetwork.BuildAQGreen(
                            tenantId,
                            activeParticipations,
                            DateTime.MaxValue));
            }

            return BuildProgress(
                participation,
                entryLevel,
                v2StructuralResult,
                v2CreditedRecruitCount,
                activeParticipations,
                commissions,
                periods,
                obligations,
                funeralCover,
                programmeTerms,
                commissionTerms);
        }

        private static MyProgrammeProgressDto BuildProgress(
            EntryParticipation participation,
            EntryNetworkLevel qualifiedLevel,
            AQGreenStructuralCompletionResult v2StructuralResult,
            int? v2CreditedRecruitCount,
            IReadOnlyCollection<EntryParticipation> activeParticipations,
            IReadOnlyCollection<EntryWeeklyCommission> commissions,
            IReadOnlyDictionary<Guid, EntryCommissionPeriod> periods,
            IReadOnlyList<EntryMonthlyObligation> obligations,
            AQGreenFuneralCoverEntitlement funeralCover,
            EntryProgrammeTerms programmeTerms,
            EntryCommissionTerms commissionTerms)
        {
            var directRecruits = v2CreditedRecruitCount ??
                (participation == null
                    ? 0
                    : activeParticipations.Count(item =>
                        item.RecruiterCustomerId == participation.CustomerId));
            var nextLevel = NextLevel(qualifiedLevel);
            var progressPercent = directRecruits <= 0
                ? 0
                : Math.Min(
                    100,
                    (int)Math.Round(
                        directRecruits * 100d /
                        EntryNetworkQualificationEvaluator.BranchSize));

            var earned = CommissionsIn(
                commissions,
                WeeklyCommissionPayoutStatus.Earned);
            var held = CommissionsIn(
                commissions,
                WeeklyCommissionPayoutStatus.Held);
            var released = CommissionsIn(
                commissions,
                WeeklyCommissionPayoutStatus.Released);
            var paid = CommissionsIn(
                commissions,
                WeeklyCommissionPayoutStatus.Paid);
            var totalEarned = earned + held + released + paid;

            var openObligation = obligations
                .Where(obligation =>
                    obligation.Status != EntryMonthlyObligationStatus.Paid)
                .OrderBy(obligation => obligation.PeriodYear)
                .ThenBy(obligation => obligation.PeriodMonth)
                .FirstOrDefault();

            return new MyProgrammeProgressDto
            {
                HasEntryParticipation = participation != null,
                QualifiedLevelLabel = LevelLabel(qualifiedLevel),
                QualifiedLevel = (int)qualifiedLevel,
                NextLevelLabel = nextLevel.HasValue
                    ? LevelLabel(nextLevel.Value)
                    : null,
                DirectRecruits = directRecruits,
                DirectRecruitsRequired =
                    EntryNetworkQualificationEvaluator.BranchSize,
                RecruitsRemaining = Math.Max(
                    0,
                    EntryNetworkQualificationEvaluator.BranchSize -
                    directRecruits),
                RecruitmentProgressPercent = progressPercent,
                StructuralProgress = v2StructuralResult == null
                    ? null
                    : BuildV2StructuralProgress(v2StructuralResult),
                Currency = programmeTerms.Currency,
                TotalEarned = totalEarned,
                EarnedAwaitingRelease = earned,
                OnHold = held,
                ReleasedAwaitingPayment = released,
                Paid = paid,
                RecentEarnings = commissions
                    .Take(MaxRecentEarnings)
                    .Select(commission => MapEarning(
                        commission,
                        periods[commission.CommissionPeriodId]))
                    .ToList(),
                MonthlyObligationStatus = openObligation != null
                    ? ObligationStatusLabel(openObligation.Status)
                    : obligations.Count > 0
                        ? "Paid"
                        : null,
                MonthlyObligationAmount = obligations.Count > 0
                    ? obligations[0].AmountDue
                    : (decimal?)null,
                MonthlyObligationDueAt = openObligation?.DueAt,
                MonthlyObligationOutstanding = openObligation?.OutstandingAmount,
                NextAction = NextActionFor(openObligation),
                NextActionAmount = openObligation?.OutstandingAmount,
                FuneralCoverIncluded = funeralCover != null,
                FuneralCoverBenefitAmount = funeralCover?.FuneralCoverAmount ?? 0m,
                Education = EducationItems(
                    programmeTerms,
                    commissionTerms,
                    v2StructuralResult != null)
            };
        }

        private static MemberWeeklyEarningDto MapEarning(
            EntryWeeklyCommission commission,
            EntryCommissionPeriod period) =>
            new MemberWeeklyEarningDto
            {
                PeriodStart = period.PeriodStart,
                PeriodEnd = period.PeriodEnd,
                TotalAmount = commission.TotalAmount,
                Status = CommissionPayoutStatusPresenter.ToBusinessLabel(
                    commission.PayoutStatus),
                HoldReason = commission.HoldReason,
                HighestLevel = commission.HighestCommissionedLevel,
                HighestQualifiedLevel = commission.HighestQualifiedNetworkLevel,
                HighestCommissionedLevel = commission.HighestCommissionedLevel,
                CalculatedAt = commission.CalculatedAt,
                Components = commission.Components
                    .OrderBy(component => component.Level)
                    .Select(component => new MemberEarningComponentDto
                    {
                        Level = component.Level,
                        Amount = component.Amount
                    })
                    .ToList()
            };

        private static decimal CommissionsIn(
            IEnumerable<EntryWeeklyCommission> commissions,
            WeeklyCommissionPayoutStatus status) =>
            commissions
                .Where(commission =>
                    commission.PayoutStatus == status)
                .Sum(commission => commission.TotalAmount);

        private static EntryNetworkLevel? NextLevel(EntryNetworkLevel level) =>
            level switch
            {
                EntryNetworkLevel.None => EntryNetworkLevel.Level1,
                EntryNetworkLevel.Level1 => EntryNetworkLevel.Level2,
                EntryNetworkLevel.Level2 => EntryNetworkLevel.Level3,
                _ => null
            };

        private static string LevelLabel(EntryNetworkLevel level) =>
            level switch
            {
                EntryNetworkLevel.Level1 => "Level 1",
                EntryNetworkLevel.Level2 => "Level 2",
                EntryNetworkLevel.Level3 => "Level 3",
                _ => "Not yet qualified"
            };

        private static string ObligationStatusLabel(
            EntryMonthlyObligationStatus status) =>
            status switch
            {
                EntryMonthlyObligationStatus.Due => "Payment due",
                EntryMonthlyObligationStatus.GracePeriod =>
                    "Grace period",
                EntryMonthlyObligationStatus.Overdue => "Overdue",
                EntryMonthlyObligationStatus.Paid => "Paid",
                _ => status.ToString()
            };

        private static string NextActionFor(
            EntryMonthlyObligation openObligation)
        {
            if (openObligation == null)
            {
                return "You are up to date on your AQGreen monthly subscription.";
            }

            return openObligation.Status ==
                   EntryMonthlyObligationStatus.Overdue
                ? "Pay your overdue AQGreen subscription to restore your weekly earnings eligibility."
                : "Pay your AQGreen monthly subscription.";
        }

        private async Task<Dictionary<Guid, EntryCommissionPeriod>>
            LoadPeriodsAsync(IEnumerable<Guid> periodIds)
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

        private async Task<AQGreenStructuralCompletionResult>
            EvaluateV2StructuralProgressAsync(
            int tenantId,
            Guid participantId,
            DateTime cutoff)
        {
            try
            {
                var result = await _v2StructuralEvaluator.EvaluateAsync(
                    tenantId,
                    participantId,
                    cutoff);
                _ = MapV2StructuralLevel(result.StructuralCompletionLevel);
                return result;
            }
            catch (AQGreenStructuralEvaluationNotPlacedException)
            {
                throw new UserFriendlyException(
                    "Your AQGreen structural progress cannot be evaluated.",
                    "You do not have an authoritative V2 placement. Contact your Area administrator.");
            }
            catch (AQGreenStructuralContributionPolicyRequiredException)
            {
                throw new UserFriendlyException(
                    "Your AQGreen structural progress cannot be evaluated.",
                    "Your participation has a lifecycle state that requires a business policy decision (AQG-V2-D08). Contact your Area administrator.");
            }
            catch (AQGreenPlacementTopologyIntegrityException ex)
            {
                throw new UserFriendlyException(
                    "Your AQGreen structural progress cannot be evaluated.",
                    ex.Message);
            }
        }

        private static EntryNetworkLevel MapV2StructuralLevel(AQGreenStructuralCompletionLevel v2Level)
        {
            return v2Level switch
            {
                AQGreenStructuralCompletionLevel.Level0 => EntryNetworkLevel.None,
                AQGreenStructuralCompletionLevel.Level1 => EntryNetworkLevel.Level1,
                AQGreenStructuralCompletionLevel.Level2 => EntryNetworkLevel.Level2,
                AQGreenStructuralCompletionLevel.Level3 => EntryNetworkLevel.Level3,
                _ => throw new AQGreenPlacementTopologyIntegrityException(
                    $"AQGreen structural progress returned unsupported completion " +
                    $"level {(int)v2Level}.")
            };
        }

        private static AQGreenStructuralProgressDto BuildV2StructuralProgress(
            AQGreenStructuralCompletionResult result)
        {
            int? targetLevel;
            int targetRelativeDepth;
            switch (result.StructuralCompletionLevel)
            {
                case AQGreenStructuralCompletionLevel.Level0:
                    targetLevel = 1;
                    targetRelativeDepth = 1;
                    break;
                case AQGreenStructuralCompletionLevel.Level1:
                    targetLevel = 2;
                    targetRelativeDepth = 2;
                    break;
                case AQGreenStructuralCompletionLevel.Level2:
                    targetLevel = 3;
                    targetRelativeDepth = 3;
                    break;
                case AQGreenStructuralCompletionLevel.Level3:
                    targetLevel = null;
                    targetRelativeDepth = 3;
                    break;
                default:
                    throw new AQGreenPlacementTopologyIntegrityException(
                        $"AQGreen structural progress returned unsupported completion " +
                        $"level {(int)result.StructuralCompletionLevel}.");
            }

            var required = AQGreenStructuralCompletionCalculator
                .GetRequiredPopulation(targetRelativeDepth);
            var achieved = result.GetQualifyingCountAtRelativeDepth(
                targetRelativeDepth);
            return new AQGreenStructuralProgressDto
            {
                CompletedLevel = (int)result.StructuralCompletionLevel,
                TargetLevel = targetLevel,
                AchievedCount = achieved,
                RequiredCount = required,
                RemainingCount = Math.Max(0, required - achieved),
                ProgressPercent = Percent(achieved, required),
                MeasureLabel = "Qualifying placement occupants",
                Cutoff = result.Cutoff,
                RulesVersion = result.RulesVersion
            };
        }

        private static IReadOnlyList<ProgrammeEducationItemDto> EducationItems(
            EntryProgrammeTerms programmeTerms,
            EntryCommissionTerms commissionTerms,
            bool v2StructuralProgress = false)
        {
            var currency = programmeTerms.Currency;
            return new List<ProgrammeEducationItemDto>
            {
                new()
                {
                    Title = "Your R1,200 joining payment",
                    Body =
                        "AQGreen joining is a one-time R1,200 payment, paid " +
                        "once or in two R600 instalments. Completing the joining " +
                        "obligation includes the R30,000 funeral-cover benefit. " +
                        "Your separate R600 monthly subscription keeps your " +
                        "participation active and is not a joining instalment."
                },
                new()
                {
                    Title = "Build your network",
                    Body = v2StructuralProgress
                        ? "AQGreen V2 structural progress counts qualifying " +
                          "placement occupants at relative depths 1, 2, and 3. " +
                          "Spillover can contribute even when you did not " +
                          "personally recruit the occupant. Personal recruitment " +
                          "credit remains a separate fact. Level 3 is the final " +
                          "AQGreen structural level."
                        :
                        "Invite Club Members to join AQGreen under you. Every " +
                        "level needs 5 active direct recruits, each of whom has " +
                        "completed their own joining: Level 1 needs 5 direct " +
                        $"recruits, Level 2 needs 25 across your network, and " +
                        $"Level 3 needs 125. Level 3 is the final AQGreen " +
                        "level. Progress counts only active " +
                        "participations."
                },
                new()
                {
                    Title = "Weekly earnings",
                    Body =
                        $"Each completed level earns a weekly component of " +
                        $"{commissionTerms.GetComponentAmount(1):0.00}, " +
                        $"{commissionTerms.GetComponentAmount(2):0.00}, and " +
                        $"{commissionTerms.GetComponentAmount(3):0.00} " +
                        $"({currency}) for Levels 1, 2, and 3. AQGreen ends " +
                        "at Level 3. Earnings are " +
                        "held until they are released for payment. While your " +
                        "own AQGreen subscription is overdue, your own weekly " +
                        "earnings are held."
                },
                new()
                {
                    Title = "Your monthly subscription",
                    Body =
                        $"A {programmeTerms.MonthlyCommitmentAmount:0.00} " +
                        $"{currency} AQGreen monthly subscription falls due " +
                        $"each month with a {programmeTerms.GracePeriodDays}-day " +
                        "grace period. Paying it on time keeps your own weekly " +
                        "earnings moving."
                }
            };
        }
    }
}
