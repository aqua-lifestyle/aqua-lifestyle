using System.Threading.Tasks;
using AqualLifeStyle.Application.Admin.Users;
using AqualLifeStyle.Authorization;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Web.Tests.Integration
{
    public class DiagnosticInjection_Tests : AqualLifeStyleWebTestBase
    {
        [Fact]
        public void AdminUserAppService_ShouldHaveTenantManagerInjected()
        {
            var administration = IocManager.Resolve<IAdminUserAppService>();
            administration.ShouldNotBeNull();
            var prop = administration.GetType().GetProperty("TenantManager");
            prop.ShouldNotBeNull();
            var value = prop.GetValue(administration);
            // Assert non-null (if null, property injection failed)
            value.ShouldNotBeNull("TenantManager was not injected into AdminUserAppService");
        }
    }
}
