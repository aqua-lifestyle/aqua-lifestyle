namespace AqualLifeStyle.Application.Memberships.Dto
{
    public class TierBenefitsDto
    {
        public int Tier { get; set; }
        public string TierName { get; set; }
        public decimal MonthlyObligation { get; set; }
        public int OrderWindowStartDay { get; set; }
        public int OrderWindowEndDay { get; set; }
        public int SavingsWindowOpenDay { get; set; }
        public int SavingsWindowCloseDay { get; set; }
        public decimal ProductPricingDiscount { get; set; }
        public decimal SavingsMaturityInterestRate { get; set; }
        public int MaxConcurrentOrders { get; set; }
        public decimal ReferralCommissionRate { get; set; }
        public decimal ProfitSharePercentage { get; set; }
        public bool IsOrderWindowOpen { get; set; }
        public bool IsSavingsWindowOpen { get; set; }
    }
}
