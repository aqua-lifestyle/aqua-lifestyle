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
                    AquaPermissions.AreaLeaders.View,
                    AquaPermissions.AreaLeaders.ViewSelf,
                    AquaPermissions.AreaLeaders.Apply,
                    AquaPermissions.AreaLeaders.Approve,
                    AquaPermissions.AreaLeaders.Manage,
                    AquaPermissions.AreaSpaces.View,
                    AquaPermissions.AreaSpaces.Apply,
                    AquaPermissions.AreaSpaces.Approve,
                    AquaPermissions.AreaSpaces.Manage,
                    AquaPermissions.Facilitators.View,
                    AquaPermissions.Facilitators.Promote,
                    AquaPermissions.Members.View,
                    AquaPermissions.Members.Create,
                    AquaPermissions.Members.Edit,
                    AquaPermissions.Orders.View,
                    AquaPermissions.Orders.Place,
                    AquaPermissions.Orders.Process,
                    AquaPermissions.Orders.Approve,
                    AquaPermissions.Enquiries.View,
                    AquaPermissions.Enquiries.Create,
                    AquaPermissions.Enquiries.Update,
                    AquaPermissions.Enquiries.Resolve,
                    AquaPermissions.Referrals.View,
                    AquaPermissions.Referrals.Create,
                    AquaPermissions.Referrals.Confirm
                },
                [AquaUserRole.Facilitator] = new[]
                {
                    AquaPermissions.Facilitators.View,
                    AquaPermissions.Facilitators.ViewSelf,
                    AquaPermissions.Facilitators.Register,
                    AquaPermissions.Facilitators.Refer,
                    AquaPermissions.Facilitators.Promote,
                    AquaPermissions.Members.View,
                    AquaPermissions.Members.Create,
                    AquaPermissions.Members.Edit,
                    AquaPermissions.Enquiries.View,
                    AquaPermissions.Enquiries.Create,
                    AquaPermissions.Enquiries.Update,
                    AquaPermissions.Enquiries.ViewSelf,
                    AquaPermissions.Referrals.View,
                    AquaPermissions.Referrals.Create,
                    AquaPermissions.Referrals.ViewSelf,
                    AquaPermissions.Orders.View,
                    AquaPermissions.Orders.Place,
                    AquaPermissions.Orders.ViewSelf
                },
                [AquaUserRole.Member] = new[]
                {
                    AquaPermissions.Members.ViewSelf,
                    AquaPermissions.Members.EditSelf,
                    AquaPermissions.Memberships.ViewSelf,
                    AquaPermissions.Memberships.Upgrade,
                    AquaPermissions.ProgrammeParticipations.ViewSelf,
                    AquaPermissions.ProgrammeParticipations.Join,
                    AquaPermissions.Enquiries.View,
                    AquaPermissions.Enquiries.Create,
                    AquaPermissions.Enquiries.ViewSelf,
                    AquaPermissions.Referrals.View,
                    AquaPermissions.Referrals.Create,
                    AquaPermissions.Referrals.ViewSelf,
                    AquaPermissions.Orders.View,
                    AquaPermissions.Orders.Place,
                    AquaPermissions.Orders.ViewSelf,
                    AquaPermissions.Savings.View,
                    AquaPermissions.Savings.Deposit,
                    AquaPermissions.Savings.Withdraw,
                    AquaPermissions.Savings.ViewSelf,
                    AquaPermissions.Loans.ViewSelf,
                    AquaPermissions.EntryMonthlyObligations.ViewSelf
                },
                [AquaUserRole.Guest] = new[]
                {
                    AquaPermissions.Members.ViewSelf,
                    AquaPermissions.Memberships.ViewSelf,
                    AquaPermissions.Memberships.Upgrade,
                    AquaPermissions.ProgrammeParticipations.ViewSelf,
                    AquaPermissions.ProgrammeParticipations.Join,
                    AquaPermissions.Orders.Place
                }
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
