using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AqualLifeStyle.Application.AreaLeaders;
using AqualLifeStyle.Application.AreaLeaders.Dto;
using AqualLifeStyle.Application.Customers;
using AqualLifeStyle.Application.Customers.Dto;
using AqualLifeStyle.Application.Facilitators;
using AqualLifeStyle.Application.Facilitators.Dto;
using AqualLifeStyle.Domain.AreaLeaders;
using AqualLifeStyle.Domain.Facilitators;
using AqualLifeStyle.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class FacilitatorAppServiceTests : AqualLifeStyleTestBase
    {
        private readonly IFacilitatorAppService _facilitatorAppService;
        private readonly IAreaLeaderAppService _areaLeaderAppService;
        private readonly ICustomerAppService _customerAppService;

        public FacilitatorAppServiceTests()
        {
            _facilitatorAppService = Resolve<IFacilitatorAppService>();
            _areaLeaderAppService = Resolve<IAreaLeaderAppService>();
            _customerAppService = Resolve<ICustomerAppService>();
        }

        [Fact]
        public async Task RegisterAsync_CreatesFacilitatorUnderLeader_AndIncrementsLeaderCount()
        {
            var leaderCustomerId = await CreateCustomerAsync("Leader");
            var leader = await _areaLeaderAppService.ApplyAsync(
                new RegisterAreaLeaderDto { CustomerId = leaderCustomerId, LicenseType = (int)LicenseType.EntreLevel });

            var facilitatorCustomerId = await CreateCustomerAsync("Facilitator");
            var facilitator = await _facilitatorAppService.RegisterAsync(
                new RegisterFacilitatorDto { CustomerId = facilitatorCustomerId, AreaLeaderId = leader.Id });

            facilitator.Id.ShouldBeGreaterThan(0);
            facilitator.AreaLeaderId.ShouldBe(leader.Id);
            facilitator.Rank.ShouldBe((int)FacilitatorRank.Bronze);

            var updatedLeader = await _areaLeaderAppService.GetAsync(leader.Id);
            updatedLeader.DirectReferrals.ShouldBe(1);
        }

        [Fact]
        public async Task GetByCustomerAsync_ReturnsRegisteredFacilitator()
        {
            var leaderCustomerId = await CreateCustomerAsync("Leader2");
            var leader = await _areaLeaderAppService.ApplyAsync(
                new RegisterAreaLeaderDto { CustomerId = leaderCustomerId, LicenseType = (int)LicenseType.EntreLevel });

            var facilitatorCustomerId = await CreateCustomerAsync("Facilitator2");
            var facilitator = await _facilitatorAppService.RegisterAsync(
                new RegisterFacilitatorDto { CustomerId = facilitatorCustomerId, AreaLeaderId = leader.Id });

            var found = await _facilitatorAppService.GetByCustomerAsync(facilitatorCustomerId);
            found.ShouldNotBeNull();
            found.Id.ShouldBe(facilitator.Id);
        }

        private async Task<int> CreateCustomerAsync(string name)
        {
            await _customerAppService.CreateAsync(new CreateCustomerDto
            {
                Name = name,
                Email = $"{name.ToLower()}@example.com"
            });

            return await UsingDbContextAsync(async ctx =>
            {
                var customer = await ctx.Customers.FirstAsync(c => c.Email.Value == $"{name.ToLower()}@example.com");
                return customer.Id;
            });
        }
    }
}
