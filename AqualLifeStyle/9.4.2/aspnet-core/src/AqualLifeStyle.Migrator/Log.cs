using Castle.Core.Logging;
using Abp.Dependency;

namespace AqualLifeStyle.Migrator
{
    public class Log : ITransientDependency
    {
        public ILogger Logger { get; set; }

        public Log()
        {
            Logger = NullLogger.Instance;
        }

        public void Write(string text)
        {
            Logger.Info(text);
        }
    }
}
