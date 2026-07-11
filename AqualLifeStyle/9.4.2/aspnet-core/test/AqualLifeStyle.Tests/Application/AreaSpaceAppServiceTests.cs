using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Abp.Runtime.Session;
using Moq;
using AqualLifeStyle.Application.AreaLeaders;
using AqualLifeStyle.Application.AreaLeaders.Dto;
using AqualLifeStyle.Application.Exceptions;
using AqualLifeStyle.Domain.AreaLeaders;
using Shouldly;

namespace AqualLifeStyle.Tests.Application
{
    public class AreaSpaceAppServiceTests
    {
        private readonly Mock<IAreaSpaceRepository> _areaSpaceRepositoryMock;
        private readonly Mock<IAreaLeaderRepository> _areaLeaderRepositoryMock;
        private readonly AreaSpaceAppService _service;

        public AreaSpaceAppServiceTests()
        {
            _areaSpaceRepositoryMock = new Mock<IAreaSpaceRepository>();
            _areaLeaderRepositoryMock = new Mock<IAreaLeaderRepository>();
            _service = new AreaSpaceAppService(
                _areaSpaceRepositoryMock.Object,
                _areaLeaderRepositoryMock.Object)
            {
                AbpSession = Mock.Of<IAbpSession>(s => s.TenantId == 1)
            };
        }

        [Fact]
        public async Task ApplyAsync_ThrowsNotFound_WhenAreaLeaderDoesNotExistForCurrentTenant()
        {
            _areaLeaderRepositoryMock
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<AreaLeader, bool>>>()))
                .ReturnsAsync((AreaLeader)null);

            var ex = await Assert.ThrowsAsync<AqualLifeStyleNotFoundException>(() =>
                _service.ApplyAsync(new CreateAreaSpaceDto
                {
                    AreaLeaderId = 33,
                    AddressLine = "404 Aqua Street",
                    Capacity = "50",
                    InterestedMembers = 20
                }));

            ex.Message.ShouldContain("AreaLeader");
            _areaSpaceRepositoryMock.Verify(r => r.InsertAndGetIdAsync(It.IsAny<AreaSpace>()), Times.Never);
        }
    }
}
