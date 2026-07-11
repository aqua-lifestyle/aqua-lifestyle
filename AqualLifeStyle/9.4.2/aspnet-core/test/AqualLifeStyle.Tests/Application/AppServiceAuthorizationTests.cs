using System;
using System.Linq;
using System.Reflection;
using Abp.Authorization;
using AqualLifeStyle.Application.AreaLeaders;
using AqualLifeStyle.Application.Facilitators;
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
