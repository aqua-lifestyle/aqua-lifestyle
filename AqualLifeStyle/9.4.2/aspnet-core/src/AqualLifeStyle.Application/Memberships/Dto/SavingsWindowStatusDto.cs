namespace AqualLifeStyle.Application.Memberships.Dto
{
    public class SavingsWindowStatusDto
    {
        public int Tier { get; set; }
        public string TierName { get; set; }
        public int SavingsWindowOpenDay { get; set; }
        public int SavingsWindowCloseDay { get; set; }
        public int CurrentDay { get; set; }
        public string AsOfDate { get; set; }
        public bool IsSavingsWindowOpen { get; set; }
        public string StatusLabel { get; set; }
    }
}
