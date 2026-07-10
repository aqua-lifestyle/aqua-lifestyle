using System.Linq;
using System.Threading.Tasks;
using Abp.UI;
using Microsoft.EntityFrameworkCore;
using AqualLifeStyle.Application.AreaLeaders;
using AqualLifeStyle.Application.AreaLeaders.Dto;
using AqualLifeStyle.Application.Customers;
using AqualLifeStyle.Application.Customers.Dto;
using AqualLifeStyle.Application.Exceptions;
using AqualLifeStyle.Application.Facilitators;
using AqualLifeStyle.Application.Facilitators.Dto;
using AqualLifeStyle.Domain.AreaLeaders;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Facilitators;
using AqualLifeStyle.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class FacilitatorAppServiceTests : AqualLifeStyleTestBase
    {
        private readonly IFacilitatorAppService _facilitatorAppService;
        private readonly IFacilitatorRepository _facilitatorRepository;
        private readonly IAreaLeaderAppService _areaLeaderAppService;
        private readonly ICustomerAppService _customerAppService;

        public FacilitatorAppServiceTests()
        {
            _facilitatorAppService = Resolve<IFacilitatorAppService>();
            _facilitatorRepository = Resolve<IFacilitatorRepository>();
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
        public async Task RegisterAsync_WhenMultipleFacilitatorsRegisterUnderSameLeader_IncrementsLeaderCountPerRegistration()
        {
            var leaderCustomerId = await CreateCustomerAsync("LeaderMulti");
            var leader = await _areaLeaderAppService.ApplyAsync(
                new RegisterAreaLeaderDto { CustomerId = leaderCustomerId, LicenseType = (int)LicenseType.EntreLevel });

            var firstFacilitatorCustomerId = await CreateCustomerAsync("FacilitatorMultiOne");
            var secondFacilitatorCustomerId = await CreateCustomerAsync("FacilitatorMultiTwo");

            await _facilitatorAppService.RegisterAsync(
                new RegisterFacilitatorDto { CustomerId = firstFacilitatorCustomerId, AreaLeaderId = leader.Id });
            await _facilitatorAppService.RegisterAsync(
                new RegisterFacilitatorDto { CustomerId = secondFacilitatorCustomerId, AreaLeaderId = leader.Id });

            var updatedLeader = await _areaLeaderAppService.GetAsync(leader.Id);
            updatedLeader.DirectReferrals.ShouldBe(2);

            var facilitators = await _facilitatorAppService.GetAllAsync();
            facilitators.Count(f => f.AreaLeaderId == leader.Id).ShouldBe(2);
        }

        [Fact]
        public async Task RegisterAsync_ShouldThrow_WhenFacilitatorAlreadyExistsForCustomer()
        {
            var leaderCustomerId = await CreateCustomerAsync("LeaderDuplicate");
            var leader = await _areaLeaderAppService.ApplyAsync(
                new RegisterAreaLeaderDto { CustomerId = leaderCustomerId, LicenseType = (int)LicenseType.EntreLevel });

            var facilitatorCustomerId = await CreateCustomerAsync("FacilitatorDuplicate");
            await _facilitatorAppService.RegisterAsync(
                new RegisterFacilitatorDto { CustomerId = facilitatorCustomerId, AreaLeaderId = leader.Id });

            var ex = await Assert.ThrowsAsync<UserFriendlyException>(() => _facilitatorAppService.RegisterAsync(
                new RegisterFacilitatorDto { CustomerId = facilitatorCustomerId, AreaLeaderId = leader.Id }));

            ex.Message.ShouldBe("Facilitator registration failed.");
            ex.Details.ShouldBe("A facilitator for this customer already exists.");
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

        [Fact]
        public async Task GetAsync_ShouldThrowNotFound_WhenFacilitatorBelongsToDifferentTenant()
        {
            var tenantTwoFacilitator = await CreateFacilitatorAsync("TenantTwoLeaderForGet", "TenantTwoFacilitatorForGet", tenantId: 2);

            using (UsingTenantId(1))
            {
                await Assert.ThrowsAsync<AqualLifeStyleNotFoundException>(() => _facilitatorAppService.GetAsync(tenantTwoFacilitator.Id));
            }
        }

        [Fact]
        public async Task GetByCustomerAsync_ShouldReturnNull_WhenFacilitatorBelongsToDifferentTenant()
        {
            var tenantTwoFacilitator = await CreateFacilitatorAsync("TenantTwoLeaderForCustomer", "TenantTwoFacilitatorForCustomer", tenantId: 2);

            using (UsingTenantId(1))
            {
                var found = await _facilitatorAppService.GetByCustomerAsync(tenantTwoFacilitator.CustomerId);
                found.ShouldBeNull();
            }
        }

        [Fact]
        public async Task GetAllAsync_ReturnsOnlyCurrentTenantFacilitators()
        {
            var tenantOneFacilitator = await CreateFacilitatorAsync("TenantOneLeader", "TenantOneFacilitator", tenantId: 1);
            await CreateFacilitatorAsync("TenantTwoLeader", "TenantTwoFacilitator", tenantId: 2);

            var facilitators = await _facilitatorAppService.GetAllAsync();

            facilitators.ShouldContain(f => f.Id == tenantOneFacilitator.Id);
            facilitators.ShouldAllBe(f => f.TenantId == 1);
            facilitators.ShouldNotContain(f => f.TenantId == 2);
        }

        [Fact]
        public async Task GetWithAreaLeaderAsync_LoadsLinkedAreaLeader()
        {
            var facilitator = await CreateFacilitatorAsync("LeaderWithInclude", "FacilitatorWithInclude", tenantId: 1);

            var found = await _facilitatorRepository.GetWithAreaLeaderAsync(facilitator.Id);

            found.ShouldNotBeNull();
            found.Id.ShouldBe(facilitator.Id);
            found.AreaLeader.ShouldNotBeNull();
            found.AreaLeader.Id.ShouldBe(facilitator.AreaLeaderId);
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

        private async Task<Facilitator> CreateFacilitatorAsync(string leaderName, string facilitatorName, int tenantId)
        {
            return await UsingDbContextAsync(tenantId, async ctx =>
            {
                var leaderCustomer = Customer.Create(tenantId, leaderName, new EmailAddress($"{leaderName.ToLower()}@example.com"));
                ctx.Customers.Add(leaderCustomer);
                await ctx.SaveChangesAsync();

                var leader = AreaLeader.Apply(tenantId, leaderCustomer.Id, LicenseType.EntreLevel);
                ctx.AreaLeaders.Add(leader);
                await ctx.SaveChangesAsync();

                var facilitatorCustomer = Customer.Create(tenantId, facilitatorName, new EmailAddress($"{facilitatorName.ToLower()}@example.com"));
                ctx.Customers.Add(facilitatorCustomer);
                await ctx.SaveChangesAsync();

                var facilitator = Facilitator.Register(tenantId, facilitatorCustomer.Id, leader.Id);
                ctx.Facilitators.Add(facilitator);
                await ctx.SaveChangesAsync();

                return facilitator;
            });
        }
    }
}
