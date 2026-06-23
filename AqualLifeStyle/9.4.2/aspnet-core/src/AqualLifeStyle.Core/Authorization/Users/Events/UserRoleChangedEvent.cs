using System;
using Abp.Events.Bus;
using AqualLifeStyle.Domain.Enums;

namespace AqualLifeStyle.Authorization.Users.Events
{
    [Serializable]
    public class UserRoleChangedEvent : EventData
    {
        public long UserId { get; }
        public AquaUserRole OldRole { get; }
        public AquaUserRole NewRole { get; }

        public UserRoleChangedEvent(long userId, AquaUserRole oldRole, AquaUserRole newRole)
        {
            UserId = userId;
            OldRole = oldRole;
            NewRole = newRole;
        }
    }
}
