using System;
using AqualLifeStyle.Domain.Enums;

namespace AqualLifeStyle.Domain.Memberships
{
    /// <summary>
    /// Value object representing tier-specific benefits for each membership type.
    /// Benefits include order window rules, savings behaviour enforcement, and member perks.
    /// </summary>
    public class TierBenefits
    {
        public MembershipType Tier { get; private set; }
        public string TierName { get; private set; }
        public decimal MonthlyObligation { get; private set; }
        
        // Order window boundaries (day of month when orders can be placed)
        public int OrderWindowStartDay { get; private set; }
        public int OrderWindowEndDay { get; private set; }
        
        // Savings window boundaries
        public int SavingsWindowOpenDay { get; private set; }      // 1st-15th
        public int SavingsWindowCloseDay { get; private set; }     // 17th-24th
        
        // Benefit multipliers and discounts
        public decimal ProductPricingDiscount { get; private set; } // % discount on products
        public decimal InterestRate { get; private set; }          // Monthly interest on savings
        public int MaxConcurrentOrders { get; private set; }       // Orders allowed per month
        
        // Commission and referral benefits
        public decimal ReferralCommissionRate { get; private set; } // % of referred member spending
        public decimal ProfitSharePercentage { get; private set; }  // % of company profit share
        
        protected TierBenefits() { }

        private TierBenefits(MembershipType tier, string tierName, decimal monthlyObligation,
            int orderWindowStart, int orderWindowEnd, int savingsOpen, int savingsClose,
            decimal discount, decimal interest, int maxOrders, 
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
            InterestRate = interest;
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
                interest: 0.003m,         // 0.3% monthly = 3.6% annual
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
                savingsClose: 20,
                discount: 0.10m,          // 10% discount
                interest: 0.005m,         // 0.5% monthly = 6% annual
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
                savingsClose: 24,
                discount: 0.15m,          // 15% discount
                interest: 0.007m,         // 0.7% monthly = 8.4% annual
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
                savingsClose: 24,
                discount: 0.20m,          // 20% discount
                interest: 0.010m,         // 1% monthly = 12% annual
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
        /// Check if today is within the savings window for this tier.
        /// </summary>
        public bool IsSavingsWindowOpen(DateTime? date = null)
        {
            var today = (date ?? DateTime.UtcNow).Day;
            // Savings window is 1st-15th (open) and 17th-24th (locked)
            // This method returns true for the open window only
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
        /// Calculate monthly interest earned on a savings balance.
        /// </summary>
        public decimal CalculateMonthlyInterest(decimal savingsBalance)
        {
            if (savingsBalance < 0) throw new ArgumentException("Balance cannot be negative.", nameof(savingsBalance));
            return savingsBalance * InterestRate;
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
