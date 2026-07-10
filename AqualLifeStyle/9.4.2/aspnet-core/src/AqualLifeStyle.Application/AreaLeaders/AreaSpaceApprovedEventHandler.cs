using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Uow;
using Abp.Events.Bus;
using Abp.Events.Bus.Handlers;
using AqualLifeStyle.Domain.AreaLeaders;

namespace AqualLifeStyle.Application.AreaLeaders
{
    /// <summary>
    /// When an area space is approved, links it back to its area leader so the leader can operate.
    /// </summary>
    public class AreaSpaceApprovedEventHandler : IAsyncEventHandler<AreaSpaceApprovedEvent>, ITransientDependency
    {
        private readonly IAreaLeaderRepository _areaLeaderRepository;
        private readonly IUnitOfWorkManager _unitOfWorkManager;

        public AreaSpaceApprovedEventHandler(IAreaLeaderRepository areaLeaderRepository, IUnitOfWorkManager unitOfWorkManager)
        {
            _areaLeaderRepository = areaLeaderRepository;
            _unitOfWorkManager = unitOfWorkManager;
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
                        return;
                    }

                    leader.LinkAreaSpace(evt.AreaSpaceId);
                    await _areaLeaderRepository.UpdateAsync(leader);
                }

                await uow.CompleteAsync();
            }
        }
    }
}
