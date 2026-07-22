using System.ComponentModel.DataAnnotations;
using Abp.MultiTenancy;

namespace AqualLifeStyle.Authorization.Accounts.Dto
{
    public class GetTenantSelfRegistrationAvailabilityInput
    {
        [Required]
        [StringLength(AbpTenantBase.MaxTenancyNameLength)]
        public string TenancyName { get; set; }
    }
}
