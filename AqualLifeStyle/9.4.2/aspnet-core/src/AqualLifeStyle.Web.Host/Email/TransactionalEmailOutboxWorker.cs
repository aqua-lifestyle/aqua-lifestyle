using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Threading.BackgroundWorkers;
using Abp.Threading.Timers;
using AqualLifeStyle.Email;
using Microsoft.Extensions.Configuration;

namespace AqualLifeStyle.Web.Host.Email
{
    public sealed class TransactionalEmailOutboxWorker
        : AsyncPeriodicBackgroundWorkerBase, ISingletonDependency
    {
        private readonly IConfiguration _configuration;
        private readonly TransactionalEmailOutboxProcessor _processor;

        public TransactionalEmailOutboxWorker(
            AbpAsyncTimer timer,
            IConfiguration configuration,
            TransactionalEmailOutboxProcessor processor)
            : base(timer)
        {
            _configuration = configuration;
            _processor = processor;
            Timer.Period = 30_000;
        }

        protected override async Task DoWorkAsync()
        {
            if (!_configuration.GetValue<bool>("Bird:Enabled")) return;
            await _processor.ProcessPendingAsync();
        }
    }
}
