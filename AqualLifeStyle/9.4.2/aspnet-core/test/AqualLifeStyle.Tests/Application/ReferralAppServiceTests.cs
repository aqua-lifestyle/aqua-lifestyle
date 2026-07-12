using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AqualLifeStyle.Application.AreaLeaders;
using AqualLifeStyle.Application.AreaLeaders.Dto;
using AqualLifeStyle.Application.Customers;
using AqualLifeStyle.Application.Customers.Dto;
using AqualLifeStyle.Application.Enquiries;
using AqualLifeStyle.Application.Enquiries.Dto;
using AqualLifeStyle.Application.Exceptions;
using AqualLifeStyle.Application.Facilitators;
using AqualLifeStyle.Application.Facilitators.Dto;
using AqualLifeStyle.Application.Referrals;
using AqualLifeStyle.Domain.AreaLeaders;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enquiries;
using AqualLifeStyle.Domain.Facilitators;
using AqualLifeStyle.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class ReferralAppServiceTests : AqualLifeStyleTestBase
    {
        private readonly IReferralAppService _referralAppService;
        private readonly IReferralRepository _referralRepository;

        public ReferralAppServiceTests()
        {
            _referralAppService = Resolve<IReferralAppService>();
            _referralRepository = Resolve<IReferralRepository>();
        }

        [Fact]
        public async Task ConfirmAwardAsync_MarksReferralAwardIssued()
        {
            var (referralId, _, _) = await CreateReferralAsync(AbpSession.TenantId ?? 1);

            var confirmed = await _referralAppService.ConfirmAwardAsync(referralId);

            confirmed.AwardIssued.ShouldBeTrue();
            confirmed.ConfirmedAt.ShouldNotBeNull();

            await UsingDbContextAsync(async ctx =>
            {
                var fromDb = await ctx.Referrals.FirstAsync(r => r.Id == referralId);
                fromDb.AwardIssued.ShouldBeTrue();
            });
        }

        [Fact]
        public async Task ConfirmAwardAsync_ThrowsNotFound_WhenReferralBelongsToDifferentTenant()
        {
            var (tenantTwoReferralId, _, _) = await CreateReferralAsync(tenantId: 2);

            using (UsingTenantId(1))
            {
                await Assert.ThrowsAsync<AqualLifeStyleNotFoundException>(() => _referralAppService.ConfirmAwardAsync(tenantTwoReferralId));
            }

            await UsingDbContextAsync(2, async ctx =>
            {
                var fromDb = await ctx.Referrals.FirstAsync(r => r.Id == tenantTwoReferralId);
                fromDb.AwardIssued.ShouldBeFalse();
                fromDb.ConfirmedAt.ShouldBeNull();
            });
        }

        [Fact]
        public async Task ConfirmAwardAsync_ThrowsNotFound_WhenReferralDoesNotExist()
        {
            using (UsingTenantId(1))
            {
                await Assert.ThrowsAsync<AqualLifeStyleNotFoundException>(() => _referralAppService.ConfirmAwardAsync(int.MaxValue));
            }
        }

        [Fact]
        public async Task GetByEnquiryAsync_ReturnsMatchingReferral()
        {
            var (_, enquiryId, _) = await CreateReferralAsync(AbpSession.TenantId ?? 1);

            var found = await _referralAppService.GetByEnquiryAsync(enquiryId);
            found.ShouldNotBeNull();
            found.SourceEnquiryId.ShouldBe(enquiryId);
        }

        [Fact]
        public async Task GetByEnquiryAsync_ReturnsCurrentTenantReferral_WhenSourceEnquiryExistsInMultipleTenants()
        {
            var (tenantOneReferralId, tenantOneEnquiryId, _) = await CreateReferralAsync(tenantId: 1);
            var (_, tenantTwoEnquiryId, _) = await CreateReferralAsync(tenantId: 2);

            using (UsingTenantId(1))
            {
                var found = await _referralAppService.GetByEnquiryAsync(tenantOneEnquiryId);

                found.ShouldNotBeNull();
                found.Id.ShouldBe(tenantOneReferralId);
                found.TenantId.ShouldBe(1);
            }
        }

        [Fact]
        public async Task GetAllAsync_ReturnsOnlyCurrentTenantReferrals()
        {
            var (tenantOneReferralId, _, _) = await CreateReferralAsync(tenantId: 1);
            await CreateReferralAsync(tenantId: 2);

            var referrals = await _referralAppService.GetAllAsync();

            referrals.ShouldContain(r => r.Id == tenantOneReferralId);
            referrals.ShouldAllBe(r => r.TenantId == 1);
            referrals.ShouldNotContain(r => r.TenantId == 2);
        }

        [Fact]
        public async Task RepositoryGetBySourceEnquiryAsync_UsesAmbientTenantFilter_WhenTenantIsNotPassedExplicitly()
        {
            var (tenantOneReferralId, tenantOneEnquiryId, _) = await CreateReferralAsync(tenantId: 1);
            var (tenantTwoReferralId, tenantTwoEnquiryId, _) = await CreateReferralAsync(tenantId: 2);

            using (UsingTenantId(1))
            {
                var found = await _referralRepository.GetBySourceEnquiryAsync(tenantOneEnquiryId);

                found.ShouldNotBeNull();
                found.Id.ShouldBe(tenantOneReferralId);
                found.TenantId.ShouldBe(1);
                found.Id.ShouldNotBe(tenantTwoReferralId);
            }
        }

        private async Task<(int referralId, int enquiryId, int customerId)> CreateReferralAsync(int tenantId)
        {
            var customerId = await CreateCustomerAsync(tenantId);
            var facilitatorId = await CreateFacilitatorAsync(tenantId, customerId);
            var enquiryId = await CreateEnquiryAsync(tenantId, customerId);

            return await UsingDbContextAsync(tenantId, async ctx =>
            {
                var referral = Referral.CreateDirect(
                    tenantId,
                    referrerFacilitatorId: facilitatorId,
                    referredCustomerId: customerId,
                    sourceEnquiryId: enquiryId,
                    awardAmount: 100m,
                    convertedAt: DateTime.UtcNow);
                ctx.Referrals.Add(referral);
                await ctx.SaveChangesAsync();
                return (referral.Id, enquiryId, customerId);
            });
        }

        private async Task<int> CreateCustomerAsync(int tenantId)
        {
            var userId = await CreateTestUserAsync(tenantId, $"user-{Guid.NewGuid():N}", $"user-{Guid.NewGuid():N}@example.com");
            return await UsingDbContextAsync(tenantId, async ctx =>
            {
                var customer = Customer.Create(tenantId, userId, $"ReferralTestCustomer{tenantId}", new EmailAddress($"referraltestcustomer{tenantId}@example.com"));
                ctx.Customers.Add(customer);
                await ctx.SaveChangesAsync();
                return customer.Id;
            });
        }

        private async Task<int> CreateFacilitatorAsync(int tenantId, int customerId)
        {
            var leaderCustomerId = await CreateCustomerAsync(tenantId, $"ReferralTestLeader{tenantId}");
            var leader = await UsingDbContextAsync(tenantId, async ctx =>
            {
                var l = AreaLeader.Apply(tenantId, leaderCustomerId, LicenseType.EntreLevel);
                ctx.AreaLeaders.Add(l);
                await ctx.SaveChangesAsync();
                return l;
            });
            return await UsingDbContextAsync(tenantId, async ctx =>
            {
                var facilitator = Facilitator.Register(tenantId, customerId, leader.Id);
                ctx.Facilitators.Add(facilitator);
                await ctx.SaveChangesAsync();
                return facilitator.Id;
            });
        }

        private async Task<int> CreateEnquiryAsync(int tenantId, int customerId)
        {
            return await UsingDbContextAsync(tenantId, async ctx =>
            {
                var enquiry = Enquiry.Create(tenantId, customerId, 1, "Referral test enquiry");
                ctx.Enquiries.Add(enquiry);
                await ctx.SaveChangesAsync();
                return enquiry.Id;
            });
        }

        private async Task<int> CreateCustomerAsync(int tenantId, string name)
        {
            var userId = await CreateTestUserAsync(tenantId, $"user-{Guid.NewGuid():N}", $"user-{Guid.NewGuid():N}@example.com");
            return await UsingDbContextAsync(tenantId, async ctx =>
            {
                var customer = Customer.Create(tenantId, userId, name, new EmailAddress($"{name.ToLower()}@example.com"));
                ctx.Customers.Add(customer);
                await ctx.SaveChangesAsync();
                return customer.Id;
            });
        }
    }
}
