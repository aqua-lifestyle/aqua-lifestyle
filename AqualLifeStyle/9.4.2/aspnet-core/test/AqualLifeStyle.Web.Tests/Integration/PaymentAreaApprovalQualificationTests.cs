using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Abp.Authorization.Roles;
using Abp.Authorization.Users;
using Abp.Domain.Uow;
using Abp.Runtime.Session;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Authorization.Roles;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.Areas;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.Email;
using AqualLifeStyle.Models.TokenAuth;
using AqualLifeStyle.Payments;
using AqualLifeStyle.Payments.Yoco;
using AqualLifeStyle.Web.Host.Controllers;
using AqualLifeStyle.Web.Host.Email;
using Castle.MicroKernel.Registration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Web.Tests.Integration
{
    public sealed class PaymentApprovalQualificationFactAttribute : FactAttribute
    {
        public PaymentApprovalQualificationFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("PAYMENT_APPROVAL_QUALIFICATION") != "true")
                Skip = "Explicit isolated payment/approval browser qualification only.";
        }
    }

    [Collection("Authentication web journeys")]
    public sealed class PaymentAreaApprovalQualificationTests : AqualLifeStyleWebTestBase
    {
        private static readonly string Password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)) + "!Aa1";
        private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        protected override IWebHostBuilder CreateWebHostBuilder()
        {
            // This runs before the existing PostgreSQL test startup can touch a DB.
            var connection = new NpgsqlConnectionStringBuilder(
                Environment.GetEnvironmentVariable("REPRO_PG_CONNECTION") ?? "");
            if (Environment.GetEnvironmentVariable("PAYMENT_APPROVAL_QUALIFICATION") != "true" ||
                Environment.GetEnvironmentVariable("REPRO_PG") != "true" ||
                Environment.GetEnvironmentVariable("REPRO_PG_SCHEMA_FROM_MODEL") != "true" ||
                connection.Host != "127.0.0.1" ||
                !connection.Database.StartsWith("aqgreen_approval_qualification_", StringComparison.Ordinal))
                throw new InvalidOperationException("Qualification requires its disposable loopback PostgreSQL database.");
            return base.CreateWebHostBuilder().UseEnvironment("Development");
        }

        [PaymentApprovalQualificationFact]
        public async Task SignedTestBoundary_ThenExplicitIsolatedConfirmation_OriginalPortalAndAreaDecisions()
        {
            var output = Environment.GetEnvironmentVariable("PAYMENT_APPROVAL_FIXTURE_OUTPUT");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("Restricted fixture output required.");
            var configuration = IocManager.Resolve<IConfiguration>();
            configuration["ASPNETCORE_ENVIRONMENT"] = "Development";
            configuration["App:ClientRootAddress"] = "http://127.0.0.1:3310";
            configuration["Yoco:Mode"] = "test";
            configuration["Bird:DeliveryNotBeforeUtc"] = DateTime.UtcNow.AddSeconds(-1).ToString("O");
            foreach (var setting in new[] { "App:FinancialOperations:Enabled", "App:BackgroundJobs:Enabled",
                "App:WeeklyCommissions:Enabled", "App:EntryMonthlyObligations:Enabled", "App:AQGreenV2:Commissioning:Enabled" })
                configuration[setting] = "false";
            // The browser must use its real JWT claims, never a test's ambient admin identity.
            IocManager.IocContainer.Register(Component.For<IAbpSession>()
                .ImplementedBy<ClaimsAbpSession>().Named("qualification-claims-session").IsDefault().LifestyleSingleton());
            IocManager.Resolve<IAbpSession>().ShouldBeAssignableTo<ClaimsAbpSession>();
            IocManager.Resolve<Abp.Web.Configuration.IAbpWebCommonModuleConfiguration>().SendAllExceptionsToClients = true;
            IocManager.IocContainer.Register(Component.For<IYocoCheckoutGateway>()
                .Instance(new QualificationCheckoutGateway()).IsDefault());
            IocManager.Resolve<IYocoCheckoutGateway>().ShouldBeOfType<QualificationCheckoutGateway>();
            var fixture = await SeedAsync();
            var first = await CreateCheckoutAsync(fixture.Member);
            var second = await CreateCheckoutAsync(fixture.RejectedMember);
            await AssertSignedTestBoundaryAsync(first, configuration);

            // Test-assembly-only authority. No endpoint/flag can expose this to production.
            var firstResult = await ConfirmInIsolationAsync(first, "approve");
            var replay = await ConfirmInIsolationAsync(first, "approve");
            replay.PaymentId.ShouldBe(firstResult.PaymentId);
            replay.WasAlreadyProcessed.ShouldBeTrue();
            var secondResult = await ConfirmInIsolationAsync(second, "reject");
            await UsingDbContextAsync(async db =>
            {
                db.Database.IsNpgsql().ShouldBeTrue();
                var pending = await db.OnyxParticipations.IgnoreQueryFilters().ToListAsync();
                pending.Count.ShouldBe(2);
                pending.ShouldAllBe(p => p.Status == OnyxParticipationStatus.PaymentConfirmedAwaitingApproval && !p.ActivatedAt.HasValue);
                (await db.MemberPayments.IgnoreQueryFilters().CountAsync()).ShouldBe(2);
                (await db.TransactionalEmailOutboxMessages.IgnoreQueryFilters().CountAsync(m =>
                    m.NotificationType == "ProgrammeParticipationAwaitingApproval")).ShouldBe(2);
                var alert = await db.TransactionalEmailOutboxMessages.IgnoreQueryFilters().SingleAsync(m =>
                    m.NotificationType == "ProgrammeParticipationAwaitingApproval" &&
                    m.IdempotencyKey.Contains(firstResult.ParticipationId.ToString()));
                alert.Recipient.ShouldBe(fixture.Administrator.EmailAddress);
                var content = IocManager.Resolve<ITransactionalEmailBodyProtector>().Unprotect(alert.TextBody);
                content.ShouldContain("TEST Approval Member");
                content.ShouldContain(fixture.Area.Name);
                content.ShouldContain("http://127.0.0.1:3310/admin/programme-participations");
            });

            var reference = $"programme-approval:Onyx:{firstResult.ParticipationId}:administrator:{fixture.Administrator.Id}";
            var gateway = new RestrictedQualificationDelivery(reference, output);
            IocManager.IocContainer.Register(Component.For<ITransactionalEmailDeliveryGateway>()
                .Instance(gateway).IsDefault());
            if (gateway.IsConfigured)
            {
                await IocManager.Resolve<TransactionalEmailOutboxProcessor>().ProcessPendingAsync();
                gateway.ProviderMessageId.ShouldNotBeNullOrWhiteSpace("the exact administrator outbox message must be accepted");
                await UsingDbContextAsync(async db =>
                {
                    var sent = await db.TransactionalEmailOutboxMessages.IgnoreQueryFilters().SingleAsync(m => m.IdempotencyKey == reference);
                    sent.Status.ShouldBe(AqualLifeStyle.Domain.Email.TransactionalEmailStatus.Sent);
                    sent.AttemptCount.ShouldBe(1);
                });
            }

            Client.DefaultRequestHeaders.Clear();
            AbpSession.UserId = null; AbpSession.TenantId = null;
            using var bridge = new WebHostBuilder().UseKestrel().UseUrls("http://127.0.0.1:3320")
                .Configure(app => app.Run(async context =>
                {
                    using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), context.Request.Path + context.Request.QueryString);
                    if (context.Request.ContentLength > 0)
                        request.Content = new StreamContent(context.Request.Body);
                    foreach (var header in context.Request.Headers)
                        if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
                            request.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                    using var response = await Client.SendAsync(request);
                    context.Response.StatusCode = (int)response.StatusCode;
                    foreach (var header in response.Headers.Concat(response.Content.Headers))
                        context.Response.Headers[header.Key] = header.Value.ToArray();
                    context.Response.Headers.Remove("transfer-encoding");
                    await response.Content.CopyToAsync(context.Response.Body);
                })).Build();
            await bridge.StartAsync();
            var handoff = new
            {
                testOnly = true, tenantId = 1, tenant = "Default", password = Password,
                areaId = fixture.Area.Id, areaName = fixture.Area.Name,
                administrator = fixture.Administrator.UserName, otherAreaAdministrator = fixture.OtherAdministrator.UserName,
                hostObserver = fixture.HostObserver.UserName,
                member = new { userName = fixture.Member.UserName, name = "TEST Approval Member", participationId = firstResult.ParticipationId,
                    amount = first.Amount, currency = first.Currency },
                rejectedMember = new { userName = fixture.RejectedMember.UserName, name = "TEST Rejection Member", participationId = secondResult.ParticipationId },
                paymentBoundary = "Signed Test Mode event produces no writes; explicit isolated confirmation invokes the existing processor",
                administratorOutboxGenerated = true, birdAttempted = gateway.IsConfigured, birdMessageId = gateway.ProviderMessageId
            };
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(handoff, Json));
            File.SetUnixFileMode(output, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            try
            {
                var completed = output + ".browser-complete";
                var deadline = DateTime.UtcNow.AddMinutes(8);
                while (!File.Exists(completed) && DateTime.UtcNow < deadline) await Task.Delay(500);
                File.Exists(completed).ShouldBeTrue("the real original-portal browser workflow must finish");
                var result = JsonDocument.Parse(await File.ReadAllTextAsync(completed));
                result.RootElement.GetProperty("passed").GetBoolean().ShouldBeTrue();
                await UsingDbContextAsync(async db =>
                {
                    var approved = await db.OnyxParticipations.IgnoreQueryFilters().SingleAsync(p => p.Id == firstResult.ParticipationId);
                    var rejected = await db.OnyxParticipations.IgnoreQueryFilters().SingleAsync(p => p.Id == secondResult.ParticipationId);
                    approved.Status.ShouldBe(OnyxParticipationStatus.Active);
                    approved.ActivatedAt.ShouldNotBeNull();
                    rejected.Status.ShouldBe(OnyxParticipationStatus.Rejected);
                    rejected.ActivatedAt.ShouldBeNull();
                    var decisions = await db.OnyxParticipationApprovalDecisions.IgnoreQueryFilters().ToListAsync();
                    decisions.Count.ShouldBe(2);
                    decisions.Count(d => d.Approved).ShouldBe(1);
                    decisions.Count(d => !d.Approved && d.Reason == "TEST ONLY evidence rejection").ShouldBe(1);
                    decisions.ShouldAllBe(d => d.AdministratorUserId == fixture.Administrator.Id);
                    (await db.MemberPayments.IgnoreQueryFilters().CountAsync()).ShouldBe(2);
                    (await db.EntryMonthlyObligations.IgnoreQueryFilters().CountAsync()).ShouldBe(0);
                    (await db.OnyxWeeklyCommissions.IgnoreQueryFilters().CountAsync()).ShouldBe(0);
                    (await db.AQGreenNetworkPlacements.IgnoreQueryFilters().CountAsync()).ShouldBe(0);
                    (await db.YocoWebhookReceipts.IgnoreQueryFilters().CountAsync()).ShouldBe(0);
                });
            }
            finally { await bridge.StopAsync(); }
        }

        private async Task<Fixture> SeedAsync() => await UsingDbContextAsync(async db =>
        {
            if (!await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == 1))
            {
                var tenant = new AqualLifeStyle.MultiTenancy.Tenant("Default", "TEST Qualification Tenant");
                db.Tenants.Add(tenant); await db.SaveChangesAsync();
                tenant.Id.ShouldBe(1);
            }
            var now = DateTime.UtcNow;
            var area = Area.Create(1, "QUAL-A", "TEST Qualification Area A");
            var other = Area.Create(1, "QUAL-B", "TEST Qualification Area B");
            db.Areas.AddRange(area, other);
            var membership = Membership.Create(1, "TEST Qualification Onyx", "Disposable qualification", MembershipType.Onyx);
            db.Memberships.Add(membership);
            var admin = UserFor("qual-area-admin", 1, AquaUserRole.SystemAdmin,
                Environment.GetEnvironmentVariable("PAYMENT_APPROVAL_EMAIL_RECIPIENT") ?? "area-admin@example.test");
            var otherAdmin = UserFor("qual-other-admin", 1, AquaUserRole.SystemAdmin);
            var host = UserFor("qual-host-observer", null, AquaUserRole.SystemAdmin);
            var member = UserFor("qual-member", 1, AquaUserRole.Guest);
            var rejected = UserFor("qual-rejection-member", 1, AquaUserRole.Guest);
            db.Users.AddRange(admin, otherAdmin, host, member, rejected);
            await db.SaveChangesAsync();
            var memberRole = new Role(1, AquaUserRole.Member.ToString(), "Member");
            db.Roles.Add(memberRole);
            await db.SaveChangesAsync();
            db.Permissions.AddRange(AquaRolePermissions.GetFor(AquaUserRole.Member).Select(p => new RolePermissionSetting
            { TenantId = 1, RoleId = memberRole.Id, Name = p, IsGranted = true }));
            foreach (var user in new[] { admin, otherAdmin, host, member, rejected })
            {
                var roleName = user.Role.ToString();
                var role = await db.Roles.IgnoreQueryFilters().SingleOrDefaultAsync(r =>
                    r.TenantId == user.TenantId && r.Name == roleName);
                if (role == null)
                {
                    role = new Role(user.TenantId, roleName, roleName);
                    db.Roles.Add(role); await db.SaveChangesAsync();
                    var permissions = user == member || user == rejected
                        ? new[] { AquaPermissions.ProgrammeParticipations.Default, AquaPermissions.ProgrammeParticipations.ViewSelf, AquaPermissions.ProgrammeParticipations.Join }
                        : new[] { AquaPermissions.Admin.Default, AquaPermissions.Admin.ProgrammeParticipations.Default,
                            AquaPermissions.Admin.ProgrammeParticipations.View, user == host ? AquaPermissions.Admin.AllTenants : AquaPermissions.Admin.ProgrammeParticipations.Approve };
                    db.Permissions.AddRange(permissions.Select(p => new RolePermissionSetting
                    { TenantId = user.TenantId, RoleId = role.Id, Name = p, IsGranted = true }));
                }
                db.UserRoles.Add(new UserRole(user.TenantId, user.Id, role.Id));
                if (user.TenantId.HasValue)
                {
                    var customer = Customer.Create(1, user.Id,
                        user == member ? "TEST Approval Member" : user == rejected ? "TEST Rejection Member" : "TEST Area Administrator",
                        new EmailAddress(user.EmailAddress), user: user);
                    customer.AssignInitialArea(user == otherAdmin ? other : area, now, "Disposable qualification identity");
                    db.Customers.Add(customer);
                }
            }
            db.AreaAdminAssignments.AddRange(AreaAdminAssignment.Assign(area, admin.Id, 1, now), AreaAdminAssignment.Assign(other, otherAdmin.Id, 1, now));
            await db.SaveChangesAsync();
            return new Fixture(area, admin, otherAdmin, host, member, rejected);
        });

        private static User UserFor(string name, int? tenant, AquaUserRole role, string email = null)
        {
            var user = new User { TenantId = tenant, UserName = name, Name = "TEST", Surname = name,
                EmailAddress = email ?? name + "@example.test", IsActive = true, IsEmailConfirmed = true };
            user.SetRole(role); user.SetNormalizedNames();
            user.Password = new PasswordHasher<User>(Options.Create(new PasswordHasherOptions())).HashPassword(user, Password);
            return user;
        }

        private async Task<DirectOnyxCheckoutIntent> CreateCheckoutAsync(User user)
        {
            AbpSession.TenantId = 1;
            await AuthenticateAsync("Default", new AuthenticateModel { UserNameOrEmailAddress = user.UserName, Password = Password });
            using var response = await Client.PostAsync("/api/services/app/ClubMemberProgrammeParticipation/CreateDirectOnyxCheckout",
                new StringContent("{}", Encoding.UTF8, "application/json"));
            response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            Client.DefaultRequestHeaders.Clear(); AbpSession.UserId = null; AbpSession.TenantId = null;
            return await UsingDbContextAsync(async db => await db.DirectOnyxCheckoutIntents.IgnoreQueryFilters()
                .SingleAsync(c => c.CustomerId == db.Customers.IgnoreQueryFilters().Single(x => x.UserId == user.Id).Id));
        }

        private async Task AssertSignedTestBoundaryAsync(DirectOnyxCheckoutIntent checkout, IConfiguration configuration)
        {
            var secret = RandomNumberGenerator.GetBytes(32);
            configuration["Yoco:WebhookSecret"] = "whsec_" + Convert.ToBase64String(secret);
            var payload = JsonSerializer.SerializeToUtf8Bytes(new { id = "evt_isolated_qualification", type = "payment.succeeded",
                payload = new { id = "pmt_isolated_qualification", type = "payment", status = "succeeded", mode = "test",
                    amount = (int)(checkout.Amount * 100), currency = checkout.Currency, createdDate = DateTimeOffset.UtcNow,
                    metadata = new Dictionary<string, string> { [YocoCheckoutMetadata.ProviderCheckoutId] = checkout.ProviderCheckoutId,
                        [YocoCheckoutMetadata.Purpose] = YocoCheckoutMetadata.DirectOnyxPurpose } } }, Json);
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            const string id = "evt_isolated_qualification";
            var signed = Encoding.UTF8.GetBytes(id + "." + timestamp + ".").Concat(payload).ToArray();
            var http = new DefaultHttpContext(); http.Request.Body = new MemoryStream(payload);
            http.Request.Headers["webhook-id"] = id; http.Request.Headers["webhook-timestamp"] = timestamp;
            http.Request.Headers["webhook-signature"] = "v1," + Convert.ToBase64String(HMACSHA256.HashData(secret, signed));
            var controller = new YocoPaymentsController(new YocoWebhookSignatureVerifier(configuration),
                IocManager.Resolve<YocoPaymentNotificationProcessor>(), Options.Create(new Microsoft.AspNetCore.Mvc.JsonOptions
                { JsonSerializerOptions = { PropertyNameCaseInsensitive = true } }), NullLogger<YocoPaymentsController>.Instance)
            { ControllerContext = new ControllerContext { HttpContext = http } };
            (await controller.WebhookAsync()).ShouldBeOfType<OkResult>();
            http.Response.Headers["X-Aqua-Test-Payment"].ToString().ShouldBe("true");
            http.Response.Headers["X-Aqua-Business-Payment-Applied"].ToString().ShouldBe("false");
            await UsingDbContextAsync(async db =>
            {
                (await db.MemberPayments.IgnoreQueryFilters().CountAsync()).ShouldBe(0);
                (await db.OnyxParticipations.IgnoreQueryFilters().CountAsync()).ShouldBe(0);
                (await db.YocoWebhookReceipts.IgnoreQueryFilters().CountAsync()).ShouldBe(0);
            });
        }

        private async Task<ProgrammePaymentConfirmationResult> ConfirmInIsolationAsync(DirectOnyxCheckoutIntent checkout, string reference)
        {
            using var uow = IocManager.Resolve<IUnitOfWorkManager>().Begin(new UnitOfWorkOptions { IsTransactional = true });
            var result = await IocManager.Resolve<ProgrammePaymentConfirmationProcessor>().ProcessDirectOnyxCheckoutAsync(
                checkout.Id, "ISOLATED-QUALIFICATION", "TEST-" + reference + "-" + checkout.Id.ToString("N"),
                checkout.ProviderCheckoutId, checkout.Amount, checkout.Currency, DateTime.UtcNow);
            await uow.CompleteAsync(); return result;
        }

        private sealed record Fixture(Area Area, User Administrator, User OtherAdministrator, User HostObserver, User Member, User RejectedMember);

        private sealed class QualificationCheckoutGateway : IYocoCheckoutGateway
        {
            public Task<YocoCheckout> CreateAsync(CreateYocoCheckout checkout) => Task.FromResult(new YocoCheckout
            { Id = "checkout_" + checkout.ReferenceId.ToString("N"), RedirectUrl = "https://payments.example.test/checkout/" + checkout.ReferenceId.ToString("N") });
        }

        private sealed class RestrictedQualificationDelivery : ITransactionalEmailDeliveryGateway
        {
            private readonly string _reference;
            private readonly string _output;
            private readonly string _recipient = Environment.GetEnvironmentVariable("PAYMENT_APPROVAL_EMAIL_RECIPIENT");
            private readonly BirdTransactionalEmailDeliveryGateway _bird;
            public string ProviderMessageId { get; private set; }
            public bool IsConfigured => _bird != null;
            public RestrictedQualificationDelivery(string reference, string output)
            {
                _reference = reference;
                _output = output;
                if (Environment.GetEnvironmentVariable("PAYMENT_APPROVAL_BIRD_SEND") == "true")
                {
                    if (string.IsNullOrWhiteSpace(_recipient)) throw new InvalidOperationException("Approved recipient required.");
                    _bird = new BirdTransactionalEmailDeliveryGateway(new HttpClient { Timeout = TimeSpan.FromSeconds(20) },
                        Options.Create(new BirdOptions { Enabled = true,
                            ApiKey = Environment.GetEnvironmentVariable("PAYMENT_APPROVAL_BIRD_KEY"),
                            FromEmail = Environment.GetEnvironmentVariable("PAYMENT_APPROVAL_BIRD_FROM"),
                            FromName = Environment.GetEnvironmentVariable("PAYMENT_APPROVAL_BIRD_NAME"),
                            ReplyToEmail = Environment.GetEnvironmentVariable("PAYMENT_APPROVAL_BIRD_REPLY") }));
                }
            }
            public async Task<string> SendAsync(TransactionalEmail email, CancellationToken cancellationToken = default)
            {
                if (_bird == null || email.Reference != _reference || email.Recipient != _recipient || ProviderMessageId != null)
                    throw new InvalidOperationException("Qualification denies every other recipient/reference or repeated send.");
                ProviderMessageId = await _bird.SendAsync(email, cancellationToken);
                await File.WriteAllTextAsync(_output + ".bird-acceptance.json", JsonSerializer.Serialize(new
                { providerMessageId = ProviderMessageId, acceptedAtUtc = DateTime.UtcNow }, Json), cancellationToken);
                return ProviderMessageId;
            }
        }
    }
}
