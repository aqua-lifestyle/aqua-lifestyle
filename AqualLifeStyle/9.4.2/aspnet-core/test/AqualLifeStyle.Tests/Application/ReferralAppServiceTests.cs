using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AqualLifeStyle.Application.Exceptions;
using AqualLifeStyle.Application.Referrals;
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
            var referralId = await UsingDbContextAsync(async ctx =>
            {
                var referral = Referral.CreateDirect(
                    AbpSession.TenantId ?? 1,
                    referrerFacilitatorId: 1,
                    referredCustomerId: 2,
                    sourceEnquiryId: 3,
                    awardAmount: 100m,
                    convertedAt: DateTime.UtcNow);
                ctx.Referrals.Add(referral);
                await ctx.SaveChangesAsync();
                return referral.Id;
            });

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
            var tenantTwoReferralId = await CreateReferralAsync(tenantId: 2, sourceEnquiryId: 3003);

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
            var enquiryId = 42;
            await UsingDbContextAsync(async ctx =>
            {
                var referral = Referral.CreateIndirect(
                    AbpSession.TenantId ?? 1,
                    referrerAreaLeaderId: 5,
                    referredCustomerId: 7,
                    sourceEnquiryId: enquiryId,
                    awardAmount: 250m,
                    convertedAt: DateTime.UtcNow);
                ctx.Referrals.Add(referral);
                await ctx.SaveChangesAsync();
            });

            var found = await _referralAppService.GetByEnquiryAsync(enquiryId);
            found.ShouldNotBeNull();
            found.SourceEnquiryId.ShouldBe(enquiryId);
        }

        [Fact]
        public async Task GetByEnquiryAsync_ReturnsCurrentTenantReferral_WhenSourceEnquiryExistsInMultipleTenants()
        {
            const int enquiryId = 4242;
            var tenantOneReferralId = await CreateReferralAsync(tenantId: 1, sourceEnquiryId: enquiryId);
            await CreateReferralAsync(tenantId: 2, sourceEnquiryId: enquiryId);

            using (UsingTenantId(1))
            {
                var found = await _referralAppService.GetByEnquiryAsync(enquiryId);

                found.ShouldNotBeNull();
                found.Id.ShouldBe(tenantOneReferralId);
                found.TenantId.ShouldBe(1);
            }
        }

        [Fact]
        public async Task GetAllAsync_ReturnsOnlyCurrentTenantReferrals()
        {
            var tenantOneReferralId = await CreateReferralAsync(tenantId: 1, sourceEnquiryId: 1001);
            await CreateReferralAsync(tenantId: 2, sourceEnquiryId: 2002);

            var referrals = await _referralAppService.GetAllAsync();

            referrals.ShouldContain(r => r.Id == tenantOneReferralId);
            referrals.ShouldAllBe(r => r.TenantId == 1);
            referrals.ShouldNotContain(r => r.TenantId == 2);
        }

        [Fact]
        public async Task RepositoryGetBySourceEnquiryAsync_UsesAmbientTenantFilter_WhenTenantIsNotPassedExplicitly()
        {
            const int enquiryId = 5252;
            var tenantOneReferralId = await CreateReferralAsync(tenantId: 1, sourceEnquiryId: enquiryId);
            var tenantTwoReferralId = await CreateReferralAsync(tenantId: 2, sourceEnquiryId: enquiryId);

            using (UsingTenantId(1))
            {
                var found = await _referralRepository.GetBySourceEnquiryAsync(enquiryId);

                found.ShouldNotBeNull();
                found.Id.ShouldBe(tenantOneReferralId);
                found.TenantId.ShouldBe(1);
                found.Id.ShouldNotBe(tenantTwoReferralId);
            }
        }

        private Task<int> CreateReferralAsync(int tenantId, int sourceEnquiryId)
        {
            return UsingDbContextAsync(tenantId, async ctx =>
            {
                var referral = Referral.CreateDirect(
                    tenantId,
                    referrerFacilitatorId: tenantId * 10,
                    referredCustomerId: tenantId * 100,
                    sourceEnquiryId: sourceEnquiryId,
                    awardAmount: 100m,
                    convertedAt: DateTime.UtcNow);
                ctx.Referrals.Add(referral);
                await ctx.SaveChangesAsync();
                return referral.Id;
            });
        }
    }
}
