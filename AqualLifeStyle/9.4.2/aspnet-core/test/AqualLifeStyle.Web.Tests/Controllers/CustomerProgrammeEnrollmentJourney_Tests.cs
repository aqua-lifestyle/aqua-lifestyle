using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Abp.Configuration;
using Abp.MultiTenancy;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Models.TokenAuth;
using AqualLifeStyle.Authorization.Users;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Web.Tests.Controllers
{
    [Collection("Authentication web journeys")]
    public class CustomerProgrammeEnrollmentJourney_Tests
        : AqualLifeStyleWebTestBase
    {
        [Fact]
        public async Task Customer_CanRegisterLoginAndEnrollInEntry()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userName = $"enrollment_{suffix}";
            var password = "Customer!101";
            AbpSession.TenantId = 1;
            await IocManager.Resolve<ISettingManager>()
                .ChangeSettingForTenantAsync(
                    1,
                    "Abp.Account.IsSelfRegistrationEnabled",
                    "true");

            using (var registrationRequest = new HttpRequestMessage(
                       HttpMethod.Post,
                       "/api/services/app/Account/Register"))
            {
                registrationRequest.Headers.Add(
                    "__tenant",
                    AbpTenantBase.DefaultTenantName);
                registrationRequest.Content = JsonContent(new
                {
                    emailAddress = $"{userName}@example.test",
                    contactNumber = "+27 82 123 4567",
                    homeAddress = "10 Enrollment Road, Johannesburg",
                    name = "Enrollment",
                    password,
                    surname = "Test",
                    userName
                });

                var registrationResponse =
                    await Client.SendAsync(registrationRequest);
                var registrationBody =
                    await registrationResponse.Content.ReadAsStringAsync();
                registrationResponse.StatusCode.ShouldBe(
                    HttpStatusCode.OK,
                    registrationBody);
            }

            var userManager = IocManager.Resolve<UserManager>();
            await userManager.InitializeOptionsAsync(1);
            var registeredUser = await userManager.FindByNameAsync(userName);
            using (var unconfirmedLoginRequest = new HttpRequestMessage(
                       HttpMethod.Post,
                       "/api/TokenAuth/Authenticate"))
            {
                unconfirmedLoginRequest.Headers.Add("__tenant", AbpTenantBase.DefaultTenantName);
                unconfirmedLoginRequest.Content = JsonContent(new
                {
                    password,
                    userNameOrEmailAddress = userName
                });
                var unconfirmedLoginResponse = await Client.SendAsync(unconfirmedLoginRequest);
                var unconfirmedLoginBody = await unconfirmedLoginResponse.Content.ReadAsStringAsync();
                unconfirmedLoginResponse.StatusCode.ShouldNotBe(HttpStatusCode.OK);
                unconfirmedLoginBody.ShouldContain("Email verification required.");
            }
            var confirmationToken = await userManager.GenerateEmailConfirmationTokenAsync(registeredUser);
            using (var confirmationRequest = new HttpRequestMessage(
                       HttpMethod.Post,
                       "/api/services/app/Account/ConfirmEmail"))
            {
                confirmationRequest.Content = JsonContent(new
                {
                    tenantId = 1,
                    token = confirmationToken,
                    userId = registeredUser.Id
                });
                var confirmationResponse = await Client.SendAsync(confirmationRequest);
                confirmationResponse.StatusCode.ShouldBe(HttpStatusCode.OK,
                    await confirmationResponse.Content.ReadAsStringAsync());
            }

            var authentication = await AuthenticateAsync(
                AbpTenantBase.DefaultTenantName,
                new AuthenticateModel
                {
                    Password = password,
                    UserNameOrEmailAddress = userName
                });
            var permissions = new JwtSecurityTokenHandler()
                .ReadJwtToken(authentication.AccessToken)
                .Claims.Single(claim => claim.Type == "permissions")
                .Value.Split(',');
            permissions.ShouldContain(AquaPermissions.Members.ViewSelf);
            permissions.ShouldContain(AquaPermissions.Memberships.ViewSelf);
            permissions.ShouldContain(
                AquaPermissions.ProgrammeParticipations.ViewSelf);
            permissions.ShouldContain(
                AquaPermissions.ProgrammeParticipations.Join);
            Client.DefaultRequestHeaders.Add(
                "__tenant",
                AbpTenantBase.DefaultTenantName);

            var profile = JsonDocument.Parse(await GetResponseAsStringAsync(
                "/api/services/app/MyAccount/GetProfile"));
            var profileResult = profile.RootElement.GetProperty("result");
            profileResult.GetProperty("contactNumber").GetString()
                .ShouldBe("+27 82 123 4567");
            profileResult.GetProperty("homeAddress").GetString()
                .ShouldBe("10 Enrollment Road, Johannesburg");

            var memberships = await GetResponseAsStringAsync(
                "/api/services/app/Membership/GetActiveTiers");
            memberships.ShouldContain("\"success\":true");

            var beforeEnrollment = await GetResponseAsStringAsync(
                "/api/services/app/ClubMemberProgrammeParticipation/GetMyParticipations");
            beforeEnrollment.ShouldContain("\"canJoinEntry\":true");
            beforeEnrollment.ShouldContain("\"canJoinOnyxDirectly\":true");

            using (var enrollmentRequest = new HttpRequestMessage(
                       HttpMethod.Post,
                       "/api/services/app/ClubMemberProgrammeParticipation/StartEntry"))
            {
                enrollmentRequest.Content = JsonContent(new { });
                var enrollmentResponse = await Client.SendAsync(enrollmentRequest);
                enrollmentResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
                var enrollmentBody =
                    await enrollmentResponse.Content.ReadAsStringAsync();
                enrollmentBody.ShouldContain("\"programmeName\":\"AQGreen\"");
                enrollmentBody.ShouldContain(
                    "\"status\":\"Awaiting joining payment\"");
            }

            var afterEnrollment = await GetResponseAsStringAsync(
                "/api/services/app/ClubMemberProgrammeParticipation/GetMyParticipations");
            afterEnrollment.ShouldContain("\"entry\":{");
            afterEnrollment.ShouldContain("\"canJoinEntry\":false");
            AbpSession.UserId = null;
            AbpSession.TenantId = null;
        }

        private static StringContent JsonContent(object value) =>
            new StringContent(
                JsonSerializer.Serialize(value),
                Encoding.UTF8,
                "application/json");
    }
}
