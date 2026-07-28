using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using AqualLifeStyle.Domain.Payments;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Payments.Yoco
{
    public sealed class StaleYocoCheckoutSnapshot
    {
        public int AQGreenCount { get; }
        public int OnyxCount { get; }
        public DateTime? OldestCheckoutCreatedAt { get; }
        public int TotalCount => AQGreenCount + OnyxCount;

        public StaleYocoCheckoutSnapshot(
            int aqGreenCount,
            int onyxCount,
            DateTime? oldestCheckoutCreatedAt)
        {
            AQGreenCount = aqGreenCount;
            OnyxCount = onyxCount;
            OldestCheckoutCreatedAt = oldestCheckoutCreatedAt;
        }
    }

    /// <summary>
    /// Finds provider checkouts that still await confirmation after the operational threshold.
    /// It does not infer that payment succeeded; operations must reconcile them with Yoco.
    /// </summary>
    public class StaleYocoCheckoutDetector : ITransientDependency
    {
        private readonly IRepository<AQGreenJoiningCheckout, Guid> _aqGreenCheckoutRepository;
        private readonly IRepository<DirectOnyxCheckoutIntent, Guid> _onyxCheckoutRepository;
        private readonly IUnitOfWorkManager _unitOfWorkManager;

        public StaleYocoCheckoutDetector(
            IRepository<AQGreenJoiningCheckout, Guid> aqGreenCheckoutRepository,
            IRepository<DirectOnyxCheckoutIntent, Guid> onyxCheckoutRepository,
            IUnitOfWorkManager unitOfWorkManager)
        {
            _aqGreenCheckoutRepository = aqGreenCheckoutRepository;
            _onyxCheckoutRepository = onyxCheckoutRepository;
            _unitOfWorkManager = unitOfWorkManager;
        }

        [UnitOfWork]
        public virtual async Task<StaleYocoCheckoutSnapshot> DetectAsync(DateTime cutoffUtc)
        {
            if (cutoffUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("The stale-checkout cutoff must be UTC.", nameof(cutoffUtc));

            using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MustHaveTenant))
            {
                var aqGreenQuery = _aqGreenCheckoutRepository.GetAll().Where(checkout =>
                    checkout.Status == HostedPaymentCheckoutStatus.AwaitingPayment &&
                    checkout.CheckoutCreatedAt <= cutoffUtc);
                var onyxQuery = _onyxCheckoutRepository.GetAll().Where(checkout =>
                    checkout.Status == HostedPaymentCheckoutStatus.AwaitingPayment &&
                    checkout.CheckoutCreatedAt <= cutoffUtc);

                var aqGreenCount = await aqGreenQuery.CountAsync();
                var onyxCount = await onyxQuery.CountAsync();
                var aqGreenOldest = await aqGreenQuery
                    .Select(checkout => checkout.CheckoutCreatedAt)
                    .MinAsync();
                var onyxOldest = await onyxQuery
                    .Select(checkout => checkout.CheckoutCreatedAt)
                    .MinAsync();

                return new StaleYocoCheckoutSnapshot(
                    aqGreenCount,
                    onyxCount,
                    Earlier(aqGreenOldest, onyxOldest));
            }
        }

        private static DateTime? Earlier(DateTime? first, DateTime? second)
        {
            if (!first.HasValue) return second;
            if (!second.HasValue) return first;
            return first.Value <= second.Value ? first : second;
        }
    }
}
