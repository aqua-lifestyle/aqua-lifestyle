using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Abp.Threading.Timers;
using AqualLifeStyle.Payments.Yoco;
using AqualLifeStyle.Web.Host.Payments.Yoco;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Payments
{
    public class YocoPaymentOperationsMonitorTests
    {
        private sealed class TestableYocoPaymentOperationsMonitor
            : YocoPaymentOperationsMonitor
        {
            public TestableYocoPaymentOperationsMonitor(
                AbpAsyncTimer timer,
                StaleYocoCheckoutDetector detector,
                IConfiguration configuration,
                ILogger<YocoPaymentOperationsMonitor> logger)
                : base(timer, detector, configuration, logger)
            {
            }

            public Task RunOnceAsync() => DoWorkAsync();
        }

        [Fact]
        public async Task RunOnceAsync_EmitsOneAggregateAlertForStaleCheckouts()
        {
            var detector = new Mock<StaleYocoCheckoutDetector>(
                null!, null!, null!, null!);
            detector.Setup(service => service.DetectAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(new StaleYocoCheckoutSnapshot(
                    aqGreenCount: 2,
                    onyxCount: 1,
                    oldestCheckoutCreatedAt: DateTime.UtcNow.AddMinutes(-90)));
            var logger = new TestLogger<YocoPaymentOperationsMonitor>();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Yoco:Monitoring:ScanIntervalMinutes"] = "10",
                    ["Yoco:Monitoring:StaleCheckoutThresholdMinutes"] = "60"
                })
                .Build();
            var monitor = new TestableYocoPaymentOperationsMonitor(
                new AbpAsyncTimer(),
                detector.Object,
                configuration,
                logger);

            await monitor.RunOnceAsync();

            logger.Entries.Count.ShouldBe(1);
            logger.Entries.ShouldContain(entry =>
                entry.Level == LogLevel.Warning &&
                entry.Message.Contains("stale_yoco_checkouts") &&
                entry.Message.Contains("AQGreen=2") &&
                entry.Message.Contains("Onyx=1"));
        }

        [Fact]
        public async Task RunOnceAsync_DoesNotAlertWhenNoCheckoutIsStale()
        {
            var detector = new Mock<StaleYocoCheckoutDetector>(
                null!, null!, null!, null!);
            detector.Setup(service => service.DetectAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(new StaleYocoCheckoutSnapshot(0, 0, null));
            var logger = new TestLogger<YocoPaymentOperationsMonitor>();
            var monitor = new TestableYocoPaymentOperationsMonitor(
                new AbpAsyncTimer(),
                detector.Object,
                new ConfigurationBuilder().Build(),
                logger);

            await monitor.RunOnceAsync();

            logger.Entries.ShouldBeEmpty();
        }

        [Fact]
        public async Task RunOnceAsync_AlertsAndRethrowsWhenMonitoringFails()
        {
            var detector = new Mock<StaleYocoCheckoutDetector>(
                null!, null!, null!, null!);
            detector.Setup(service => service.DetectAsync(It.IsAny<DateTime>()))
                .ThrowsAsync(new InvalidOperationException("Database unavailable"));
            var logger = new TestLogger<YocoPaymentOperationsMonitor>();
            var monitor = new TestableYocoPaymentOperationsMonitor(
                new AbpAsyncTimer(),
                detector.Object,
                new ConfigurationBuilder().Build(),
                logger);

            await Should.ThrowAsync<InvalidOperationException>(() => monitor.RunOnceAsync());

            logger.Entries.ShouldContain(entry =>
                entry.Level == LogLevel.Error &&
                entry.Message.Contains("yoco_payment_monitor_failed"));
        }
    }
}
