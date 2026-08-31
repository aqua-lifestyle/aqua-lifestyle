using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Auditing;
using Abp.Authorization;
using Abp.UI;
using AqualLifeStyle.Application.Admin.Commissions;
using AqualLifeStyle.Application.Admin.Commissions.Dto;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.AQGreen;
using Castle.MicroKernel.Registration;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public sealed class AdminAQGreenWeeklySalesEligibilitySafetyTests
        : AqualLifeStyleTestBase
    {
        private static readonly DateTime CanonicalWeekStartUtc =
            new(2026, 8, 20, 22, 0, 0, DateTimeKind.Utc);

        [Fact]
        public async Task ProductionGate_Denial_AuditsActionsWithoutEvidenceParameters()
        {
            LoginAsHostAdmin();
            var service = Resolve<IAdminAQGreenWeeklySalesEligibilityAppService>();
            var confirmParticipantId = Guid.NewGuid();
            var rejectParticipantId = Guid.NewGuid();
            const string confirmEvidence = "audit-secret-confirm-disabled";
            const string rejectEvidence = "audit-secret-reject-disabled";

            var confirmException = await Should.ThrowAsync<UserFriendlyException>(() =>
                service.ConfirmAsync(new ConfirmAQGreenWeeklySalesEligibilityInput
                {
                    TenantId = 1,
                    ParticipantId = confirmParticipantId,
                    CommissionWeekStartUtc = CanonicalWeekStartUtc,
                    SprayQuantity = 5,
                    OneLitreQuantity = 5,
                    FiveLitreQuantity = 5,
                    EvidenceReferences = new List<string> { confirmEvidence }
                }));
            var rejectException = await Should.ThrowAsync<UserFriendlyException>(() =>
                service.RejectAsync(new RejectAQGreenWeeklySalesEligibilityInput
                {
                    TenantId = 1,
                    ParticipantId = rejectParticipantId,
                    CommissionWeekStartUtc = CanonicalWeekStartUtc,
                    RejectionReason = "evidence could not be verified",
                    EvidenceReferences = new List<string> { rejectEvidence }
                }));

            confirmException.Message.ShouldContain("disabled");
            rejectException.Message.ShouldContain("disabled");
            UsingDbContext(null, context =>
            {
                context.AQGreenWeeklySalesEligibilityDecisions.Count().ShouldBe(0);
                context.AQGreenWeeklySalesEvidenceReferences.Count().ShouldBe(0);

                var audits = context.Set<AuditLog>()
                    .Where(log => log.MethodName == nameof(
                                      IAdminAQGreenWeeklySalesEligibilityAppService.ConfirmAsync) ||
                                  log.MethodName == nameof(
                                      IAdminAQGreenWeeklySalesEligibilityAppService.RejectAsync))
                    .ToList();
                var confirmAudit = audits.Single(log => log.MethodName == nameof(
                    IAdminAQGreenWeeklySalesEligibilityAppService.ConfirmAsync));
                var rejectAudit = audits.Single(log => log.MethodName == nameof(
                    IAdminAQGreenWeeklySalesEligibilityAppService.RejectAsync));

                foreach (var audit in audits)
                {
                    audit.UserId.ShouldBe(AbpSession.UserId);
                    audit.TenantId.ShouldBeNull();
                    audit.ExecutionTime.ShouldNotBe(default);
                    audit.ExceptionMessage.ShouldContain("disabled");
                    audit.Parameters.Length.ShouldBeLessThan(1024);
                    audit.Parameters.ShouldNotContain(confirmEvidence);
                    audit.Parameters.ShouldNotContain(rejectEvidence);
                }
                confirmAudit.Parameters.ShouldContain(confirmParticipantId.ToString());
                rejectAudit.Parameters.ShouldContain(rejectParticipantId.ToString());
            });
        }

        [Fact]
        public async Task TenantAdministrator_CannotReachReviewWritePath()
        {
            LoginAsDefaultTenantAdmin();
            var service = Resolve<IAdminAQGreenWeeklySalesEligibilityAppService>();

            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                service.BeginReviewAsync(new BeginAQGreenWeeklySalesReviewInput
                {
                    TenantId = 1,
                    ParticipantId = Guid.NewGuid(),
                    CommissionWeekStartUtc = CanonicalWeekStartUtc
                }));
        }

        [Fact]
        public async Task HostWithoutReviewPermission_IsDeniedWithoutMutation()
        {
            LoginAsHostAdmin();
            ReplacePermissionChecker(reviewGranted: false, allTenantsGranted: true);
            var service = Resolve<IAdminAQGreenWeeklySalesEligibilityAppService>();

            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                service.ConfirmAsync(ConfirmInput("ticket:no-review-permission")));

            AssertNoDecisionOrEvidence();
        }

        [Fact]
        public async Task HostWithoutAllTenants_IsDeniedWithoutMutation()
        {
            LoginAsHostAdmin();
            ReplacePermissionChecker(reviewGranted: true, allTenantsGranted: false);
            var service = Resolve<IAdminAQGreenWeeklySalesEligibilityAppService>();

            var exception = await Should.ThrowAsync<AbpAuthorizationException>(() =>
                service.ConfirmAsync(ConfirmInput("ticket:no-all-tenants")));

            exception.Message.ShouldContain("manage all Areas");
            AssertNoDecisionOrEvidence();
        }

        [Fact]
        public async Task HostDeniedByReviewScope_IsDeniedWithoutMutation()
        {
            LoginAsHostAdmin();
            ReplacePermissionChecker(reviewGranted: true, allTenantsGranted: true);
            var scopePolicy = Substitute.For<IAQGreenWeeklySalesReviewScopePolicy>();
            scopePolicy.CanReviewAsync(Arg.Any<int>()).Returns(Task.FromResult(false));
            LocalIocManager.IocContainer.Register(
                Component.For<IAQGreenWeeklySalesReviewScopePolicy>()
                    .Instance(scopePolicy)
                    .Named($"b53-denying-scope-{Guid.NewGuid():N}")
                    .IsDefault());
            var service = Resolve<IAdminAQGreenWeeklySalesEligibilityAppService>();

            var exception = await Should.ThrowAsync<AbpAuthorizationException>(() =>
                service.ConfirmAsync(ConfirmInput("ticket:scope-denied")));

            exception.Message.ShouldContain("outside the authorized scope");
            AssertNoDecisionOrEvidence();
        }

        [Fact]
        public async Task DisabledGate_IsUnconditionallyFalse()
        {
            var gate = new DisabledAQGreenWeeklySalesReviewGate();

            (await gate.IsEnabledAsync(1)).ShouldBeFalse();
            (await gate.IsEnabledAsync(2)).ShouldBeFalse();
        }

        [Fact]
        public async Task TestGate_EnablesOnlyTheExplicitControlledTenant()
        {
            var gate = new AQGreenWeeklySalesReviewTestGate();

            (await gate.IsEnabledAsync(1)).ShouldBeTrue();
            (await gate.IsEnabledAsync(2)).ShouldBeFalse();
        }

        [Fact]
        public void ReviewerInputs_DoNotExposeDerivedVersionResultOrAuditFacts()
        {
            var forbiddenProperties = new[]
            {
                "SalesEligibilityRulesVersion",
                "RulesVersion",
                "ThresholdResult",
                "ReviewedAt",
                "ReviewedByUserId",
                "CreatorUserId"
            };

            foreach (var inputType in new[]
                     {
                         typeof(BeginAQGreenWeeklySalesReviewInput),
                         typeof(ConfirmAQGreenWeeklySalesEligibilityInput),
                         typeof(RejectAQGreenWeeklySalesEligibilityInput)
                     })
            foreach (var propertyName in forbiddenProperties)
                inputType.GetProperty(propertyName).ShouldBeNull();
        }

        private static ConfirmAQGreenWeeklySalesEligibilityInput ConfirmInput(
            string evidenceReference) => new()
        {
            TenantId = 1,
            ParticipantId = Guid.NewGuid(),
            CommissionWeekStartUtc = CanonicalWeekStartUtc,
            SprayQuantity = 5,
            OneLitreQuantity = 5,
            FiveLitreQuantity = 5,
            EvidenceReferences = new List<string> { evidenceReference }
        };

        private void ReplacePermissionChecker(
            bool reviewGranted,
            bool allTenantsGranted)
        {
            var checker = Substitute.For<IPermissionChecker>();
            checker.IsGrantedAsync(Arg.Any<string>()).Returns(call =>
            {
                var permission = call.Arg<string>();
                return Task.FromResult(permission ==
                        AquaPermissions.Admin.Commissions
                            .ReviewAQGreenWeeklySalesEligibility
                    ? reviewGranted
                    : permission == AquaPermissions.Admin.AllTenants &&
                      allTenantsGranted);
            });
            LocalIocManager.IocContainer.Register(
                Component.For<IPermissionChecker>()
                    .Instance(checker)
                    .Named($"b53-permission-checker-{Guid.NewGuid():N}")
                    .IsDefault());
        }

        private void AssertNoDecisionOrEvidence()
        {
            UsingDbContext(null, context =>
            {
                context.AQGreenWeeklySalesEligibilityDecisions.Count().ShouldBe(0);
                context.AQGreenWeeklySalesEvidenceReferences.Count().ShouldBe(0);
            });
        }
    }
}
