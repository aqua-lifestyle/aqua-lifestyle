using System;
using System.Threading.Tasks;
using AqualLifeStyle.Application.Admin.Users;
using AqualLifeStyle.Application.Admin.Users.Dto;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Web.Tests.Integration
{
    public class AdminCreate_DirectInvocation_Tests : AqualLifeStyleWebTestBase
    {
        [Fact]
        public async Task AdminUserAppService_CreateAsync_ShouldWork_WhenResolvedFromIoc()
        {
            // Arrange
            LoginAsDefaultTenantAdmin();
            var administration = IocManager.Resolve<IAdminUserAppService>();
            administration.ShouldNotBeNull();

            var prop = administration.GetType().GetProperty("TenantManager");
            prop.ShouldNotBeNull();
            var tm = prop.GetValue(administration);
            // Record whether TenantManager is present before invoking CreateAsync
            tm.ShouldNotBeNull("TenantManager unexpectedly null when inspecting resolved IAdminUserAppService");

            var email = $"diag-invite-{Guid.NewGuid():N}@example.test";

            // Act
            try
            {
                // Ensure a UoW is active in this test-host invocation so CurrentUnitOfWork is available
                var uowManager = IocManager.Resolve<Abp.Domain.Uow.IUnitOfWorkManager>();
                using (var uow = uowManager.Begin())
                {
                    var created = await administration.CreateAsync(new AdminCreateUserInput
                    {
                        TenantId = 1,
                        FirstName = "Diag",
                        LastName = "Invite",
                        Email = email,
                        Role = AqualLifeStyle.Domain.Enums.AquaUserRole.SystemAdmin,
                        Justification = "Diag test"
                    });

                    // Assert
                    created.ShouldNotBeNull();
                    created.Email.ShouldBe(email);

                    await uow.CompleteAsync();
                }
            }
            catch (System.Exception ex)
            {
                var adminType = administration.GetType().FullName;
                var propType = tm?.GetType().FullName ?? "<null>";

                // Diagnostic: attempt to resolve the concrete AdminUserAppService and invoke CreateAsync
                try
                {
                    var concrete = IocManager.Resolve<AqualLifeStyle.Application.Admin.Users.AdminUserAppService>();
                    var concreteTmProp = concrete.GetType().GetProperty("TenantManager");
                    var concreteTm = concreteTmProp?.GetValue(concrete);
                    Console.WriteLine($"Diagnostic: concrete AdminUserAppService resolved. TenantManager={(concreteTm==null?"<null>":concreteTm.GetType().FullName)}");

                    var created2 = await concrete.CreateAsync(new AdminCreateUserInput
                    {
                        TenantId = 1,
                        FirstName = "Diag",
                        LastName = "Invite",
                        Email = email,
                        Role = AqualLifeStyle.Domain.Enums.AquaUserRole.SystemAdmin,
                        Justification = "Diag test"
                    });
                    Console.WriteLine($"Diagnostic: concrete CreateAsync succeeded: {created2?.Email}");
                }
                catch (Exception ex2)
                {
                    Console.WriteLine($"Diagnostic: concrete invocation failed: {ex2}");
                }

                throw new System.Exception($"CreateAsync failed. AdminType={adminType}; TenantManagerType={propType}; Inner={ex}", ex);
            }
        }
    }
}
