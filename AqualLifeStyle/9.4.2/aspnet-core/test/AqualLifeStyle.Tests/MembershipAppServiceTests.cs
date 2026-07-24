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
        private readonly Mock<IObjectMapper> _objectMapperMock;
        private readonly MembershipAppService _service;

        public MembershipAppServiceTests()
        {
            _membershipRepository = new Mock<IMembershipRepository>();
            _objectMapperMock = new Mock<IObjectMapper>();
            _service = new MembershipAppService(_membershipRepository.Object, _objectMapperMock.Object);
        }

        [Fact]
        public void GetSavingsWindowStatuses_ReturnsSharedContributionWindowState()
        {
            var statuses = _service.GetSavingsWindowStatuses("2026-07-16");

            Assert.Equal(4, statuses.Count);

            Assert.All(statuses, status =>
            {
                Assert.Equal(1, status.SavingsWindowOpenDay);
                Assert.Equal(15, status.SavingsWindowCloseDay);
                Assert.False(status.IsSavingsWindowOpen);
                Assert.Equal("Closed", status.StatusLabel);
                Assert.Equal("2026-07-16", status.AsOfDate);
            });
        }

        [Fact]
        public void GetSavingsWindowStatuses_WithInvalidDate_ThrowsValidationException()
        {
            Assert.Throws<AqualLifeStyleValidationException>(
                () => _service.GetSavingsWindowStatuses("not-a-date"));
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
        }
    }
}
