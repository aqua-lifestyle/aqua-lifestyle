using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Abp.Domain.Uow;
using Abp.Runtime.Session;
using Abp.UI;
using Moq;
using AqualLifeStyle.Application.Facilitators;
using AqualLifeStyle.Application.Facilitators.Dto;
using AqualLifeStyle.Domain.AreaLeaders;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Facilitators;
using Shouldly;

namespace AqualLifeStyle.Tests.Application
{
    public class FacilitatorAppServiceTransactionTests
    {
        private readonly Mock<IFacilitatorRepository> _facilitatorRepositoryMock;
        private readonly Mock<IAreaLeaderRepository> _areaLeaderRepositoryMock;
        private readonly Mock<ICustomerRepository> _customerRepositoryMock;
        private readonly Mock<IUnitOfWorkManager> _unitOfWorkManagerMock;
        private readonly Mock<IUnitOfWorkCompleteHandle> _unitOfWorkMock;
        private readonly FacilitatorAppService _service;

        public FacilitatorAppServiceTransactionTests()
        {
            _facilitatorRepositoryMock = new Mock<IFacilitatorRepository>();
            _areaLeaderRepositoryMock = new Mock<IAreaLeaderRepository>();
            _customerRepositoryMock = new Mock<ICustomerRepository>();
            _unitOfWorkManagerMock = new Mock<IUnitOfWorkManager>();
            _unitOfWorkMock = new Mock<IUnitOfWorkCompleteHandle>();

            _customerRepositoryMock
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Customer, bool>>>()))
                .ReturnsAsync(() =>
                {
                    var c = Customer.Create(1, "TransactionTest", new EmailAddress("tx@example.com"));
                    c.Id = 22;
                    return c;
                });

            _unitOfWorkManagerMock
                .Setup(m => m.Begin(It.IsAny<UnitOfWorkOptions>()))
                .Returns(_unitOfWorkMock.Object);

            _service = new FacilitatorAppService(
                _facilitatorRepositoryMock.Object,
                _areaLeaderRepositoryMock.Object,
                _customerRepositoryMock.Object,
                _unitOfWorkManagerMock.Object)
            {
                AbpSession = Mock.Of<IAbpSession>(s => s.TenantId == 1)
            };
        }

        [Fact]
        public async Task RegisterAsync_BeginsSerializableTransaction_AndCompletesUnitOfWork()
        {
            var leader = AreaLeader.Apply(tenantId: 1, customerId: 10, LicenseType.EntreLevel);

            _facilitatorRepositoryMock
                .Setup(r => r.GetByCustomerIdAsync(22, 1))
                .ReturnsAsync((Facilitator)null);
            _facilitatorRepositoryMock
                .Setup(r => r.InsertAndGetIdAsync(It.IsAny<Facilitator>()))
                .ReturnsAsync(42);
            _areaLeaderRepositoryMock
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<System.Func<AreaLeader, bool>>>()))
                .ReturnsAsync(leader);
            _areaLeaderRepositoryMock
                .Setup(r => r.UpdateAsync(leader))
                .Returns(Task.FromResult(leader));
            _unitOfWorkMock
                .Setup(u => u.CompleteAsync())
                .Returns(Task.CompletedTask);

            var result = await _service.RegisterAsync(new RegisterFacilitatorDto
            {
                CustomerId = 22,
                AreaLeaderId = 33
            });

            result.AreaLeaderId.ShouldBe(33);
            leader.DirectReferrals.ShouldBe(1);

            _unitOfWorkManagerMock.Verify(m => m.Begin(It.Is<UnitOfWorkOptions>(o =>
                o.IsTransactional == true &&
                o.IsolationLevel == System.Transactions.IsolationLevel.Serializable)), Times.Once);
            _facilitatorRepositoryMock.Verify(r => r.GetByCustomerIdAsync(22, 1), Times.Once);
            _facilitatorRepositoryMock.Verify(r => r.InsertAndGetIdAsync(It.IsAny<Facilitator>()), Times.Once);
            _areaLeaderRepositoryMock.Verify(r => r.UpdateAsync(leader), Times.Once);
            _unitOfWorkMock.Verify(u => u.CompleteAsync(), Times.Once);
        }

        [Fact]
        public async Task RegisterAsync_Throws_WhenFacilitatorAlreadyExistsForCustomer()
        {
            _facilitatorRepositoryMock
                .Setup(r => r.GetByCustomerIdAsync(22, 1))
                .ReturnsAsync(Facilitator.Register(1, 22, 33));

            var ex = await Assert.ThrowsAsync<UserFriendlyException>(() => _service.RegisterAsync(new RegisterFacilitatorDto
            {
                CustomerId = 22,
                AreaLeaderId = 33
            }));

            ex.Message.ShouldBe("Facilitator registration failed.");
            ex.Details.ShouldBe("A facilitator for this customer already exists.");

            _facilitatorRepositoryMock.Verify(r => r.GetByCustomerIdAsync(22, 1), Times.Once);
            _facilitatorRepositoryMock.Verify(r => r.InsertAndGetIdAsync(It.IsAny<Facilitator>()), Times.Never);
            _areaLeaderRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<AreaLeader>()), Times.Never);
            _unitOfWorkMock.Verify(u => u.CompleteAsync(), Times.Never);
        }

        [Fact]
        public async Task RegisterAsync_ThrowsNotFound_WhenCustomerDoesNotBelongToCurrentTenant()
        {
            _customerRepositoryMock
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Customer, bool>>>()))
                .ReturnsAsync((Customer)null);

            var ex = await Assert.ThrowsAsync<AqualLifeStyle.Application.Exceptions.AqualLifeStyleNotFoundException>(() =>
                _service.RegisterAsync(new RegisterFacilitatorDto
                {
                    CustomerId = 22,
                    AreaLeaderId = 33
                }));

            ex.Message.ShouldContain("Customer");

            _facilitatorRepositoryMock.Verify(r => r.GetByCustomerIdAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
            _facilitatorRepositoryMock.Verify(r => r.InsertAndGetIdAsync(It.IsAny<Facilitator>()), Times.Never);
            _areaLeaderRepositoryMock.Verify(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<AreaLeader, bool>>>()), Times.Never);
            _areaLeaderRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<AreaLeader>()), Times.Never);
            _unitOfWorkMock.Verify(u => u.CompleteAsync(), Times.Never);
        }
    }
}
