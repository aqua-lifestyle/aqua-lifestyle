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

            var customers = context.CreatePermission(PermissionNames.Pages_Customers, L("Customers"));
            customers.CreateChildPermission(PermissionNames.Pages_Customers_Manage, L("CustomersManage"));

            var memberships = context.CreatePermission(PermissionNames.Pages_Memberships, L("Memberships"));
            memberships.CreateChildPermission(PermissionNames.Pages_Memberships_Manage, L("MembershipsManage"));

            var membershipBenefits = context.CreatePermission(PermissionNames.Pages_MembershipBenefits, L("MembershipBenefits"));
            membershipBenefits.CreateChildPermission(PermissionNames.Pages_MembershipBenefits_Manage, L("MembershipBenefitsManage"));

            var enquiries = context.CreatePermission(PermissionNames.Pages_Enquiries, L("Enquiries"));
            enquiries.CreateChildPermission(PermissionNames.Pages_Enquiries_Manage, L("EnquiriesManage"));

            var orders = context.CreatePermission(PermissionNames.Pages_Orders, L("Orders"));
            orders.CreateChildPermission(PermissionNames.Pages_Orders_Manage, L("OrdersManage"));

            var products = context.CreatePermission(PermissionNames.Pages_Products, L("Products"));
            products.CreateChildPermission(PermissionNames.Pages_Products_Manage, L("ProductsManage"));
        }

        private static ILocalizableString L(string name)
        {
            return new LocalizableString(name, AqualLifeStyleConsts.LocalizationSourceName);
        }
    }
}
