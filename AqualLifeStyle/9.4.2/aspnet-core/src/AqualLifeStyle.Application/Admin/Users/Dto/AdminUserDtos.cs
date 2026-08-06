using System;
using System.ComponentModel.DataAnnotations;
using Abp.Application.Services.Dto;
using Abp.Auditing;
using Abp.Authorization.Users;
using AqualLifeStyle.Domain.Enums;

namespace AqualLifeStyle.Application.Admin.Users.Dto
{
    public class AdminUserListInput : PagedResultRequestDto
    {
        [StringLength(256)] public string Keyword { get; set; }
        [Range(1, int.MaxValue)] public int? TenantId { get; set; }
        public bool? IsActive { get; set; }
        public AquaUserRole? Role { get; set; }
    }

    public class AdminUserDto : EntityDto<long>
    {
        public int? TenantId { get; set; }
        public string UserName { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public bool IsActive { get; set; }
        public AquaUserRole Role { get; set; }
        public DateTime CreationTime { get; set; }
        public string InvitationStatus { get; set; }
        public DateTime? InvitationExpiresAt { get; set; }
        public bool RequiresPasswordSetup { get; set; }
    }

    [DisableAuditing]
    public class AdminCreateUserInput
    {
        [Range(1, int.MaxValue)] public int TenantId { get; set; }
        [Required, StringLength(AbpUserBase.MaxNameLength, MinimumLength = 1)] public string FirstName { get; set; }
        [Required, StringLength(AbpUserBase.MaxSurnameLength, MinimumLength = 1)] public string LastName { get; set; }
        [Required, EmailAddress, StringLength(AbpUserBase.MaxEmailAddressLength)] public string Email { get; set; }
        public AquaUserRole Role { get; set; }
        [Required, StringLength(500, MinimumLength = 3)] public string Justification { get; set; }
    }

    public class AdminUpdateUserInput : EntityDto<long>
    {
        [Required, StringLength(AbpUserBase.MaxNameLength, MinimumLength = 1)] public string FirstName { get; set; }
        [Required, StringLength(AbpUserBase.MaxSurnameLength, MinimumLength = 1)] public string LastName { get; set; }
        [Required, EmailAddress, StringLength(AbpUserBase.MaxEmailAddressLength)] public string Email { get; set; }
        public bool IsActive { get; set; }
        [Required, StringLength(500, MinimumLength = 3)] public string Justification { get; set; }
    }

    public class AdminAssignUserRoleInput : EntityDto<long>
    {
        public AquaUserRole Role { get; set; }
        [Required, StringLength(500, MinimumLength = 3)] public string Justification { get; set; }
    }

    public class AdminResetUserPasswordInput : EntityDto<long>
    {
        [Required, StringLength(500, MinimumLength = 3)] public string Justification { get; set; }
    }

    public class AdminDeleteUserInput : EntityDto<long>
    {
        [Required, StringLength(500, MinimumLength = 3)] public string Justification { get; set; }
    }

    public class AdminUserInvitationActionInput : EntityDto<long>
    {
        [Required, StringLength(500, MinimumLength = 3)] public string Justification { get; set; }
    }
}
