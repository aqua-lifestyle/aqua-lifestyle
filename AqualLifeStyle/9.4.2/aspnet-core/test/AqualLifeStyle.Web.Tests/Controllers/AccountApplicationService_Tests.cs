using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Abp.Authorization.Users;
using Abp.Configuration;
using AqualLifeStyle.Application.Recruitment;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.Domain.Recruitment;
using AqualLifeStyle.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Web.Tests.Controllers
{
    public class AccountApplicationService_Tests : AqualLifeStyleWebTestBase
    {
        [Fact]
        public async Task TenantSelfRegistrationAvailability_ReturnsTheLiveAreaSettingWithoutAuthentication()
        {
            var settingManager = IocManager.Resolve<ISettingManager>();
            await settingManager.ChangeSettingForTenantAsync(1, "Abp.Account.IsSelfRegistrationEnabled", "true");

            try
            {
                var response = await Client.GetAsync(
                    "/api/services/app/Account/GetTenantSelfRegistrationAvailability?tenancyName=Default");

                response.StatusCode.ShouldBe(HttpStatusCode.OK);
                (await response.Content.ReadAsStringAsync())
                    .ShouldContain("\"isSelfRegistrationEnabled\":true");
            }
            finally
            {
                await settingManager.ChangeSettingForTenantAsync(
                    1,
                    "Abp.Account.IsSelfRegistrationEnabled",
                    "false");
            }
        }

        [Fact]
        public async Task InvitationRegistration_RejectsConflictingAbpTenantIdHeaderWithoutMutation()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var fixture = await UsingDbContextAsync(async context =>
            {
                var user = await context.Users.SingleAsync(item =>
                    item.TenantId == 1 && item.UserName == AbpUserBase.AdminUserName);
                var area = await context.Areas.FirstAsync(item =>
                    item.TenantId == 1 && item.IsActive);
                var customer = Customer.Create(
                    1,
                    user.Id,
                    $"Tenant boundary recruiter {suffix}",
                    new EmailAddress($"tenant-boundary-{suffix}@example.test"),
                    membershipId: null,
                    user: user);
                customer.AssignInitialArea(area, DateTime.UtcNow, "Tenant boundary test");
                context.Customers.Add(customer);
                await context.SaveChangesAsync();

                var effectiveFrom = DateTime.UtcNow.AddDays(-1);
                var terms = EntryProgrammeTerms.Create(
                    $"tenant-boundary-{suffix}",
                    effectiveFrom,
                    registrationPaymentAmount: 600m,
                    activationPaymentAmount: 600m,
                    monthlyCommitmentAmount: 600m,
                    gracePeriodDays: 7);
                var participation = EntryParticipation.StartIndependently(
                    1,
                    customer.Id,
                    terms,
                    effectiveFrom);
                var registrationPayment = MemberPayment.CreatePending(
                    1,
                    customer.Id,
                    MemberPaymentPurpose.EntryRegistration,
                    600m,
                    "Test",
                    $"tenant-boundary-registration-{suffix}",
                    effectiveFrom);
                registrationPayment.Confirm(effectiveFrom.AddMinutes(1));
                participation.ApplyConfirmedActivationPayment(registrationPayment);
                var activationPayment = MemberPayment.CreatePending(
                    1,
                    customer.Id,
                    MemberPaymentPurpose.EntryActivation,
                    600m,
                    "Test",
                    $"tenant-boundary-activation-{suffix}",
                    effectiveFrom);
                activationPayment.Confirm(effectiveFrom.AddMinutes(2));
                participation.ApplyConfirmedActivationPayment(activationPayment);
                participation.ApproveByAdministrator(user.Id, effectiveFrom.AddMinutes(3));
                var invitation = ProgrammeInvitation.Create(
                    1,
                    RecruitmentProgrammeKeys.AQGreen,
                    participation.Id);
                var otherTenant = new Tenant(
                    $"Other{suffix}"[..15],
                    $"Other tenant {suffix}");
                context.EntryParticipations.Add(participation);
                context.MemberPayments.AddRange(registrationPayment, activationPayment);
                context.ProgrammeInvitations.Add(invitation);
                context.Tenants.Add(otherTenant);
                await context.SaveChangesAsync();

                return new
                {
                    InvitationCode = invitation.Code,
                    OtherTenantId = otherTenant.Id,
                    AreaAssignments = await context.CustomerAreaAssignments.CountAsync(),
                    Customers = await context.Customers.CountAsync(),
                    EntryParticipations = await context.EntryParticipations.CountAsync(),
                    Invitations = await context.ProgrammeInvitations.CountAsync(),
                    OnyxParticipations = await context.OnyxParticipations.CountAsync(),
                    Users = await context.Users.CountAsync()
                };
            });
            var candidateUserName = $"wrong_tenant_{suffix}";
            AbpSession.TenantId = null;
            AbpSession.UserId = null;
            Client.DefaultRequestHeaders.Authorization = null;

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "/api/services/app/Account/Register");
            request.Headers.Add("Abp.TenantId", fixture.OtherTenantId.ToString());
            request.Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    emailAddress = $"{candidateUserName}@example.test",
                    contactNumber = "+27 82 123 4567",
                    homeAddress = "10 Tenant Boundary Road, Johannesburg",
                    inviteCode = fixture.InvitationCode,
                    name = "Wrong",
                    password = "Customer!101",
                    surname = "Tenant",
                    userName = candidateUserName
                }),
                Encoding.UTF8,
                "application/json");

            var response = await Client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();
            response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError, responseBody);
            responseBody.ShouldContain("The invitation could not be accepted.");
            responseBody.ShouldContain("different organisation");

            await UsingDbContextAsync(async context =>
            {
                (await context.Users.AnyAsync(item =>
                    item.UserName == candidateUserName)).ShouldBeFalse();
                (await context.Users.CountAsync()).ShouldBe(fixture.Users);
                (await context.Customers.CountAsync()).ShouldBe(fixture.Customers);
                (await context.CustomerAreaAssignments.CountAsync())
                    .ShouldBe(fixture.AreaAssignments);
                (await context.EntryParticipations.CountAsync())
                    .ShouldBe(fixture.EntryParticipations);
                (await context.OnyxParticipations.CountAsync())
                    .ShouldBe(fixture.OnyxParticipations);
                (await context.ProgrammeInvitations.CountAsync())
                    .ShouldBe(fixture.Invitations);
            });
        }
    }
}
