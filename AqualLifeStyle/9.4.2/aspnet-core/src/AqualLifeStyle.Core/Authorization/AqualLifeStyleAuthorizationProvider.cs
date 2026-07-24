using Abp.Authorization;
using Abp.Localization;
using Abp.MultiTenancy;

namespace AqualLifeStyle.Authorization
{
    public class AqualLifeStyleAuthorizationProvider : AuthorizationProvider
    {
        public override void SetPermissions(IPermissionDefinitionContext context)
        {
            RegisterAquaPermissions(context);

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

        private static void RegisterAquaPermissions(IPermissionDefinitionContext context)
        {
            CreateGroup(context, AquaPermissions.Members.Default, AquaPermissions.Members.View, AquaPermissions.Members.Create, AquaPermissions.Members.Edit, AquaPermissions.Members.Delete, AquaPermissions.Members.Upgrade, AquaPermissions.Members.ViewSelf, AquaPermissions.Members.EditSelf);
            CreateGroup(context, AquaPermissions.Facilitators.Default, AquaPermissions.Facilitators.View, AquaPermissions.Facilitators.Register, AquaPermissions.Facilitators.Refer, AquaPermissions.Facilitators.Promote, AquaPermissions.Facilitators.ViewSelf);
            CreateGroup(context, AquaPermissions.AreaLeaders.Default, AquaPermissions.AreaLeaders.View, AquaPermissions.AreaLeaders.Apply, AquaPermissions.AreaLeaders.Approve, AquaPermissions.AreaLeaders.Manage, AquaPermissions.AreaLeaders.ViewSelf);
            CreateGroup(context, AquaPermissions.AreaSpaces.Default, AquaPermissions.AreaSpaces.View, AquaPermissions.AreaSpaces.Apply, AquaPermissions.AreaSpaces.Approve, AquaPermissions.AreaSpaces.Manage);
            CreateGroup(context, AquaPermissions.Orders.Default, AquaPermissions.Orders.View, AquaPermissions.Orders.Place, AquaPermissions.Orders.Process, AquaPermissions.Orders.Approve, AquaPermissions.Orders.ViewSelf);
            CreateGroup(context, AquaPermissions.Savings.Default, AquaPermissions.Savings.View, AquaPermissions.Savings.Deposit, AquaPermissions.Savings.Withdraw, AquaPermissions.Savings.Approve, AquaPermissions.Savings.ViewSelf);
            CreateGroup(context, AquaPermissions.Enquiries.Default, AquaPermissions.Enquiries.View, AquaPermissions.Enquiries.Create, AquaPermissions.Enquiries.Update, AquaPermissions.Enquiries.Resolve, AquaPermissions.Enquiries.ViewSelf);
            CreateGroup(context, AquaPermissions.Referrals.Default, AquaPermissions.Referrals.View, AquaPermissions.Referrals.Create, AquaPermissions.Referrals.Confirm, AquaPermissions.Referrals.ViewSelf);
            CreateGroup(context, AquaPermissions.Memberships.Default, AquaPermissions.Memberships.View, AquaPermissions.Memberships.ViewSelf, AquaPermissions.Memberships.Upgrade);
            CreateGroup(context, AquaPermissions.ProgrammeParticipations.Default, AquaPermissions.ProgrammeParticipations.ViewSelf, AquaPermissions.ProgrammeParticipations.Join);
            RegisterAdminPermissions(context);
        }

        private static void RegisterAdminPermissions(IPermissionDefinitionContext context)
        {
            var sides = MultiTenancySides.Host | MultiTenancySides.Tenant;
            var admin = context.CreatePermission(
                AquaPermissions.Admin.Default,
                L(AquaPermissions.Admin.Default),
                multiTenancySides: sides);
            CreateChildren(
                admin,
                sides,
                AquaPermissions.Admin.Dashboard,
                AquaPermissions.Admin.Reports,
                AquaPermissions.Admin.Audit,
                AquaPermissions.Admin.Settings);

            var users = admin.CreateChildPermission(
                AquaPermissions.Admin.Users.Default,
                L(AquaPermissions.Admin.Users.Default),
                multiTenancySides: sides);
            CreateChildren(users, sides,
                AquaPermissions.Admin.Users.View,
                AquaPermissions.Admin.Users.Create,
                AquaPermissions.Admin.Users.Edit,
                AquaPermissions.Admin.Users.Delete,
                AquaPermissions.Admin.Users.AssignRole,
                AquaPermissions.Admin.Users.ResetPassword);

            var customers = admin.CreateChildPermission(
                AquaPermissions.Admin.Customers.Default,
                L(AquaPermissions.Admin.Customers.Default),
                multiTenancySides: sides);
            CreateChildren(customers, sides,
                AquaPermissions.Admin.Customers.View,
                AquaPermissions.Admin.Customers.Create,
                AquaPermissions.Admin.Customers.Edit,
                AquaPermissions.Admin.Customers.Delete,
                AquaPermissions.Admin.Customers.Import);

            var areaLeaders = admin.CreateChildPermission(
                AquaPermissions.Admin.AreaLeaders.Default,
                L(AquaPermissions.Admin.AreaLeaders.Default),
                multiTenancySides: sides);
            CreateChildren(areaLeaders, sides,
                AquaPermissions.Admin.AreaLeaders.View,
                AquaPermissions.Admin.AreaLeaders.Approve,
                AquaPermissions.Admin.AreaLeaders.Promote,
                AquaPermissions.Admin.AreaLeaders.Demote,
                AquaPermissions.Admin.AreaLeaders.Remove);

            var facilitators = admin.CreateChildPermission(
                AquaPermissions.Admin.Facilitators.Default,
                L(AquaPermissions.Admin.Facilitators.Default),
                multiTenancySides: sides);
            CreateChildren(facilitators, sides,
                AquaPermissions.Admin.Facilitators.View,
                AquaPermissions.Admin.Facilitators.Approve,
                AquaPermissions.Admin.Facilitators.Promote,
                AquaPermissions.Admin.Facilitators.Demote,
                AquaPermissions.Admin.Facilitators.Remove);

            var members = admin.CreateChildPermission(
                AquaPermissions.Admin.Members.Default,
                L(AquaPermissions.Admin.Members.Default),
                multiTenancySides: sides);
            CreateChildren(members, sides,
                AquaPermissions.Admin.Members.View,
                AquaPermissions.Admin.Members.Edit,
                AquaPermissions.Admin.Members.Suspend,
                AquaPermissions.Admin.Members.ChangeTier);

            var programmeParticipations = admin.CreateChildPermission(
                AquaPermissions.Admin.ProgrammeParticipations.Default,
                L(AquaPermissions.Admin.ProgrammeParticipations.Default),
                multiTenancySides: sides);
            CreateChildren(
                programmeParticipations,
                sides,
                AquaPermissions.Admin.ProgrammeParticipations.View);

            var host = MultiTenancySides.Host;
            var commissions = admin.CreateChildPermission(
                AquaPermissions.Admin.Commissions.Default,
                L(AquaPermissions.Admin.Commissions.Default),
                multiTenancySides: sides);
            CreateChildren(
                commissions,
                sides,
                AquaPermissions.Admin.Commissions.View);
            CreateChildren(
                commissions,
                host,
                AquaPermissions.Admin.Commissions.Calculate,
                AquaPermissions.Admin.Commissions.Release,
                AquaPermissions.Admin.Commissions.RecordPayment);

            var savings = admin.CreateChildPermission(
                AquaPermissions.Admin.Savings.Default,
                L(AquaPermissions.Admin.Savings.Default),
                multiTenancySides: sides);
            CreateChildren(
                savings,
                sides,
                AquaPermissions.Admin.Savings.View);

            CreateChildren(admin, host, AquaPermissions.Admin.AllTenants);
            var tenants = admin.CreateChildPermission(
                AquaPermissions.Admin.Tenants.Default,
                L(AquaPermissions.Admin.Tenants.Default),
                multiTenancySides: host);
            CreateChildren(tenants, host,
                AquaPermissions.Admin.Tenants.View,
                AquaPermissions.Admin.Tenants.Create,
                AquaPermissions.Admin.Tenants.Edit,
                AquaPermissions.Admin.Tenants.Activate,
                AquaPermissions.Admin.Tenants.AssignLeader);
        }

        private static void CreateChildren(
            Permission parent,
            MultiTenancySides sides,
            params string[] childNames)
        {
            foreach (var childName in childNames)
            {
                parent.CreateChildPermission(childName, L(childName), multiTenancySides: sides);
            }
        }

        private static void CreateGroup(IPermissionDefinitionContext context, string parentName, params string[] childNames)
        {
            CreateGroup(context, parentName, MultiTenancySides.Tenant, childNames);
        }

        private static void CreateGroup(IPermissionDefinitionContext context, string parentName, MultiTenancySides sides, params string[] childNames)
        {
            var parent = context.CreatePermission(parentName, L(parentName), multiTenancySides: sides);
            foreach (var childName in childNames)
            {
                parent.CreateChildPermission(childName, L(childName), multiTenancySides: sides);
            }
        }

        private static ILocalizableString L(string name)
        {
            return new LocalizableString(name, AqualLifeStyleConsts.LocalizationSourceName);
        }
    }
}
