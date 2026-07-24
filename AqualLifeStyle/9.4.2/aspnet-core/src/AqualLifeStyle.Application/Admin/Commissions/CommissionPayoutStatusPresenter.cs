using AqualLifeStyle.Domain.Onyx;

namespace AqualLifeStyle.Application.Admin.Commissions
{
    public static class CommissionPayoutStatusPresenter
    {
        public static string ToBusinessLabel(WeeklyCommissionPayoutStatus status) =>
            status switch
            {
                WeeklyCommissionPayoutStatus.NotEarned => "Not earned",
                WeeklyCommissionPayoutStatus.Earned => "Earned — awaiting release",
                WeeklyCommissionPayoutStatus.Held => "On hold",
                WeeklyCommissionPayoutStatus.Released => "Released — awaiting payment",
                WeeklyCommissionPayoutStatus.Paid => "Paid",
                _ => "Unknown"
            };
    }
}
