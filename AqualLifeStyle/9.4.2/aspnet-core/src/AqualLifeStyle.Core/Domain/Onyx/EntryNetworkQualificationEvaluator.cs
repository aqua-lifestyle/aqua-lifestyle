using System;
using System.Collections.Generic;
using System.Linq;

namespace AqualLifeStyle.Domain.Onyx
{
    public enum EntryNetworkLevel
    {
        None = 0,
        Level1 = 1,
        Level2 = 2,
        Level3 = 3
    }

    public sealed class EntryNetworkQualificationEvaluator
    {
        public const int BranchSize = 5;
        public const int MaximumLevel = 3;

        public EntryNetworkLevel Evaluate(
            int customerId,
            IEnumerable<EntryParticipation> participations)
        {
            if (customerId <= 0) throw new ArgumentOutOfRangeException(nameof(customerId));
            if (participations == null) throw new ArgumentNullException(nameof(participations));

            var qualified = participations
                .Where(participation => participation.IsQualifiedForNetwork)
                .ToList();
            EnsureCustomerParticipationIsUnique(qualified);

            if (qualified.All(participation => participation.CustomerId != customerId))
            {
                return EntryNetworkLevel.None;
            }

            var byRecruiter = qualified
                .Where(participation => participation.RecruiterCustomerId.HasValue)
                .GroupBy(participation => participation.RecruiterCustomerId.Value)
                .ToDictionary(group => group.Key, group => group.ToList());

            if (!IsCompleteBranch(customerId, 1, byRecruiter))
            {
                return EntryNetworkLevel.None;
            }

            if (!IsCompleteBranch(customerId, 2, byRecruiter))
            {
                return EntryNetworkLevel.Level1;
            }

            return IsCompleteBranch(customerId, 3, byRecruiter)
                ? EntryNetworkLevel.Level3
                : EntryNetworkLevel.Level2;
        }

        private static bool IsCompleteBranch(
            int customerId,
            int remainingDepth,
            IReadOnlyDictionary<int, List<EntryParticipation>> byRecruiter)
        {
            if (remainingDepth == 0)
            {
                return true;
            }

            if (!byRecruiter.TryGetValue(customerId, out var directRecruits) ||
                directRecruits.Count != BranchSize)
            {
                return false;
            }

            return directRecruits.All(recruit =>
                IsCompleteBranch(recruit.CustomerId, remainingDepth - 1, byRecruiter));
        }

        private static void EnsureCustomerParticipationIsUnique(
            IEnumerable<EntryParticipation> participations)
        {
            var duplicateCustomer = participations
                .GroupBy(participation => participation.CustomerId)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicateCustomer != null)
            {
                throw new InvalidOperationException(
                    $"Customer {duplicateCustomer.Key} has more than one qualified AQGreen participation.");
            }
        }
    }
}
