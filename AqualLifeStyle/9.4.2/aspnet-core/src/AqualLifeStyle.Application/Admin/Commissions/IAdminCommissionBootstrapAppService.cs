using System.Threading.Tasks;
using AqualLifeStyle.Application.Admin.Commissions.Dto;

namespace AqualLifeStyle.Application.Admin.Commissions
{
    /// <summary>
    /// Host-only, reviewed bootstrap and preflight for the authorised clean
    /// operational cutover. It can only insert the exact authorised initial
    /// commission terms and September 2026 due-policy rows; it never updates or
    /// deletes existing immutable evidence and fails closed on any conflict.
    /// It never changes worker configuration.
    /// </summary>
    public interface IAdminCommissionBootstrapAppService
    {
        Task<CommissionTermsBootstrapResult> BootstrapInitialCommissionTermsAsync(
            BootstrapInitialCommissionTermsInput input);

        Task<WeeklyEnablementPreflightOutput> GetWeeklyEnablementPreflightAsync();

        Task<MonthlyEnablementPreflightOutput> GetMonthlyEnablementPreflightAsync();

        Task<SeptemberDueDatePolicyBootstrapResult>
            BootstrapSeptemberDueDatePolicyAsync(
                BootstrapSeptemberDueDatePolicyInput input);
    }
}
