using System;
using System.Collections.Generic;
using AqualLifeStyle.Domain.Enums;

namespace AqualLifeStyle.Authorization
{
    public static class AquaRolePermissions
    {
        private static readonly IReadOnlyDictionary<AquaUserRole, IReadOnlyCollection<string>> Mappings =
            new Dictionary<AquaUserRole, IReadOnlyCollection<string>>
            {
                [AquaUserRole.SystemAdmin] = AquaPermissions.GetAll(),
                [AquaUserRole.AreaLeader] = new[]
                {
                    AquaPermissions.AreaLeaders.ViewSelf,
                    AquaPermissions.AreaLeaders.Manage,
                    AquaPermissions.AreaSpaces.View,
                    AquaPermissions.AreaSpaces.Manage,
                    AquaPermissions.Facilitators.View,
                    AquaPermissions.Facilitators.Promote,
                    AquaPermissions.Members.View,
                    AquaPermissions.Orders.View,
                    AquaPermissions.Orders.Process,
                    AquaPermissions.Orders.Approve,
                    AquaPermissions.Enquiries.View,
                    AquaPermissions.Enquiries.Update,
                    AquaPermissions.Enquiries.Resolve,
                    AquaPermissions.Referrals.View
                },
                [AquaUserRole.Facilitator] = new[]
                {
                    AquaPermissions.Facilitators.ViewSelf,
                    AquaPermissions.Facilitators.Refer,
                    AquaPermissions.Members.Create,
                    AquaPermissions.Enquiries.Create,
                    AquaPermissions.Enquiries.ViewSelf,
                    AquaPermissions.Referrals.Create,
                    AquaPermissions.Referrals.ViewSelf
                },
                [AquaUserRole.Member] = new[]
                {
                    AquaPermissions.Members.ViewSelf,
                    AquaPermissions.Members.EditSelf,
                    AquaPermissions.Orders.Place,
                    AquaPermissions.Orders.ViewSelf,
                    AquaPermissions.Savings.Deposit,
                    AquaPermissions.Savings.Withdraw,
                    AquaPermissions.Savings.ViewSelf,
                    AquaPermissions.Enquiries.Create,
                    AquaPermissions.Enquiries.ViewSelf,
                    AquaPermissions.Referrals.Create,
                    AquaPermissions.Referrals.ViewSelf
                },
                [AquaUserRole.Guest] = Array.Empty<string>()
            };

        public static IReadOnlyCollection<string> GetFor(AquaUserRole role)
        {
            if (!Mappings.TryGetValue(role, out var permissions))
            {
                throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown Aqua user role.");
            }

            return permissions;
        }
    }
}
