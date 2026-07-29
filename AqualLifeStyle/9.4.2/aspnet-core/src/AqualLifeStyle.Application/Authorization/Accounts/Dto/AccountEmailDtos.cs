using System.ComponentModel.DataAnnotations;
using Abp.Authorization.Users;
using Abp.Auditing;

namespace AqualLifeStyle.Authorization.Accounts.Dto
{
    public sealed class ConfirmEmailInput
    {
        public int TenantId { get; set; }
        public long UserId { get; set; }
        [Required, DisableAuditing] public string Token { get; set; }
    }

    public sealed class RequestAccountEmailInput
    {
        [Required, EmailAddress, StringLength(AbpUserBase.MaxEmailAddressLength)]
        public string EmailAddress { get; set; }
        [Required] public string AreaName { get; set; }
        [StringLength(2048)] public string RedirectPath { get; set; }
    }

    public sealed class CompletePasswordResetInput
    {
        public int TenantId { get; set; }
        public long UserId { get; set; }
        [Required, DisableAuditing] public string Token { get; set; }
        [Required, DisableAuditing, StringLength(AbpUserBase.MaxPlainPasswordLength)]
        public string NewPassword { get; set; }
    }

    public sealed class AccountEmailRequestOutput
    {
        public string Message { get; set; }
    }
}
