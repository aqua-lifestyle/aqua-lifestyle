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
                enrollmentBody.ShouldContain("\"programmeName\":\"Entry\"");
                enrollmentBody.ShouldContain(
                    "\"status\":\"Awaiting registration payment\"");
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
