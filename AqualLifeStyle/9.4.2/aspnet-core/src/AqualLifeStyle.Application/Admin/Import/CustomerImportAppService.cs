using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Abp.Auditing;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.Runtime.Caching;
using Abp.Runtime.Session;
using Abp.UI;
using AqualLifeStyle.Application.Admin.Import.Dto;
using AqualLifeStyle.Application.Admin.Customers;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Memberships;
using Magicodes.ExporterAndImporter.Core;
using Magicodes.ExporterAndImporter.Core.Models;
using Magicodes.ExporterAndImporter.Csv;
using Magicodes.ExporterAndImporter.Excel;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.Admin.Import
{
    [Audited]
    public class CustomerImportAppService : AqualLifeStyleAppServiceBase, ICustomerImportAppService
    {
        private const int MaxFileBytes = 5 * 1024 * 1024;
        private const int MaxRows = 1000;
        private const int PreviewRows = 10;
        private const string CacheName = "CustomerImportPreviews";
        private static readonly TimeSpan PreviewLifetime = TimeSpan.FromMinutes(15);
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        private readonly ICustomerRepository _customerRepository;
        private readonly IMembershipRepository _membershipRepository;
        private readonly IRepository<User, long> _userRepository;
        private readonly IAdminCustomerAccountManager _accountManager;
        private readonly IUnitOfWorkManager _unitOfWorkManager;
        private readonly ITypedCache<string, CachedCustomerImportPreview> _previewCache;

        public CustomerImportAppService(
            ICustomerRepository customerRepository,
            IMembershipRepository membershipRepository,
            IRepository<User, long> userRepository,
            IAdminCustomerAccountManager accountManager,
            IUnitOfWorkManager unitOfWorkManager,
            ICacheManager cacheManager)
        {
            _customerRepository = customerRepository;
            _membershipRepository = membershipRepository;
            _userRepository = userRepository;
            _accountManager = accountManager;
            _unitOfWorkManager = unitOfWorkManager;
            _previewCache = cacheManager.GetCache<string, CachedCustomerImportPreview>(CacheName);
        }

        [AbpAuthorize(AquaPermissions.Admin.Customers.Import)]
        public async Task<CustomerImportPreviewDto> PreviewAsync(PreviewCustomerImportInput input)
        {
            if (input == null)
                throw new UserFriendlyException("Import preview failed.", "The request body was empty.");

            var actorId = AbpSession.GetUserId();
            var tenantId = ResolveTenantId(input.TenantId);
            var safeFileName = Path.GetFileName(input.FileName ?? string.Empty);
            var extension = Path.GetExtension(safeFileName).ToLowerInvariant();
            var errors = new List<CustomerImportErrorDto>();
            var importResult = await ParseAsync(DecodeAndValidateFile(input.ContentBase64, extension), extension);

            AddParserErrors(importResult, errors);
            var parsedRows = importResult.Data?.ToList() ?? new List<CustomerImportFileRow>();
            if (parsedRows.Count == 0)
                errors.Add(Error(0, "File", "The file does not contain any customer rows."));
            else if (parsedRows.Count > MaxRows)
                errors.Add(Error(0, "File", $"The file contains {parsedRows.Count} rows; the maximum is {MaxRows}."));

            var rows = NormalizeRows(parsedRows, errors);
            using (CurrentUnitOfWork.SetTenantId(tenantId))
            {
                await ValidateBusinessRulesAsync(tenantId, rows, errors);
            }

            var errorRows = errors.Where(error => error.RowNumber > 0).Select(error => error.RowNumber).Distinct().Count();
            var canImport = errors.Count == 0 && rows.Count > 0 && rows.Count <= MaxRows;
            string previewId = null;
            if (canImport)
            {
                previewId = Guid.NewGuid().ToString("N");
                await _previewCache.SetAsync(CacheKey(actorId, previewId), new CachedCustomerImportPreview
                {
                    TenantId = tenantId,
                    UserId = actorId,
                    FileName = safeFileName,
                    Rows = rows
                }, PreviewLifetime);
            }

            Logger.Info($"Customer import preview actor={actorId} tenant={tenantId} file={safeFileName} rows={rows.Count} errors={errors.Count}");
            return new CustomerImportPreviewDto
            {
                PreviewId = previewId,
                FileName = safeFileName,
                TotalRows = rows.Count,
                ValidRows = Math.Max(0, rows.Count - errorRows),
                CanImport = canImport,
                Rows = rows.Take(PreviewRows).Select(ToDto).ToList(),
                Errors = errors
            };
        }

        [AbpAuthorize(AquaPermissions.Admin.Customers.Import)]
        [UnitOfWork(IsDisabled = true)]
        public async Task<CustomerImportResultDto> ImportAsync(ConfirmCustomerImportInput input)
        {
            if (input == null || string.IsNullOrWhiteSpace(input.PreviewId))
                throw new UserFriendlyException("Customer import failed.", "A valid preview is required before import.");

            var actorId = AbpSession.GetUserId();
            var cacheKey = CacheKey(actorId, input.PreviewId.Trim());
            var preview = await _previewCache.GetOrDefaultAsync(cacheKey);
            if (preview == null || preview.UserId != actorId)
                throw new UserFriendlyException("Customer import failed.", "The preview is invalid or has expired. Preview the file again.");

            EnsureTenantStillAllowed(preview.TenantId);
            var errors = new List<CustomerImportErrorDto>();
            var imported = 0;
            foreach (var row in preview.Rows)
            {
                try
                {
                    using (var uow = _unitOfWorkManager.Begin(new UnitOfWorkOptions { IsTransactional = true }))
                    using (_unitOfWorkManager.Current.SetTenantId(preview.TenantId))
                    {
                        await ImportRowAsync(preview.TenantId, row);
                        await uow.CompleteAsync();
                    }
                    imported++;
                }
                catch (Exception exception)
                {
                    Logger.Warn($"Customer import row failed actor={actorId} tenant={preview.TenantId} row={row.RowNumber} type={exception.GetType().Name}");
                    errors.Add(Error(row.RowNumber, "Row", ToSafeImportError(exception)));
                }
            }

            _previewCache.Remove(cacheKey);
            Logger.Info($"Customer import completed actor={actorId} tenant={preview.TenantId} file={preview.FileName} total={preview.Rows.Count} imported={imported} failed={errors.Count}");
            return new CustomerImportResultDto
            {
                TotalRows = preview.Rows.Count,
                ImportedRows = imported,
                FailedRows = preview.Rows.Count - imported,
                Errors = errors
            };
        }

        private async Task ImportRowAsync(int tenantId, CachedCustomerImportRow row)
        {
            var accountResult = await _accountManager.CreateOrFindRemovedAsync(new AdminCustomerAccountInput
            {
                TenantId = tenantId,
                FirstName = row.FirstName,
                LastName = row.LastName,
                Email = row.Email,
                ContactNumber = row.ContactNumber,
                HomeAddress = row.HomeAddress,
                MembershipId = row.MembershipId,
                IsActive = row.IsActive,
                AllowSystemGeneratedPassword = true,
            });
            if (accountResult.RemovedCustomer != null)
                throw new UserFriendlyException("Customer import requires review.", "This email belongs to a removed customer and must be restored explicitly.");
        }

        private async Task ValidateBusinessRulesAsync(int tenantId, List<CachedCustomerImportRow> rows,
            List<CustomerImportErrorDto> errors)
        {
            foreach (var duplicate in rows.GroupBy(row => row.Email, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1))
                foreach (var row in duplicate)
                    errors.Add(Error(row.RowNumber, "Email", "The email is duplicated in this file."));

            var normalizedEmails = rows.Select(row => row.Email.ToUpperInvariant()).Distinct().ToList();
            var existingUsers = await _userRepository.GetAll()
                .Where(user => normalizedEmails.Contains(user.NormalizedEmailAddress))
                .Select(user => user.NormalizedEmailAddress).ToListAsync();
            var existingCustomers = await _customerRepository.GetAll()
                .Where(customer => normalizedEmails.Contains(customer.Email.Value.ToUpper()))
                .Select(customer => customer.Email.Value).ToListAsync();
            var existing = new HashSet<string>(existingUsers.Concat(existingCustomers), StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows.Where(row => existing.Contains(row.Email)))
                errors.Add(Error(row.RowNumber, "Email", "A customer or user with this email already exists."));

            var membershipIds = rows.Where(row => row.MembershipId.HasValue).Select(row => row.MembershipId.Value).Distinct().ToList();
            var validMembershipIds = new HashSet<int>(await _membershipRepository.GetAll()
                .Where(item => item.TenantId == tenantId && item.IsActive && membershipIds.Contains(item.Id))
                .Select(item => item.Id).ToListAsync());
            foreach (var row in rows.Where(row => row.MembershipId.HasValue && !validMembershipIds.Contains(row.MembershipId.Value)))
                errors.Add(Error(row.RowNumber, "MembershipId", "The membership does not exist in this tenant or is inactive."));
        }

        private static List<CachedCustomerImportRow> NormalizeRows(IReadOnlyList<CustomerImportFileRow> parsedRows,
            List<CustomerImportErrorDto> errors)
        {
            var rows = new List<CachedCustomerImportRow>(Math.Min(parsedRows.Count, MaxRows));
            for (var index = 0; index < parsedRows.Count && index < MaxRows; index++)
            {
                var source = parsedRows[index];
                var rowNumber = index + 2;
                var firstName = source.FirstName?.Trim();
                var lastName = source.LastName?.Trim();
                var email = source.Email?.Trim();
                var contactNumber = source.ContactNumber?.Trim();
                var homeAddress = source.HomeAddress?.Trim();
                if (string.IsNullOrWhiteSpace(firstName)) errors.Add(Error(rowNumber, "FirstName", "First name is required."));
                else if (firstName.Length > 64) errors.Add(Error(rowNumber, "FirstName", "First name cannot exceed 64 characters."));
                if (string.IsNullOrWhiteSpace(lastName)) errors.Add(Error(rowNumber, "LastName", "Last name is required."));
                else if (lastName.Length > 64) errors.Add(Error(rowNumber, "LastName", "Last name cannot exceed 64 characters."));
                try { _ = new EmailAddress(email); }
                catch (ArgumentException) { errors.Add(Error(rowNumber, "Email", "Email is required and must be valid.")); }
                try
                {
                    var contactDetails = new User();
                    contactDetails.UpdateContactDetails(contactNumber, homeAddress);
                }
                catch (ArgumentException exception)
                {
                    var field = exception.ParamName == "homeAddress" ? "HomeAddress" : "ContactNumber";
                    errors.Add(Error(rowNumber, field, exception.Message));
                }

                int? membershipId = null;
                if (!string.IsNullOrWhiteSpace(source.MembershipId))
                {
                    if (!int.TryParse(source.MembershipId, out var parsedMembershipId) || parsedMembershipId <= 0)
                        errors.Add(Error(rowNumber, "MembershipId", "MembershipId must be a positive whole number."));
                    else membershipId = parsedMembershipId;
                }

                var isActive = true;
                if (!string.IsNullOrWhiteSpace(source.IsActive) && !bool.TryParse(source.IsActive, out isActive))
                    errors.Add(Error(rowNumber, "IsActive", "IsActive must be true or false."));

                rows.Add(new CachedCustomerImportRow
                {
                    RowNumber = rowNumber, FirstName = firstName, LastName = lastName, Email = email ?? string.Empty,
                    ContactNumber = contactNumber, HomeAddress = homeAddress,
                    MembershipId = membershipId, IsActive = isActive
                });
            }
            return rows;
        }

        private static async Task<ImportResult<CustomerImportFileRow>> ParseAsync(byte[] bytes, string extension)
        {
            using (var stream = new MemoryStream(bytes, false))
            {
                IImporter importer = extension == ".csv" ? (IImporter)new CsvImporter() : new ExcelImporter();
                try { return await importer.Import<CustomerImportFileRow>(stream); }
                catch (Exception exception)
                {
                    throw new UserFriendlyException("Import preview failed.", "The file could not be parsed. Verify the template and try again.", exception);
                }
            }
        }

        private static byte[] DecodeAndValidateFile(string base64, string extension)
        {
            if (extension != ".csv" && extension != ".xlsx")
                throw new UserFriendlyException("Import preview failed.", "Only .csv and .xlsx files are supported.");
            byte[] bytes;
            try { bytes = Convert.FromBase64String(base64 ?? string.Empty); }
            catch (FormatException exception)
            {
                throw new UserFriendlyException("Import preview failed.", "The uploaded file content is invalid.", exception);
            }
            if (bytes.Length == 0 || bytes.Length > MaxFileBytes)
                throw new UserFriendlyException("Import preview failed.", "The file must be between 1 byte and 5 MB.");
            if (extension == ".xlsx" && (bytes.Length < 4 || bytes[0] != 0x50 || bytes[1] != 0x4B))
                throw new UserFriendlyException("Import preview failed.", "The file content is not a valid .xlsx package.");
            if (extension == ".csv")
            {
                if (bytes.Contains((byte)0)) throw new UserFriendlyException("Import preview failed.", "The CSV file contains binary data.");
                try { StrictUtf8.GetString(bytes); }
                catch (DecoderFallbackException exception)
                {
                    throw new UserFriendlyException("Import preview failed.", "CSV files must use UTF-8 encoding.", exception);
                }
            }
            return bytes;
        }

        private static void AddParserErrors(ImportResult<CustomerImportFileRow> result, ICollection<CustomerImportErrorDto> errors)
        {
            foreach (var templateError in result.TemplateErrors ?? Array.Empty<TemplateErrorInfo>())
                errors.Add(Error(0, templateError.ColumnName ?? "Template", templateError.Message ?? "The import template is invalid."));
            foreach (var rowError in result.RowErrors ?? Array.Empty<DataRowErrorInfo>())
                foreach (var fieldError in rowError.FieldErrors ?? new Dictionary<string, string>())
                    errors.Add(Error(rowError.RowIndex, fieldError.Key, fieldError.Value));
        }

        private int ResolveTenantId(int? requestedTenantId)
        {
            if (AbpSession.TenantId.HasValue)
            {
                if (requestedTenantId.HasValue && requestedTenantId != AbpSession.TenantId)
                    throw new AbpAuthorizationException("Cross-tenant customer imports are not allowed.");
                return AbpSession.TenantId.Value;
            }
            if (!requestedTenantId.HasValue || requestedTenantId.Value <= 0)
                throw new UserFriendlyException("Import preview failed.", "Host administrators must select a tenant.");
            return requestedTenantId.Value;
        }

        private void EnsureTenantStillAllowed(int tenantId)
        {
            if (AbpSession.TenantId.HasValue && AbpSession.TenantId.Value != tenantId)
                throw new AbpAuthorizationException("Cross-tenant customer imports are not allowed.");
        }

        private static string ToSafeImportError(Exception exception)
        {
            if (exception is UserFriendlyException friendly) return friendly.Details ?? friendly.Message;
            if (exception is AbpAuthorizationException) return "Authorization failed while importing this row.";
            return "The row could not be imported. Preview the file again and retry.";
        }
        private static string CacheKey(long userId, string previewId) => $"{userId}:{previewId}";
        private static CustomerImportErrorDto Error(int row, string field, string message) =>
            new CustomerImportErrorDto { RowNumber = row, Field = field, Message = message };
        private static CustomerImportRowDto ToDto(CachedCustomerImportRow row) => new CustomerImportRowDto
        {
            RowNumber = row.RowNumber, FirstName = row.FirstName, LastName = row.LastName, Email = row.Email,
            ContactNumber = row.ContactNumber, HomeAddress = row.HomeAddress,
            MembershipId = row.MembershipId, IsActive = row.IsActive
        };
    }
}
