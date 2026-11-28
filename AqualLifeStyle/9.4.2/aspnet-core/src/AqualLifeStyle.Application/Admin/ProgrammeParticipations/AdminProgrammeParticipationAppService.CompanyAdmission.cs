using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using Abp.Authorization;
using Abp.Domain.Uow;
using Abp.Runtime.Session;
using AqualLifeStyle.Application.Admin.ProgrammeParticipations.Dto;
using AqualLifeStyle.Application.ProgrammeParticipations;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.Admin.ProgrammeParticipations
{
    public partial class AdminProgrammeParticipationAppService
    {
        public IAQGreenPlacementV2JoiningGate CompanyJoiningGate { get; set; }
        public ICurrentProgrammeTermsProvider CompanyJoiningTerms { get; set; }

        [AbpAuthorize(AquaPermissions.Admin.ProgrammeParticipations.Approve)]
        public async Task<List<AQGreenAdmissionTreeDto>> GetAQGreenAdmissionTreesAsync(int tenantId)
        {
            ValidateRequestedTenant(tenantId, "AQGreen networks");
            if (tenantId <= 0 || !AbpSession.TenantId.HasValue &&
                !await PermissionChecker.IsGrantedAsync(AquaPermissions.Admin.AllTenants))
                throw new AbpAuthorizationException("An authorized Tenant is required.");
            using (DisableAllTenantDataFiltersForHost())
            {
                var areas = await GetAuthorizedAreaScopeAsync(null);
                var roots = from placement in _networkPlacementRepository.GetAll().AsNoTracking()
                    join entry in _entryParticipationRepository.GetAll().AsNoTracking() on placement.ParticipantId equals entry.Id
                    join customer in _customerRepository.GetAll().AsNoTracking() on entry.CustomerId equals customer.Id
                    where placement.TenantId == tenantId && entry.TenantId == tenantId && customer.TenantId == tenantId &&
                        !placement.PlacementParentParticipantId.HasValue && entry.Status == EntryParticipationStatus.Active &&
                        customer.IsActive && customer.AreaId.HasValue && (areas == null || areas.Contains(customer.AreaId.Value))
                    select new AQGreenAdmissionTreeDto { ScopeId = placement.PlacementTreeScopeId,
                        RootMemberNumber = customer.ClubMemberNumber,
                        HistoricalHold = HistoricalTreeReviews.GetAll().Any(r => r.TenantId == tenantId &&
                            r.ScopeId == placement.PlacementTreeScopeId && !r.ClearedAt.HasValue) };
                return await roots.OrderBy(t => t.RootMemberNumber).ToListAsync();
            }
        }

        [AbpAuthorize(AquaPermissions.Admin.Customers.Create, AquaPermissions.Admin.ProgrammeParticipations.Approve,
            RequireAllPermissions = true)]
        [UnitOfWork(IsDisabled = true)]
        public async Task<Guid> RegisterAQGreenCompanyAdmissionAsync(RegisterAQGreenCompanyAdmissionInput input)
        {
            if (input == null || input.TenantId <= 0 || input.CustomerId <= 0 || input.ScopeId == Guid.Empty ||
                string.IsNullOrWhiteSpace(input.AcquisitionEvidence) || input.AcquisitionEvidence.Trim().Length > 500)
                throw Failed("Company registration", "Select a customer and eligible network and record the acquisition evidence reference.");

            ValidateRequestedTenant(input.TenantId, "Company registration");
            if (!AbpSession.TenantId.HasValue && !await PermissionChecker.IsGrantedAsync(AquaPermissions.Admin.AllTenants))
                throw new AbpAuthorizationException("Host company registration requires All Tenants authority.");
            if (!await CompanyJoiningGate.IsEnabledAsync(input.TenantId))
                throw Failed("Company registration", "AQGreen V2 joining is not available for this Tenant.");

            using var uow = _unitOfWorkManager.Begin(new UnitOfWorkOptions
                { IsTransactional = true, IsolationLevel = IsolationLevel.ReadCommitted });
            using (DisableAllTenantDataFiltersForHost())
            {
                var rootHint = await GetCompanyRootCustomerAsync(input.TenantId, input.ScopeId);
                await _hostedPaymentCheckoutLock.AcquireCustomerAreaTransitionsAsync(input.CustomerId, rootHint.Id);
                var customer = await _customerRepository.FirstOrDefaultAsync(c => c.TenantId == input.TenantId && c.Id == input.CustomerId);
                var root = await GetCompanyRootCustomerAsync(input.TenantId, input.ScopeId);
                if (customer == null || !customer.IsActive || root.Id != rootHint.Id || root.Id == customer.Id)
                    throw Failed("Company registration", "An eligible customer and a separate active network are required.");
                var authorityActor = AbpSession.TenantId.HasValue ? AbpSession.GetUserId() : (long?)null;
                var customerArea = await _approvalAuthorityStabilizer.StabilizeAsync(input.TenantId, customer.Id, authorityActor);
                var rootArea = await _approvalAuthorityStabilizer.StabilizeAsync(input.TenantId, root.Id, authorityActor);
                if (customer.AreaId != customerArea || root.AreaId != rootArea)
                    throw Failed("Company registration", "The Area assignment changed. Reload before proceeding.");
                await EnsureCanAdministerAreaAsync(customer);
                await EnsureCanAdministerAreaAsync(root);
                if (!customer.AreaId.HasValue) throw Failed("Company registration", "Assign an eligible Area before registration.");
                await CompanyJoiningGate.ValidateDestinationAsync(input.TenantId, customer.AreaId.Value);
                await _placementTreeLock.AcquireAsync(input.ScopeId);

                var existing = await _entryParticipationRepository.FirstOrDefaultAsync(p =>
                    p.TenantId == input.TenantId && p.CustomerId == customer.Id);
                if (existing != null)
                {
                    var attribution = await _attributionRepository.FirstOrDefaultAsync(a => a.TenantId == input.TenantId && a.ParticipantId == existing.Id);
                    if (attribution?.AttributionKind != AQGreenRecruitmentAttributionKind.CompanyAdmission ||
                        attribution.SourceReferenceId != input.ScopeId || attribution.AssignmentReason != input.AcquisitionEvidence.Trim())
                        throw Failed("Company registration", "This customer already has a different AQGreen registration.");
                    await uow.CompleteAsync();
                    return existing.Id;
                }

                using (CurrentUnitOfWork.DisableFilter(AbpDataFilters.MayHaveTenant, AbpDataFilters.SoftDelete))
                {
                    if (await QueryLegacyAQGreenMembershipEvidence(_customerRepository.GetAll().Where(c =>
                        c.Id == customer.Id && c.TenantId == input.TenantId)).AnyAsync(e =>
                            e.LegacyMembershipId.HasValue || e.MembershipAssignmentUnverifiable) ||
                        await _entryParticipationRepository.GetAll().AnyAsync(p => p.TenantId == input.TenantId && p.CustomerId == customer.Id) ||
                        await _paymentRepository.GetAll().AnyAsync(p => p.TenantId == input.TenantId && p.CustomerId == customer.Id &&
                            (p.Purpose == MemberPaymentPurpose.AQGreenJoining ||
                             p.Purpose == MemberPaymentPurpose.EntryRegistration || p.Purpose == MemberPaymentPurpose.EntryActivation)))
                        throw Failed("Company registration", "Existing AQGreen records must be reconciled before requesting a joining payment.");
                }

                if (await HistoricalTreeReviews.GetAll().AnyAsync(r => r.TenantId == input.TenantId && r.ScopeId == input.ScopeId && !r.ClearedAt.HasValue))
                    throw Failed("Company registration", "The selected network is held for historical review.");
                var now = await _placementClock.GetUtcNowAsync();
                var entry = EntryParticipation.StartIndependently(input.TenantId, customer.Id, CompanyJoiningTerms.GetEntryTerms(), now);
                await _entryParticipationRepository.InsertAsync(entry);
                await _attributionRepository.InsertAsync(AQGreenRecruitmentAttribution.Create(input.TenantId, entry.Id, null,
                    AQGreenRecruitmentAttributionKind.CompanyAdmission, AQGreenAcquisitionSource.CompanyAcquisition,
                    input.ScopeId, now, AbpSession.GetUserId(), input.AcquisitionEvidence, AQGreenRecruitmentAttributionRules.CurrentVersion));
                await uow.CompleteAsync();
                return entry.Id;
            }
        }

        private async Task<Customer> GetCompanyRootCustomerAsync(int tenantId, Guid scopeId)
        {
            var customer = await (from p in _networkPlacementRepository.GetAll().AsNoTracking()
                join e in _entryParticipationRepository.GetAll().AsNoTracking() on p.ParticipantId equals e.Id
                join c in _customerRepository.GetAll().AsNoTracking() on e.CustomerId equals c.Id
                where p.TenantId == tenantId && e.TenantId == tenantId && c.TenantId == tenantId &&
                      p.PlacementTreeScopeId == scopeId && !p.PlacementParentParticipantId.HasValue &&
                      e.Status == EntryParticipationStatus.Active && c.IsActive
                select c).SingleOrDefaultAsync();
            return customer ?? throw Failed("Company registration", "An eligible AQGreen network was not found.");
        }
    }
}
