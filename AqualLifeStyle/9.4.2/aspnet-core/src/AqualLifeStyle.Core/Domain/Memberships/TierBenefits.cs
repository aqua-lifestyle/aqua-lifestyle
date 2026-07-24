using System;
using AqualLifeStyle.Domain.Enums;

namespace AqualLifeStyle.Domain.Memberships
{
    /// <summary>
    /// Value object representing benefits for each membership type.
    /// Tier-specific benefits include order rules and member perks. Savings terms are
    /// common to all participating members.
    /// </summary>
    public class TierBenefits
    {
        public MembershipType Tier { get; private set; }
        public string TierName { get; private set; }
        public decimal MonthlyObligation { get; private set; }
        
        // Order window boundaries (day of month when orders can be placed)
        public int OrderWindowStartDay { get; private set; }
        public int OrderWindowEndDay { get; private set; }
        
        // Savings contribution window boundaries
        public int SavingsWindowOpenDay { get; private set; }
        public int SavingsWindowCloseDay { get; private set; }
        
        // Benefit multipliers and discounts
        public decimal ProductPricingDiscount { get; private set; } // % discount on products
        public decimal SavingsMaturityInterestRate { get; private set; }
        public int MaxConcurrentOrders { get; private set; }       // Orders allowed per month
        
        // Commission and referral benefits
        public decimal ReferralCommissionRate { get; private set; } // % of referred member spending
        public decimal ProfitSharePercentage { get; private set; }  // % of company profit share
        
        protected TierBenefits() { }

        private TierBenefits(MembershipType tier, string tierName, decimal monthlyObligation,
            int orderWindowStart, int orderWindowEnd, int savingsOpen, int savingsClose,
            decimal discount, decimal savingsMaturityInterestRate, int maxOrders,
            decimal referralRate, decimal profitShare)
        {
            Tier = tier;
            TierName = tierName ?? throw new ArgumentNullException(nameof(tierName));
            MonthlyObligation = monthlyObligation;
            OrderWindowStartDay = orderWindowStart;
            OrderWindowEndDay = orderWindowEnd;
            SavingsWindowOpenDay = savingsOpen;
            SavingsWindowCloseDay = savingsClose;
            ProductPricingDiscount = discount;
            SavingsMaturityInterestRate = savingsMaturityInterestRate;
            MaxConcurrentOrders = maxOrders;
            ReferralCommissionRate = referralRate;
            ProfitSharePercentage = profitShare;
        }

        /// <summary>
        /// Get tier benefits for the specified membership type.
        /// These values align with the business requirements for AqualLifeStyle.
        /// </summary>
        public static TierBenefits ForTier(MembershipType membershipType) => membershipType switch
        {
            MembershipType.Jasper => new TierBenefits(
                tier: MembershipType.Jasper,
                tierName: "Jasper",
                monthlyObligation: 100m,
                orderWindowStart: 1,
                orderWindowEnd: 15,
                savingsOpen: 1,
                savingsClose: 15,
                discount: 0.05m,          // 5% discount
                savingsMaturityInterestRate: 0.20m,
                maxOrders: 1,
                referralRate: 0.10m,      // 10% referral commission
                profitShare: 0.0m         // No profit share at entry level
            ),
            MembershipType.Onyx => new TierBenefits(
                tier: MembershipType.Onyx,
                tierName: "Onyx",
                monthlyObligation: 250m,
                orderWindowStart: 1,
                orderWindowEnd: 20,
                savingsOpen: 1,
                savingsClose: 15,
                discount: 0.10m,          // 10% discount
                savingsMaturityInterestRate: 0.20m,
                maxOrders: 2,
                referralRate: 0.15m,      // 15% referral commission
                profitShare: 0.05m        // 5% profit share
            ),
            MembershipType.AQGreen => new TierBenefits(
                tier: MembershipType.AQGreen,
                tierName: "AQGreen",
                monthlyObligation: 500m,
                orderWindowStart: 1,
                orderWindowEnd: 25,
                savingsOpen: 1,
                savingsClose: 15,
                discount: 0.15m,          // 15% discount
                savingsMaturityInterestRate: 0.20m,
                maxOrders: 3,
                referralRate: 0.20m,      // 20% referral commission
                profitShare: 0.10m        // 10% profit share
            ),
            MembershipType.BusinessPremier => new TierBenefits(
                tier: MembershipType.BusinessPremier,
                tierName: "Business Premier",
                monthlyObligation: 750m,
                orderWindowStart: 1,
                orderWindowEnd: 30,
                savingsOpen: 1,
                savingsClose: 15,
                discount: 0.20m,          // 20% discount
                savingsMaturityInterestRate: 0.20m,
                maxOrders: 5,
                referralRate: 0.25m,      // 25% referral commission
                profitShare: 0.15m        // 15% profit share
            ),
            _ => throw new ArgumentException($"Unknown membership type: {membershipType}", nameof(membershipType))
        };

        /// <summary>
        /// Check if today is within the order window for this tier.
        /// </summary>
        public bool IsOrderWindowOpen(DateTime? date = null)
        {
            var today = (date ?? DateTime.UtcNow).Day;
            return today >= OrderWindowStartDay && today <= OrderWindowEndDay;
        }

        /// <summary>
        /// Check if today is within the savings contribution window.
        /// </summary>
        public bool IsSavingsWindowOpen(DateTime? date = null)
        {
            var today = (date ?? DateTime.UtcNow).Day;
            return today >= SavingsWindowOpenDay && today <= SavingsWindowCloseDay;
        }

        /// <summary>
        /// Calculate the effective price for a product with tier discount applied.
        /// </summary>
        public decimal ApplyDiscount(decimal basePrice)
        {
            if (basePrice < 0) throw new ArgumentException("Price cannot be negative.", nameof(basePrice));
            return basePrice * (1 - ProductPricingDiscount);
        }

        /// <summary>
        /// Calculate the interest earned when the savings account matures.
        /// </summary>
        public decimal CalculateSavingsMaturityInterest(decimal savingsBalance)
        {
            if (savingsBalance < 0) throw new ArgumentException("Balance cannot be negative.", nameof(savingsBalance));
            return savingsBalance * SavingsMaturityInterestRate;
        }

        /// <summary>
        /// Calculate referral commission earned from a referred member's purchase.
        /// </summary>
        public decimal CalculateReferralCommission(decimal referredMemberSpending)
        {
            if (referredMemberSpending < 0) throw new ArgumentException("Spending cannot be negative.", nameof(referredMemberSpending));
            return referredMemberSpending * ReferralCommissionRate;
        }
    }
}
