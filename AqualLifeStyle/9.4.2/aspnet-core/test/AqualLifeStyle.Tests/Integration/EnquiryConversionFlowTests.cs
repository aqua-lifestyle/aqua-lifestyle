using System.Linq;
using System.Threading.Tasks;
using Abp.Domain.Uow;
using Microsoft.EntityFrameworkCore;
using AqualLifeStyle.Application.AreaLeaders;
using AqualLifeStyle.Application.AreaLeaders.Dto;
using AqualLifeStyle.Application.Customers;
using AqualLifeStyle.Application.Customers.Dto;
using AqualLifeStyle.Application.Enquiries;
using AqualLifeStyle.Application.Facilitators;
using AqualLifeStyle.Application.Facilitators.Dto;
using AqualLifeStyle.Domain.AreaLeaders;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enquiries;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Facilitators;
using AqualLifeStyle.Domain.Memberships;
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

        [Fact]
        public async Task HandleEventAsync_WhenConversionEventIsRetried_DoesNotDuplicateReferrals()
        {
            var leaderCustomerId = await CreateCustomerAsync("RetryLeader");
            var leader = await _areaLeaderAppService.ApplyAsync(
                new RegisterAreaLeaderDto { CustomerId = leaderCustomerId, LicenseType = (int)LicenseType.EntreLevel });

            var facilitatorCustomerId = await CreateCustomerAsync("RetryFacilitator");
            var facilitator = await _facilitatorAppService.RegisterAsync(
                new RegisterFacilitatorDto { CustomerId = facilitatorCustomerId, AreaLeaderId = leader.Id });

            var prospectCustomerId = await CreateCustomerAsync("RetryProspect");
            await _enquiryAppService.CreateAsync(new AqualLifeStyle.Application.Enquiries.Dto.CreateEnquiryDto
            {
                CustomerId = prospectCustomerId,
                ProductId = 1,
                Message = "Retry conversion event"
            });

            var enquiryId = await UsingDbContextAsync(async ctx =>
                (await ctx.Enquiries.FirstAsync(e => e.CustomerId == prospectCustomerId)).Id);

            await SetReferredByFacilitatorAsync(enquiryId, facilitator.Id);

            await _enquiryAppService.ConvertToCustomerAsync(enquiryId, new AqualLifeStyle.Application.Enquiries.Dto.ConvertEnquiryToCustomerDto());

            await HandleConvertedEventAsync(
                new EnquiryConvertedEvent(enquiryId, prospectCustomerId, 1, facilitator.Id, System.DateTime.UtcNow, tenantId: 1));

            await UsingDbContextAsync(async ctx =>
            {
                var referrals = ctx.Referrals.Where(r => r.SourceEnquiryId == enquiryId).ToList();
                referrals.Count.ShouldBe(2);
                referrals.Count(r => r.Type == ReferralType.Direct).ShouldBe(1);
                referrals.Count(r => r.Type == ReferralType.Indirect).ShouldBe(1);

                var updatedFacilitator = await ctx.Facilitators.FirstAsync(f => f.Id == facilitator.Id);
                updatedFacilitator.DirectReferrals.ShouldBe(1);

                var updatedLeader = await ctx.AreaLeaders.FirstAsync(a => a.Id == leader.Id);
                updatedLeader.IndirectReferrals.ShouldBe(1);
            });
        }

        [Fact]
        public async Task ConvertEnquiry_AssignsLowestIdActiveMembership_WithoutOverwritingExistingAssignment()
        {
            var firstMembershipId = await CreateMembershipAsync("FlowJasper");
            var secondMembershipId = await CreateMembershipAsync("FlowOnyx", MembershipType.Onyx);
            secondMembershipId.ShouldBeGreaterThan(firstMembershipId);

            var customerId = await CreateCustomerAsync("MembershipProspect");
            await _enquiryAppService.CreateAsync(new AqualLifeStyle.Application.Enquiries.Dto.CreateEnquiryDto
            {
                CustomerId = customerId,
                ProductId = 1,
                Message = "Assign membership on conversion"
            });

            var enquiryId = await UsingDbContextAsync(async ctx =>
                (await ctx.Enquiries.FirstAsync(e => e.CustomerId == customerId)).Id);

            await _enquiryAppService.ConvertToCustomerAsync(enquiryId, new AqualLifeStyle.Application.Enquiries.Dto.ConvertEnquiryToCustomerDto());
            await HandleConvertedEventAsync(
                new EnquiryConvertedEvent(enquiryId, customerId, 1, null, System.DateTime.UtcNow, tenantId: 1));

            await UsingDbContextAsync(async ctx =>
            {
                var customer = await ctx.Customers.FirstAsync(c => c.Id == customerId);
                customer.MembershipId.ShouldBe(firstMembershipId);
            });
        }

        [Fact]
        public async Task LinkCustomerAsync_FiltersMembershipsByEventTenant()
        {
            var tenantTwoMembershipId = await CreateMembershipAsync("TenantTwoTier", MembershipType.Onyx, tenantId: 2);
            var tenantOneMembershipId = await CreateMembershipAsync("TenantOneTier", MembershipType.Jasper, tenantId: 1);
            tenantTwoMembershipId.ShouldBeLessThan(tenantOneMembershipId);

            var tenantOneCustomerId = await CreateCustomerAsync("TenantScopedProspect", tenantId: 1);

            using (UsingTenantId(null))
            {
                await HandleConvertedEventAsync(
                    new EnquiryConvertedEvent(101, tenantOneCustomerId, 1, null, System.DateTime.UtcNow, tenantId: 1));
            }

            await UsingDbContextAsync(1, async ctx =>
            {
                var customer = await ctx.Customers.FirstAsync(c => c.Id == tenantOneCustomerId);
                customer.MembershipId.ShouldBe(tenantOneMembershipId);
            });
        }

        private async Task HandleConvertedEventAsync(EnquiryConvertedEvent evt)
        {
            // The handler scopes its work to evt.TenantId via the ambient unit of work, so simulate
            // the production path (where the event fires inside ConvertToCustomerAsync's UoW).
            using (var uow = Resolve<IUnitOfWorkManager>().Begin())
            {
                await Resolve<AqualLifeStyle.Application.Enquiries.EnquiryConvertedEventHandler>().HandleEventAsync(evt);
                await uow.CompleteAsync();
            }
        }

        private async Task SetReferredByFacilitatorAsync(int enquiryId, int facilitatorId)
        {
            var repo = Resolve<IEnquiryRepository>();
            var enquiry = await repo.GetAsync(enquiryId);
            enquiry.SetReferredByFacilitator(facilitatorId);
            await repo.UpdateAsync(enquiry);
        }

        private async Task<int> CreateCustomerAsync(string name, int? tenantId = 1)
        {
            if (tenantId == AbpSession.TenantId)
            {
                await _customerAppService.CreateAsync(new CreateCustomerDto
                {
                    Name = name,
                    Email = $"{name.ToLower()}@example.com"
                });
            }
            else
            {
                await UsingDbContextAsync(tenantId, async ctx =>
                {
                    ctx.Customers.Add(Customer.Create(tenantId, name, new AqualLifeStyle.Domain.Common.EmailAddress($"{name.ToLower()}@example.com")));
                    await ctx.SaveChangesAsync();
                });
            }

            return await UsingDbContextAsync(tenantId, async ctx =>
                (await ctx.Customers.FirstAsync(c => c.Email.Value == $"{name.ToLower()}@example.com")).Id);
        }

        private Task<int> CreateMembershipAsync(string name, MembershipType membershipType = MembershipType.Jasper, int? tenantId = 1)
        {
            return UsingDbContextAsync(tenantId, async ctx =>
            {
                var membership = Membership.Create(tenantId, name, $"{name} description", membershipType);
                ctx.Memberships.Add(membership);
                await ctx.SaveChangesAsync();
                return membership.Id;
            });
        }
    }
}
