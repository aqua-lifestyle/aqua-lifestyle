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

        public static int GetRequiredPopulation(EntryNetworkLevel level)
        {
            if (level < EntryNetworkLevel.Level1 ||
                level > EntryNetworkLevel.Level3)
            {
                throw new ArgumentOutOfRangeException(nameof(level));
            }

            var requiredPopulation = 1;
            for (var depth = 0; depth < (int)level; depth++)
            {
                requiredPopulation *= BranchSize;
            }

            return requiredPopulation;
        }

        public EntryNetworkLevel Evaluate(
            int customerId,
            IEnumerable<EntryParticipation> participations)
        {
            if (customerId <= 0) throw new ArgumentOutOfRangeException(nameof(customerId));
            if (participations == null) throw new ArgumentNullException(nameof(participations));

            var supplied = participations.ToList();
            var qualified = supplied
                .Where(participation => participation.IsQualifiedForNetwork)
                .ToList();
            var duplicateCustomer = qualified
                .GroupBy(participation => participation.CustomerId)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicateCustomer != null)
            {
                throw new InvalidOperationException(
                    $"Customer {duplicateCustomer.Key} has more than one qualified AQGreen participation.");
            }

            if (qualified.All(participation => participation.CustomerId != customerId))
            {
                return EntryNetworkLevel.None;
            }
            var expectedTenantId = qualified
                .First(participation => participation.CustomerId == customerId)
                .TenantId;
            return Evaluate(
                customerId,
                EffectiveProgrammeNetwork.BuildAQGreen(
                    expectedTenantId,
                    supplied,
                    DateTime.MaxValue));
        }

        public EntryNetworkLevel Evaluate(
            int customerId,
            EffectiveProgrammeNetwork network)
        {
            if (customerId <= 0) throw new ArgumentOutOfRangeException(nameof(customerId));
            if (network == null) throw new ArgumentNullException(nameof(network));
            if (network.Kind != ProgrammeNetworkKind.AQGreen)
            {
                throw new ArgumentException("An AQGreen network is required.", nameof(network));
            }

            if (!network.ContainsCustomer(customerId))
            {
                return EntryNetworkLevel.None;
            }

            return EvaluateHighestCompleteLevel(level =>
                IsCompleteBranch(customerId, level, network));
        }

        private static EntryNetworkLevel EvaluateHighestCompleteLevel(
            Func<int, bool> isCompleteLevel)
        {
            var highestCompletedLevel = EntryNetworkLevel.None;
            for (var level = 1; level <= MaximumLevel; level++)
            {
                if (!isCompleteLevel(level))
                {
                    break;
                }

                highestCompletedLevel = (EntryNetworkLevel)level;
            }

            return highestCompletedLevel;
        }

        private static bool IsCompleteBranch(
            int customerId,
            int remainingDepth,
            EffectiveProgrammeNetwork network)
        {
            if (remainingDepth == 0)
            {
                return true;
            }

            var directRecruits = network.GetSelectedChildren(customerId);
            if (directRecruits.Count < BranchSize)
            {
                return false;
            }

            return directRecruits.All(recruit =>
                IsCompleteBranch(recruit.CustomerId, remainingDepth - 1, network));
        }

    }
}
