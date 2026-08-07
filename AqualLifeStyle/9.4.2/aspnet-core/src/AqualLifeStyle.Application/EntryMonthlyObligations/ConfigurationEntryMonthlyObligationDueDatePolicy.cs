using System;
using Abp.Dependency;
using Microsoft.Extensions.Configuration;

namespace AqualLifeStyle.Application.EntryMonthlyObligations
{
    /// <summary>
    /// Reads the monthly due date from explicit configuration
    /// (<c>App:EntryMonthlyObligations:DueDayOfMonth</c>). A missing or invalid
    /// value means the due-date policy (PD-07) is unresolved; the scheduler then
    /// refuses to invent a due date and no new obligation is created.
    /// </summary>
    public sealed class ConfigurationEntryMonthlyObligationDueDatePolicy
        : IEntryMonthlyObligationDueDatePolicy, ITransientDependency
    {
        private readonly IConfiguration _configuration;

        public ConfigurationEntryMonthlyObligationDueDatePolicy(
            IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public DateTime? ResolveDueDate(int periodYear, int periodMonth)
        {
            if (periodYear < 2000 || periodYear > 9999 ||
                periodMonth < 1 || periodMonth > 12)
            {
                return null;
            }

            var configuredDay = _configuration[
                "App:EntryMonthlyObligations:DueDayOfMonth"];
            if (!int.TryParse(configuredDay, out var dueDay) ||
                dueDay < 1 ||
                dueDay > 28)
            {
                return null;
            }

            return new DateTime(
                periodYear,
                periodMonth,
                dueDay,
                0,
                0,
                0,
                DateTimeKind.Utc);
        }
    }
}
