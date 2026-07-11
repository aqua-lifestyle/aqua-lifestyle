using System;
using System.Linq;
using System.Reflection;
using Abp.Authorization;
using AqualLifeStyle.Application.AreaLeaders;
using AqualLifeStyle.Application.Customers;
using AqualLifeStyle.Application.Enquiries;
using AqualLifeStyle.Application.Facilitators;
using AqualLifeStyle.Application.Memberships;
using AqualLifeStyle.Application.Orders;
using AqualLifeStyle.Application.Products;
using AqualLifeStyle.Application.Referrals;
using AqualLifeStyle.Authorization;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class AppServiceAuthorizationTests
    {
        [Fact]
        public void AreaLeaderAppService_ShouldRequireAreaLeaderPermissions()
        {
            AssertAuthorizeAttribute(typeof(AreaLeaderAppService), PermissionNames.Pages_AreaLeaders);
            AssertAuthorizeAttribute(typeof(AreaLeaderAppService), nameof(AreaLeaderAppService.ApplyAsync), PermissionNames.Pages_AreaLeaders_Manage);
            AssertAuthorizeAttribute(typeof(AreaLeaderAppService), nameof(AreaLeaderAppService.RecordStartupOrderAsync), PermissionNames.Pages_AreaLeaders_Manage);
            AssertAuthorizeAttribute(typeof(AreaLeaderAppService), nameof(AreaLeaderAppService.PromoteAsync), PermissionNames.Pages_AreaLeaders_Manage);
        }

        [Fact]
        public void AreaSpaceAppService_ShouldRequireAreaSpacePermissions()
        {
            AssertAuthorizeAttribute(typeof(AreaSpaceAppService), PermissionNames.Pages_AreaSpaces);
            AssertAuthorizeAttribute(typeof(AreaSpaceAppService), nameof(AreaSpaceAppService.ApplyAsync), PermissionNames.Pages_AreaSpaces_Manage);
            AssertAuthorizeAttribute(typeof(AreaSpaceAppService), nameof(AreaSpaceAppService.StartReviewAsync), PermissionNames.Pages_AreaSpaces_Manage);
            AssertAuthorizeAttribute(typeof(AreaSpaceAppService), nameof(AreaSpaceAppService.RecordPresentationAsync), PermissionNames.Pages_AreaSpaces_Manage);
            AssertAuthorizeAttribute(typeof(AreaSpaceAppService), nameof(AreaSpaceAppService.RecordStartupOrderAsync), PermissionNames.Pages_AreaSpaces_Manage);
            AssertAuthorizeAttribute(typeof(AreaSpaceAppService), nameof(AreaSpaceAppService.ApproveAsync), PermissionNames.Pages_AreaSpaces_Manage);
            AssertAuthorizeAttribute(typeof(AreaSpaceAppService), nameof(AreaSpaceAppService.SuspendAsync), PermissionNames.Pages_AreaSpaces_Manage);
        }

        [Fact]
        public void FacilitatorAppService_ShouldRequireFacilitatorPermissions()
        {
            AssertAuthorizeAttribute(typeof(FacilitatorAppService), PermissionNames.Pages_Facilitators);
            AssertAuthorizeAttribute(typeof(FacilitatorAppService), nameof(FacilitatorAppService.RegisterAsync), PermissionNames.Pages_Facilitators_Manage);
        }

        [Fact]
        public void ReferralAppService_ShouldRequireReferralPermissions()
        {
            AssertAuthorizeAttribute(typeof(ReferralAppService), PermissionNames.Pages_Referrals);
            AssertAuthorizeAttribute(typeof(ReferralAppService), nameof(ReferralAppService.ConfirmAwardAsync), PermissionNames.Pages_Referrals_Manage);
        }

        [Fact]
        public void CustomerAppService_ShouldRequireCustomerPermissions()
        {
            AssertAuthorizeAttribute(typeof(CustomerAppService), PermissionNames.Pages_Customers);
            AssertAuthorizeAttribute(typeof(CustomerAppService), nameof(CustomerAppService.CreateAsync), PermissionNames.Pages_Customers_Manage);
            AssertAuthorizeAttribute(typeof(CustomerAppService), nameof(CustomerAppService.UpdateAsync), PermissionNames.Pages_Customers_Manage);
        }

        [Fact]
        public void MembershipAppService_ShouldRequireMembershipPermissions()
        {
            AssertAuthorizeAttribute(typeof(MembershipAppService), PermissionNames.Pages_Memberships);
            AssertAuthorizeAttribute(typeof(MembershipAppService), nameof(MembershipAppService.CreateAsync), PermissionNames.Pages_Memberships_Manage);
            AssertAuthorizeAttribute(typeof(MembershipAppService), nameof(MembershipAppService.UpdateAsync), PermissionNames.Pages_Memberships_Manage);
            AssertAuthorizeAttribute(typeof(MembershipAppService), nameof(MembershipAppService.SetActivationDateAsync), PermissionNames.Pages_Memberships_Manage);
            AssertAuthorizeAttribute(typeof(MembershipAppService), nameof(MembershipAppService.SetMonthlyObligationAsync), PermissionNames.Pages_Memberships_Manage);
            AssertAuthorizeAttribute(typeof(MembershipAppService), nameof(MembershipAppService.MarkObligationMetAsync), PermissionNames.Pages_Memberships_Manage);
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
            AssertAuthorizeAttribute(typeof(EnquiryAppService), nameof(EnquiryAppService.CreateAsync), PermissionNames.Pages_Enquiries_Manage);
            AssertAuthorizeAttribute(typeof(EnquiryAppService), nameof(EnquiryAppService.RespondAsync), PermissionNames.Pages_Enquiries_Manage);
            AssertAuthorizeAttribute(typeof(EnquiryAppService), nameof(EnquiryAppService.CloseAsync), PermissionNames.Pages_Enquiries_Manage);
            AssertAuthorizeAttribute(typeof(EnquiryAppService), nameof(EnquiryAppService.ReopenAsync), PermissionNames.Pages_Enquiries_Manage);
            AssertAuthorizeAttribute(typeof(EnquiryAppService), nameof(EnquiryAppService.AssignToMemberAsync), PermissionNames.Pages_Enquiries_Manage);
            AssertAuthorizeAttribute(typeof(EnquiryAppService), nameof(EnquiryAppService.ConvertToCustomerAsync), PermissionNames.Pages_Enquiries_Manage);
            AssertAuthorizeAttribute(typeof(EnquiryAppService), nameof(EnquiryAppService.ClearAssignmentAsync), PermissionNames.Pages_Enquiries_Manage);
            AssertAuthorizeAttribute(typeof(EnquiryAppService), nameof(EnquiryAppService.RecordFollowUpAsync), PermissionNames.Pages_Enquiries_Manage);
        }

        [Fact]
        public void OrderIntentAppService_ShouldRequireOrderPermissions()
        {
            AssertAuthorizeAttribute(typeof(OrderIntentAppService), PermissionNames.Pages_Orders);
            AssertAuthorizeAttribute(typeof(OrderIntentAppService), nameof(OrderIntentAppService.CreateFromEnquiryAsync), PermissionNames.Pages_Orders_Manage);
            AssertAuthorizeAttribute(typeof(OrderIntentAppService), nameof(OrderIntentAppService.CancelAsync), PermissionNames.Pages_Orders_Manage);
            AssertAuthorizeAttribute(typeof(OrderIntentAppService), nameof(OrderIntentAppService.CompleteAsync), PermissionNames.Pages_Orders_Manage);
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

        private static void AssertAuthorizeAttribute(Type serviceType, string methodName, string permissionName)
        {
            var method = serviceType.GetMethod(methodName);
            method.ShouldNotBeNull($"{serviceType.Name}.{methodName} should exist.");

            var attribute = method.GetCustomAttributes(typeof(AbpAuthorizeAttribute), inherit: true)
                .Cast<AbpAuthorizeAttribute>()
                .SingleOrDefault();

            attribute.ShouldNotBeNull($"{serviceType.Name}.{methodName} should declare AbpAuthorize.");
            attribute.Permissions.ShouldContain(permissionName);
        }
    }
}
