using AqualLifeStyle.Migrator;
using Castle.Core.Logging;
using NSubstitute;
using Shouldly;

namespace AqualLifeStyle.Tests.Migrator
{
    /// <summary>
    /// Covers the simplified <see cref="Log"/> class, which now only forwards messages to the
    /// configured Castle logger (the previous direct <c>Console.WriteLine</c> call was removed
    /// in favor of routing everything through Serilog via the logging facility).
    /// </summary>
    public class LogTests
    {
        [Fact]
        public void DefaultLogger_IsNullLoggerInstance()
        {
            var log = new Log();

            log.Logger.ShouldBe(NullLogger.Instance);
        }

        [Fact]
        public void Write_DelegatesToLoggerInfo()
        {
            var logger = Substitute.For<ILogger>();
            var log = new Log { Logger = logger };

            log.Write("hello world");

            logger.Received(1).Info("hello world");
        }

        [Fact]
        public void Write_WithEmptyText_StillDelegatesToLoggerInfo()
        {
            var logger = Substitute.For<ILogger>();
            var log = new Log { Logger = logger };

            log.Write(string.Empty);

            logger.Received(1).Info(string.Empty);
        }

        [Fact]
        public void Write_CalledMultipleTimes_ForwardsEachMessageInOrder()
        {
            var logger = Substitute.For<ILogger>();
            var log = new Log { Logger = logger };

            log.Write("first");
            log.Write("second");

            Received.InOrder(() =>
            {
                logger.Info("first");
                logger.Info("second");
            });
        }
    }
}