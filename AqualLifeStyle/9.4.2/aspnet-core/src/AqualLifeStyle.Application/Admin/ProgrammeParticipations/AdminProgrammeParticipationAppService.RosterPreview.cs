using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using Abp.Auditing;
using Abp.Authorization;
using Abp.Domain.Uow;
using Abp.Runtime.Session;
using AqualLifeStyle.Application.Admin.ProgrammeParticipations.Dto;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.AQGreen;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.Admin.ProgrammeParticipations
{
    public partial class AdminProgrammeParticipationAppService
    {
        [AbpAuthorize(AquaPermissions.Admin.Customers.Import,
            AquaPermissions.Admin.ProgrammeParticipations.ViewLegacyPaymentReconciliation, RequireAllPermissions = true)]
        [DisableAuditing] // Never persist the uploaded file, filename or member identifiers in ABP's audit log.
        [UnitOfWork(IsDisabled = true)]
        public async Task<LegacyAQGreenRosterPreviewDto> PreviewLegacyAQGreenRosterAsync(
            PreviewLegacyAQGreenRosterInput input)
        {
            if (input == null || input.TenantId <= 0)
                throw Failed("AQGreen roster preview", "Select a valid Tenant.");
            ValidateRequestedTenant(input.TenantId, "AQGreen roster preview");
            if (!AbpSession.TenantId.HasValue && !await PermissionChecker.IsGrantedAsync(AquaPermissions.Admin.AllTenants))
                throw new AbpAuthorizationException("Host-wide roster preview is not authorised.");

            using var uow = _unitOfWorkManager.Begin(new UnitOfWorkOptions
            {
                Scope = TransactionScopeOption.RequiresNew,
                IsTransactional = true,
                IsolationLevel = IsolationLevel.RepeatableRead
            });
            await _reconciliationEvidenceSnapshot.RequireReadOnlyTransactionAsync();
            using (DisableAllTenantDataFiltersForHost())
            {
                if (!await _tenantRepository.GetAll().AnyAsync(tenant => tenant.Id == input.TenantId && tenant.IsActive))
                    throw new AbpAuthorizationException("The requested roster Tenant is unavailable.");
                var areas = await GetAuthorizedAreaScopeAsync(requestedAreaId: null, effectiveAt: DateTime.UtcNow);
                if (areas is { Length: 0 })
                    throw new AbpAuthorizationException("AQGreen roster preview requires an active Area assignment.");

                var rows = await LegacyAQGreenRosterPreview.ReadRowsAsync(input);
                var emails = rows.Select(row => LegacyAQGreenRosterPreview.Key(row.Email)).Where(key => key.Length > 0).Distinct().ToArray();
                var clubNumbers = rows.Select(row => LegacyAQGreenRosterPreview.Key(row.ClubMemberNumber)).Where(key => key.Length > 0).Distinct().ToArray();

                using (CurrentUnitOfWork.DisableFilter(AbpDataFilters.SoftDelete))
                {
                    var visibleCustomers = _customerRepository.GetAll().AsNoTracking().Where(customer => customer.TenantId == input.TenantId);
                    if (areas != null)
                        visibleCustomers = visibleCustomers.Where(customer => customer.AreaId.HasValue && areas.Contains(customer.AreaId.Value));

                    var accounts = await visibleCustomers.Where(customer =>
                            clubNumbers.Contains(customer.ClubMemberNumber.ToUpper()) || emails.Contains(customer.Email.Value.ToUpper()) ||
                            (customer.User.TenantId == input.TenantId && emails.Contains(customer.User.NormalizedEmailAddress)))
                        .Select(customer => new LegacyAQGreenRosterAccount(input.TenantId, customer.Id, customer.ClubMemberNumber,
                            customer.Email.Value, customer.User.TenantId == input.TenantId ? customer.User.EmailAddress : null,
                            !customer.IsDeleted && customer.IsActive && customer.User.TenantId == input.TenantId &&
                            !customer.User.IsDeleted && customer.User.IsActive && customer.User.IsEmailConfirmed))
                        .ToListAsync();
                    var customerIds = accounts.Select(account => account.CustomerId).ToArray();
                    var tenantParticipations = _entryParticipationRepository.GetAll().AsNoTracking()
                        .Where(item => item.TenantId == input.TenantId);
                    var participations = await tenantParticipations.Where(item => customerIds.Contains(item.CustomerId)).ToListAsync();
                    var participationIds = participations.Select(item => item.Id).ToArray();
                    var attributions = await _attributionRepository.GetAll().AsNoTracking()
                        .Where(item => item.TenantId == input.TenantId && participationIds.Contains(item.ParticipantId))
                        .ToListAsync();
                    // Read slots only for owners the same reconciliation operator can see.
                    var placements = participationIds.Length == 0
                        ? new List<AQGreenNetworkPlacement>()
                        : await (from placement in _networkPlacementRepository.GetAll().AsNoTracking()
                                 join entry in tenantParticipations on placement.ParticipantId equals entry.Id
                                 join customer in visibleCustomers on entry.CustomerId equals customer.Id
                                 where placement.TenantId == input.TenantId &&
                                       (participationIds.Contains(placement.ParticipantId) ||
                                        (placement.PlacementParentParticipantId.HasValue &&
                                         participationIds.Contains(placement.PlacementParentParticipantId.Value)))
                                 select placement).ToListAsync();

                    var result = LegacyAQGreenRosterPreview.Classify(input.TenantId, rows, accounts, participations, placements, attributions);
                    // Dispose without completing; this preview has no durable state to commit.
                    Logger.Info($"AQGreen roster preview actor={AbpSession.GetUserId()} tenant={input.TenantId} rows={rows.Count} " +
                                $"accepted={result.AcceptedRows} duplicates={result.DuplicateRows} unmatched={result.UnmatchedRows} ambiguous={result.AmbiguousRows}");
                    return result;
                }
            }
        }
    }
}
