using System.ComponentModel.DataAnnotations;
using Abp.Auditing;
using Abp.Authorization.Users;

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
        [DisableAuditing]
        public string NewPassword { get; set; }
    }
}
