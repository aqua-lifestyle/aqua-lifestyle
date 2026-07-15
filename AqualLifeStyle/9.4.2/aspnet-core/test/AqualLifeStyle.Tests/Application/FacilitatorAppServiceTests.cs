using System;
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
            var (customerToLeaderId, leaderUserId) = await CreateCustomerAsync("Leader");
            SetCurrentUser(leaderUserId, 1);
            var leader = await _areaLeaderAppService.ApplyAsync(
                new RegisterAreaLeaderDto { CustomerId = customerToLeaderId, LicenseType = (int)LicenseType.EntreLevel });

            var (facilitatorCustomerId, facilitatorUserId) = await CreateCustomerAsync("Facilitator");
            SetCurrentUser(facilitatorUserId, 1);
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
            var (customerToLeaderId, leaderUserId) = await CreateCustomerAsync("LeaderMulti");
            SetCurrentUser(leaderUserId, 1);
            var leader = await _areaLeaderAppService.ApplyAsync(
                new RegisterAreaLeaderDto { CustomerId = customerToLeaderId, LicenseType = (int)LicenseType.EntreLevel });

            var (firstFacilitatorCustomerId, firstFacilitatorUserId) = await CreateCustomerAsync("FacilitatorMultiOne");
            var (secondFacilitatorCustomerId, secondFacilitatorUserId) = await CreateCustomerAsync("FacilitatorMultiTwo");

            SetCurrentUser(firstFacilitatorUserId, 1);
            await _facilitatorAppService.RegisterAsync(
                new RegisterFacilitatorDto { CustomerId = firstFacilitatorCustomerId, AreaLeaderId = leader.Id });

            SetCurrentUser(secondFacilitatorUserId, 1);
            await _facilitatorAppService.RegisterAsync(
                new RegisterFacilitatorDto { CustomerId = secondFacilitatorCustomerId, AreaLeaderId = leader.Id });

            var updatedLeader = await _areaLeaderAppService.GetAsync(leader.Id);
            updatedLeader.DirectReferrals.ShouldBe(2);

            var facilitators = await _facilitatorAppService.GetAllAsync();
            facilitators.Count(f => f.AreaLeaderId == leader.Id).ShouldBe(2);
        }

        [Fact]
        public async Task RegisterAsync_ShouldThrowNotFound_WhenAreaLeaderBelongsToDifferentTenant()
        {
            var tenantTwoLeader = await CreateAreaLeaderAsync("TenantTwoLeaderForRegister", tenantId: 2);
            var (facilitatorCustomerId, facilitatorUserId) = await CreateCustomerAsync("FacilitatorCrossTenant");
            SetCurrentUser(facilitatorUserId, 1);

            var ex = await Assert.ThrowsAsync<AqualLifeStyleNotFoundException>(() => _facilitatorAppService.RegisterAsync(
                new RegisterFacilitatorDto { CustomerId = facilitatorCustomerId, AreaLeaderId = tenantTwoLeader.Id }));

            ex.Message.ShouldContain("AreaLeader");

            var facilitator = await _facilitatorAppService.GetByCustomerAsync(facilitatorCustomerId);
            facilitator.ShouldBeNull();

            var tenantTwoLeaderAfter = await UsingDbContextAsync(2, async ctx =>
                await ctx.AreaLeaders.AsNoTracking().FirstAsync(a => a.Id == tenantTwoLeader.Id));
            tenantTwoLeaderAfter.DirectReferrals.ShouldBe(0);
        }

        [Fact]
        public async Task RegisterAsync_ShouldThrowNotFound_WhenCustomerBelongsToDifferentTenant()
        {
            var (customerToLeaderId, leaderUserId) = await CreateCustomerAsync("LeaderForeignCustomer");
            SetCurrentUser(leaderUserId, 1);
            var leader = await _areaLeaderAppService.ApplyAsync(
                new RegisterAreaLeaderDto { CustomerId = customerToLeaderId, LicenseType = (int)LicenseType.EntreLevel });
            var (tenantTwoCustomerId, _) = await CreateCustomerAsync("ForeignTenantFacilitator", tenantId: 2);

            var ex = await Assert.ThrowsAsync<AqualLifeStyleNotFoundException>(() => _facilitatorAppService.RegisterAsync(
                new RegisterFacilitatorDto { CustomerId = tenantTwoCustomerId, AreaLeaderId = leader.Id }));

            ex.Message.ShouldContain("Customer");

            var facilitators = await _facilitatorAppService.GetAllAsync();
            facilitators.ShouldNotContain(f => f.CustomerId == tenantTwoCustomerId);
        }

        [Fact]
        public async Task RegisterAsync_ShouldThrow_WhenFacilitatorAlreadyExistsForCustomer()
        {
            var (customerToLeaderId, leaderUserId) = await CreateCustomerAsync("LeaderDuplicate");
            SetCurrentUser(leaderUserId, 1);
            var leader = await _areaLeaderAppService.ApplyAsync(
                new RegisterAreaLeaderDto { CustomerId = customerToLeaderId, LicenseType = (int)LicenseType.EntreLevel });

            var (facilitatorCustomerId, facilitatorUserId) = await CreateCustomerAsync("FacilitatorDuplicate");
            SetCurrentUser(facilitatorUserId, 1);
            await _facilitatorAppService.RegisterAsync(
                new RegisterFacilitatorDto { CustomerId = facilitatorCustomerId, AreaLeaderId = leader.Id });

            var ex = await Assert.ThrowsAsync<UserFriendlyException>(() => _facilitatorAppService.RegisterAsync(
                new RegisterFacilitatorDto { CustomerId = facilitatorCustomerId, AreaLeaderId = leader.Id }));

            ex.Message.ShouldBe("Facilitator registration failed.");
            ex.Details.ShouldBe("A facilitator for this customer already exists.");
        }

        [MultiTenantFact]
        public async Task RegisterAsync_ShouldIgnoreDuplicateFacilitatorFromDifferentTenant()
        {
            var (customerToLeaderId, leaderUserId) = await CreateCustomerAsync("LeaderTenantScoped");
            SetCurrentUser(leaderUserId, 1);
            var leader = await _areaLeaderAppService.ApplyAsync(
                new RegisterAreaLeaderDto { CustomerId = customerToLeaderId, LicenseType = (int)LicenseType.EntreLevel });
            var (facilitatorCustomerId, facilitatorUserId) = await CreateCustomerAsync("FacilitatorTenantScoped");
            SetCurrentUser(facilitatorUserId, 1);
            var tenantTwoLeader = await CreateAreaLeaderAsync("TenantTwoLeaderDuplicateScope", tenantId: 2);

            await UsingDbContextAsync(2, async ctx =>
            {
                ctx.Facilitators.Add(Facilitator.Register(2, facilitatorCustomerId, tenantTwoLeader.Id));
                await ctx.SaveChangesAsync();
            });

            var facilitator = await _facilitatorAppService.RegisterAsync(
                new RegisterFacilitatorDto { CustomerId = facilitatorCustomerId, AreaLeaderId = leader.Id });

            facilitator.ShouldNotBeNull();
            facilitator.CustomerId.ShouldBe(facilitatorCustomerId);
            facilitator.TenantId.ShouldBe(1);

            var tenantOneFacilitatorCount = await UsingDbContextAsync(1, ctx =>
                ctx.Facilitators.CountAsync(f => f.CustomerId == facilitatorCustomerId && f.TenantId == 1));
            var tenantTwoFacilitatorCount = await UsingDbContextAsync(2, ctx =>
                ctx.Facilitators.CountAsync(f => f.CustomerId == facilitatorCustomerId && f.TenantId == 2));

            tenantOneFacilitatorCount.ShouldBe(1);
            tenantTwoFacilitatorCount.ShouldBe(1);
        }

        [Fact]
        public async Task GetByCustomerAsync_ReturnsRegisteredFacilitator()
        {
            var (customerToLeaderId, leaderUserId) = await CreateCustomerAsync("Leader2");
            SetCurrentUser(leaderUserId, 1);
            var leader = await _areaLeaderAppService.ApplyAsync(
                new RegisterAreaLeaderDto { CustomerId = customerToLeaderId, LicenseType = (int)LicenseType.EntreLevel });

            var (facilitatorCustomerId, facilitatorUserId) = await CreateCustomerAsync("Facilitator2");
            SetCurrentUser(facilitatorUserId, 1);
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
        public async Task GetByCustomerAsync_ShouldReturnNull_WhenCustomerBelongsToDifferentTenant()
        {
            var (tenantTwoCustomerId, _) = await CreateCustomerAsync("TenantTwoCustomerWithoutFacilitator", tenantId: 2);

            using (UsingTenantId(1))
            {
                var found = await _facilitatorAppService.GetByCustomerAsync(tenantTwoCustomerId);
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

        [Fact]
        public async Task GetWithAreaLeaderAsync_DoesNotHydrateAreaLeaderFromAnotherTenant()
        {
            var (tenantOneCustomerId, _) = await CreateCustomerAsync("CrossTenantLinkCustomer", tenantId: 1);
            var tenantTwoLeader = await CreateAreaLeaderAsync("CrossTenantLinkedLeader", tenantId: 2);
            var facilitatorId = await UsingDbContextAsync(1, async ctx =>
            {
                var facilitator = Facilitator.Register(1, tenantOneCustomerId, tenantTwoLeader.Id);
                ctx.Facilitators.Add(facilitator);
                await ctx.SaveChangesAsync();
                return facilitator.Id;
            });

            using (UsingTenantId(1))
            {
                var found = await _facilitatorRepository.GetWithAreaLeaderAsync(facilitatorId);

                found.ShouldNotBeNull();
                found.Id.ShouldBe(facilitatorId);
                found.AreaLeader.ShouldBeNull();
            }
        }

        private async Task<(int customerId, long userId)> CreateCustomerAsync(string name)
        {
            return await CreateCustomerAsync(name, tenantId: 1);
        }

        private async Task<(int customerId, long userId)> CreateCustomerAsync(string name, int tenantId)
        {
            var userId = await CreateTestUserAsync(tenantId, $"user-{Guid.NewGuid():N}", $"user-{Guid.NewGuid():N}@example.com");

            await UsingDbContextAsync(tenantId, async ctx =>
            {
                var customer = Customer.Create(tenantId, userId, name, new EmailAddress($"{name.ToLower()}@example.com"));
                ctx.Customers.Add(customer);
                await ctx.SaveChangesAsync();
            });

            return await UsingDbContextAsync(tenantId, async ctx =>
            {
                var customer = await ctx.Customers.FirstAsync(c => c.Email.Value == $"{name.ToLower()}@example.com");
                if (customer.UserId != userId)
                {
                    throw new Exception($"Customer UserId mismatch: expected {userId}, got {customer.UserId}");
                }
                return (customer.Id, customer.UserId);
            });
        }

        private async Task<AreaLeader> CreateAreaLeaderAsync(string leaderName, int tenantId)
        {
            var userId = await CreateTestUserAsync(tenantId, $"user-{Guid.NewGuid():N}", $"user-{Guid.NewGuid():N}@example.com");
            return await UsingDbContextAsync(tenantId, async ctx =>
            {
                var customerToLeader = Customer.Create(tenantId, userId, leaderName, new EmailAddress($"{leaderName.ToLower()}@example.com"));
                ctx.Customers.Add(customerToLeader);
                await ctx.SaveChangesAsync();

                var leader = AreaLeader.Apply(tenantId, customerToLeader.Id, LicenseType.EntreLevel);
                ctx.AreaLeaders.Add(leader);
                await ctx.SaveChangesAsync();

                return leader;
            });
        }

        private async Task<Facilitator> CreateFacilitatorAsync(string leaderName, string facilitatorName, int tenantId)
        {
            var userId = await CreateTestUserAsync(tenantId, $"user-{Guid.NewGuid():N}", $"user-{Guid.NewGuid():N}@example.com");
            return await UsingDbContextAsync(tenantId, async ctx =>
            {
                var leader = await CreateAreaLeaderAsync(leaderName, tenantId);

                var facilitatorCustomer = Customer.Create(tenantId, userId, facilitatorName, new EmailAddress($"{facilitatorName.ToLower()}@example.com"));
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
