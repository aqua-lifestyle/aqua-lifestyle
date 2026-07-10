using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Repositories;
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

        public AreaSpaceApprovedEventHandler(IAreaLeaderRepository areaLeaderRepository)
        {
            _areaLeaderRepository = areaLeaderRepository;
        }

        public async Task HandleEventAsync(AreaSpaceApprovedEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            var leader = await _areaLeaderRepository.FirstOrDefaultAsync(l => l.Id == evt.AreaLeaderId);
            if (leader == null)
            {
                return;
            }

            leader.LinkAreaSpace(evt.AreaSpaceId);
            await _areaLeaderRepository.UpdateAsync(leader);
        }
    }
}
