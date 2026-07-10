using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Uow;
using Abp.Events.Bus;
using Abp.Events.Bus.Handlers;
using AqualLifeStyle.Application.Exceptions;
using AqualLifeStyle.Domain.AreaLeaders;
using Castle.Core.Logging;

namespace AqualLifeStyle.Application.AreaLeaders
{
    /// <summary>
    /// When an area space is approved, links it back to its area leader so the leader can operate.
    /// </summary>
    public class AreaSpaceApprovedEventHandler : IAsyncEventHandler<AreaSpaceApprovedEvent>, ITransientDependency
    {
        private readonly IAreaLeaderRepository _areaLeaderRepository;
        private readonly IUnitOfWorkManager _unitOfWorkManager;
        public ILogger Logger { get; set; }

        public AreaSpaceApprovedEventHandler(IAreaLeaderRepository areaLeaderRepository, IUnitOfWorkManager unitOfWorkManager)
        {
            _areaLeaderRepository = areaLeaderRepository;
            _unitOfWorkManager = unitOfWorkManager;
            Logger = NullLogger.Instance;
        }

        public async Task HandleEventAsync(AreaSpaceApprovedEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            using (var uow = _unitOfWorkManager.Begin())
            {
                using (_unitOfWorkManager.Current.SetTenantId(evt.TenantId))
                {
                    var leader = await _areaLeaderRepository.FirstOrDefaultAsync(
                        l => l.Id == evt.AreaLeaderId && l.TenantId == evt.TenantId);
                    if (leader == null)
                    {
                        Logger.Error(
                            $"Failed to link approved area space {evt.AreaSpaceId} in tenant {evt.TenantId}: missing area leader {evt.AreaLeaderId}.");
                        throw new AqualLifeStyleDependencyException(
                            $"Cannot link approved area space {evt.AreaSpaceId} in tenant {evt.TenantId}: required area leader {evt.AreaLeaderId} was not found.");
                    }

                    leader.LinkAreaSpace(evt.AreaSpaceId);
                    await _areaLeaderRepository.UpdateAsync(leader);
                }

                await uow.CompleteAsync();
            }
        }
    }
}
