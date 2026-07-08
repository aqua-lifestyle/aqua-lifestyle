using System.Linq;
using AqualLifeStyle.Application.Exceptions;
using AqualLifeStyle.Application.Memberships;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using Moq;
using Xunit;

namespace AqualLifeStyle.Tests
{
    public class MembershipAppServiceTests
    {
        private readonly MembershipAppService _service;

        public MembershipAppServiceTests()
        {
            var membershipRepository = new Mock<IMembershipRepository>();
            _service = new MembershipAppService(membershipRepository.Object);
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
    }
}
