using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Castle.Core.Logging;
using AqualLifeStyle.Application.AreaLeaders;
using AqualLifeStyle.Application.AreaLeaders.Dto;
using AqualLifeStyle.Application.Customers;
using AqualLifeStyle.Application.Customers.Dto;
using AqualLifeStyle.Application.Enquiries;
using AqualLifeStyle.Application.Exceptions;
using AqualLifeStyle.Application.Facilitators;
using AqualLifeStyle.Application.Facilitators.Dto;
using AqualLifeStyle.Application.Memberships;
using AqualLifeStyle.Application.Memberships.Dto;
using AqualLifeStyle.Domain.AreaLeaders;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enquiries;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Facilitators;
using AqualLifeStyle.Domain.Memberships;
using AqualLifeStyle.EntityFrameworkCore;
using NSubstitute;
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
        private readonly IMembershipAppService _membershipAppService;

        public EnquiryConversionFlowTests()
        {
            _areaLeaderAppService = Resolve<IAreaLeaderAppService>();
            _facilitatorAppService = Resolve<IFacilitatorAppService>();
            _enquiryAppService = Resolve<IEnquiryAppService>();
            _customerAppService = Resolve<ICustomerAppService>();
            _membershipAppService = Resolve<IMembershipAppService>();
        }

        [Fact]
        public async Task ConvertEnquiry_SourcedByFacilitator_AttributesReferralsAndUpdatesNetwork()
        {
            var customerToLeaderId = await CreateCustomerAsync("FlowLeader");
            var leader = await _areaLeaderAppService.ApplyAsync(
                new RegisterAreaLeaderDto { CustomerId = customerToLeaderId, LicenseType = (int)LicenseType.EntreLevel });

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
                var referrals = ctx.Referrals
                    .Where(r => r.SourceEnquiryId == enquiryId)
                    .ToList();
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
            var customerToLeaderId = await CreateCustomerAsync("RetryLeader");
            var leader = await _areaLeaderAppService.ApplyAsync(
                new RegisterAreaLeaderDto { CustomerId = customerToLeaderId, LicenseType = (int)LicenseType.EntreLevel });

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
                new EnquiryConvertedEvent(enquiryId, prospectCustomerId, 1, facilitator.Id, DateTime.UtcNow, tenantId: 1));

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
        public async Task HandleEventAsync_WhenAnotherTenantHasSameSourceEnquiryId_StillAttributesCurrentTenantReferrals()
        {
            var customerToLeaderId = await CreateCustomerAsync("CrossTenantLeader");
            var leader = await _areaLeaderAppService.ApplyAsync(
                new RegisterAreaLeaderDto { CustomerId = customerToLeaderId, LicenseType = (int)LicenseType.EntreLevel });

            var facilitatorCustomerId = await CreateCustomerAsync("CrossTenantFacilitator");
            var facilitator = await _facilitatorAppService.RegisterAsync(
                new RegisterFacilitatorDto { CustomerId = facilitatorCustomerId, AreaLeaderId = leader.Id });

            var prospectCustomerId = await CreateCustomerAsync("CrossTenantProspect");
            await _enquiryAppService.CreateAsync(new AqualLifeStyle.Application.Enquiries.Dto.CreateEnquiryDto
            {
                CustomerId = prospectCustomerId,
                ProductId = 1,
                Message = "Cross-tenant idempotency guard"
            });

            var enquiryId = await UsingDbContextAsync(async ctx =>
                (await ctx.Enquiries.FirstAsync(e => e.CustomerId == prospectCustomerId)).Id);

            await SetReferredByFacilitatorAsync(enquiryId, facilitator.Id);
            await CreateDirectReferralAsync(tenantId: 2, sourceEnquiryId: enquiryId);

            await HandleConvertedEventAsync(
                new EnquiryConvertedEvent(enquiryId, prospectCustomerId, 1, facilitator.Id, System.DateTime.UtcNow, tenantId: 1));

            await UsingDbContextAsync(async ctx =>
            {
                var tenantOneReferrals = ctx.Referrals
                    .Where(r => r.TenantId == 1 && r.SourceEnquiryId == enquiryId)
                    .ToList();

                tenantOneReferrals.Count.ShouldBe(2);
                tenantOneReferrals.Count(r => r.Type == ReferralType.Direct).ShouldBe(1);
                tenantOneReferrals.Count(r => r.Type == ReferralType.Indirect).ShouldBe(1);
                ctx.Referrals.Count(r => r.TenantId == 2 && r.SourceEnquiryId == enquiryId).ShouldBe(1);

                var updatedFacilitator = await ctx.Facilitators.FirstAsync(f => f.Id == facilitator.Id);
                updatedFacilitator.DirectReferrals.ShouldBe(1);

                var updatedLeader = await ctx.AreaLeaders.FirstAsync(a => a.Id == leader.Id);
                updatedLeader.IndirectReferrals.ShouldBe(1);
            });
        }

        [Fact]
        public async Task HandleEventAsync_WhenFacilitatorReferencesMissingAreaLeader_DoesNotCreateReferrals()
        {
            var scenario = await CreateOrphanedReferralScenarioAsync("MissingLeader");

            await HandleConvertedEventAsync(
                new EnquiryConvertedEvent(
                    scenario.enquiryId,
                    scenario.prospectCustomerId,
                    1,
                    scenario.facilitatorId,
                    System.DateTime.UtcNow,
                    tenantId: 1));

            await UsingDbContextAsync(async ctx =>
            {
                ctx.Referrals.Count(r => r.SourceEnquiryId == scenario.enquiryId).ShouldBe(0);

                var facilitator = await ctx.Facilitators.FirstAsync(f => f.Id == scenario.facilitatorId);
                facilitator.DirectReferrals.ShouldBe(0);

                var areaLeader = await ctx.AreaLeaders.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == scenario.areaLeaderId);
                areaLeader.ShouldNotBeNull();
                areaLeader.IsDeleted.ShouldBeTrue();
            });
        }

        [Fact]
        public async Task HandleEventAsync_WhenFacilitatorReferencesMissingAreaLeader_LogsWarning()
        {
            var scenario = await CreateOrphanedReferralScenarioAsync("MissingLeaderWarn");
            var logger = Substitute.For<ILogger>();
            var handler = Resolve<AqualLifeStyle.Application.Enquiries.EnquiryConvertedEventHandler>();
            handler.Logger = logger;

            await handler.HandleEventAsync(
                new EnquiryConvertedEvent(
                    scenario.enquiryId,
                    scenario.prospectCustomerId,
                    1,
                    scenario.facilitatorId,
                    System.DateTime.UtcNow,
                    tenantId: 1));

            logger.Received(1).Warn(Arg.Is<string>(message =>
                message.Contains($"enquiry {scenario.enquiryId}")
                && message.Contains("tenant 1")
                && message.Contains($"facilitator {scenario.facilitatorId}")
                && message.Contains($"area leader {scenario.areaLeaderId}")));
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

            await UsingDbContextAsync(async ctx =>
            {
                var customer = await ctx.Customers.FirstAsync(c => c.Id == customerId);
                customer.MembershipId.ShouldBe(firstMembershipId);
            });
        }

        [Fact]
        public async Task ConvertEnquiry_AssignsMembershipAfterMembershipCreateInvalidatesCachedMiss()
        {
            var activeMembershipCache = Resolve<IActiveMembershipCache>();
            (await activeMembershipCache.GetFirstActiveMembershipIdAsync(1)).ShouldBeNull();

            await _membershipAppService.CreateAsync(new CreateMembershipDto
            {
                Name = "CachedMissRecoveryTier",
                Description = "Created after empty cache result",
                MembershipType = MembershipType.Jasper
            });

            var membershipId = await UsingDbContextAsync(async ctx =>
                (await ctx.Memberships.FirstAsync(m => m.Name == "CachedMissRecoveryTier")).Id);

            var customerId = await CreateCustomerAsync("CachedMissProspect");
            await _enquiryAppService.CreateAsync(new AqualLifeStyle.Application.Enquiries.Dto.CreateEnquiryDto
            {
                CustomerId = customerId,
                ProductId = 1,
                Message = "Membership should assign after cache invalidation"
            });

            var enquiryId = await UsingDbContextAsync(async ctx =>
                (await ctx.Enquiries.FirstAsync(e => e.CustomerId == customerId)).Id);

            await _enquiryAppService.ConvertToCustomerAsync(
                enquiryId,
                new AqualLifeStyle.Application.Enquiries.Dto.ConvertEnquiryToCustomerDto());

            await UsingDbContextAsync(async ctx =>
            {
                var customer = await ctx.Customers.FirstAsync(c => c.Id == customerId);
                customer.MembershipId.ShouldBe(membershipId);
            });
        }

        [Fact]
        public async Task HandleEventAsync_WithoutTenantIdOnEvent_ThrowsAuthorizationException()
        {
            await CreateMembershipAsync("TenantOneTier", MembershipType.Jasper, tenantId: 1);
            var tenantOneCustomerId = await CreateCustomerAsync("TenantScopedProspect", tenantId: 1);

            var ex = await Assert.ThrowsAsync<AqualLifeStyleAuthorizationException>(() =>
                HandleConvertedEventAsync(
                    new EnquiryConvertedEvent(101, tenantOneCustomerId, 1, null, System.DateTime.UtcNow, tenantId: null)));

            ex.Message.ShouldContain("tenant context");

            await UsingDbContextAsync(1, async ctx =>
            {
                var customer = await ctx.Customers.FirstAsync(c => c.Id == tenantOneCustomerId);
                customer.MembershipId.ShouldBeNull();
            });
        }

        private async Task HandleConvertedEventAsync(EnquiryConvertedEvent evt)
        {
            await Resolve<AqualLifeStyle.Application.Enquiries.EnquiryConvertedEventHandler>().HandleEventAsync(evt);
        }

        private async Task SetReferredByFacilitatorAsync(int enquiryId, int facilitatorId)
        {
            await UsingDbContextAsync(async context =>
            {
                var enquiry = await context.Enquiries.SingleAsync(item => item.Id == enquiryId);
                enquiry.SetReferredByFacilitator(facilitatorId);
                await context.SaveChangesAsync();
            });
        }

        private async Task<(int areaLeaderId, int facilitatorId, int prospectCustomerId, int enquiryId)> CreateOrphanedReferralScenarioAsync(string namePrefix)
        {
            var customerToLeaderId = await CreateCustomerAsync($"{namePrefix}Leader");
            var leader = await _areaLeaderAppService.ApplyAsync(
                new RegisterAreaLeaderDto { CustomerId = customerToLeaderId, LicenseType = (int)LicenseType.EntreLevel });

            var facilitatorCustomerId = await CreateCustomerAsync($"{namePrefix}Facilitator");
            var facilitator = await _facilitatorAppService.RegisterAsync(
                new RegisterFacilitatorDto { CustomerId = facilitatorCustomerId, AreaLeaderId = leader.Id });

            var prospectCustomerId = await CreateCustomerAsync($"{namePrefix}Prospect");
            await _enquiryAppService.CreateAsync(new AqualLifeStyle.Application.Enquiries.Dto.CreateEnquiryDto
            {
                CustomerId = prospectCustomerId,
                ProductId = 1,
                Message = $"Orphaned area leader scenario for {namePrefix}"
            });

            var enquiryId = await UsingDbContextAsync(async ctx =>
                (await ctx.Enquiries.FirstAsync(e => e.CustomerId == prospectCustomerId)).Id);

            await SetReferredByFacilitatorAsync(enquiryId, facilitator.Id);

            // Simulate a facilitator whose area leader has been removed. The Facilitator -> AreaLeader
            // relationship is required with OnDelete(Restrict), so the leader cannot be hard-deleted;
            // instead it is soft-deleted. The conversion handler's lookup applies the soft-delete
            // filter to the included AreaLeader, so the facilitator is loaded with a null AreaLeader.
            await UsingDbContextAsync(async ctx =>
            {
                var areaLeader = await ctx.AreaLeaders.FirstAsync(a => a.Id == leader.Id);
                areaLeader.IsDeleted = true;
                areaLeader.DeletionTime = System.DateTime.Now;
                areaLeader.DeleterUserId = AbpSession.UserId;
                await ctx.SaveChangesAsync();
            });

            return (leader.Id, facilitator.Id, prospectCustomerId, enquiryId);
        }

        private async Task<int> CreateCustomerAsync(string name, int? tenantId = 1)
        {
            if (tenantId == AbpSession.TenantId)
            {
                var userId = await CreateTestUserAsync(tenantId, $"user-{Guid.NewGuid():N}", $"user-{Guid.NewGuid():N}@example.com");
                SetCurrentUser(userId, tenantId);
                await _customerAppService.CreateAsync(new CreateCustomerDto
                {
                    Name = name,
                    Email = $"{name.ToLower()}@example.com"
                });
            }
            else
            {
                var userId = await CreateTestUserAsync(tenantId, $"user-{Guid.NewGuid():N}", $"user-{Guid.NewGuid():N}@example.com");
                await UsingDbContextAsync(tenantId, async ctx =>
                {
                    ctx.Customers.Add(Customer.Create(tenantId, userId, name, new AqualLifeStyle.Domain.Common.EmailAddress($"{name.ToLower()}@example.com")));
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

        private async Task<int> CreateDirectReferralAsync(int tenantId, int sourceEnquiryId)
        {
            var customerUserId = await CreateTestUserAsync(tenantId, $"user-{Guid.NewGuid():N}", $"user-{Guid.NewGuid():N}@example.com");
            var customerId = await UsingDbContextAsync(tenantId, async ctx =>
            {
                var customer = Customer.Create(tenantId, customerUserId, $"ReferralCustomer{tenantId}", new EmailAddress($"referralcustomer{tenantId}@example.com"));
                ctx.Customers.Add(customer);
                await ctx.SaveChangesAsync();
                return customer.Id;
            });

            var leaderUserId = await CreateTestUserAsync(tenantId, $"user-{Guid.NewGuid():N}", $"user-{Guid.NewGuid():N}@example.com");
            var leaderCustomerId = await UsingDbContextAsync(tenantId, async ctx =>
            {
                var c = Customer.Create(tenantId, leaderUserId, $"ReferralLeader{tenantId}", new EmailAddress($"referralleader{tenantId}@example.com"));
                ctx.Customers.Add(c);
                await ctx.SaveChangesAsync();
                return c.Id;
            });

            var leader = await UsingDbContextAsync(tenantId, async ctx =>
            {
                var l = AreaLeader.Apply(tenantId, leaderCustomerId, LicenseType.EntreLevel);
                ctx.AreaLeaders.Add(l);
                await ctx.SaveChangesAsync();
                return l;
            });

            var facilitatorId = await UsingDbContextAsync(tenantId, async ctx =>
            {
                var f = Facilitator.Register(tenantId, customerId, leader.Id);
                ctx.Facilitators.Add(f);
                await ctx.SaveChangesAsync();
                return f.Id;
            });

            return await UsingDbContextAsync(tenantId, async ctx =>
            {
                var referral = Referral.CreateDirect(
                    tenantId,
                    referrerFacilitatorId: facilitatorId,
                    referredCustomerId: customerId,
                    sourceEnquiryId: sourceEnquiryId,
                    awardAmount: 100m,
                    convertedAt: System.DateTime.UtcNow);
                ctx.Referrals.Add(referral);
                await ctx.SaveChangesAsync();
                return referral.Id;
            });
        }
    }
}
