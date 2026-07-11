using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Abp.Runtime.Session;
using Moq;
using Abp.ObjectMapping;
using Abp.Events.Bus;
using Abp.Domain.Uow;
using AqualLifeStyle.Application.AreaLeaders;
using AqualLifeStyle.Application.AreaLeaders.Dto;
using AqualLifeStyle.Application.Exceptions;
using AqualLifeStyle.Domain.AreaLeaders;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class AreaSpaceAppServiceTests
    {
        private readonly Mock<IAreaSpaceRepository> _areaSpaceRepositoryMock;
        private readonly Mock<IAreaLeaderRepository> _areaLeaderRepositoryMock;
        private readonly Mock<IObjectMapper> _objectMapperMock;
        private readonly Mock<IEventBus> _eventBusMock;
        private readonly Mock<IUnitOfWorkManager> _unitOfWorkManagerMock;
        private readonly Mock<IActiveUnitOfWork> _activeUnitOfWorkMock;
        private readonly AreaSpaceAppService _service;

        public AreaSpaceAppServiceTests()
        {
            _areaSpaceRepositoryMock = new Mock<IAreaSpaceRepository>();
            _objectMapperMock = new Mock<IObjectMapper>();
            _areaLeaderRepositoryMock = new Mock<IAreaLeaderRepository>();
            _eventBusMock = new Mock<IEventBus>();
            _activeUnitOfWorkMock = new Mock<IActiveUnitOfWork>();
            _unitOfWorkManagerMock = new Mock<IUnitOfWorkManager>();
            _unitOfWorkManagerMock.Setup(m => m.Current).Returns(_activeUnitOfWorkMock.Object);

            _service = new AreaSpaceAppService(
                _areaSpaceRepositoryMock.Object,
                _areaLeaderRepositoryMock.Object,
                _objectMapperMock.Object,
                _eventBusMock.Object)
            {
                AbpSession = Mock.Of<IAbpSession>(s => s.TenantId == 1),
                UnitOfWorkManager = _unitOfWorkManagerMock.Object
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

        [Fact]
        public async Task ApproveAsync_TriggersAreaSpaceApprovedEventWhenUnitOfWorkCompletes()
        {
            var space = AreaSpace.Apply(
                tenantId: 1,
                areaLeaderId: 9,
                new Address("1 Main St", "Cape Town", "WC", "8001"),
                "20 by 40",
                AreaSpaceApprovalRules.MinInterestedMembers);
            space.Id = 44;
            space.StartReview();
            for (var i = 0; i < AreaSpaceApprovalRules.RequiredPresentations; i++) space.RecordPresentation();
            for (var i = 0; i < AreaSpaceApprovalRules.RequiredStartupOrders; i++) space.RecordStartupOrder();

            _areaSpaceRepositoryMock
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<AreaSpace, bool>>>()))
                .ReturnsAsync(space);

            var approvedAt = space.ReviewStartedAt.Value.AddHours(AreaSpaceApprovalRules.ReviewWindowHours + 1);

            await _service.ApproveAsync(space.Id, approvedAt);

            _areaSpaceRepositoryMock.Verify(r => r.UpdateAsync(space), Times.Once);
            _eventBusMock.Verify(e => e.Trigger(It.IsAny<AreaSpaceApprovedEvent>()), Times.Never);

            _activeUnitOfWorkMock.Raise(uow => uow.Completed += null, EventArgs.Empty);

            _eventBusMock.Verify(e => e.Trigger(It.Is<AreaSpaceApprovedEvent>(evt =>
                evt.TenantId == 1 &&
                evt.AreaSpaceId == 44 &&
                evt.AreaLeaderId == 9)), Times.Once);
        }
    }
}
