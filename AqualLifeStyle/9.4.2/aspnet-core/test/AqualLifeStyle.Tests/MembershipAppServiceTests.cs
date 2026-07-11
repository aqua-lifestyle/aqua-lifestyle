using System.Linq;
using System.Threading.Tasks;
using Abp.Runtime.Session;
using Abp.UI;
using AqualLifeStyle.Application.Exceptions;
using AqualLifeStyle.Application.Memberships;
using AqualLifeStyle.Application.Memberships.Dto;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using Moq;
using Abp.ObjectMapping;
using Xunit;

namespace AqualLifeStyle.Tests
{
    public class MembershipAppServiceTests
    {
        private readonly Mock<IMembershipRepository> _membershipRepository;
        private readonly Mock<IActiveMembershipCache> _activeMembershipCache;
        private readonly Mock<IObjectMapper> _objectMapperMock;
        private readonly MembershipAppService _service;

        public MembershipAppServiceTests()
        {
            _membershipRepository = new Mock<IMembershipRepository>();
            _activeMembershipCache = new Mock<IActiveMembershipCache>();
            _objectMapperMock = new Mock<IObjectMapper>();
            _service = new MembershipAppService(_membershipRepository.Object, _activeMembershipCache.Object,
                _objectMapperMock.Object);
        }

        [Fact]
        public void GetSavingsWindowStatuses_ReturnsTierSpecificWindowState()
        {
            var statuses = _service.GetSavingsWindowStatuses("2026-07-16");

            Assert.Equal(4, statuses.Count);

            var jasper = statuses.Single(status => status.Tier == (int)MembershipType.Jasper);
            var onyx = statuses.Single(status => status.Tier == (int)MembershipType.Onyx);
            var aqGreen = statuses.Single(status => status.Tier == (int)MembershipType.AQGreen);
            var businessPremier = statuses.Single(status => status.Tier == (int)MembershipType.BusinessPremier);

            Assert.False(jasper.IsSavingsWindowOpen);
            Assert.Equal("Closed", jasper.StatusLabel);
            Assert.True(onyx.IsSavingsWindowOpen);
            Assert.True(aqGreen.IsSavingsWindowOpen);
            Assert.True(businessPremier.IsSavingsWindowOpen);
            Assert.All(statuses, status => Assert.Equal("2026-07-16", status.AsOfDate));
        }

        [Fact]
        public void GetSavingsWindowStatuses_WithInvalidDate_ThrowsValidationException()
        {
            Assert.Throws<AqualLifeStyleValidationException>(
                () => _service.GetSavingsWindowStatuses("not-a-date"));
        }

        [Fact]
        public async Task CreateAsync_InvalidatesActiveMembershipCache()
        {
            _service.AbpSession = Mock.Of<IAbpSession>(s => s.TenantId == 7);

            await _service.CreateAsync(new CreateMembershipDto
            {
                Name = "Jasper",
                Description = "Entry tier",
                MembershipType = MembershipType.Jasper
            });

            _membershipRepository.Verify(x => x.InsertAsync(It.Is<Membership>(m => m.TenantId == 7)), Times.Once);
            _activeMembershipCache.Verify(x => x.Remove(7), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_ThrowsUserFriendlyException_WhenTenantContextIsMissing()
        {
            _service.AbpSession = Mock.Of<IAbpSession>(s => s.TenantId == (int?)null);

            var ex = await Assert.ThrowsAsync<UserFriendlyException>(() => _service.CreateAsync(new CreateMembershipDto
            {
                Name = "Host Membership",
                Description = "Should not be created from host context",
                MembershipType = MembershipType.Jasper
            }));

            Assert.Equal("Membership creation failed.", ex.Message);
            Assert.Equal("A tenant context is required.", ex.Details);
            _membershipRepository.Verify(x => x.InsertAsync(It.IsAny<Membership>()), Times.Never);
            _activeMembershipCache.Verify(x => x.Remove(It.IsAny<int?>()), Times.Never);
        }

        [Fact]
        public async Task UpdateAsync_InvalidatesActiveMembershipCache()
        {
            var membership = Membership.Create(tenantId: 11, name: "Jasper", description: "Initial", membershipType: MembershipType.Jasper);

            _membershipRepository
                .Setup(x => x.GetAsync(7))
                .ReturnsAsync(membership);

            await _service.UpdateAsync(new MembershipDto
            {
                Id = 7,
                Name = "Onyx",
                Description = "Updated",
                MembershipType = MembershipType.Onyx
            });

            _membershipRepository.Verify(x => x.UpdateAsync(membership), Times.Once);
            _activeMembershipCache.Verify(x => x.Remove(11), Times.Once);
        }

        [Fact]
        public async Task SetActivationDateAsync_InvalidatesActiveMembershipCacheForMembershipTenant()
        {
            var membership = Membership.Create(tenantId: 13, name: "Jasper", description: "Initial", membershipType: MembershipType.Jasper);

            _membershipRepository
                .Setup(x => x.GetAsync(9))
                .ReturnsAsync(membership);

            await _service.SetActivationDateAsync(9, new SetMembershipActivationDto
            {
                ActivationDate = "2026-07-10"
            });

            _membershipRepository.Verify(x => x.UpdateAsync(membership), Times.Once);
            _activeMembershipCache.Verify(x => x.Remove(13), Times.Once);
        }

        [Fact]
        public async Task SetMonthlyObligationAsync_InvalidatesActiveMembershipCacheForMembershipTenant()
        {
            var membership = Membership.Create(tenantId: 17, name: "Onyx", description: "Initial", membershipType: MembershipType.Onyx);

            _membershipRepository
                .Setup(x => x.GetAsync(12))
                .ReturnsAsync(membership);

            await _service.SetMonthlyObligationAsync(12, new SetMonthlyObligationDto
            {
                Amount = 300m
            });

            _membershipRepository.Verify(x => x.UpdateAsync(membership), Times.Once);
            _activeMembershipCache.Verify(x => x.Remove(17), Times.Once);
        }

        [Fact]
        public async Task MarkObligationMetAsync_InvalidatesActiveMembershipCacheForMembershipTenant()
        {
            var membership = Membership.Create(tenantId: 19, name: "AQ Green", description: "Initial", membershipType: MembershipType.AQGreen);

            _membershipRepository
                .Setup(x => x.GetAsync(14))
                .ReturnsAsync(membership);

            await _service.MarkObligationMetAsync(14, new MarkObligationMetDto
            {
                AsOfDate = "2026-07-10"
            });

            _membershipRepository.Verify(x => x.UpdateAsync(membership), Times.Once);
            _activeMembershipCache.Verify(x => x.Remove(19), Times.Once);
        }
    }
}
