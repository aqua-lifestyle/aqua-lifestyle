using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.EntryMonthlyObligations
{
    /// <summary>
    /// Host-side production mechanism behind the recurring AQGreen R600 monthly
    /// obligations. Every operation is idempotent and can be re-run safely: a
    /// member never receives two obligations for the same period and a confirmed
    /// payment is never applied twice. The scheduler never invents due dates; the
    /// caller (or the due-date policy) supplies them.
    /// </summary>
    public class EntryMonthlyObligationScheduler
        : IEntryMonthlyObligationScheduler, ITransientDependency
    {
        private readonly IRepository<EntryMonthlyObligation, Guid> _obligationRepository;
        private readonly IRepository<EntryParticipation, Guid> _participationRepository;
        private readonly IRepository<MemberPayment, Guid> _paymentRepository;
        private readonly IUnitOfWorkManager _unitOfWorkManager;

        public EntryMonthlyObligationScheduler(
            IRepository<EntryMonthlyObligation, Guid> obligationRepository,
            IRepository<EntryParticipation, Guid> participationRepository,
            IRepository<MemberPayment, Guid> paymentRepository,
            IUnitOfWorkManager unitOfWorkManager)
        {
            _obligationRepository = obligationRepository;
            _participationRepository = participationRepository;
            _paymentRepository = paymentRepository;
            _unitOfWorkManager = unitOfWorkManager;
        }

        [UnitOfWork]
        public virtual async Task<int> EnsureObligationsForPeriodAsync(
            int periodYear,
            int periodMonth,
            DateTime dueAt)
        {
            ValidatePeriod(periodYear, periodMonth);
            ValidateUtc(dueAt, nameof(dueAt));

            using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MustHaveTenant))
            {
                var existingParticipationIds = await _obligationRepository.GetAll()
                    .Where(obligation =>
                        obligation.PeriodYear == periodYear &&
                        obligation.PeriodMonth == periodMonth)
                    .Select(obligation => obligation.EntryParticipationId)
                    .ToListAsync();
                var existingSet = new HashSet<Guid>(existingParticipationIds);

                var activeParticipations = await _participationRepository.GetAll()
                    .Where(participation =>
                        participation.Status == EntryParticipationStatus.Active)
                    .ToListAsync();

                var created = 0;
                foreach (var participation in activeParticipations)
                {
                    if (existingSet.Contains(participation.Id))
                    {
                        continue;
                    }

                    await _obligationRepository.InsertAsync(
                        EntryMonthlyObligation.Create(
                            participation,
                            periodYear,
                            periodMonth,
                            dueAt));
                    existingSet.Add(participation.Id);
                    created++;
                }

                if (created > 0)
                {
                    await _unitOfWorkManager.Current.SaveChangesAsync();
                }

                return created;
            }
        }

        [UnitOfWork]
        public virtual async Task<int> AssessObligationsAsync(DateTime asOf)
        {
            ValidateUtc(asOf, nameof(asOf));

            using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MustHaveTenant))
            {
                var unpaid = await _obligationRepository.GetAll()
                    .Where(obligation =>
                        obligation.Status != EntryMonthlyObligationStatus.Paid)
                    .ToListAsync();

                foreach (var obligation in unpaid)
                {
                    obligation.AssessStatus(asOf);
                }

                if (unpaid.Count > 0)
                {
                    await _unitOfWorkManager.Current.SaveChangesAsync();
                }

                return unpaid.Count;
            }
        }

        [UnitOfWork]
        public virtual async Task<int> AllocateConfirmedMonthlyPaymentsAsync()
        {
            using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MustHaveTenant))
            {
                var linkedPaymentIds = await _obligationRepository.GetAll()
                    .Where(obligation => obligation.PaymentId.HasValue)
                    .Select(obligation => obligation.PaymentId.Value)
                    .ToListAsync();
                var linkedSet = new HashSet<Guid>(linkedPaymentIds);

                var confirmedMonthlyPayments = await _paymentRepository.GetAll()
                    .Where(payment =>
                        payment.Status == MemberPaymentStatus.Confirmed &&
                        payment.Purpose == MemberPaymentPurpose.EntryMonthlyCommitment)
                    .ToListAsync();

                var openObligations = await _obligationRepository.GetAll()
                    .Where(obligation =>
                        obligation.Status != EntryMonthlyObligationStatus.Paid &&
                        obligation.PaymentId == null)
                    .OrderBy(obligation => obligation.PeriodYear)
                    .ThenBy(obligation => obligation.PeriodMonth)
                    .ToListAsync();

                var openByMember = openObligations
                    .GroupBy(obligation => new
                    {
                        obligation.TenantId,
                        obligation.CustomerId
                    })
                    .ToDictionary(group => group.Key, group => group.ToList());

                var allocated = 0;
                foreach (var payment in confirmedMonthlyPayments)
                {
                    if (linkedSet.Contains(payment.Id))
                    {
                        continue;
                    }

                    var memberKey = new { payment.TenantId, payment.CustomerId };
                    if (!openByMember.TryGetValue(memberKey, out var memberObligations))
                    {
                        continue;
                    }

                    var target = memberObligations.FirstOrDefault(obligation =>
                        obligation.AmountDue == payment.Amount &&
                        string.Equals(
                            obligation.Currency,
                            payment.Currency,
                            StringComparison.Ordinal));
                    if (target == null)
                    {
                        continue;
                    }

                    target.ApplyConfirmedPayment(payment);
                    memberObligations.Remove(target);
                    linkedSet.Add(payment.Id);
                    allocated++;
                }

                if (allocated > 0)
                {
                    await _unitOfWorkManager.Current.SaveChangesAsync();
                }

                return allocated;
            }
        }

        private static void ValidatePeriod(int periodYear, int periodMonth)
        {
            if (periodYear < 2000 || periodYear > 9999)
            {
                throw new ArgumentOutOfRangeException(nameof(periodYear));
            }

            if (periodMonth < 1 || periodMonth > 12)
            {
                throw new ArgumentOutOfRangeException(nameof(periodMonth));
            }
        }

        private static void ValidateUtc(DateTime value, string parameterName)
        {
            if (value == default)
            {
                throw new ArgumentException(
                    "A UTC timestamp is required.",
                    parameterName);
            }

            if (value.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "The timestamp must be UTC.",
                    parameterName);
            }
        }
    }
}
