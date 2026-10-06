using System;
using System.Collections.Generic;
using Abp.Dependency;
using AqualLifeStyle.Domain.AQGreen;
using Castle.DynamicProxy;

namespace AqualLifeStyle.Application.ProgrammeParticipations
{
    // Runtime commissioning boundary in addition to (never instead of) ABP permissions.
    // All other application-service capabilities remain unreachable in the pilot,
    // including direct IoC-resolved service calls and newly added endpoints.
    public sealed class PilotApplicationBoundaryInterceptor : IInterceptor, ITransientDependency
    {
        private readonly IAQGreenPilotMode _mode;
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

        public PilotApplicationBoundaryInterceptor(IAQGreenPilotMode mode) => _mode = mode;
        public void Intercept(IInvocation invocation)
        {
            var type = invocation.TargetType;
            while (type != null && type.Namespace?.StartsWith("Castle.Proxies", StringComparison.Ordinal) == true)
                type = type.BaseType;
            if (_mode.IsEnabled && invocation.Method.IsPublic && !invocation.Method.IsSpecialName &&
                !Allowed.Contains(type?.Name + "." + invocation.Method.Name))
                _mode.DenyOutsidePilot("Uncommissioned application operation");
            invocation.Proceed();
        }
    }
}
