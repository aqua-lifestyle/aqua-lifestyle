using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Repositories;
using Abp.UI;
using AqualLifeStyle.Domain.Areas;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.Areas
{
    public interface IAreaAssignmentResolver
    {
        Task<Area> ResolveActiveAreaAsync(int tenantId, Guid? requestedAreaId, string operation);
    }

    public class AreaAssignmentResolver : IAreaAssignmentResolver, ITransientDependency
    {
        private readonly IRepository<Area, Guid> _areaRepository;

        public AreaAssignmentResolver(IRepository<Area, Guid> areaRepository)
        {
            _areaRepository = areaRepository;
        }

        public async Task<Area> ResolveActiveAreaAsync(
            int tenantId,
            Guid? requestedAreaId,
            string operation)
        {
            if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));

            if (requestedAreaId.HasValue)
            {
                var requested = await _areaRepository.FirstOrDefaultAsync(area =>
                    area.Id == requestedAreaId.Value &&
                    area.TenantId == tenantId &&
                    area.IsActive);
                if (requested == null)
                    throw new UserFriendlyException(operation, "Select an active Area in your Tenant.");
                return requested;
            }

            var activeAreas = await _areaRepository.GetAll()
                .Where(area => area.TenantId == tenantId && area.IsActive)
                .OrderBy(area => area.Code)
                .Take(2)
                .ToListAsync();
            if (activeAreas.Count == 1) return activeAreas[0];

            throw new UserFriendlyException(
                operation,
                activeAreas.Count == 0
                    ? "Your Tenant has no active Area configured. Contact the club team."
                    : "Select the Area for this customer.");
        }
    }
}
