using System;
using System.Collections.Generic;
using System.Linq;

namespace AqualLifeStyle.Domain.Onyx
{
    public enum OnyxNetworkLevel
    {
        None = 0,
        Level1 = 1
    }

    public sealed class OnyxNetworkQualificationEvaluator
    {
        public const int LevelOneBranchSize = 5;
        public const int HighestConfirmedLevel = 1;

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

            var activeDirectRecruits = activeParticipations.Count(candidate =>
                candidate.RecruiterCustomerId == participation.CustomerId);

            return activeDirectRecruits == LevelOneBranchSize
                ? OnyxNetworkLevel.Level1
                : OnyxNetworkLevel.None;
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
