using System;
using AqualLifeStyle.Domain.Email;
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
        public const string VerificationEmailSetting = "App:AQGreenV2:VerificationEmail:Enabled";
        public const int VerificationEmailTenantId = 1;
        public const string VerificationEmailNotificationType = "EmailVerification";

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

        public static bool CanDispatchVerificationEmail(IConfiguration configuration)
        {
            // Independent, default-off permission for the existing outbox only.
            // It grants no registration access, background jobs or financial work.
            if (!configuration.GetValue<bool>(VerificationEmailSetting)) return false;
            if (ApplicationRecoveryMode.IsEnabled(configuration) ||
                new YocoCutoverOptions(configuration).CompatibilityReceiver ||
                configuration.GetValue<bool>("AQGreenV2Pilot:Enabled") ||
                configuration.GetValue<bool>("AQGreenV2Demo:Enabled") ||
                configuration["App:AQGreenV2:Commissioning:AdmissionMode"] != "CLEAN_START")
                throw new RuntimeOperationDeniedException("Verification email delivery is outside the authorized runtime scope.");
            return configuration.GetValue<bool>("Bird:Enabled");
        }

        public static DateTime RequireVerificationEmailDispatch(IConfiguration configuration)
        {
            if (!CanDispatchVerificationEmail(configuration))
                throw new RuntimeOperationDeniedException("Nonfinancial verification email is not authorized for this runtime.");
            return TransactionalEmailDeliveryBoundary.Require(configuration);
        }

        public static void RequireVerificationMessage(IConfiguration configuration, TransactionalEmailOutboxMessage message)
        {
            var cutoff = RequireVerificationEmailDispatch(configuration);
            if (message == null || message.TenantId != VerificationEmailTenantId ||
                message.NotificationType != VerificationEmailNotificationType ||
                message.CreationTime < cutoff || message.Status != TransactionalEmailStatus.Processing)
                throw new RuntimeOperationDeniedException("Email is outside the nonfinancial verification delivery scope.");
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
