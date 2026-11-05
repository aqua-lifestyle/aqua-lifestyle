using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Abp.Authorization;
using Abp.Configuration;
using Abp.UI;
using AqualLifeStyle.Authorization.Accounts;
using AqualLifeStyle.Authorization.Accounts.Dto;
using AqualLifeStyle.Configuration;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Web.Host.Startup;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class FirstAccountBootstrapTests : AqualLifeStyleTestBase
    {
        private readonly IConfiguration _configuration;
        private readonly IAccountAppService _account;

        public FirstAccountBootstrapTests()
        {
            _configuration = Resolve<IConfiguration>();
            _configuration["App:AQGreenV2:Commissioning:AdmissionMode"] = "CLEAN_START";
            _configuration["App:AQGreenV2:Commissioning:Enabled"] = "false";
            _configuration[YocoCutoverOptions.Prefix + "Enabled"] = "true";
            _configuration[RuntimeOperationGuards.FinancialSetting] = "false";
            _account = Resolve<IAccountAppService>();
        }

        [Fact]
        public async Task CleanStartAcceptsAnAccountBeforeRootSelectionWithFinancialWorkHeld()
        {
            await Resolve<ISettingManager>().ChangeSettingForTenantAsync(1, "Abp.Account.IsSelfRegistrationEnabled", "true");
            var name = "first_account_" + Guid.NewGuid().ToString("N");
            RegisterOutput output = null;
            var context = Context("POST", "/api/services/app/Account/Register");
            using (UsingTenantId(null))
                await new ApplicationRecoveryMiddleware(async _ =>
                {
                    YocoCutoverDispatch.IsBusinessAuthorized.ShouldBeFalse();
                    YocoCutoverDispatch.IsAuthorized.ShouldBeFalse();
                    output = await _account.Register(Input(name));
                }, _configuration).InvokeAsync(context);

            context.Response.StatusCode.ShouldBe(200);
            output.ShouldNotBeNull(); output.CanLogin.ShouldBeFalse(); output.RequiresEmailVerification.ShouldBeTrue();
            await UsingDbContextAsync(1, async database =>
            {
                var user = await database.Users.SingleAsync(x => x.UserName == name);
                user.IsEmailConfirmed.ShouldBeFalse();
                var customer = await database.Customers.SingleAsync(x => x.UserId == user.Id);
                customer.TenantId.ShouldBe(1); customer.AreaId.ShouldNotBeNull(); customer.MembershipId.ShouldBeNull();
                (await database.EntryParticipations.AnyAsync(x => x.CustomerId == customer.Id)).ShouldBeFalse();
                (await database.MemberPayments.AnyAsync(x => x.CustomerId == customer.Id)).ShouldBeFalse();
                (await database.TransactionalEmailOutboxMessages.CountAsync(x => x.Recipient == user.EmailAddress &&
                    x.NotificationType == "EmailVerification")).ShouldBe(1);
            });
            _configuration[RuntimeOperationGuards.FinancialSetting].ShouldBe("false");
            YocoCutoverDispatch.IsAccountBootstrapAuthorized.ShouldBeFalse();
        }

        [Fact]
        public async Task ExistingVerificationAndResendWorkWithoutCreatingMembershipOrFinancialAuthority()
        {
            await Resolve<ISettingManager>().ChangeSettingForTenantAsync(1, "Abp.Account.IsSelfRegistrationEnabled", "true");
            var name = "first_verify_" + Guid.NewGuid().ToString("N");
            using (UsingTenantId(1))
            {
                await new ApplicationRecoveryMiddleware(async _ => await _account.Register(Input(name)), _configuration)
                    .InvokeAsync(Context("POST", "/api/services/app/Account/Register"));
                long userId; string token;
                var manager = Resolve<UserManager>(); await manager.InitializeOptionsAsync(1);
                var user = await manager.FindByNameAsync(name); userId = user.Id;
                token = await manager.GenerateEmailConfirmationTokenAsync(user);
                await new ApplicationRecoveryMiddleware(async _ => await _account.ResendEmailVerification(
                    new RequestAccountEmailInput { AreaName = "Default", EmailAddress = user.EmailAddress }), _configuration)
                    .InvokeAsync(Context("POST", "/api/services/app/Account/ResendEmailVerification"));
                await Should.ThrowAsync<UserFriendlyException>(() => new ApplicationRecoveryMiddleware(async _ =>
                    await _account.ConfirmEmail(new ConfirmEmailInput { TenantId = 1, UserId = userId, Token = "synthetic-invalid-token" }),
                    _configuration).InvokeAsync(Context("POST", "/api/services/app/Account/ConfirmEmail")));
                YocoCutoverDispatch.IsAccountBootstrapAuthorized.ShouldBeFalse();
                await new ApplicationRecoveryMiddleware(async _ =>
                    (await _account.ConfirmEmail(new ConfirmEmailInput { TenantId = 1, UserId = userId, Token = token })).ShouldBeTrue(),
                    _configuration).InvokeAsync(Context("POST", "/api/services/app/Account/ConfirmEmail"));
                await UsingDbContextAsync(1, async database =>
                {
                    (await database.Users.SingleAsync(x => x.Id == userId)).IsEmailConfirmed.ShouldBeTrue();
                    var customer = await database.Customers.SingleAsync(x => x.UserId == userId);
                    customer.MembershipId.ShouldBeNull();
                    (await database.EntryParticipations.AnyAsync(x => x.CustomerId == customer.Id)).ShouldBeFalse();
                    (await database.MemberPayments.AnyAsync(x => x.CustomerId == customer.Id)).ShouldBeFalse();
                });
            }
        }

        [Fact]
        public async Task ExistingTenantRegistrationDisablementStillRejectsAndScopeIsRestored()
        {
            await Resolve<ISettingManager>().ChangeSettingForTenantAsync(1, "Abp.Account.IsSelfRegistrationEnabled", "false");
            await Should.ThrowAsync<UserFriendlyException>(() => new ApplicationRecoveryMiddleware(
                async _ => await _account.Register(Input("disabled_first_account")), _configuration)
                .InvokeAsync(Context("POST", "/api/services/app/Account/Register")));
            YocoCutoverDispatch.IsAccountBootstrapAuthorized.ShouldBeFalse();
        }

        [Fact]
        public async Task BootstrapRejectsOtherTenantRegistrationVerificationAndAvailability()
        {
            var otherId = await UsingDbContextAsync(null, async database =>
            {
                var tenant = new AqualLifeStyle.MultiTenancy.Tenant("OtherBootstrap", "Other bootstrap tenant");
                database.Tenants.Add(tenant); await database.SaveChangesAsync(); return tenant.Id;
            });
            using (UsingTenantId(otherId))
            {
                await Should.ThrowAsync<AbpAuthorizationException>(() => new ApplicationRecoveryMiddleware(
                    async _ => await _account.Register(Input("cross_tenant_first_account")), _configuration)
                    .InvokeAsync(Context("POST", "/api/services/app/Account/Register")));
                await Should.ThrowAsync<AbpAuthorizationException>(() => new ApplicationRecoveryMiddleware(
                    async _ => await _account.ConfirmEmail(new ConfirmEmailInput { TenantId = 1, UserId = 1, Token = "synthetic-invalid-token" }),
                    _configuration).InvokeAsync(Context("POST", "/api/services/app/Account/ConfirmEmail")));
            }
            await Should.ThrowAsync<AbpAuthorizationException>(() => new ApplicationRecoveryMiddleware(
                async _ => await _account.ConfirmEmail(new ConfirmEmailInput { TenantId = otherId, UserId = 1, Token = "synthetic-invalid-token" }),
                _configuration).InvokeAsync(Context("POST", "/api/services/app/Account/ConfirmEmail")));
            await Should.ThrowAsync<AbpAuthorizationException>(() => new ApplicationRecoveryMiddleware(
                async _ => await _account.IsTenantAvailable(new IsTenantAvailableInput { TenancyName = "OtherBootstrap" }),
                _configuration).InvokeAsync(Context("POST", "/api/services/app/Account/IsTenantAvailable")));
        }

        [Fact]
        public async Task DirectAccountCallDoesNotGainTheHttpBootstrapScope()
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => _account.Register(Input("unscoped_first_account")));
            YocoCutoverDispatch.IsAccountBootstrapAuthorized.ShouldBeFalse();
        }

        private static RegisterInput Input(string name) => new RegisterInput
        {
            Name = "Synthetic", Surname = "First Account", UserName = name,
            EmailAddress = name + "@example.test", Password = "Synthetic!101",
            ContactNumber = "+27 72 111 2233", HomeAddress = "Synthetic fixture address"
        };

        private static DefaultHttpContext Context(string method, string path)
        {
            var context = new DefaultHttpContext(); context.Request.Method = method; context.Request.Path = path;
            context.Response.Body = new MemoryStream(); return context;
        }
    }
}
