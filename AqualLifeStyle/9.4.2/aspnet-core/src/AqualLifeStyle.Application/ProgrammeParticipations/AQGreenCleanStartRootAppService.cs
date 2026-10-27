using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Abp.Application.Services;
using Abp.Auditing;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.Runtime.Session;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Areas;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.ProgrammeParticipations
{
    public sealed class CleanStartRootInput
    {
        [Range(1,1)] public int TenantId { get; set; }
        [Required, StringLength(500)] public string Reason { get; set; }
    }
    public interface IAQGreenCleanStartRootAppService : IApplicationService
    { Task<Guid> AuthorizeRootAsync(CleanStartRootInput input); }

    // The existing joining API requires an active sponsor invitation. An empty
    // Tenant therefore needs exactly one separately owner-selected prospective
    // root. This creates neither a payment nor activation nor a placement.
    [Audited]
    public class AQGreenCleanStartRootAppService : AqualLifeStyleAppServiceBase, IAQGreenCleanStartRootAppService
    {
        private readonly IAQGreenD10CommissioningSource _source;
        private readonly AQGreenD10Runtime _runtime;
        private readonly ICustomerRepository _customers;
        private readonly IRepository<EntryParticipation,Guid> _participations;
        private readonly IRepository<AQGreenProspectiveRootAdmission,Guid> _admissions;
        private readonly IRepository<AQGreenRecruitmentAttribution,Guid> _attributions;
        private readonly IRepository<AQGreenRecruitmentAttributionConfirmation,Guid> _confirmations;
        private readonly IRepository<CustomerAreaAssignment,Guid> _assignments;
        private readonly IRepository<Area,Guid> _areas;
        private readonly IHostedPaymentCheckoutLock _locks;
        private readonly ICurrentProgrammeTermsProvider _terms;
        public AQGreenCleanStartRootAppService(IAQGreenD10CommissioningSource source, AQGreenD10Runtime runtime,
            ICustomerRepository customers, IRepository<EntryParticipation,Guid> participations,
            IRepository<AQGreenProspectiveRootAdmission,Guid> admissions, IRepository<AQGreenRecruitmentAttribution,Guid> attributions,
            IRepository<AQGreenRecruitmentAttributionConfirmation,Guid> confirmations,
            IRepository<CustomerAreaAssignment,Guid> assignments, IRepository<Area,Guid> areas,
            IHostedPaymentCheckoutLock locks, ICurrentProgrammeTermsProvider terms)
        { _source=source;_runtime=runtime;_customers=customers;_participations=participations;_admissions=admissions;
            _attributions=attributions;_confirmations=confirmations;_assignments=assignments;_areas=areas;_locks=locks;_terms=terms; }

        [AbpAuthorize(AquaPermissions.Admin.ProgrammeParticipations.BootstrapAQGreenRoot,
            AquaPermissions.Admin.AllTenants, RequireAllPermissions=true)]
        [UnitOfWork(true)]
        public virtual async Task<Guid> AuthorizeRootAsync(CleanStartRootInput input)
        {
            if(AbpSession.TenantId.HasValue || !AbpSession.UserId.HasValue)
                throw new AbpAuthorizationException("Clean-start root admission requires authorized host administration.");
            if(input==null || input.TenantId!=1 || string.IsNullOrWhiteSpace(input.Reason))
                throw new ArgumentException("Exact Tenant and root-admission reason are required.",nameof(input));
            using(CurrentUnitOfWork.SetTenantId(input.TenantId))
            {
                AQGreenD10Commissioning.Require(await _locks.HasCurrentReadTransactionAsync(), "D10_JOINING_REQUIRES_CURRENT_READ_TRANSACTION");
                var a=_source.Read();
                AQGreenD10Commissioning.Require(a?.CleanStartEvidence!=null && a.Decision.TenantId==input.TenantId &&
                    a.Decision.CleanStartRootCustomerId>0, "D10_CLEAN_START_ROOT_NOT_AUTHORIZED");
                AQGreenD10Commissioning.Require(await _runtime.ForTenantAsync(input.TenantId,true), "D10_CLEAN_START_ROOT_NOT_AUTHORIZED");
                var d=a.Decision;var customerId=d.CleanStartRootCustomerId.Value;
                await _locks.AcquireCustomerAreaTransitionsAsync(new[]{customerId});
                var c=await _customers.GetAll().SingleOrDefaultAsync(x=>x.Id==customerId && x.TenantId==input.TenantId);
                AQGreenD10Commissioning.Require(c!=null && c.IsActive && c.AreaId.HasValue && d.AuthorizedAreaIds.Contains(c.AreaId.Value),
                    "D10_AREA_OUTSIDE_AUTHORIZED_SCOPE");
                var assignments=await _assignments.GetAll().IgnoreQueryFilters().Where(x=>x.CustomerId==customerId &&
                    !x.IsDeleted && !x.EffectiveTo.HasValue).ToListAsync();
                AQGreenD10Commissioning.Require(assignments.Count==1 && assignments[0].TenantId==input.TenantId &&
                    assignments[0].AreaId==c.AreaId && assignments[0].EffectiveFrom<=DateTime.UtcNow &&
                    await _areas.GetAll().AnyAsync(x=>x.Id==c.AreaId && x.TenantId==input.TenantId && x.IsActive),
                    "D10_CURRENT_JOINING_AREA_UNVERIFIABLE");
                var prior=await _participations.GetAll().IgnoreQueryFilters().SingleOrDefaultAsync(x=>x.TenantId==input.TenantId && x.CustomerId==customerId);
                if(prior!=null)
                {
                    var admission=await _admissions.GetAll().SingleOrDefaultAsync(x=>x.TenantId==input.TenantId && x.ParticipantId==prior.Id);
                    var attribution=await _attributions.GetAll().SingleOrDefaultAsync(x=>x.TenantId==input.TenantId && x.ParticipantId==prior.Id);
                    var confirmation=attribution==null?null:await _confirmations.GetAll().SingleOrDefaultAsync(x=>x.TenantId==input.TenantId && x.AttributionId==attribution.Id);
                    AQGreenD10Commissioning.Require(!prior.IsDeleted && prior.StartedAt>=d.V2EffectiveAtUtc && admission!=null &&
                        admission.AdministratorUserId==AbpSession.GetUserId() && admission.Reason==input.Reason.Trim() &&
                        attribution?.SourceReferenceId==admission.Id && attribution.AttributionKind==AQGreenRecruitmentAttributionKind.AuthorisedProspectiveRoot &&
                        confirmation?.EvidenceReferenceId==admission.Id, "D10_CLEAN_START_ROOT_REPLAY_CONFLICT");
                    return prior.Id;
                }
                AQGreenD10Commissioning.Require(!await _participations.GetAll().IgnoreQueryFilters().AnyAsync(x=>x.TenantId==input.TenantId && !x.IsDeleted),
                    "D10_CLEAN_START_ROOT_POPULATION_NOT_EMPTY");
                var now=DateTime.UtcNow;
                var p=EntryParticipation.StartIndependently(input.TenantId,customerId,_terms.GetEntryTerms(),now);
                await _participations.InsertAsync(p);await CurrentUnitOfWork.SaveChangesAsync();
                var root=AQGreenProspectiveRootAdmission.Record(input.TenantId,p.Id,AbpSession.GetUserId(),now,input.Reason);
                await _admissions.InsertAsync(root);
                var attributionNew=AQGreenRecruitmentAttribution.Create(input.TenantId,p.Id,null,
                    AQGreenRecruitmentAttributionKind.AuthorisedProspectiveRoot,AQGreenAcquisitionSource.AuthorisedDirectAdmission,
                    root.Id,now,AbpSession.GetUserId(),root.Reason,AQGreenRecruitmentAttributionRules.CurrentVersion);
                await _attributions.InsertAsync(attributionNew);
                await _confirmations.InsertAsync(AQGreenRecruitmentAttributionConfirmation.Confirm(attributionNew,now,AbpSession.GetUserId(),
                    AQGreenAttributionConfirmationMethod.AuthorisedProspectiveRootConfirmation,root.Id,AQGreenRecruitmentAttributionRules.CurrentVersion));
                await CurrentUnitOfWork.SaveChangesAsync();return p.Id;
            }
        }
    }
}
