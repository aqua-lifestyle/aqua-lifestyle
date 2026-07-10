using System;
using System.Linq;
using System.Threading.Tasks;
using Castle.Core.Logging;
using Microsoft.EntityFrameworkCore;
using AqualLifeStyle.Application.AreaLeaders;
using AqualLifeStyle.Application.AreaLeaders.Dto;
using AqualLifeStyle.Application.Customers;
using AqualLifeStyle.Application.Customers.Dto;
using AqualLifeStyle.Application.Exceptions;
using AqualLifeStyle.Domain.AreaLeaders;
using AqualLifeStyle.EntityFrameworkCore;
using NSubstitute;
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

            await UsingDbContextAsync(async ctx =>
            {
                var updatedLeader = await ctx.AreaLeaders.FirstAsync(a => a.Id == leader.Id);
                updatedLeader.AreaSpaceId.ShouldBe(space.Id);
            });
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

        [Fact]
        public async Task HandleEventAsync_WhenLeaderMissing_LogsErrorAndThrowsDependencyException()
        {
            var customerId = await CreateCustomerAsync("LeaderE");
            var leader = await _areaLeaderAppService.ApplyAsync(
                new RegisterAreaLeaderDto { CustomerId = customerId, LicenseType = (int)LicenseType.EntreLevel });

            var space = await _areaSpaceAppService.ApplyAsync(new CreateAreaSpaceDto
            {
                AreaLeaderId = leader.Id,
                AddressLine = "3 Aqua Street",
                Capacity = "50",
                InterestedMembers = 20
            });

            await UsingDbContextAsync(async ctx =>
            {
                var persistedLeader = await ctx.AreaLeaders.FirstAsync(a => a.Id == leader.Id);
                ctx.AreaLeaders.Remove(persistedLeader);
            });

            var logger = Substitute.For<ILogger>();
            var handler = Resolve<AreaSpaceApprovedEventHandler>();
            handler.Logger = logger;

            var ex = await Assert.ThrowsAsync<AqualLifeStyleDependencyException>(() =>
                handler.HandleEventAsync(new AreaSpaceApprovedEvent(1, space.Id, leader.Id)));

            ex.Message.ShouldContain($"area space {space.Id}");
            ex.Message.ShouldContain("tenant 1");
            ex.Message.ShouldContain($"area leader {leader.Id}");

            logger.Received(1).Error(Arg.Is<string>(message =>
                message.Contains($"area space {space.Id}")
                && message.Contains("tenant 1")
                && message.Contains($"area leader {leader.Id}")));
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
