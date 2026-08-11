using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Auditing;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Runtime.Session;
using Abp.UI;
using AqualLifeStyle.Application.Admin.Commissions;
using AqualLifeStyle.Application.ProgrammeParticipations.Dto;
using AqualLifeStyle.Authorization;
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
        private readonly IRepository<EntryWeeklyCommission, Guid>
            _entryCommissionRepository;
        private readonly IRepository<EntryCommissionPeriod, Guid>
            _entryPeriodRepository;
        private readonly IRepository<EntryMonthlyObligation, Guid>
            _obligationRepository;
        private readonly IRepository<AQGreenFuneralCoverEntitlement, Guid>
            _funeralCoverRepository;
        private readonly ICurrentProgrammeTermsProvider _programmeTermsProvider;
        private readonly ICurrentCommissionTermsProvider _commissionTermsProvider;

        public ClubMemberProgrammeProgressAppService(
            ICustomerRepository customerRepository,
            IRepository<EntryParticipation, Guid> entryParticipationRepository,
            IRepository<EntryWeeklyCommission, Guid> entryCommissionRepository,
            IRepository<EntryCommissionPeriod, Guid> entryPeriodRepository,
            IRepository<EntryMonthlyObligation, Guid> obligationRepository,
            IRepository<AQGreenFuneralCoverEntitlement, Guid> funeralCoverRepository,
            ICurrentProgrammeTermsProvider programmeTermsProvider,
            ICurrentCommissionTermsProvider commissionTermsProvider)
        {
            _customerRepository = customerRepository;
            _entryParticipationRepository = entryParticipationRepository;
            _entryCommissionRepository = entryCommissionRepository;
            _entryPeriodRepository = entryPeriodRepository;
            _obligationRepository = obligationRepository;
            _funeralCoverRepository = funeralCoverRepository;
            _programmeTermsProvider = programmeTermsProvider;
            _commissionTermsProvider = commissionTermsProvider;
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
            var level = participation == null
                ? EntryNetworkLevel.None
                : new EntryNetworkQualificationEvaluator().Evaluate(
                    customer.Id,
                    EffectiveProgrammeNetwork.BuildAQGreen(
                        tenantId,
                        activeParticipations,
                        DateTime.MaxValue));

            return BuildProgress(
                participation,
                level,
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
            IReadOnlyCollection<EntryParticipation> activeParticipations,
            IReadOnlyCollection<EntryWeeklyCommission> commissions,
            IReadOnlyDictionary<Guid, EntryCommissionPeriod> periods,
            IReadOnlyList<EntryMonthlyObligation> obligations,
            AQGreenFuneralCoverEntitlement funeralCover,
            EntryProgrammeTerms programmeTerms,
            EntryCommissionTerms commissionTerms)
        {
            var directRecruits = participation == null
                ? 0
                : activeParticipations.Count(item =>
                    item.RecruiterCustomerId == participation.CustomerId);
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
                Education = EducationItems(programmeTerms, commissionTerms)
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

        private static IReadOnlyList<ProgrammeEducationItemDto> EducationItems(
            EntryProgrammeTerms programmeTerms,
            EntryCommissionTerms commissionTerms)
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
                    Body =
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
