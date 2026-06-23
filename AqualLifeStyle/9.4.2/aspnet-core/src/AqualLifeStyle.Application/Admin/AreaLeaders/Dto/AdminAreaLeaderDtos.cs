using System;
using System.ComponentModel.DataAnnotations;
using Abp.Application.Services.Dto;

namespace AqualLifeStyle.Application.Admin.AreaLeaders.Dto
{
    public class AdminAreaLeaderListInput : PagedResultRequestDto
    {
        [StringLength(256)] public string Keyword { get; set; }
        [Range(1, int.MaxValue)] public int? TenantId { get; set; }
        public bool? IsApproved { get; set; }
    }

    public class AdminAreaLeaderDto : EntityDto<int>
    {
        public int TenantId { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string Email { get; set; }
        public int LicenseType { get; set; }
        public int Rank { get; set; }
        public bool IsApproved { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public int DirectReferrals { get; set; }
        public int IndirectReferrals { get; set; }
        public int OrderTarget { get; set; }
        public DateTime CreationTime { get; set; }
    }

    public abstract class AreaLeaderAdminMutationInput : EntityDto<int>
    {
        [Required, StringLength(500, MinimumLength = 3)] public string Justification { get; set; }
    }
    public class ApproveAreaLeaderInput : AreaLeaderAdminMutationInput { }
    public class PromoteAreaLeaderInput : AreaLeaderAdminMutationInput { }
    public class DemoteAreaLeaderInput : AreaLeaderAdminMutationInput { }
    public class RemoveAreaLeaderInput : AreaLeaderAdminMutationInput { }
}
