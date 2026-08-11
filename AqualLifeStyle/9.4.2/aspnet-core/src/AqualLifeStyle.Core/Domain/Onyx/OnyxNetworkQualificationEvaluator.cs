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

        public static int GetRequiredPopulation(OnyxNetworkLevel level)
        {
            if (level < OnyxNetworkLevel.Level1 ||
                level > OnyxNetworkLevel.Level5)
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

            var supplied = networkParticipations.ToList();
            var active = supplied
                .Where(candidate => candidate.Status == OnyxParticipationStatus.Active)
                .ToList();
            var duplicateCustomer = active
                .GroupBy(candidate => candidate.CustomerId)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicateCustomer != null)
            {
                throw new InvalidOperationException(
                    $"Customer {duplicateCustomer.Key} has more than one active Onyx participation.");
            }

            if (active.All(candidate => candidate.Id != participation.Id))
            {
                return OnyxNetworkLevel.None;
            }

            return Evaluate(
                participation.CustomerId,
                EffectiveProgrammeNetwork.BuildOnyx(
                    participation.TenantId,
                    supplied,
                    DateTime.MaxValue));
        }

        public OnyxNetworkLevel Evaluate(
            int customerId,
            EffectiveProgrammeNetwork network)
        {
            if (customerId <= 0) throw new ArgumentOutOfRangeException(nameof(customerId));
            if (network == null) throw new ArgumentNullException(nameof(network));
            if (network.Kind != ProgrammeNetworkKind.Onyx)
            {
                throw new ArgumentException("An Onyx network is required.", nameof(network));
            }

            if (!network.ContainsCustomer(customerId))
            {
                return OnyxNetworkLevel.None;
            }

            var highestCompletedLevel = OnyxNetworkLevel.None;
            for (var level = 1; level <= HighestConfirmedStructuralLevel; level++)
            {
                if (!IsCompleteBranch(
                        customerId,
                        level,
                        network))
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
                IsCompleteBranch(
                    recruit.CustomerId,
                    remainingDepth - 1,
                    network));
        }

    }
}
