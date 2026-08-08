using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Abp.Authorization.Users;
using Abp.Configuration;
using Abp.MultiTenancy;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Authentication.JwtBearer;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.Models.TokenAuth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Web.Tests.Controllers
{
    [Collection("Authentication web journeys")]
    public class ProgrammeApprovalSessionJourney_Tests : AqualLifeStyleWebTestBase
    {
        private static readonly DateTime EffectiveFrom =
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        private static readonly EntryProgrammeTerms LegacySplitPaymentTerms =
            EntryProgrammeTerms.Create(
                "entry-2026-07",
                EffectiveFrom,
                registrationPaymentAmount: 600m,
                activationPaymentAmount: 600m,
                monthlyCommitmentAmount: 600m,
                gracePeriodDays: 7);

        private void EnsureAdminPassword(int? tenantId, string password)
        {
            var passwordHasher = new PasswordHasher<User>(
                new OptionsWrapper<PasswordHasherOptions>(new PasswordHasherOptions()));

            UsingDbContext(context =>
            {
                var user = context.Users
                    .Single(u => u.TenantId == tenantId && u.UserName == AbpUserBase.AdminUserName);
                user.Password = passwordHasher.HashPassword(user, password);
                user.CompleteRequiredPasswordReset();
            });
        }

        [Fact]
        public async Task ApprovedParticipation_InvalidatesExistingGuestJwt_AndIssuesMemberAccess()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userName = $"approval_{suffix}";
            var emailAddress = $"{userName}@example.test";
            var password = "Guest!101";

            AbpSession.TenantId = 1;
            await IocManager.Resolve<ISettingManager>()
                .ChangeSettingForTenantAsync(
                    1,
                    "Abp.Account.IsSelfRegistrationEnabled",
                    "true");

            // 1. Real HTTP self-registration creates the Guest account + customer row.
            var registerResponse = await SendJsonAsync(
                HttpMethod.Post,
                "/api/services/app/Account/Register",
                new
                {
                    emailAddress,
                    contactNumber = "+27 82 000 0000",
                    homeAddress = "1 Approval Road, Pretoria",
                    name = "Approval",
                    password,
                    surname = "Journey",
                    userName
                },
                tenantName: AbpTenantBase.DefaultTenantName);
            registerResponse.StatusCode.ShouldBe(HttpStatusCode.OK,
                await registerResponse.Content.ReadAsStringAsync());

            // 2. Confirm the email so a JWT session can be created.
            var userManager = IocManager.Resolve<UserManager>();
            await userManager.InitializeOptionsAsync(1);
            var registeredUser = await userManager.FindByNameAsync(userName);
            registeredUser.ShouldNotBeNull();
            var confirmationToken =
                await userManager.GenerateEmailConfirmationTokenAsync(registeredUser);
            var confirmResponse = await SendJsonAsync(
                HttpMethod.Post,
                "/api/services/app/Account/ConfirmEmail",
                new
                {
                    tenantId = 1,
                    token = confirmationToken,
                    userId = registeredUser.Id
                });
            confirmResponse.StatusCode.ShouldBe(HttpStatusCode.OK,
                await confirmResponse.Content.ReadAsStringAsync());

            // 3. Real TokenAuth endpoint: the Guest JWT must NOT carry the Invite permission and
            //    must contain the initially issued security stamp.
            var guestJwt = await AuthenticateAsync("Default", new AuthenticateModel
            {
                UserNameOrEmailAddress = userName,
                Password = password
            });
            var guestToken = guestJwt.AccessToken;
            var guestClaims = new JwtSecurityTokenHandler()
                .ReadJwtToken(guestToken).Claims.ToList();
            var guestPermissions = guestClaims
                .Single(claim => claim.Type == "permissions").Value.Split(',');
            guestPermissions.ShouldNotContain(AquaPermissions.ProgrammeParticipations.Invite);
            guestPermissions.ShouldContain(AquaPermissions.ProgrammeParticipations.ViewSelf);
            var guestStampClaim = guestClaims.Single(
                claim => claim.Type == JwtSessionSecurityStampValidator.SecurityStampClaimType).Value;

            // 4. Before approval the Guest JWT reaches the authorization layer but is refused (403).
            var preApproval = await Client.GetAsync(
                "/api/services/app/ProgrammeInvitation/GetMyInvitations");
            var preApprovalBody = await preApproval.Content.ReadAsStringAsync();
            preApproval.StatusCode.ShouldBe(HttpStatusCode.Forbidden, preApprovalBody);

            // 5. Seed the confirmed joining payments + awaiting-approval participation for the
            //    registered customer (the same domain states the production webhook creates).
            var participationId = await SeedAwaitingApprovalParticipationAsync(registeredUser.Id);

            // 6. The Area administrator approves the participation through the real HTTP API.
            EnsureAdminPassword(1, User.DefaultPassword);
            var adminAuth = await AuthenticateAsync("Default", new AuthenticateModel
            {
                UserNameOrEmailAddress = "admin",
                Password = "123qwe"
            });
            var approvalResponse = await SendJsonWithTokenAsync(
                HttpMethod.Post,
                "/api/services/app/AdminProgrammeParticipation/ApproveProgrammeParticipation",
                new { programme = 0, participationId },
                adminAuth.AccessToken);
            var approvalBody = await approvalResponse.Content.ReadAsStringAsync();
            approvalResponse.StatusCode.ShouldBe(HttpStatusCode.OK, approvalBody);
            approvalBody.ShouldContain("\"success\":true");

            // 7. The composed state is persisted: active participation, Member promotion,
            //    rotated security stamp.
            await UsingDbContextAsync(async context =>
            {
                var participation = await context.EntryParticipations
                    .SingleAsync(item => item.Id == participationId);
                participation.Status.ShouldBe(EntryParticipationStatus.Active);
                participation.IsQualifiedForNetwork.ShouldBeTrue();

                var promotedUser = await context.Users.SingleAsync(
                    item => item.Id == registeredUser.Id && item.TenantId == 1);
                promotedUser.Role.ShouldBe(AquaUserRole.Member);
                promotedUser.SecurityStamp.ShouldNotBe(guestStampClaim);
            });

            // 8. The pre-approval JWT is now invalid: replayed with no test-side session
            //    context, the old token is rejected at the authentication stage (401),
            //    not merely at the authorization stage.
            Client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", guestToken);
            AbpSession.UserId = null;
            AbpSession.TenantId = null;
            var staleSession = await Client.GetAsync(
                "/api/services/app/ProgrammeInvitation/GetMyInvitations");
            var staleSessionBody = await staleSession.Content.ReadAsStringAsync();
            staleSession.StatusCode.ShouldBe(
                HttpStatusCode.Unauthorized,
                staleSessionBody);

            // 9. A fresh login reflects the Member role: Invite permission present, new
            //    security stamp, invitation API reachable.
            var memberAuth = await AuthenticateAsync("Default", new AuthenticateModel
            {
                UserNameOrEmailAddress = userName,
                Password = password
            });
            AbpSession.TenantId = 1;
            var memberClaims = new JwtSecurityTokenHandler()
                .ReadJwtToken(memberAuth.AccessToken).Claims.ToList();
            var memberPermissions = memberClaims
                .Single(claim => claim.Type == "permissions").Value.Split(',');
            memberPermissions.ShouldContain(
                AquaPermissions.ProgrammeParticipations.Invite);
            var memberStampClaim = memberClaims.Single(
                claim => claim.Type == JwtSessionSecurityStampValidator.SecurityStampClaimType).Value;
            memberStampClaim.ShouldNotBe(guestStampClaim);

            var memberInvitations = await Client.GetAsync(
                "/api/services/app/ProgrammeInvitation/GetMyInvitations");
            var memberInvitationsBody = await memberInvitations.Content.ReadAsStringAsync();
            memberInvitations.StatusCode.ShouldBe(HttpStatusCode.OK, memberInvitationsBody);
            memberInvitationsBody.ShouldContain("\"success\":true");
        }

        [Fact]
        public async Task RejectedParticipation_KeepsGuestRole_AndKeepsExistingJwtWorking()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userName = $"reject_{suffix}";
            var emailAddress = $"{userName}@example.test";
            var password = "Guest!201";

            AbpSession.TenantId = 1;
            await IocManager.Resolve<ISettingManager>()
                .ChangeSettingForTenantAsync(
                    1,
                    "Abp.Account.IsSelfRegistrationEnabled",
                    "true");

            var registerResponse = await SendJsonAsync(
                HttpMethod.Post,
                "/api/services/app/Account/Register",
                new
                {
                    emailAddress,
                    contactNumber = "+27 82 111 1111",
                    homeAddress = "2 Approval Road, Pretoria",
                    name = "Rejected",
                    password,
                    surname = "Journey",
                    userName
                },
                tenantName: AbpTenantBase.DefaultTenantName);
            registerResponse.StatusCode.ShouldBe(HttpStatusCode.OK,
                await registerResponse.Content.ReadAsStringAsync());

            var userManager = IocManager.Resolve<UserManager>();
            await userManager.InitializeOptionsAsync(1);
            var registeredUser = await userManager.FindByNameAsync(userName);
            registeredUser.ShouldNotBeNull();
            var confirmationToken =
                await userManager.GenerateEmailConfirmationTokenAsync(registeredUser);
            var confirmResponse = await SendJsonAsync(
                HttpMethod.Post,
                "/api/services/app/Account/ConfirmEmail",
                new
                {
                    tenantId = 1,
                    token = confirmationToken,
                    userId = registeredUser.Id
                });
            confirmResponse.StatusCode.ShouldBe(HttpStatusCode.OK,
                await confirmResponse.Content.ReadAsStringAsync());

            var guestAuth = await AuthenticateAsync("Default", new AuthenticateModel
            {
                UserNameOrEmailAddress = userName,
                Password = password
            });
            var guestToken = guestAuth.AccessToken;
            var guestStampClaim = new JwtSecurityTokenHandler()
                .ReadJwtToken(guestToken).Claims
                .Single(claim =>
                    claim.Type == JwtSessionSecurityStampValidator.SecurityStampClaimType)
                .Value;

            var participationId = await SeedAwaitingApprovalParticipationAsync(registeredUser.Id);

            EnsureAdminPassword(1, User.DefaultPassword);
            var adminAuth = await AuthenticateAsync("Default", new AuthenticateModel
            {
                UserNameOrEmailAddress = "admin",
                Password = "123qwe"
            });
            var rejectionResponse = await SendJsonWithTokenAsync(
                HttpMethod.Post,
                "/api/services/app/AdminProgrammeParticipation/RejectProgrammeParticipation",
                new { programme = 0, participationId, reason = "Policy requirements not met" },
                adminAuth.AccessToken);
            var rejectionBody = await rejectionResponse.Content.ReadAsStringAsync();
            rejectionResponse.StatusCode.ShouldBe(HttpStatusCode.OK, rejectionBody);
            rejectionBody.ShouldContain("\"success\":true");

            await UsingDbContextAsync(async context =>
            {
                var participation = await context.EntryParticipations
                    .SingleAsync(item => item.Id == participationId);
                participation.Status.ShouldBe(EntryParticipationStatus.Rejected);

                var unchangedUser = await context.Users.SingleAsync(
                    item => item.Id == registeredUser.Id && item.TenantId == 1);
                unchangedUser.Role.ShouldBe(AquaUserRole.Guest);
                unchangedUser.SecurityStamp.ShouldBe(guestStampClaim);
            });

            // Rejection does not rotate the session: the existing Guest JWT still works.
            Client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", guestToken);
            AbpSession.UserId = registeredUser.Id;
            AbpSession.TenantId = 1;
            var stillWorking = await Client.GetAsync(
                "/api/services/app/ClubMemberProgrammeParticipation/GetMyParticipations");
            var stillWorkingBody = await stillWorking.Content.ReadAsStringAsync();
            stillWorking.StatusCode.ShouldBe(HttpStatusCode.OK, stillWorkingBody);
        }

        private async Task<Guid> SeedAwaitingApprovalParticipationAsync(long userId)
        {
            var participationId = Guid.Empty;
            await UsingDbContextAsync(async context =>
            {
                var customer = await context.Customers
                    .SingleAsync(item => item.UserId == userId && item.TenantId == 1);

                var participation = EntryParticipation.StartIndependently(
                    1,
                    customer.Id,
                    LegacySplitPaymentTerms,
                    EffectiveFrom);
                context.EntryParticipations.Add(participation);
                await context.SaveChangesAsync();

                var reference = $"approval-{userId}";
                var registration = MemberPayment.CreatePending(
                    1,
                    customer.Id,
                    MemberPaymentPurpose.EntryRegistration,
                    600m,
                    "Test",
                    $"{reference}-registration",
                    EffectiveFrom);
                registration.Confirm(EffectiveFrom.AddMinutes(1));
                participation.ApplyConfirmedActivationPayment(registration);
                var activation = MemberPayment.CreatePending(
                    1,
                    customer.Id,
                    MemberPaymentPurpose.EntryActivation,
                    600m,
                    "Test",
                    $"{reference}-activation",
                    EffectiveFrom);
                activation.Confirm(EffectiveFrom.AddMinutes(2));
                participation.ApplyConfirmedActivationPayment(activation);
                context.MemberPayments.AddRange(registration, activation);
                await context.SaveChangesAsync();

                participationId = participation.Id;
            });
            return participationId;
        }

        private async Task<HttpResponseMessage> SendJsonWithTokenAsync(
            HttpMethod method,
            string url,
            object body,
            string token)
        {
            using var request = new HttpRequestMessage(method, url)
            {
                Content = JsonContent(body)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return await Client.SendAsync(request);
        }

        private async Task<HttpResponseMessage> SendJsonAsync(
            HttpMethod method,
            string url,
            object body,
            string tenantName = null)
        {
            using var request = new HttpRequestMessage(method, url)
            {
                Content = JsonContent(body)
            };
            if (!string.IsNullOrEmpty(tenantName))
            {
                request.Headers.Add("__tenant", tenantName);
            }
            return await Client.SendAsync(request);
        }

        private static StringContent JsonContent(object value) =>
            new StringContent(
                JsonSerializer.Serialize(value),
                Encoding.UTF8,
                "application/json");
    }
}