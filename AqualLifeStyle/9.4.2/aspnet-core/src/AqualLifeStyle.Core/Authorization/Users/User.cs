using System;
using System.Collections.Generic;
using Abp.Authorization.Users;
using Abp.Domain.Entities;
using Abp.Events.Bus;
using Abp.Extensions;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Authorization.Users.Events;

namespace AqualLifeStyle.Authorization.Users
{
    public class User : AbpUser<User>, IGeneratesDomainEvents
    {
        public const string DefaultPassword = "123qwe";
        public const int MaxHomeAddressLength = 512;

        public AquaUserRole Role { get; private set; } = AquaUserRole.Guest;

        public string HomeAddress { get; private set; }

        public ICollection<IEventData> DomainEvents { get; } = new List<IEventData>();

        public static string CreateRandomPassword()
        {
            return Guid.NewGuid().ToString("N").Truncate(16);
        }

        public static User CreateTenantAdminUser(int tenantId, string emailAddress)
        {
            var user = new User
            {
                TenantId = tenantId,
                UserName = AdminUserName,
                Name = AdminUserName,
                Surname = AdminUserName,
                EmailAddress = emailAddress,
                Roles = new List<UserRole>(),
                Role = AquaUserRole.SystemAdmin
            };

            user.SetNormalizedNames();

            return user;
        }

        public bool IsSystemAdmin() => Role == AquaUserRole.SystemAdmin;

        public bool IsAreaLeader() => Role == AquaUserRole.AreaLeader;

        public bool IsFacilitator() => Role == AquaUserRole.Facilitator;

        public bool IsMember() => Role == AquaUserRole.Member;

        public bool IsGuest() => Role == AquaUserRole.Guest;

        public bool RequiresPasswordReset() => !PasswordResetCode.IsNullOrWhiteSpace();

        public void RequirePasswordReset()
        {
            PasswordResetCode = Guid.NewGuid().ToString("N");
        }

        public void CompleteRequiredPasswordReset()
        {
            PasswordResetCode = null;
        }

        public void UpdateContactDetails(string contactNumber, string homeAddress)
        {
            if (string.IsNullOrWhiteSpace(contactNumber))
            {
                throw new ArgumentException("The contact number is required.", nameof(contactNumber));
            }

            var normalizedContactNumber = contactNumber.Trim();
            if (normalizedContactNumber.Length > MaxPhoneNumberLength)
            {
                throw new ArgumentException($"The contact number cannot exceed {MaxPhoneNumberLength} characters.", nameof(contactNumber));
            }

            if (string.IsNullOrWhiteSpace(homeAddress))
            {
                throw new ArgumentException("The home address is required.", nameof(homeAddress));
            }

            var normalizedHomeAddress = homeAddress.Trim();
            if (normalizedHomeAddress.Length > MaxHomeAddressLength)
            {
                throw new ArgumentException($"The home address cannot exceed {MaxHomeAddressLength} characters.", nameof(homeAddress));
            }

            PhoneNumber = normalizedContactNumber;
            HomeAddress = normalizedHomeAddress;
        }

        public void SetRole(AquaUserRole role)
        {
            if (!Enum.IsDefined(typeof(AquaUserRole), role))
            {
                throw new ArgumentException($"'{role}' is not a valid user role.", nameof(role));
            }

            if (Role == role)
            {
                return;
            }

            var oldRole = Role;
            Role = role;
            DomainEvents.Add(new UserRoleChangedEvent(Id, oldRole, role));
        }
    }
}
