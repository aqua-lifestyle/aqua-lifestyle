using System;
using System.Collections.Generic;
using Abp.Dependency;
using AqualLifeStyle.Domain.AQGreen;
using Castle.DynamicProxy;
using AqualLifeStyle.Configuration;
using Microsoft.Extensions.Configuration;
using Abp.Authorization;

namespace AqualLifeStyle.Application.ProgrammeParticipations
{
    // Runtime recovery/pilot commissioning boundary in addition to ABP permissions.
    // All other application-service capabilities remain unreachable in the pilot,
    // including direct IoC-resolved service calls and newly added endpoints.
    public sealed class PilotApplicationBoundaryInterceptor : IInterceptor, ITransientDependency
    {
        private readonly IAQGreenPilotMode _mode;
        private readonly IConfiguration _configuration;
        private static readonly HashSet<string> RecoveryAllowed = new(StringComparer.Ordinal)
        {
            "SessionAppService.GetCurrentLoginInformations",
            "AdminProgrammeParticipationAppService.GetLegacyAQGreenReconciliationAsync",
            "AdminProgrammeParticipationAppService.ExportLegacyAQGreenReconciliationEvidenceAsync",
            "AdminProgrammeParticipationAppService.GetAssignedAreasAsync"
        };
        private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
        {
            "SessionAppService.GetCurrentLoginInformations",
            "AccountAppService.IsTenantAvailable",
            "AccountAppService.GetTenantSelfRegistrationAvailability",
            "AccountAppService.Register",
            "AccountAppService.ConfirmEmail",
            "MyAccountAppService.GetProfileAsync",
            "MyAccountAppService.UpdateProfileAsync",
            "MyAccountAppService.ChangePasswordAsync",
            "CustomerAppService.GetMyCustomerAsync",
            "MembershipAppService.GetAllAsync",
            "ClubMemberProgrammeParticipationAppService.GetMyParticipationsAsync",
            "ClubMemberProgrammeParticipationAppService.StartEntryAsync",
            "ClubMemberProgrammeParticipationAppService.CreateAQGreenJoiningCheckoutAsync",
            "ClubMemberProgrammeProgressAppService.GetMyProgressAsync",
            "ClubMemberProgrammeProgressAppService.GetMyJourneyAsync",
            "ProgrammeInvitationAppService.GetMyInvitationsAsync",
            "ProgrammeInvitationAppService.GetPreviewAsync",
            "AQGreenPilotAppService.GetStatusAsync",
            "AQGreenPilotAppService.GetProspectiveCustomersAsync",
            "AQGreenPilotAppService.AuthorizeProspectiveRootAsync",
            "AQGreenPilotAppService.ConfirmTestPaymentAsync",
            "AdminProgrammeParticipationAppService.GetAllAsync",
            "AdminProgrammeParticipationAppService.GetPendingApprovalSummaryAsync",
            "AdminProgrammeParticipationAppService.GetAssignedAreasAsync",
            "AdminProgrammeParticipationAppService.ApproveProgrammeParticipationAsync",
            "AdminProgrammeParticipationAppService.RejectProgrammeParticipationAsync",
            "AdminAQGreenWeeklySalesEligibilityAppService.GetAllAsync",
            "AdminAQGreenWeeklySalesEligibilityAppService.GetAsync",
            "AdminAQGreenWeeklySalesEligibilityAppService.GetLatestClosedWeekAsync",
            "AdminAQGreenWeeklySalesEligibilityAppService.BeginReviewAsync",
            "AdminAQGreenWeeklySalesEligibilityAppService.ConfirmAsync",
            "AdminAQGreenWeeklySalesEligibilityAppService.RejectAsync",
            "AdminCommissionAppService.GetAllAsync",
            "AdminCommissionAppService.GetPeriodInventoryAsync",
            "AdminCommissionAppService.CalculateLatestClosedWeekAsync",
            "AdminCommissionBootstrapAppService.BootstrapInitialCommissionTermsAsync",
            "AdminTenantAppService.ObserveActivationStateAsync"
        };

        public PilotApplicationBoundaryInterceptor(IAQGreenPilotMode mode, IConfiguration configuration)
        { _mode = mode; _configuration = configuration; }
        public void Intercept(IInvocation invocation)
        {
            var type = invocation.TargetType;
            while (type != null && type.Namespace?.StartsWith("Castle.Proxies", StringComparison.Ordinal) == true)
                type = type.BaseType;
            if (new YocoCutoverOptions(_configuration).Enabled && invocation.Method.IsPublic && !invocation.Method.IsSpecialName &&
                !YocoCutoverDispatch.IsBusinessAuthorized && !YocoCutoverDispatch.IsAuthorized &&
                !(YocoCutoverDispatch.IsReadOnlyAuthorized && YocoCutoverMemberReads.AllowsOperation(type?.Name + "." + invocation.Method.Name)) &&
                type?.Name + "." + invocation.Method.Name != "SessionAppService.GetCurrentLoginInformations")
                throw new AbpAuthorizationException("Application business operations are fenced during payment cutover.");
            if (ApplicationRecoveryMode.IsEnabled(_configuration) && invocation.Method.IsPublic &&
                !invocation.Method.IsSpecialName &&
                !RecoveryAllowed.Contains(type?.Name + "." + invocation.Method.Name))
                throw new AbpAuthorizationException("Application business operations are suspended during API recovery.");
            if (_mode.IsEnabled && invocation.Method.IsPublic && !invocation.Method.IsSpecialName &&
                !Allowed.Contains(type?.Name + "." + invocation.Method.Name))
                _mode.DenyOutsidePilot("Uncommissioned application operation");
            invocation.Proceed();
        }
    }
}
