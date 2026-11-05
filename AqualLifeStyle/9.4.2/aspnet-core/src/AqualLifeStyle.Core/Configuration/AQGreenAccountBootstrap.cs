using System;
using System.Collections.Generic;
using Abp.Authorization;
using Microsoft.Extensions.Configuration;

namespace AqualLifeStyle.Configuration
{
    // Reuses existing account operations before a real root can be selected.
    // Tenant self-registration, verification tokens, Area assignment and throttles
    // remain authoritative. This scope grants no participation/payment authority.
    public static class AQGreenAccountBootstrap
    {
        private const string Commissioning = "App:AQGreenV2:Commissioning:";
        private static readonly HashSet<string> Routes = new(StringComparer.Ordinal)
        {
            "POST /api/services/app/Account/IsTenantAvailable",
            "GET /api/services/app/Account/IsTenantAvailable",
            "GET /api/services/app/Account/GetTenantSelfRegistrationAvailability",
            "POST /api/services/app/Account/Register",
            "POST /api/services/app/Account/ConfirmEmail",
            "POST /api/services/app/Account/ResendEmailVerification",
            "GET /AntiForgery/GetToken", "GET /AntiForgery/SetCookie"
        };
        private static readonly HashSet<string> Operations = new(StringComparer.Ordinal)
        {
            "AccountAppService.IsTenantAvailable", "AccountAppService.GetTenantSelfRegistrationAvailability",
            "AccountAppService.Register", "AccountAppService.ConfirmEmail", "AccountAppService.ResendEmailVerification"
        };

        public static bool IsEnabled(IConfiguration configuration)
            => configuration[Commissioning + "AdmissionMode"] == "CLEAN_START" &&
               !configuration.GetValue<bool>(Commissioning + "Enabled") &&
               !configuration.GetValue<bool>(RuntimeOperationGuards.FinancialSetting) &&
               !configuration.GetValue<bool>(RuntimeOperationGuards.BackgroundSetting) &&
               !configuration.GetValue<bool>("App:WeeklyCommissions:Enabled") &&
               !configuration.GetValue<bool>("App:EntryMonthlyObligations:Enabled") &&
               !configuration.GetValue<bool>("App:AutomaticPayouts:Enabled") &&
               !configuration.GetValue<bool>("AQGreenV2Pilot:Enabled") &&
               !configuration.GetValue<bool>("AQGreenV2Demo:Enabled") &&
               !configuration.GetValue<bool>("SEED_DEMO_DATA") &&
               !ApplicationRecoveryMode.IsEnabled(configuration) &&
               !new YocoCutoverOptions(configuration).CompatibilityReceiver;

        public static bool AllowsRoute(string method, string path)
            => Routes.Contains(method + " " + path?.TrimEnd('/'));
        public static bool AllowsOperation(string operation) => Operations.Contains(operation);

        public static void RequireTenant(int tenantId)
        {
            if (YocoCutoverDispatch.IsAccountBootstrapAuthorized && tenantId != 1)
                throw new AbpAuthorizationException("First-account registration is restricted to its authorized Tenant.");
        }

        public static void RequireProgramme(string programmeKey)
        {
            if (YocoCutoverDispatch.IsAccountBootstrapAuthorized && programmeKey != "AQGREEN")
                throw new AbpAuthorizationException("First-account registration cannot admit another programme invitation.");
        }
    }
}
