using System;
using System.ComponentModel.DataAnnotations;
using Abp.Application.Services.Dto;
using Abp.Authorization.Users;

namespace AqualLifeStyle.Application.Admin.Customers.Dto
{
    public class AdminCustomerListInput : PagedResultRequestDto
    {
        [StringLength(256)]
        public string Keyword { get; set; }
        public int? TenantId { get; set; }
        public bool? IsActive { get; set; }
    }

    public class AdminCustomerMembershipOptionsInput
    {
        [Range(1, int.MaxValue)] public int TenantId { get; set; }
    }

    public class AdminCustomerDto : EntityDto<int>
    {
        public int TenantId { get; set; }
        public long UserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public int? MembershipId { get; set; }
        public string MembershipName { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime? LastModificationTime { get; set; }
    }

    public class AdminCreateCustomerInput
    {
        [Range(1, int.MaxValue)]
        public int TenantId { get; set; }

        [Required, StringLength(AbpUserBase.MaxNameLength, MinimumLength = 1)]
        public string FirstName { get; set; }

        [Required, StringLength(AbpUserBase.MaxSurnameLength, MinimumLength = 1)]
        public string LastName { get; set; }

        [Required, EmailAddress, StringLength(AbpUserBase.MaxEmailAddressLength)]
        public string Email { get; set; }

        [Range(1, int.MaxValue)]
        public int? MembershipId { get; set; }

        public bool IsActive { get; set; } = true;

        [Required, StringLength(500, MinimumLength = 3)]
        public string Justification { get; set; }
    }

    public class AdminUpdateCustomerInput : EntityDto<int>
    {
        [Required, StringLength(AbpUserBase.MaxNameLength, MinimumLength = 1)]
        public string FirstName { get; set; }

        [Required, StringLength(AbpUserBase.MaxSurnameLength, MinimumLength = 1)]
        public string LastName { get; set; }

        [Required, EmailAddress, StringLength(AbpUserBase.MaxEmailAddressLength)]
        public string Email { get; set; }

        [Range(1, int.MaxValue)]
        public int? MembershipId { get; set; }
        public bool IsActive { get; set; }

        [Required, StringLength(500, MinimumLength = 3)]
        public string Justification { get; set; }
    }

    public class AdminDeleteCustomerInput : EntityDto<int>
    {
        [Required, StringLength(500, MinimumLength = 3)]
        public string Justification { get; set; }
    }
}
