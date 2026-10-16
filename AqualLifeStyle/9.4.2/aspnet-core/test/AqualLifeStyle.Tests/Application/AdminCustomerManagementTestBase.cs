using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AqualLifeStyle.Domain.AQGreen;
using Castle.MicroKernel.Registration;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace AqualLifeStyle.Tests.Application
{
    public abstract class AdminCustomerManagementTestBase : AqualLifeStyleTestBase
    {
        protected AdminCustomerManagementTestBase()
        {
            // Component-only SQLite fixture: return the actual Area, without
            // claiming PostgreSQL row-lock evidence. HTTP PostgreSQL regressions
            // separately execute the real authority stabilizer and transactions.
            var authority = Substitute.For<IAQGreenApprovalAuthorityStabilizer>();
            authority.LockCustomerAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            authority.StabilizeAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<long?>(), Arg.Any<CancellationToken>())
                .Returns(call => UsingDbContextAsync(context => context.Customers
                    .Where(customer => customer.Id == call.ArgAt<int>(1) && customer.TenantId == call.ArgAt<int>(0))
                    .Select(customer => customer.AreaId.Value).SingleAsync()));
            authority.StabilizeRemovedCustomerAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
                .Returns(call => UsingDbContextAsync(context => context.Customers.IgnoreQueryFilters()
                    .Where(customer => customer.Id == call.ArgAt<int>(1) && customer.TenantId == call.ArgAt<int>(0) && customer.IsDeleted)
                    .Select(customer => customer.AreaId.Value).SingleAsync()));
            LocalIocManager.IocContainer.Register(Component.For<IAQGreenApprovalAuthorityStabilizer>()
                .Instance(authority).IsDefault());
        }
    }
}
