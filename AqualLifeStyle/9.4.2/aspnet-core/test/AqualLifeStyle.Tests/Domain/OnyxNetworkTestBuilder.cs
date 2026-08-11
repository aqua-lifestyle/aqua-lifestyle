using System;
using System.Collections.Generic;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;

namespace AqualLifeStyle.Tests.Domain
{
    internal static class OnyxNetworkTestBuilder
    {
        public static List<OnyxParticipation> BuildLevelOneNetwork(
            int directRecruitCount,
            OnyxPlanTerms terms,
            DateTime startedAt)
        {
            var root = CreateActiveIndependentParticipant(1, terms, startedAt);
            var network = new List<OnyxParticipation> { root };
            for (var index = 0; index < directRecruitCount; index++)
            {
                network.Add(CreateActiveUnderRecruiter(
                    index + 2,
                    root,
                    terms,
                    startedAt));
            }

            return network;
        }

        public static List<OnyxParticipation> BuildCompleteNetwork(
            int maximumDepth,
            OnyxPlanTerms terms,
            DateTime startedAt)
        {
            if (maximumDepth < 0 ||
                maximumDepth > OnyxNetworkQualificationEvaluator.HighestConfirmedStructuralLevel)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumDepth));
            }

            var root = CreateActiveIndependentParticipant(1, terms, startedAt);
            var network = new List<OnyxParticipation> { root };
            var currentLevel = new List<OnyxParticipation> { root };
            var nextCustomerId = 2;

            for (var depth = 1; depth <= maximumDepth; depth++)
            {
                var nextLevel = new List<OnyxParticipation>();
                foreach (var recruiter in currentLevel)
                {
                    for (var index = 0;
                         index < OnyxNetworkQualificationEvaluator.BranchSize;
                         index++)
                    {
                        var recruit = CreateActiveUnderRecruiter(
                            nextCustomerId++,
                            recruiter,
                            terms,
                            startedAt);
                        network.Add(recruit);
                        nextLevel.Add(recruit);
                    }
                }

                currentLevel = nextLevel;
            }

            return network;
        }

        public static OnyxParticipation CreateActiveUnderRecruiter(
            int customerId,
            OnyxParticipation recruiter,
            OnyxPlanTerms terms,
            DateTime startedAt,
            int tenantId = 1)
        {
            var participation = OnyxParticipation.StartDirectUnderRecruiter(
                tenantId,
                customerId,
                recruiter,
                7,
                terms,
                startedAt);
            Activate(participation, startedAt);
            return participation;
        }

        public static OnyxParticipation CreateActiveIndependentParticipant(
            int customerId,
            OnyxPlanTerms terms,
            DateTime startedAt,
            int tenantId = 1)
        {
            var participation = OnyxParticipation.StartDirectIndependently(
                tenantId,
                customerId,
                7,
                terms,
                startedAt);
            Activate(participation, startedAt);
            return participation;
        }

        private static void Activate(
            OnyxParticipation participation,
            DateTime startedAt)
        {
            var payment = MemberPayment.CreatePending(
                participation.TenantId,
                participation.CustomerId,
                MemberPaymentPurpose.OnyxDirectEntry,
                6120m,
                "Yoco",
                $"onyx-direct-{participation.TenantId}-{participation.CustomerId}",
                startedAt);
            payment.Confirm(startedAt.AddMinutes(1));
            participation.ApplyConfirmedDirectEntryPayment(payment);
            participation.ApproveByAdministrator(1L, startedAt.AddMinutes(2));
        }
    }
}
