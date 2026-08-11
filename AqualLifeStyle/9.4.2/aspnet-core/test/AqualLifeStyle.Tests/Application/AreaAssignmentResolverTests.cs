using System;
using System.Threading.Tasks;
using Abp.Domain.Uow;
using Abp.UI;
using AqualLifeStyle.Application.Areas;
using AqualLifeStyle.Domain.Areas;
using AqualLifeStyle.MultiTenancy;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class AreaAssignmentResolverTests : AqualLifeStyleTestBase
    {
        [Fact]
        public async Task OmittedArea_ResolvesOnlyWhenTenantHasExactlyOneActiveArea()
        {
            var johannesburg = await ResolveAreaAsync(1, requestedAreaId: null);
            johannesburg.Code.ShouldBe("JHB");

            await UsingDbContextAsync(1, async context =>
            {
                context.Areas.Add(Area.Create(1, "PTA", "Pretoria"));
                await context.SaveChangesAsync();
            });

            await Should.ThrowAsync<UserFriendlyException>(() =>
                ResolveAreaAsync(1, requestedAreaId: null));
        }

        [Fact]
        public async Task ClientSuppliedArea_CannotCrossTenantOrSelectInactiveArea()
        {
            var otherTenantAreaId = await UsingDbContextAsync(null, async context =>
            {
                var tenant = new Tenant("AreaResolverTenant", "Area Resolver Tenant");
                context.Tenants.Add(tenant);
                await context.SaveChangesAsync();
                var area = Area.Create(tenant.Id, "JHB", "Johannesburg");
                context.Areas.Add(area);
                await context.SaveChangesAsync();
                return area.Id;
            });
            var inactiveAreaId = await UsingDbContextAsync(1, async context =>
            {
                var area = Area.Create(1, "CPT", "Cape Town");
                area.Deactivate();
                context.Areas.Add(area);
                await context.SaveChangesAsync();
                return area.Id;
            });

            await Should.ThrowAsync<UserFriendlyException>(() =>
                ResolveAreaAsync(1, otherTenantAreaId));
            await Should.ThrowAsync<UserFriendlyException>(() =>
                ResolveAreaAsync(1, inactiveAreaId));
        }

        private async Task<Area> ResolveAreaAsync(int tenantId, Guid? requestedAreaId)
        {
            var unitOfWorkManager = Resolve<IUnitOfWorkManager>();
            using var unitOfWork = unitOfWorkManager.Begin();
            using (unitOfWorkManager.Current.SetTenantId(tenantId))
            {
                var area = await Resolve<IAreaAssignmentResolver>()
                    .ResolveActiveAreaAsync(
                        tenantId,
                        requestedAreaId,
                        "Area resolution failed.");
                await unitOfWork.CompleteAsync();
                return area;
            }
        }
    }
}
