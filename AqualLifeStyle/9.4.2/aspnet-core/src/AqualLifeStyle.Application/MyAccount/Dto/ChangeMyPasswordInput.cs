using System.ComponentModel.DataAnnotations;
using Abp.Auditing;
using Abp.Authorization.Users;
using AqualLifeStyle.Authorization.Accounts;

namespace AqualLifeStyle.Application.MyAccount.Dto
{
    public class ChangeMyPasswordInput
    {
        [Required]
        [StringLength(AbpUserBase.MaxPlainPasswordLength)]
        [DisableAuditing]
        public string CurrentPassword { get; set; }

        [Required]
        [StringLength(AbpUserBase.MaxPlainPasswordLength, MinimumLength = 8)]
        [RegularExpression(AccountAppService.PasswordRegex)]
        [DisableAuditing]
        public string NewPassword { get; set; }
    }
}
