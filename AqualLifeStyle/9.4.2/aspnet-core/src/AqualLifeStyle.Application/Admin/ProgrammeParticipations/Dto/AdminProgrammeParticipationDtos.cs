using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Abp.Application.Services.Dto;

namespace AqualLifeStyle.Application.Admin.ProgrammeParticipations.Dto
{
    public enum AdminProgrammeType
    {
        Entry = 0,
        Onyx = 1
    }

    public class AdminProgrammeParticipationListInput : PagedResultRequestDto
    {
        [StringLength(256)]
        public string Keyword { get; set; }

        [Range(1, int.MaxValue)]
        public int? TenantId { get; set; }

        public AdminProgrammeType Programme { get; set; }
    }

    public class AdminProgrammePaymentDto
    {
        public Guid Id { get; set; }
        public string Description { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
        public string Provider { get; set; }
        public string ProviderReference { get; set; }
        public DateTime ConfirmedAt { get; set; }
    }

    public class AdminProgrammeParticipationDto
    {
        public Guid Id { get; set; }
        public int TenantId { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string Email { get; set; }
        public string ProgrammeName { get; set; }
        public string Status { get; set; }
        public bool IsActive { get; set; }
        public bool JoinedIndependently { get; set; }
        public int? RecruiterCustomerId { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? ActivatedAt { get; set; }
        public decimal? NextPaymentAmount { get; set; }
        public string NextPaymentDescription { get; set; }
        public string Currency { get; set; }
        public IReadOnlyList<AdminProgrammePaymentDto> ConfirmedPayments { get; set; }
    }
}
