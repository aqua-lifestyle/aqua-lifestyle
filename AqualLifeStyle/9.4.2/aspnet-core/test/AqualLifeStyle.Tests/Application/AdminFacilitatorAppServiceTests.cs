using System.Linq;
using System.Threading.Tasks;
using AqualLifeStyle.Application.Admin.Facilitators;
using AqualLifeStyle.Application.Admin.Facilitators.Dto;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Facilitators;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class AdminFacilitatorAppServiceTests : AqualLifeStyleTestBase
    {
        private readonly IAdminFacilitatorAppService _facilitatorAdministration;
        public AdminFacilitatorAppServiceTests() => _facilitatorAdministration = Resolve<IAdminFacilitatorAppService>();

        [Fact]
        public async Task FacilitatorLifecycle_ApprovesSynchronizesRoleAndRemoves()
        {
            var facilitatorId = await UsingDbContextAsync(async context =>
            {
                var areaLeaderId = await context.AreaLeaders.Where(item => item.TenantId == 1).Select(item => item.Id).FirstAsync();
                var customer = await context.Customers.FirstAsync(item => item.TenantId == 1 &&
                    !context.Facilitators.Any(existing => existing.TenantId == 1 && existing.CustomerId == item.Id));
                var facilitator = Facilitator.Register(1, customer.Id, areaLeaderId);
                context.Facilitators.Add(facilitator);
                await context.SaveChangesAsync();
                return facilitator.Id;
            });

            var approved = await _facilitatorAdministration.ApproveAsync(new ApproveFacilitatorInput
            {
                Id = facilitatorId, Justification = "Application checks completed"
            });
            approved.IsApproved.ShouldBeTrue();
            await UsingDbContextAsync(async context =>
            {
                var userId = await context.Customers.Where(item => item.Id == approved.CustomerId).Select(item => item.UserId).SingleAsync();
                (await context.Users.SingleAsync(item => item.Id == userId)).Role.ShouldBe(AquaUserRole.Facilitator);
            });

            await _facilitatorAdministration.RemoveAsync(new RemoveFacilitatorInput
            {
                Id = facilitatorId, Justification = "Facilitator requested removal"
            });
            await UsingDbContextAsync(async context =>
                (await context.Facilitators.IgnoreQueryFilters().SingleAsync(item => item.Id == facilitatorId)).IsDeleted.ShouldBeTrue());
        }
    }
}
