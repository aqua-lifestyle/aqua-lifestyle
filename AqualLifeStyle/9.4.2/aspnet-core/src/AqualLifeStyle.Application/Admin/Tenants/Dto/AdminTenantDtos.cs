using System.ComponentModel.DataAnnotations;
using Abp.Application.Services.Dto;
using Abp.Authorization.Users;
using Abp.MultiTenancy;

namespace AqualLifeStyle.Application.Admin.Tenants.Dto
{
    public class AdminTenantListInput : PagedResultRequestDto
    {
        [StringLength(256)] public string Keyword { get; set; }
        public bool? IsActive { get; set; }
    }
    public class AdminTenantDto : EntityDto<int>
    {
        public string TenancyName { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; }
        public int? AreaLeaderId { get; set; }
        public string AreaLeaderName { get; set; }
    }
    public class CreateAdminTenantInput
    {
        [Required, StringLength(AbpTenantBase.MaxTenancyNameLength), RegularExpression(AbpTenantBase.TenancyNameRegex)] public string TenancyName { get; set; }
        [Required, StringLength(AbpTenantBase.MaxNameLength)] public string Name { get; set; }
        [Required, EmailAddress, StringLength(AbpUserBase.MaxEmailAddressLength)] public string AdminEmailAddress { get; set; }
        [StringLength(AbpTenantBase.MaxConnectionStringLength)] public string ConnectionString { get; set; }
        public bool IsActive { get; set; } = true;
        [Required, StringLength(500, MinimumLength = 3)] public string Justification { get; set; }
    }
    public class EditAdminTenantInput : EntityDto<int>
    {
        [Required, StringLength(AbpTenantBase.MaxTenancyNameLength), RegularExpression(AbpTenantBase.TenancyNameRegex)] public string TenancyName { get; set; }
        [Required, StringLength(AbpTenantBase.MaxNameLength)] public string Name { get; set; }
        [Required, StringLength(500, MinimumLength = 3)] public string Justification { get; set; }
    }
    public class SetTenantActivationInput : EntityDto<int>
    {
        public bool IsActive { get; set; }
        [Required, StringLength(500, MinimumLength = 3)] public string Justification { get; set; }
    }
    public class AssignTenantAreaLeaderInput : EntityDto<int>
    {
        [Range(1, int.MaxValue)] public int AreaLeaderId { get; set; }
        [Required, StringLength(500, MinimumLength = 3)] public string Justification { get; set; }
    }
}
