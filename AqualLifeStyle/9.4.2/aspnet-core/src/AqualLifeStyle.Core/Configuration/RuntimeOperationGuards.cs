using System;
using Microsoft.Extensions.Configuration;

namespace AqualLifeStyle.Configuration
{
    /// <summary>
    /// Configuration holds at execution boundaries. These checks do not replace
    /// separate database/provider identities or independently controlled grants.
    /// Missing enablement never grants execution authority.
    /// </summary>
    public static class RuntimeOperationGuards
    {
        public const string BackgroundSetting = "App:BackgroundJobs:Enabled";
        public const string FinancialSetting = "App:FinancialOperations:Enabled";

        public static bool CanRunBackgroundWork(IConfiguration configuration)
            => !ApplicationRecoveryMode.IsEnabled(configuration) &&
               !new YocoCutoverOptions(configuration).Enabled &&
               configuration.GetValue<bool>(BackgroundSetting);

        public static bool CanRunFinancialWorker(IConfiguration configuration, string workerSetting)
            => CanRunBackgroundWork(configuration) &&
               configuration.GetValue<bool>(FinancialSetting) &&
               configuration.GetValue<bool>(workerSetting);

        public static bool CanDispatchEmail(IConfiguration configuration)
            => CanRunBackgroundWork(configuration) && configuration.GetValue<bool>("Bird:Enabled");

        public static DateTime RequireEmailDispatch(IConfiguration configuration)
        {
            if (!CanDispatchEmail(configuration))
                throw new RuntimeOperationDeniedException("External email dispatch is not authorized for this runtime.");
            return TransactionalEmailDeliveryBoundary.Require(configuration);
        }

        public static void RequireFinancialOperations(IConfiguration configuration)
        {
            ApplicationRecoveryMode.DenyBusinessOperation(configuration);
            if (!configuration.GetValue<bool>(FinancialSetting))
                throw new RuntimeOperationDeniedException("Financial processing is not commissioned for this runtime.");
        }
    }

    public sealed class RuntimeOperationDeniedException : InvalidOperationException
    {
        public RuntimeOperationDeniedException(string message) : base(message) { }
    }
}
