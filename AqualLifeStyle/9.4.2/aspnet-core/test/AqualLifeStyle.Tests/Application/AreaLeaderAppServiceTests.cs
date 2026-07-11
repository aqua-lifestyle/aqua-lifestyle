using System.Threading.Tasks;
using Abp.Runtime.Session;
using Abp.UI;
using Moq;
using Abp.ObjectMapping;
using AqualLifeStyle.Application.AreaLeaders;
using AqualLifeStyle.Application.AreaLeaders.Dto;
using AqualLifeStyle.Domain.AreaLeaders;
using AqualLifeStyle.Domain.Facilitators;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class AreaLeaderAppServiceTests
    {
        private readonly Mock<IAreaLeaderRepository> _areaLeaderRepositoryMock;
        private readonly Mock<IObjectMapper> _objectMapperMock;
        private readonly AreaLeaderAppService _service;

        public AreaLeaderAppServiceTests()
        {
            _areaLeaderRepositoryMock = new Mock<IAreaLeaderRepository>();
            _objectMapperMock = new Mock<IObjectMapper>();
            _service = new AreaLeaderAppService(_areaLeaderRepositoryMock.Object,
                _objectMapperMock.Object)
            {
                AbpSession = Mock.Of<IAbpSession>(s => s.TenantId == 1)
            };
        }

        [Fact]
        public async Task ApplyAsync_Throws_WhenActiveAreaLeaderCapIsReached()
        {
            _areaLeaderRepositoryMock
                .Setup(r => r.GetByCustomerIdAsync(It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync((AreaLeader)null);
            _areaLeaderRepositoryMock
                .Setup(r => r.CountActiveAsync())
                .ReturnsAsync(AreaSpaceApprovalRules.MaxAreaLeaders);

            var ex = await Assert.ThrowsAsync<UserFriendlyException>(() =>
                _service.ApplyAsync(new RegisterAreaLeaderDto
                {
                    CustomerId = 42,
                    LicenseType = (int)LicenseType.EntreLevel
                }));

            ex.Message.ShouldContain("Area leader application failed.");
            ex.Details.ShouldContain(AreaSpaceApprovalRules.MaxAreaLeaders.ToString());
            _areaLeaderRepositoryMock.Verify(r => r.InsertAndGetIdAsync(It.IsAny<AreaLeader>()), Times.Never);
        }
    }
}
