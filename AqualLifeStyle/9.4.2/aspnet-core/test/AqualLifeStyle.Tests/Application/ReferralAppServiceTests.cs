using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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

        public ReferralAppServiceTests()
        {
            _referralAppService = Resolve<IReferralAppService>();
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
        public async Task GetAllAsync_ReturnsOnlyCurrentTenantReferrals()
        {
            var tenantOneReferralId = await CreateReferralAsync(tenantId: 1, sourceEnquiryId: 1001);
            await CreateReferralAsync(tenantId: 2, sourceEnquiryId: 2002);

            var referrals = await _referralAppService.GetAllAsync();

            referrals.ShouldContain(r => r.Id == tenantOneReferralId);
            referrals.ShouldAllBe(r => r.TenantId == 1);
            referrals.ShouldNotContain(r => r.TenantId == 2);
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
