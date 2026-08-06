using System;
using System.ComponentModel.DataAnnotations;
using Abp.Auditing;
using Abp.Authorization.Users;
using AqualLifeStyle.Authorization.Accounts;

namespace AqualLifeStyle.Application.InternalAccounts.Dto
{
    public class ValidateInternalAccountInvitationInput
    {
        [Required, StringLength(128), DisableAuditing]
        public string InvitationCode { get; set; }

        [Required, DisableAuditing]
        public string SetupToken { get; set; }
    }

    public class AcceptInternalAccountInvitationInput : ValidateInternalAccountInvitationInput
    {
        [Required, StringLength(AbpUserBase.MaxPlainPasswordLength, MinimumLength = 8), DisableAuditing]
        [RegularExpression(AccountAppService.PasswordRegex)]
        public string NewPassword { get; set; }
    }

    public class InternalAccountInvitationPreviewDto
    {
        public string AreaName { get; set; }
        public string AreaDisplayName { get; set; }
        public string InviteeName { get; set; }
        public string Username { get; set; }
        public string AccessLevel { get; set; }
        public string Status { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    public class AcceptInternalAccountInvitationOutput
    {
        public bool WasAlreadyAccepted { get; set; }
        public string AreaName { get; set; }
    }
}
