using System.ComponentModel.DataAnnotations;
using Abp.Auditing;
using Abp.Authorization.Users;

namespace AqualLifeStyle.Authorization.Accounts.Dto
{
    public class CompletePasswordSetupInput
    {
        [Required, StringLength(64)]
        public string AreaName { get; set; }

        [Range(1, long.MaxValue)]
        public long UserId { get; set; }

        [Required, DisableAuditing]
        public string ResetToken { get; set; }

        [Required, StringLength(AbpUserBase.MaxPlainPasswordLength, MinimumLength = 8), DisableAuditing]
        [RegularExpression(AccountAppService.PasswordRegex)]
        public string NewPassword { get; set; }
    }
}
