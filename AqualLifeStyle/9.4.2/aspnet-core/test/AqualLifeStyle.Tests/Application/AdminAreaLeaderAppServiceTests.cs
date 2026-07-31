using System.Linq;
using System.Threading.Tasks;
using AqualLifeStyle.Application.Admin.AreaLeaders;
using AqualLifeStyle.Application.Admin.AreaLeaders.Dto;
using AqualLifeStyle.Domain.AreaLeaders;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Facilitators;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class AdminAreaLeaderAppServiceTests : AqualLifeStyleTestBase
    {
        private readonly IAdminAreaLeaderAppService _areaLeaderAdministration;

        public AdminAreaLeaderAppServiceTests()
        {
            _areaLeaderAdministration = Resolve<IAdminAreaLeaderAppService>();
        }

        [Fact]
        public async Task AreaLeaderLifecycle_ApprovesSynchronizesRoleAndRemoves()
        {
            string approvedSecurityStamp = null;
            var leaderId = await UsingDbContextAsync(async context =>
            {
                var customer = await context.Customers
                    .FirstAsync(item => item.TenantId == 1 &&
                        !context.AreaLeaders.Any(leader => leader.TenantId == 1 && leader.CustomerId == item.Id));
                var areaLeader = AreaLeader.Apply(1, customer.Id, LicenseType.EntreLevel);
                context.AreaLeaders.Add(areaLeader);
                await context.SaveChangesAsync();
                return areaLeader.Id;
            });

            var approved = await _areaLeaderAdministration.ApproveAsync(new ApproveAreaLeaderInput
            {
                Id = leaderId, Justification = "Application checks completed"
            });
            approved.IsApproved.ShouldBeTrue();
            await UsingDbContextAsync(async context =>
            {
                var userId = await context.Customers.Where(item => item.Id == approved.CustomerId)
                    .Select(item => item.UserId).SingleAsync();
                var user = await context.Users.SingleAsync(item => item.Id == userId);
                user.Role.ShouldBe(AquaUserRole.AreaLeader);
                approvedSecurityStamp = user.SecurityStamp;
                var roleNames = await (from assignment in context.UserRoles
                    join role in context.Roles on assignment.RoleId equals role.Id
                    where assignment.UserId == user.Id select role.Name).ToListAsync();
                roleNames.ShouldContain("AreaLeader");
            });

            await _areaLeaderAdministration.RemoveAsync(new RemoveAreaLeaderInput
            {
                Id = leaderId, Justification = "Leader requested removal"
            });
            await UsingDbContextAsync(async context =>
            {
                var removed = await context.AreaLeaders.IgnoreQueryFilters().SingleAsync(item => item.Id == leaderId);
                removed.IsDeleted.ShouldBeTrue();
                var userId = await context.Customers.Where(item => item.Id == approved.CustomerId)
                    .Select(item => item.UserId).SingleAsync();
                var user = await context.Users.SingleAsync(item => item.Id == userId);
                user.Role.ShouldNotBe(AquaUserRole.AreaLeader);
                user.SecurityStamp.ShouldNotBe(approvedSecurityStamp);
            });
        }
    }
}
