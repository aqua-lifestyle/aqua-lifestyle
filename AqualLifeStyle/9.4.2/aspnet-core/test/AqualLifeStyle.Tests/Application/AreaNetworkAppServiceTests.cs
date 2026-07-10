using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AqualLifeStyle.Application.AreaLeaders;
using AqualLifeStyle.Application.AreaLeaders.Dto;
using AqualLifeStyle.Application.Customers;
using AqualLifeStyle.Application.Customers.Dto;
using AqualLifeStyle.Domain.AreaLeaders;
using AqualLifeStyle.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class AreaNetworkAppServiceTests : AqualLifeStyleTestBase
    {
        private readonly IAreaLeaderAppService _areaLeaderAppService;
        private readonly IAreaSpaceAppService _areaSpaceAppService;
        private readonly ICustomerAppService _customerAppService;

        public AreaNetworkAppServiceTests()
        {
            _areaLeaderAppService = Resolve<IAreaLeaderAppService>();
            _areaSpaceAppService = Resolve<IAreaSpaceAppService>();
            _customerAppService = Resolve<ICustomerAppService>();
        }

        [Fact]
        public async Task ApplyAsync_CreatesAreaLeaderAtRubyRank()
        {
            var customerId = await CreateCustomerAsync("LeaderA");
            var leader = await _areaLeaderAppService.ApplyAsync(
                new RegisterAreaLeaderDto { CustomerId = customerId, LicenseType = (int)LicenseType.AreaIndependentLeader });

            leader.Id.ShouldBeGreaterThan(0);
            leader.Rank.ShouldBe((int)AreaLeaderRank.Ruby);
            leader.LicenseFee.ShouldBe(2500m);
        }

        [Fact]
        public async Task RecordStartupOrder_And_Promote_AdvancesRank()
        {
            var customerId = await CreateCustomerAsync("LeaderB");
            var leader = await _areaLeaderAppService.ApplyAsync(
                new RegisterAreaLeaderDto { CustomerId = customerId, LicenseType = (int)LicenseType.EntreLevel });

            for (var i = 0; i < 60; i++)
            {
                leader = await _areaLeaderAppService.RecordStartupOrderAsync(leader.Id);
            }

            leader.OrderTarget.ShouldBe(60);

            var promoted = await _areaLeaderAppService.PromoteAsync(leader.Id);
            promoted.Rank.ShouldBe((int)AreaLeaderRank.Emerald);
        }

        [Fact]
        public async Task AreaSpace_ApproveFlow_ReachesApprovedStatus()
        {
            var customerId = await CreateCustomerAsync("LeaderC");
            var leader = await _areaLeaderAppService.ApplyAsync(
                new RegisterAreaLeaderDto { CustomerId = customerId, LicenseType = (int)LicenseType.EntreLevel });

            var space = await _areaSpaceAppService.ApplyAsync(new CreateAreaSpaceDto
            {
                AreaLeaderId = leader.Id,
                AddressLine = "1 Aqua Street",
                Capacity = "50",
                InterestedMembers = 20
            });

            space.Status.ShouldBe((int)AreaSpaceStatus.Applied);

            await _areaSpaceAppService.StartReviewAsync(space.Id);
            for (var i = 0; i < 4; i++)
            {
                await _areaSpaceAppService.RecordPresentationAsync(space.Id);
            }
            for (var i = 0; i < 20; i++)
            {
                await _areaSpaceAppService.RecordStartupOrderAsync(space.Id);
            }

            var underReview = await _areaSpaceAppService.GetAsync(space.Id);
            underReview.PresentationsCompleted.ShouldBe(4);
            underReview.StartupOrdersCompleted.ShouldBe(20);

            var atUtc = underReview.ReviewStartedAt.Value.AddHours(43);
            var approved = await _areaSpaceAppService.ApproveAsync(space.Id, atUtc);

            approved.Status.ShouldBe((int)AreaSpaceStatus.Approved);
            approved.ApprovedAt.ShouldNotBeNull();
        }

        [Fact]
        public async Task AreaSpace_ApproveBeforeReviewWindow_Throws()
        {
            var customerId = await CreateCustomerAsync("LeaderD");
            var leader = await _areaLeaderAppService.ApplyAsync(
                new RegisterAreaLeaderDto { CustomerId = customerId, LicenseType = (int)LicenseType.EntreLevel });

            var space = await _areaSpaceAppService.ApplyAsync(new CreateAreaSpaceDto
            {
                AreaLeaderId = leader.Id,
                AddressLine = "2 Aqua Street",
                Capacity = "50",
                InterestedMembers = 20
            });

            await _areaSpaceAppService.StartReviewAsync(space.Id);
            for (var i = 0; i < 4; i++)
            {
                await _areaSpaceAppService.RecordPresentationAsync(space.Id);
            }
            for (var i = 0; i < 20; i++)
            {
                await _areaSpaceAppService.RecordStartupOrderAsync(space.Id);
            }

            var underReview = await _areaSpaceAppService.GetAsync(space.Id);
            var tooSoon = underReview.ReviewStartedAt.Value.AddHours(1);

            await Should.ThrowAsync<System.Exception>(async () =>
                await _areaSpaceAppService.ApproveAsync(space.Id, tooSoon));
        }

        private async Task<int> CreateCustomerAsync(string name)
        {
            await _customerAppService.CreateAsync(new CreateCustomerDto
            {
                Name = name,
                Email = $"{name.ToLower()}@example.com"
            });

            return await UsingDbContextAsync(async ctx =>
                (await ctx.Customers.FirstAsync(c => c.Email.Value == $"{name.ToLower()}@example.com")).Id);
        }
    }
}
