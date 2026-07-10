using System;
using System.Threading.Tasks;
using Abp.Runtime.Caching;
using Abp.Runtime.Caching.Configuration;
using Abp.Runtime.Caching.Memory;
using AqualLifeStyle.Application.Memberships;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using Moq;
using Xunit;

namespace AqualLifeStyle.Tests
{
    public class ActiveMembershipCacheTests
    {
        [Fact]
        public async Task GetFirstActiveMembershipIdAsync_CachesRepositoryResultPerTenant()
        {
            var membershipRepository = new Mock<IMembershipRepository>();
            var cachingConfig = new Mock<ICachingConfiguration>();
            cachingConfig.Setup(x => x.Configurators).Returns(new System.Collections.Generic.List<ICacheConfigurator>());
            var cacheManager = new AbpMemoryCacheManager(cachingConfig.Object);

            var first = Membership.Create(1, "Jasper", "Cached tier", MembershipType.Jasper);
            first.Id = 5;
            membershipRepository
                .Setup(x => x.GetFirstActiveAsync(1))
                .ReturnsAsync(first);

            var cache = new ActiveMembershipCache(membershipRepository.Object, cacheManager);

            var firstLookup = await cache.GetFirstActiveMembershipIdAsync(1);
            var secondLookup = await cache.GetFirstActiveMembershipIdAsync(1);

            Assert.Equal(firstLookup, secondLookup);
            Assert.Equal(5, firstLookup);
            membershipRepository.Verify(x => x.GetFirstActiveAsync(1), Times.Once);
        }

        [Fact]
        public async Task Remove_ClearsCachedMembershipForTenant()
        {
            var membershipRepository = new Mock<IMembershipRepository>();
            var cachingConfig = new Mock<ICachingConfiguration>();
            cachingConfig.Setup(x => x.Configurators).Returns(new System.Collections.Generic.List<ICacheConfigurator>());
            var cacheManager = new AbpMemoryCacheManager(cachingConfig.Object);

            var first = Membership.Create(1, "Jasper", "First tier", MembershipType.Jasper);
            first.Id = 5;
            var second = Membership.Create(1, "Onyx", "Second tier", MembershipType.Onyx);
            second.Id = 7;
            membershipRepository
                .SetupSequence(x => x.GetFirstActiveAsync(1))
                .ReturnsAsync(first)
                .ReturnsAsync(second);

            var cache = new ActiveMembershipCache(membershipRepository.Object, cacheManager);

            var firstLookup = await cache.GetFirstActiveMembershipIdAsync(1);
            cache.Remove(1);
            var secondLookup = await cache.GetFirstActiveMembershipIdAsync(1);

            Assert.Equal(5, firstLookup);
            Assert.Equal(7, secondLookup);
            membershipRepository.Verify(x => x.GetFirstActiveAsync(1), Times.Exactly(2));
        }

        [Fact]
        public async Task GetFirstActiveMembershipIdAsync_SeparatesHostAndTenantCacheKeys()
        {
            var membershipRepository = new Mock<IMembershipRepository>();
            var cachingConfig = new Mock<ICachingConfiguration>();
            cachingConfig.Setup(x => x.Configurators).Returns(new System.Collections.Generic.List<ICacheConfigurator>());
            var cacheManager = new AbpMemoryCacheManager(cachingConfig.Object);

            var hostMembership = Membership.Create(null, "HostJasper", "Host tier", MembershipType.Jasper);
            hostMembership.Id = 2;
            var tenantMembership = Membership.Create(1, "TenantJasper", "Tenant tier", MembershipType.Jasper);
            tenantMembership.Id = 5;

            membershipRepository
                .Setup(x => x.GetFirstActiveAsync(null))
                .ReturnsAsync(hostMembership);
            membershipRepository
                .Setup(x => x.GetFirstActiveAsync(1))
                .ReturnsAsync(tenantMembership);

            var cache = new ActiveMembershipCache(membershipRepository.Object, cacheManager);

            var firstHostLookup = await cache.GetFirstActiveMembershipIdAsync(null);
            var firstTenantLookup = await cache.GetFirstActiveMembershipIdAsync(1);
            var secondHostLookup = await cache.GetFirstActiveMembershipIdAsync(null);
            var secondTenantLookup = await cache.GetFirstActiveMembershipIdAsync(1);

            Assert.Equal(2, firstHostLookup);
            Assert.Equal(5, firstTenantLookup);
            Assert.Equal(firstHostLookup, secondHostLookup);
            Assert.Equal(firstTenantLookup, secondTenantLookup);
            membershipRepository.Verify(x => x.GetFirstActiveAsync(null), Times.Once);
            membershipRepository.Verify(x => x.GetFirstActiveAsync(1), Times.Once);
        }
    }
}
