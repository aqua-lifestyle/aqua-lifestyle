using System;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Threading.BackgroundWorkers;
using Abp.Threading.Timers;
using AqualLifeStyle.Payments.Yoco;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AqualLifeStyle.Web.Host.Payments.Yoco
{
    public class YocoPaymentOperationsMonitor
        : AsyncPeriodicBackgroundWorkerBase, ISingletonDependency
    {
        private const int DefaultScanIntervalMinutes = 15;
        private const int DefaultStaleCheckoutThresholdMinutes = 60;

        private readonly StaleYocoCheckoutDetector _detector;
        private readonly ILogger<YocoPaymentOperationsMonitor> _logger;
        private readonly int _staleCheckoutThresholdMinutes;

        public YocoPaymentOperationsMonitor(
            AbpAsyncTimer timer,
            StaleYocoCheckoutDetector detector,
            IConfiguration configuration,
            ILogger<YocoPaymentOperationsMonitor> logger)
            : base(timer)
        {
            _detector = detector;
            _logger = logger;
            _staleCheckoutThresholdMinutes = PositiveMinutes(
                configuration["Yoco:Monitoring:StaleCheckoutThresholdMinutes"],
                DefaultStaleCheckoutThresholdMinutes);
            Timer.Period = checked(PositiveMinutes(
                configuration["Yoco:Monitoring:ScanIntervalMinutes"],
                DefaultScanIntervalMinutes) * 60 * 1000);
        }

        protected override async Task DoWorkAsync()
        {
            var checkedAtUtc = DateTime.UtcNow;
            var cutoffUtc = checkedAtUtc.AddMinutes(-_staleCheckoutThresholdMinutes);
            StaleYocoCheckoutSnapshot snapshot;
            try
            {
                snapshot = await _detector.DetectAsync(cutoffUtc);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "PaymentOperationsAlert {AlertType}: stale Yoco checkout monitoring failed",
                    "yoco_payment_monitor_failed");
                throw;
            }
            if (snapshot.TotalCount == 0)
                return;

            var oldestAgeMinutes = snapshot.OldestCheckoutCreatedAt.HasValue
                ? Math.Max(0, (int)(checkedAtUtc - snapshot.OldestCheckoutCreatedAt.Value).TotalMinutes)
                : 0;
            _logger.LogWarning(
                "PaymentOperationsAlert {AlertType}: {TotalCount} Yoco checkouts remain awaiting confirmation; AQGreen={AQGreenCount}, Onyx={OnyxCount}, OldestAgeMinutes={OldestAgeMinutes}, ThresholdMinutes={ThresholdMinutes}",
                "stale_yoco_checkouts",
                snapshot.TotalCount,
                snapshot.AQGreenCount,
                snapshot.OnyxCount,
                oldestAgeMinutes,
                _staleCheckoutThresholdMinutes);
        }

        private static int PositiveMinutes(string configuredValue, int defaultValue)
        {
            return int.TryParse(configuredValue, out var minutes) && minutes > 0
                ? minutes
                : defaultValue;
        }
    }
}
