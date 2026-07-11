using Abp.Authorization;
using Abp.Localization;
using Abp.MultiTenancy;

namespace AqualLifeStyle.Authorization
{
    public class AqualLifeStyleAuthorizationProvider : AuthorizationProvider
    {
        public override void SetPermissions(IPermissionDefinitionContext context)
        {
            context.CreatePermission(PermissionNames.Pages_Users, L("Users"));
            context.CreatePermission(PermissionNames.Pages_Users_Activation, L("UsersActivation"));
            context.CreatePermission(PermissionNames.Pages_Roles, L("Roles"));
            context.CreatePermission(PermissionNames.Pages_Tenants, L("Tenants"), multiTenancySides: MultiTenancySides.Host);

            var areaLeaders = context.CreatePermission(PermissionNames.Pages_AreaLeaders, L("AreaLeaders"));
            areaLeaders.CreateChildPermission(PermissionNames.Pages_AreaLeaders_Manage, L("AreaLeadersManage"));

            var areaSpaces = context.CreatePermission(PermissionNames.Pages_AreaSpaces, L("AreaSpaces"));
            areaSpaces.CreateChildPermission(PermissionNames.Pages_AreaSpaces_Manage, L("AreaSpacesManage"));

            var facilitators = context.CreatePermission(PermissionNames.Pages_Facilitators, L("Facilitators"));
            facilitators.CreateChildPermission(PermissionNames.Pages_Facilitators_Manage, L("FacilitatorsManage"));

            var referrals = context.CreatePermission(PermissionNames.Pages_Referrals, L("Referrals"));
            referrals.CreateChildPermission(PermissionNames.Pages_Referrals_Manage, L("ReferralsManage"));
        }

        private static ILocalizableString L(string name)
        {
            return new LocalizableString(name, AqualLifeStyleConsts.LocalizationSourceName);
        }
    }
}
