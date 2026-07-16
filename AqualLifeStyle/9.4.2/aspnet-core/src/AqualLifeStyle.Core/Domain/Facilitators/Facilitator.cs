using System;
using Abp.Domain.Entities.Auditing;
using Abp.Domain.Entities;
using AqualLifeStyle.Domain.AreaLeaders;

namespace AqualLifeStyle.Domain.Facilitators
{
    /// <summary>
    /// A network member who sources leads. Each facilitator sits under one area leader (upline)
    /// and earns direct referrals; their upline earns the matching indirect referral.
    /// </summary>
    public class Facilitator : FullAuditedAggregateRoot<int>, IMustHaveTenant
    {
        public int TenantId { get; set; }

        public int CustomerId { get; private set; }
        public int AreaLeaderId { get; private set; }
        public AreaLeader AreaLeader { get; private set; }
        public FacilitatorRank Rank { get; private set; }
        public bool IsApproved { get; private set; }
        public DateTime? ApprovedAt { get; private set; }
        public int DirectReferrals { get; private set; }
        public int IndirectReferrals { get; private set; }
        public decimal AwardBalance { get; private set; }

        protected Facilitator()
        {
        }

        private Facilitator(int tenantId, int customerId, int areaLeaderId)
        {
            if (tenantId <= 0) throw new ArgumentException("TenantId must be valid.", nameof(tenantId));
            if (customerId <= 0) throw new ArgumentException("CustomerId must be valid.", nameof(customerId));
            if (areaLeaderId <= 0) throw new ArgumentException("AreaLeaderId must be valid.", nameof(areaLeaderId));

            TenantId = tenantId;
            CustomerId = customerId;
            AreaLeaderId = areaLeaderId;
            Rank = FacilitatorRank.Bronze;
            IsApproved = false;
            DirectReferrals = 0;
            IndirectReferrals = 0;
            AwardBalance = 0m;
        }

        public static Facilitator Register(int tenantId, int customerId, int areaLeaderId)
            => new Facilitator(tenantId, customerId, areaLeaderId);

        /// <summary>Increment the direct-referral count (one per converted, facilitator-sourced lead).</summary>
        public void RecordDirectReferral()
        {
            if (IsDeleted)
            {
                throw new InvalidOperationException("Cannot record a referral for a deleted facilitator.");
            }

            DirectReferrals++;
        }

        /// <summary>Increment the indirect-referral count (driven by the upline area leader).</summary>
        public void RecordIndirectReferral()
        {
            if (IsDeleted)
            {
                throw new InvalidOperationException("Cannot record a referral for a deleted facilitator.");
            }

            IndirectReferrals++;
        }

        /// <summary>
        /// Award a rank and its one-off commission. Called when a cumulative direct-referral threshold
        /// is crossed. Raises <see cref="FacilitatorRankAchievedEvent"/>. Idempotent for the same/higher rank.
        /// </summary>
        public void AwardRank(FacilitatorRank rank, decimal awardAmount)
        {
            if (rank < Rank)
            {
                throw new ArgumentException("Cannot award a lower rank.", nameof(rank));
            }

            if (rank == Rank && AwardBalance > 0m)
            {
                return;
            }

            if (IsDeleted)
            {
                throw new InvalidOperationException("Cannot award a deleted facilitator.");
            }

            Rank = rank;
            AwardBalance += awardAmount;
            DomainEvents.Add(new FacilitatorRankAchievedEvent(Id, CustomerId, rank, awardAmount));
        }

        public void ApproveApplication(DateTime? approvedAtUtc = null)
        {
            if (IsDeleted) throw new InvalidOperationException("Cannot approve a deleted facilitator.");
            if (IsApproved) return;
            IsApproved = true;
            ApprovedAt = approvedAtUtc ?? DateTime.UtcNow;
        }

        public void PromoteToEarnedRank(RankProgressionPolicy progressionPolicy, CommissionCalculator commissionCalculator)
        {
            if (progressionPolicy == null) throw new ArgumentNullException(nameof(progressionPolicy));
            if (commissionCalculator == null) throw new ArgumentNullException(nameof(commissionCalculator));
            var earnedRank = progressionPolicy.EvaluateFacilitatorRank(DirectReferrals);
            if (earnedRank <= Rank) return;
            AwardRank(earnedRank, commissionCalculator.ComputeFacilitatorAward(earnedRank).Amount);
        }

        public void DemoteOneRank()
        {
            if (IsDeleted) throw new InvalidOperationException("Cannot demote a deleted facilitator.");
            if (Rank == FacilitatorRank.Bronze) return;
            Rank = (FacilitatorRank)((int)Rank - 1);
        }
    }
}
