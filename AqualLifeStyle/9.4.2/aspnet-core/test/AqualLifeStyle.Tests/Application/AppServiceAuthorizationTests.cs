using System;
using System.Linq;
using System.Reflection;
using Abp.Authorization;
using AqualLifeStyle.Application.Admin.Import;
using AqualLifeStyle.Application.Admin.Customers;
using AqualLifeStyle.Application.Admin.Users;
using AqualLifeStyle.Application.Admin.AreaLeaders;
using AqualLifeStyle.Application.Admin.Facilitators;
using AqualLifeStyle.Application.Admin.Members;
using AqualLifeStyle.Application.Admin.Tenants;
using AqualLifeStyle.Application.Admin.Commissions;
using AqualLifeStyle.Application.Admin.Savings;
using AqualLifeStyle.Application.Admin.Loans;
using AqualLifeStyle.Application.AreaLeaders;
using AqualLifeStyle.Application.Customers;
using AqualLifeStyle.Application.Enquiries;
using AqualLifeStyle.Application.Facilitators;
using AqualLifeStyle.Application.Memberships;
using AqualLifeStyle.Application.MyAccount;
using AqualLifeStyle.Application.Orders;
using AqualLifeStyle.Application.Products;
using AqualLifeStyle.Application.Referrals;
using AqualLifeStyle.Application.Savings;
using AqualLifeStyle.Application.Loans;
using AqualLifeStyle.Authorization;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class AppServiceAuthorizationTests
    {
        [Fact]
        public void MyAccountAppService_ShouldRequireAnAuthenticatedUser()
        {
            AssertAuthorizeAttribute(typeof(MyAccountAppService));
        }

        [Fact]
        public void AreaLeaderAppService_ShouldRequireAreaLeaderPermissions()
        {
            AssertAuthorizeAttribute(typeof(AreaLeaderAppService), PermissionNames.Pages_AreaLeaders);
            AssertAuthorizeAttribute(typeof(AreaLeaderAppService), nameof(AreaLeaderAppService.ApplyAsync), AquaPermissions.AreaLeaders.Apply);
            AssertAuthorizeAttribute(typeof(AreaLeaderAppService), nameof(AreaLeaderAppService.RecordStartupOrderAsync), AquaPermissions.Orders.Process);
            AssertAuthorizeAttribute(typeof(AreaLeaderAppService), nameof(AreaLeaderAppService.PromoteAsync), AquaPermissions.AreaLeaders.Manage);
        }

        [Fact]
        public void AreaSpaceAppService_ShouldRequireAreaSpacePermissions()
        {
            AssertAuthorizeAttribute(typeof(AreaSpaceAppService), PermissionNames.Pages_AreaSpaces);
            AssertAuthorizeAttribute(typeof(AreaSpaceAppService), nameof(AreaSpaceAppService.ApplyAsync), AquaPermissions.AreaSpaces.Apply);
            AssertAuthorizeAttribute(typeof(AreaSpaceAppService), nameof(AreaSpaceAppService.StartReviewAsync), AquaPermissions.AreaSpaces.Manage);
            AssertAuthorizeAttribute(typeof(AreaSpaceAppService), nameof(AreaSpaceAppService.RecordPresentationAsync), AquaPermissions.AreaSpaces.Manage);
            AssertAuthorizeAttribute(typeof(AreaSpaceAppService), nameof(AreaSpaceAppService.RecordStartupOrderAsync), AquaPermissions.Orders.Process);
            AssertAuthorizeAttribute(typeof(AreaSpaceAppService), nameof(AreaSpaceAppService.ApproveAsync), AquaPermissions.AreaSpaces.Approve);
            AssertAuthorizeAttribute(typeof(AreaSpaceAppService), nameof(AreaSpaceAppService.SuspendAsync), AquaPermissions.AreaSpaces.Manage);
        }

        [Fact]
        public void FacilitatorAppService_ShouldRequireFacilitatorPermissions()
        {
            AssertAuthorizeAttribute(typeof(FacilitatorAppService), PermissionNames.Pages_Facilitators);
            AssertAuthorizeAttribute(typeof(FacilitatorAppService), nameof(FacilitatorAppService.RegisterAsync), AquaPermissions.Facilitators.Register);
        }

        [Fact]
        public void ReferralAppService_ShouldRequireReferralPermissions()
        {
            AssertAuthorizeAttribute(typeof(ReferralAppService), PermissionNames.Pages_Referrals);
            AssertAuthorizeAttribute(typeof(ReferralAppService), nameof(ReferralAppService.ConfirmAwardAsync), AquaPermissions.Referrals.Confirm);
        }

        [Fact]
        public void CustomerAppService_ShouldRequireCustomerPermissions()
        {
            AssertAuthorizeAttribute(typeof(CustomerAppService), nameof(CustomerAppService.GetAllAsync), AquaPermissions.Members.View);
            AssertAuthorizeAttribute(typeof(CustomerAppService), nameof(CustomerAppService.GetMyCustomerAsync), AquaPermissions.Members.ViewSelf);
            AssertAuthorizeAttribute(typeof(CustomerAppService), nameof(CustomerAppService.CreateAsync), AquaPermissions.Members.Create);
            AssertAuthorizeAttribute(typeof(CustomerAppService), nameof(CustomerAppService.UpdateAsync), AquaPermissions.Members.Edit);
        }

        [Fact]
        public void CustomerImportAppService_ShouldRequireSeparateImportPermissionOnEveryMethod()
        {
            AssertAuthorizeAttribute(typeof(CustomerImportAppService), nameof(CustomerImportAppService.PreviewAsync), AquaPermissions.Admin.Customers.Import);
            AssertAuthorizeAttribute(typeof(CustomerImportAppService), nameof(CustomerImportAppService.ImportAsync), AquaPermissions.Admin.Customers.Import);
            AquaPermissions.Admin.Customers.Import.ShouldNotBe(AquaPermissions.Admin.Customers.Create);
        }

        [Fact]
        public void AdminCustomerAppService_ShouldRequireGranularPermissionOnEveryMethod()
        {
            AssertAuthorizeAttribute(typeof(AdminCustomerAppService), nameof(AdminCustomerAppService.GetAllAsync), AquaPermissions.Admin.Customers.View);
            AssertAuthorizeAttribute(typeof(AdminCustomerAppService), nameof(AdminCustomerAppService.GetMembershipOptionsAsync), AquaPermissions.Admin.Customers.View);
            AssertAuthorizeAttribute(typeof(AdminCustomerAppService), nameof(AdminCustomerAppService.GetAsync), AquaPermissions.Admin.Customers.View);
            AssertAuthorizeAttribute(typeof(AdminCustomerAppService), nameof(AdminCustomerAppService.CreateAsync), AquaPermissions.Admin.Customers.Create);
            AssertAuthorizeAttribute(typeof(AdminCustomerAppService), nameof(AdminCustomerAppService.RestoreAsync), AquaPermissions.Admin.Customers.Create);
            AssertAuthorizeAttribute(typeof(AdminCustomerAppService), nameof(AdminCustomerAppService.UpdateAsync), AquaPermissions.Admin.Customers.Edit);
            AssertAuthorizeAttribute(typeof(AdminCustomerAppService), nameof(AdminCustomerAppService.DeleteAsync), AquaPermissions.Admin.Customers.Delete);
        }

        [Fact]
        public void AdminUserAppService_ShouldRequireGranularPermissionOnEveryMethod()
        {
            AssertAuthorizeAttribute(typeof(AdminUserAppService), nameof(AdminUserAppService.GetAllAsync), AquaPermissions.Admin.Users.View);
            AssertAuthorizeAttribute(typeof(AdminUserAppService), nameof(AdminUserAppService.GetAsync), AquaPermissions.Admin.Users.View);
            AssertAuthorizeAttribute(typeof(AdminUserAppService), nameof(AdminUserAppService.CreateAsync), AquaPermissions.Admin.Users.Create);
            AssertAuthorizeAttribute(typeof(AdminUserAppService), nameof(AdminUserAppService.UpdateAsync), AquaPermissions.Admin.Users.Edit);
            AssertAuthorizeAttribute(typeof(AdminUserAppService), nameof(AdminUserAppService.AssignRoleAsync), AquaPermissions.Admin.Users.AssignRole);
            AssertAuthorizeAttribute(typeof(AdminUserAppService), nameof(AdminUserAppService.ResetPasswordAsync), AquaPermissions.Admin.Users.ResetPassword);
            AssertAuthorizeAttribute(typeof(AdminUserAppService), nameof(AdminUserAppService.DeleteAsync), AquaPermissions.Admin.Users.Delete);
        }

        [Fact]
        public void AdminAreaLeaderAppService_ShouldRequireGranularPermissionOnEveryMethod()
        {
            AssertAuthorizeAttribute(typeof(AdminAreaLeaderAppService), nameof(AdminAreaLeaderAppService.GetAllAsync), AquaPermissions.Admin.AreaLeaders.View);
            AssertAuthorizeAttribute(typeof(AdminAreaLeaderAppService), nameof(AdminAreaLeaderAppService.GetAsync), AquaPermissions.Admin.AreaLeaders.View);
            AssertAuthorizeAttribute(typeof(AdminAreaLeaderAppService), nameof(AdminAreaLeaderAppService.ApproveAsync), AquaPermissions.Admin.AreaLeaders.Approve);
            AssertAuthorizeAttribute(typeof(AdminAreaLeaderAppService), nameof(AdminAreaLeaderAppService.PromoteAsync), AquaPermissions.Admin.AreaLeaders.Promote);
            AssertAuthorizeAttribute(typeof(AdminAreaLeaderAppService), nameof(AdminAreaLeaderAppService.DemoteAsync), AquaPermissions.Admin.AreaLeaders.Demote);
            AssertAuthorizeAttribute(typeof(AdminAreaLeaderAppService), nameof(AdminAreaLeaderAppService.RemoveAsync), AquaPermissions.Admin.AreaLeaders.Remove);
        }

        [Fact]
        public void AdminFacilitatorAppService_ShouldRequireGranularPermissionOnEveryMethod()
        {
            AssertAuthorizeAttribute(typeof(AdminFacilitatorAppService), nameof(AdminFacilitatorAppService.GetAllAsync), AquaPermissions.Admin.Facilitators.View);
            AssertAuthorizeAttribute(typeof(AdminFacilitatorAppService), nameof(AdminFacilitatorAppService.GetAsync), AquaPermissions.Admin.Facilitators.View);
            AssertAuthorizeAttribute(typeof(AdminFacilitatorAppService), nameof(AdminFacilitatorAppService.ApproveAsync), AquaPermissions.Admin.Facilitators.Approve);
            AssertAuthorizeAttribute(typeof(AdminFacilitatorAppService), nameof(AdminFacilitatorAppService.PromoteAsync), AquaPermissions.Admin.Facilitators.Promote);
            AssertAuthorizeAttribute(typeof(AdminFacilitatorAppService), nameof(AdminFacilitatorAppService.DemoteAsync), AquaPermissions.Admin.Facilitators.Demote);
            AssertAuthorizeAttribute(typeof(AdminFacilitatorAppService), nameof(AdminFacilitatorAppService.RemoveAsync), AquaPermissions.Admin.Facilitators.Remove);
        }

        [Fact]
        public void AdminMemberAppService_ShouldRequireGranularPermissionOnEveryMethod()
        {
            AssertAuthorizeAttribute(typeof(AdminMemberAppService), nameof(AdminMemberAppService.GetAllAsync), AquaPermissions.Admin.Members.View);
            AssertAuthorizeAttribute(typeof(AdminMemberAppService), nameof(AdminMemberAppService.GetAsync), AquaPermissions.Admin.Members.View);
            AssertAuthorizeAttribute(typeof(AdminMemberAppService), nameof(AdminMemberAppService.GetMembershipOptionsAsync), AquaPermissions.Admin.Members.ChangeTier);
            AssertAuthorizeAttribute(typeof(AdminMemberAppService), nameof(AdminMemberAppService.EditProfileAsync), AquaPermissions.Admin.Members.Edit);
            AssertAuthorizeAttribute(typeof(AdminMemberAppService), nameof(AdminMemberAppService.SuspendAsync), AquaPermissions.Admin.Members.Suspend);
            AssertAuthorizeAttribute(typeof(AdminMemberAppService), nameof(AdminMemberAppService.ChangeTierAsync), AquaPermissions.Admin.Members.ChangeTier);
        }

        [Fact]
        public void AdminTenantAppService_ShouldRequireGranularPermissionOnEveryMethod()
        {
            AssertAuthorizeAttribute(typeof(AdminTenantAppService), nameof(AdminTenantAppService.GetAllAsync), AquaPermissions.Admin.Tenants.View);
            AssertAuthorizeAttribute(typeof(AdminTenantAppService), nameof(AdminTenantAppService.GetAsync), AquaPermissions.Admin.Tenants.View);
            AssertAuthorizeAttribute(typeof(AdminTenantAppService), nameof(AdminTenantAppService.CreateAsync), AquaPermissions.Admin.Tenants.Create);
            AssertAuthorizeAttribute(typeof(AdminTenantAppService), nameof(AdminTenantAppService.EditAsync), AquaPermissions.Admin.Tenants.Edit);
            AssertAuthorizeAttribute(typeof(AdminTenantAppService), nameof(AdminTenantAppService.SetActivationAsync), AquaPermissions.Admin.Tenants.Activate);
            AssertAuthorizeAttribute(typeof(AdminTenantAppService), nameof(AdminTenantAppService.AssignAreaLeaderAsync), AquaPermissions.Admin.Tenants.AssignLeader);
        }

        [Fact]
        public void AdminCommissionAppService_ShouldSeparateReviewAndHostWideCalculationPermissions()
        {
            AssertAuthorizeAttribute(
                typeof(AdminCommissionAppService),
                nameof(AdminCommissionAppService.GetAllAsync),
                AquaPermissions.Admin.Commissions.View);
            AssertAuthorizeAttribute(
                typeof(AdminCommissionAppService),
                nameof(AdminCommissionAppService.CalculateLatestClosedWeekAsync),
                AquaPermissions.Admin.Commissions.Calculate);
            AssertAuthorizeAttribute(
                typeof(AdminCommissionAppService),
                nameof(AdminCommissionAppService.CalculateLatestClosedWeekAsync),
                AquaPermissions.Admin.AllTenants);
            GetAuthorizeAttribute(
                    typeof(AdminCommissionAppService),
                    nameof(AdminCommissionAppService.CalculateLatestClosedWeekAsync))
                .RequireAllPermissions.ShouldBeTrue();
            AssertHostWideFinancialAction(
                nameof(AdminCommissionAppService.ReleaseAsync),
                AquaPermissions.Admin.Commissions.Release);
            AssertHostWideFinancialAction(
                nameof(AdminCommissionAppService.RecordPaymentAsync),
                AquaPermissions.Admin.Commissions.RecordPayment);
        }

        [Fact]
        public void SavingsServices_ShouldSeparateSelfAndAdministratorViews()
        {
            AssertAuthorizeAttribute(
                typeof(ClubMemberSavingsAppService),
                nameof(ClubMemberSavingsAppService.GetMyAccountAsync),
                AquaPermissions.Savings.ViewSelf);
            AssertAuthorizeAttribute(
                typeof(AdminSavingsAppService),
                nameof(AdminSavingsAppService.GetAllAsync),
                AquaPermissions.Admin.Savings.View);
        }

        [Fact]
        public void LoanServices_ShouldSeparateSelfAndAdministratorViews()
        {
            AssertAuthorizeAttribute(
                typeof(ClubMemberOnyxLoanAppService),
                nameof(ClubMemberOnyxLoanAppService.GetMyAgreementsAsync),
                AquaPermissions.Loans.ViewSelf);
            AssertAuthorizeAttribute(
                typeof(AdminOnyxLoanAppService),
                nameof(AdminOnyxLoanAppService.GetAllAsync),
                AquaPermissions.Admin.Loans.View);
        }

        [Fact]
        public void MembershipAppService_ShouldRequireMembershipPermissions()
        {
            AssertAuthorizeAttribute(typeof(MembershipAppService), nameof(MembershipAppService.GetAllAsync), AquaPermissions.Memberships.View);
            AssertAuthorizeAttribute(typeof(MembershipAppService), nameof(MembershipAppService.GetActiveTiersAsync), AquaPermissions.Memberships.ViewSelf);
            AssertAuthorizeAttribute(typeof(MembershipAppService), nameof(MembershipAppService.CreateAsync), AquaPermissions.Members.Create);
            AssertAuthorizeAttribute(typeof(MembershipAppService), nameof(MembershipAppService.UpdateAsync), AquaPermissions.Members.Edit);
            AssertAuthorizeAttribute(typeof(MembershipAppService), nameof(MembershipAppService.SetActivationDateAsync), AquaPermissions.Members.Edit);
            AssertAuthorizeAttribute(typeof(MembershipAppService), nameof(MembershipAppService.SetMonthlyObligationAsync), AquaPermissions.Members.Edit);
            AssertAuthorizeAttribute(typeof(MembershipAppService), nameof(MembershipAppService.MarkObligationMetAsync), AquaPermissions.Members.Edit);
        }

        [Fact]
        public void MembershipBenefitAppService_ShouldRequireMembershipBenefitPermissions()
        {
            AssertAuthorizeAttribute(typeof(MembershipBenefitAppService), PermissionNames.Pages_MembershipBenefits);
            AssertAuthorizeAttribute(typeof(MembershipBenefitAppService), nameof(MembershipBenefitAppService.CreateAsync), PermissionNames.Pages_MembershipBenefits_Manage);
            AssertAuthorizeAttribute(typeof(MembershipBenefitAppService), nameof(MembershipBenefitAppService.UpdateAsync), PermissionNames.Pages_MembershipBenefits_Manage);
            AssertAuthorizeAttribute(typeof(MembershipBenefitAppService), nameof(MembershipBenefitAppService.DeleteAsync), PermissionNames.Pages_MembershipBenefits_Manage);
        }

        [Fact]
        public void EnquiryAppService_ShouldRequireEnquiryPermissions()
        {
            AssertAuthorizeAttribute(typeof(EnquiryAppService), PermissionNames.Pages_Enquiries);
            AssertAuthorizeAttribute(typeof(EnquiryAppService), nameof(EnquiryAppService.CreateAsync), AquaPermissions.Enquiries.Create);
            AssertAuthorizeAttribute(typeof(EnquiryAppService), nameof(EnquiryAppService.RespondAsync), AquaPermissions.Enquiries.Update);
            AssertAuthorizeAttribute(typeof(EnquiryAppService), nameof(EnquiryAppService.CloseAsync), AquaPermissions.Enquiries.Resolve);
            AssertAuthorizeAttribute(typeof(EnquiryAppService), nameof(EnquiryAppService.ReopenAsync), AquaPermissions.Enquiries.Update);
            AssertAuthorizeAttribute(typeof(EnquiryAppService), nameof(EnquiryAppService.AssignToMemberAsync), AquaPermissions.Enquiries.Update);
            AssertAuthorizeAttribute(typeof(EnquiryAppService), nameof(EnquiryAppService.ConvertToCustomerAsync), AquaPermissions.Enquiries.Resolve);
            AssertAuthorizeAttribute(typeof(EnquiryAppService), nameof(EnquiryAppService.ClearAssignmentAsync), AquaPermissions.Enquiries.Update);
            AssertAuthorizeAttribute(typeof(EnquiryAppService), nameof(EnquiryAppService.RecordFollowUpAsync), AquaPermissions.Enquiries.Update);
        }

        [Fact]
        public void OrderIntentAppService_ShouldRequireOrderPermissions()
        {
            AssertAuthorizeAttribute(typeof(OrderIntentAppService), PermissionNames.Pages_Orders);
            AssertAuthorizeAttribute(typeof(OrderIntentAppService), nameof(OrderIntentAppService.CreateFromEnquiryAsync), AquaPermissions.Orders.Place);
            AssertAuthorizeAttribute(typeof(OrderIntentAppService), nameof(OrderIntentAppService.CancelAsync), AquaPermissions.Orders.Process);
            AssertAuthorizeAttribute(typeof(OrderIntentAppService), nameof(OrderIntentAppService.CompleteAsync), AquaPermissions.Orders.Process);
        }

        [Fact]
        public void ProductAppService_ShouldRequireProductPermissions()
        {
            AssertAuthorizeAttribute(typeof(ProductAppService), PermissionNames.Pages_Products);
            AssertAuthorizeAttribute(typeof(ProductAppService), nameof(ProductAppService.CreateAsync), PermissionNames.Pages_Products_Manage);
        }

        private static void AssertAuthorizeAttribute(Type serviceType, string permissionName)
        {
            var attribute = serviceType.GetCustomAttribute<AbpAuthorizeAttribute>(inherit: true);
            attribute.ShouldNotBeNull($"{serviceType.Name} should declare AbpAuthorize.");
            attribute.Permissions.ShouldContain(permissionName);
        }

        private static void AssertAuthorizeAttribute(Type serviceType)
        {
            var attribute = serviceType.GetCustomAttribute<AbpAuthorizeAttribute>(inherit: true);
            attribute.ShouldNotBeNull($"{serviceType.Name} should require an authenticated user.");
        }

        private static void AssertAuthorizeAttribute(Type serviceType, string methodName, string permissionName)
        {
            var attribute = GetAuthorizeAttribute(serviceType, methodName);

            attribute.Permissions.ShouldContain(permissionName);
        }

        private static AbpAuthorizeAttribute GetAuthorizeAttribute(
            Type serviceType,
            string methodName)
        {
            var method = serviceType.GetMethod(methodName);
            method.ShouldNotBeNull($"{serviceType.Name}.{methodName} should exist.");

            var attribute = method
                .GetCustomAttributes(typeof(AbpAuthorizeAttribute), inherit: true)
                .Cast<AbpAuthorizeAttribute>()
                .SingleOrDefault();
            attribute.ShouldNotBeNull(
                $"{serviceType.Name}.{methodName} should declare AbpAuthorize.");
            return attribute;
        }

        private static void AssertHostWideFinancialAction(
            string methodName,
            string actionPermission)
        {
            AssertAuthorizeAttribute(
                typeof(AdminCommissionAppService),
                methodName,
                actionPermission);
            AssertAuthorizeAttribute(
                typeof(AdminCommissionAppService),
                methodName,
                AquaPermissions.Admin.AllTenants);
            GetAuthorizeAttribute(typeof(AdminCommissionAppService), methodName)
                .RequireAllPermissions.ShouldBeTrue();
        }
    }
}
