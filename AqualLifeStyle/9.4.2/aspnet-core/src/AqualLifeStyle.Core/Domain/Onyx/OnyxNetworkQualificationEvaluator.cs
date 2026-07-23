using System;
using System.Collections.Generic;
using System.Linq;

namespace AqualLifeStyle.Domain.Onyx
{
    public enum OnyxNetworkLevel
    {
        None = 0,
        Level1 = 1,
        Level2 = 2,
        Level3 = 3,
        Level4 = 4,
        Level5 = 5
    }

    public sealed class OnyxNetworkQualificationEvaluator
    {
        public const int BranchSize = 5;
        public const int HighestConfirmedStructuralLevel = 5;

        public OnyxNetworkLevel Evaluate(
            OnyxParticipation participation,
            IEnumerable<OnyxParticipation> networkParticipations)
        {
            if (participation == null)
            {
                throw new ArgumentNullException(nameof(participation));
            }

            if (networkParticipations == null)
            {
                throw new ArgumentNullException(nameof(networkParticipations));
            }

            if (participation.Status != OnyxParticipationStatus.Active)
            {
                return OnyxNetworkLevel.None;
            }

            var activeParticipations = networkParticipations
                .Where(candidate => candidate.Status == OnyxParticipationStatus.Active)
                .ToList();
            EnsureCustomerParticipationIsUnique(activeParticipations);

            if (activeParticipations.All(candidate => candidate.Id != participation.Id))
            {
                return OnyxNetworkLevel.None;
            }

            var activeParticipationsByRecruiter = activeParticipations
                .Where(candidate => candidate.RecruiterCustomerId.HasValue)
                .GroupBy(candidate => candidate.RecruiterCustomerId.Value)
                .ToDictionary(group => group.Key, group => group.ToList());

            var highestCompletedLevel = OnyxNetworkLevel.None;
            for (var level = 1; level <= HighestConfirmedStructuralLevel; level++)
            {
                if (!IsCompleteBranch(
                        participation.CustomerId,
                        level,
                        activeParticipationsByRecruiter))
                {
                    break;
                }

                highestCompletedLevel = (OnyxNetworkLevel)level;
            }

            return highestCompletedLevel;
        }

        private static bool IsCompleteBranch(
            int customerId,
            int remainingDepth,
            IReadOnlyDictionary<int, List<OnyxParticipation>> activeParticipationsByRecruiter)
        {
            if (remainingDepth == 0)
            {
                return true;
            }

            if (!activeParticipationsByRecruiter.TryGetValue(
                    customerId,
                    out var directRecruits) ||
                directRecruits.Count != BranchSize)
            {
                return false;
            }

            return directRecruits.All(recruit =>
                IsCompleteBranch(
                    recruit.CustomerId,
                    remainingDepth - 1,
                    activeParticipationsByRecruiter));
        }

        private static void EnsureCustomerParticipationIsUnique(
            IEnumerable<OnyxParticipation> activeParticipations)
        {
            var duplicateCustomer = activeParticipations
                .GroupBy(participation => participation.CustomerId)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicateCustomer != null)
            {
                throw new InvalidOperationException(
                    $"Customer {duplicateCustomer.Key} has more than one active Onyx participation.");
            }
        }
    }
}
