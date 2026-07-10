using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AqualLifeStyle.Application.AreaLeaders;
using AqualLifeStyle.Application.AreaLeaders.Dto;
using AqualLifeStyle.Application.Customers;
using AqualLifeStyle.Application.Customers.Dto;
using AqualLifeStyle.Application.Enquiries;
using AqualLifeStyle.Application.Facilitators;
using AqualLifeStyle.Application.Facilitators.Dto;
using AqualLifeStyle.Domain.AreaLeaders;
using AqualLifeStyle.Domain.Enquiries;
using AqualLifeStyle.Domain.Facilitators;
using AqualLifeStyle.EntityFrameworkCore;
using AqualLifeStyle.EntityFrameworkCore.Repositories;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Integration
{
    /// <summary>
    /// End-to-end demo path: register area leader + facilitator, generate a lead enquiry sourced by
    /// the facilitator, convert it, and verify the referral network is attributed (2 referrals, counts
    /// and ranks updated) via the EnquiryConvertedEvent handler.
    /// </summary>
    public class EnquiryConversionFlowTests : AqualLifeStyleTestBase
    {
        private readonly IAreaLeaderAppService _areaLeaderAppService;
        private readonly IFacilitatorAppService _facilitatorAppService;
        private readonly IEnquiryAppService _enquiryAppService;
        private readonly ICustomerAppService _customerAppService;

        public EnquiryConversionFlowTests()
        {
            _areaLeaderAppService = Resolve<IAreaLeaderAppService>();
            _facilitatorAppService = Resolve<IFacilitatorAppService>();
            _enquiryAppService = Resolve<IEnquiryAppService>();
            _customerAppService = Resolve<ICustomerAppService>();
        }

        [Fact]
        public async Task ConvertEnquiry_SourcedByFacilitator_AttributesReferralsAndUpdatesNetwork()
        {
            var leaderCustomerId = await CreateCustomerAsync("FlowLeader");
            var leader = await _areaLeaderAppService.ApplyAsync(
                new RegisterAreaLeaderDto { CustomerId = leaderCustomerId, LicenseType = (int)LicenseType.EntreLevel });

            var facilitatorCustomerId = await CreateCustomerAsync("FlowFacilitator");
            var facilitator = await _facilitatorAppService.RegisterAsync(
                new RegisterFacilitatorDto { CustomerId = facilitatorCustomerId, AreaLeaderId = leader.Id });

            var prospectCustomerId = await CreateCustomerAsync("FlowProspect");
            await _enquiryAppService.CreateAsync(new AqualLifeStyle.Application.Enquiries.Dto.CreateEnquiryDto
            {
                CustomerId = prospectCustomerId,
                ProductId = 1,
                Message = "Interested in the club"
            });

            var enquiryId = await UsingDbContextAsync(async ctx =>
                (await ctx.Enquiries.FirstAsync(e => e.CustomerId == prospectCustomerId)).Id);

            await SetReferredByFacilitatorAsync(enquiryId, facilitator.Id);

            await _enquiryAppService.ConvertToCustomerAsync(enquiryId, new AqualLifeStyle.Application.Enquiries.Dto.ConvertEnquiryToCustomerDto());

            await UsingDbContextAsync(async ctx =>
            {
                var referrals = ctx.Referrals.ToList();
                referrals.Count.ShouldBe(2);
                referrals.Count(r => r.Type == ReferralType.Direct).ShouldBe(1);
                referrals.Count(r => r.Type == ReferralType.Indirect).ShouldBe(1);
                referrals.All(r => r.SourceEnquiryId == enquiryId).ShouldBeTrue();

                var updatedFacilitator = await ctx.Facilitators.FirstAsync(f => f.Id == facilitator.Id);
                updatedFacilitator.DirectReferrals.ShouldBe(1);

                var updatedLeader = await ctx.AreaLeaders.FirstAsync(a => a.Id == leader.Id);
                updatedLeader.IndirectReferrals.ShouldBe(1);
            });
        }

        private async Task SetReferredByFacilitatorAsync(int enquiryId, int facilitatorId)
        {
            var repo = Resolve<IEnquiryRepository>();
            var enquiry = await repo.GetAsync(enquiryId);
            enquiry.SetReferredByFacilitator(facilitatorId);
            await repo.UpdateAsync(enquiry);
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
