using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Abp.Auditing;
using Magicodes.ExporterAndImporter.Core;
using Magicodes.ExporterAndImporter.Excel;

namespace AqualLifeStyle.Application.Admin.Import.Dto
{
    public class PreviewCustomerImportInput
    {
        [Required, StringLength(255)]
        public string FileName { get; set; }

        [Required, DisableAuditing]
        public string ContentBase64 { get; set; }

        public int? TenantId { get; set; }
    }

    public class ConfirmCustomerImportInput
    {
        [Required, StringLength(64)]
        public string PreviewId { get; set; }
    }

    public class CustomerImportPreviewDto
    {
        public string PreviewId { get; set; }
        public string FileName { get; set; }
        public int TotalRows { get; set; }
        public int ValidRows { get; set; }
        public bool CanImport { get; set; }
        public IReadOnlyList<CustomerImportRowDto> Rows { get; set; }
        public IReadOnlyList<CustomerImportErrorDto> Errors { get; set; }
    }

    public class CustomerImportResultDto
    {
        public int TotalRows { get; set; }
        public int ImportedRows { get; set; }
        public int FailedRows { get; set; }
        public IReadOnlyList<CustomerImportErrorDto> Errors { get; set; }
    }

    public class CustomerImportRowDto
    {
        public int RowNumber { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public int? MembershipId { get; set; }
        public bool IsActive { get; set; }
    }

    public class CustomerImportErrorDto
    {
        public int RowNumber { get; set; }
        public string Field { get; set; }
        public string Message { get; set; }
    }

    [ExcelImporter(IsIgnoreColumnCase = true)]
    public class CustomerImportFileRow
    {
        [Required, StringLength(64), ImporterHeader(Name = "FirstName", AutoTrim = true)]
        public string FirstName { get; set; }

        [Required, StringLength(64), ImporterHeader(Name = "LastName", AutoTrim = true)]
        public string LastName { get; set; }

        [Required, EmailAddress, StringLength(256), ImporterHeader(Name = "Email", AutoTrim = true)]
        public string Email { get; set; }

        [ImporterHeader(Name = "MembershipId", AutoTrim = true)]
        public string MembershipId { get; set; }

        [ImporterHeader(Name = "IsActive", AutoTrim = true)]
        public string IsActive { get; set; }
    }

    [Serializable]
    public class CachedCustomerImportPreview
    {
        public int TenantId { get; set; }
        public long UserId { get; set; }
        public string FileName { get; set; }
        public List<CachedCustomerImportRow> Rows { get; set; }
    }

    [Serializable]
    public class CachedCustomerImportRow
    {
        public int RowNumber { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public int? MembershipId { get; set; }
        public bool IsActive { get; set; }
    }
}
