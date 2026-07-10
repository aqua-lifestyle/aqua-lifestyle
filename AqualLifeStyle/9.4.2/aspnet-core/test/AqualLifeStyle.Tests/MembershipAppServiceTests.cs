using System.Linq;
using System.Threading.Tasks;
using AqualLifeStyle.Application.Exceptions;
using AqualLifeStyle.Application.Memberships;
using AqualLifeStyle.Application.Memberships.Dto;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using Moq;
using Xunit;

namespace AqualLifeStyle.Tests
{
    public class MembershipAppServiceTests
    {
        private readonly Mock<IMembershipRepository> _membershipRepository;
        private readonly MembershipAppService _service;
        private readonly Mock<IActiveMembershipCache> _activeMembershipCache;

        public MembershipAppServiceTests()
        {
            _membershipRepository = new Mock<IMembershipRepository>();
            _activeMembershipCache = new Mock<IActiveMembershipCache>();
            _service = new MembershipAppService(_membershipRepository.Object, _activeMembershipCache.Object);
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
            await _service.CreateAsync(new CreateMembershipDto
            {
                Name = "Jasper",
                Description = "Entry tier",
                MembershipType = MembershipType.Jasper
            });

            _membershipRepository.Verify(x => x.InsertAsync(It.IsAny<Membership>()), Times.Once);
            _activeMembershipCache.Verify(x => x.Remove(null), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_InvalidatesActiveMembershipCache()
        {
            var membership = Membership.Create(tenantId: null, name: "Jasper", description: "Initial", membershipType: MembershipType.Jasper);

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
            _activeMembershipCache.Verify(x => x.Remove(null), Times.Once);
        }
    }
}
