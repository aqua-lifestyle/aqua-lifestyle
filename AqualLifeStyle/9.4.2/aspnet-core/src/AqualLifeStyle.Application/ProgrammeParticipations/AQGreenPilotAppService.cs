using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using Abp.Application.Services;
using Abp.Authorization;
using Abp.Auditing;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.Runtime.Session;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.Payments;
using AqualLifeStyle.Payments.Yoco;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.ProgrammeParticipations
{
    public sealed class PilotRootAdmissionInput
    {
        [Range(1, int.MaxValue)] public int TenantId { get; set; }
        [Range(1, int.MaxValue)] public int CustomerId { get; set; }
        [Required, StringLength(500)] public string Reason { get; set; }
    }
    public sealed class PilotPaymentInput { public Guid CheckoutId { get; set; } }
    public interface IAQGreenPilotAppService : IApplicationService
    {
        Task<object> GetStatusAsync();
        Task<List<PilotProspectiveCustomerDto>> GetProspectiveCustomersAsync(int tenantId);
        Task<Guid> AuthorizeProspectiveRootAsync(PilotRootAdmissionInput input);
        Task<ProgrammePaymentConfirmationResult> ConfirmTestPaymentAsync(PilotPaymentInput input);
    }
    public sealed class PilotProspectiveCustomerDto
    {
        public int CustomerId { get; set; }
        public string Name { get; set; }
        public string ClubMemberNumber { get; set; }
    }

    [Audited]
    public class AQGreenPilotAppService : AqualLifeStyleAppServiceBase, IAQGreenPilotAppService
    {
        private readonly ICustomerRepository _customers;
        private readonly IRepository<EntryParticipation, Guid> _participations;
        private readonly IRepository<AQGreenProspectiveRootAdmission, Guid> _admissions;
        private readonly IRepository<AQGreenRecruitmentAttribution, Guid> _attributions;
        private readonly IRepository<AQGreenRecruitmentAttributionConfirmation, Guid> _confirmations;
        private readonly IRepository<AQGreenJoiningCheckout, Guid> _checkouts;
        private readonly IHostedPaymentCheckoutLock _locks;
        private readonly ICurrentProgrammeTermsProvider _terms;
        private readonly ProgrammePaymentConfirmationProcessor _payments;
        public AQGreenPilotAppService(ICustomerRepository customers,
            IRepository<EntryParticipation, Guid> participations,
            IRepository<AQGreenProspectiveRootAdmission, Guid> admissions,
            IRepository<AQGreenRecruitmentAttribution, Guid> attributions,
            IRepository<AQGreenRecruitmentAttributionConfirmation, Guid> confirmations,
            IRepository<AQGreenJoiningCheckout, Guid> checkouts,
            IHostedPaymentCheckoutLock locks, ICurrentProgrammeTermsProvider terms,
            ProgrammePaymentConfirmationProcessor payments)
        { _customers = customers; _participations = participations; _admissions = admissions;
          _attributions = attributions; _confirmations = confirmations; _checkouts = checkouts;
          _locks = locks; _terms = terms; _payments = payments; }

        [AbpAllowAnonymous]
        public Task<object> GetStatusAsync() => Task.FromResult<object>(new
        { IsPilot = PilotMode.IsEnabled, TestMoneyOnly = PilotMode.IsEnabled });

        [AbpAuthorize(AquaPermissions.Admin.ProgrammeParticipations.BootstrapAQGreenRoot,
            AquaPermissions.Admin.AllTenants, RequireAllPermissions = true)]
        [UnitOfWork]
        public async Task<List<PilotProspectiveCustomerDto>> GetProspectiveCustomersAsync(int tenantId)
        {
            PilotMode.RequirePilot();
            if (AbpSession.TenantId.HasValue || tenantId <= 0)
                throw new AbpAuthorizationException("Exact host-authorized Tenant scope is required.");
            using (CurrentUnitOfWork.SetTenantId(tenantId))
                return await _customers.GetAll().Where(customer => customer.TenantId == tenantId && customer.IsActive &&
                        customer.AreaId.HasValue && !_participations.GetAll().Any(entry => entry.CustomerId == customer.Id))
                    .OrderBy(customer => customer.ClubMemberNumber).Take(100)
                    .Select(customer => new PilotProspectiveCustomerDto { CustomerId = customer.Id,
                        Name = customer.Name, ClubMemberNumber = customer.ClubMemberNumber }).ToListAsync();
        }

        [AbpAuthorize(AquaPermissions.Admin.ProgrammeParticipations.BootstrapAQGreenRoot,
            AquaPermissions.Admin.AllTenants, RequireAllPermissions = true)]
        [UnitOfWork]
        public async Task<Guid> AuthorizeProspectiveRootAsync(PilotRootAdmissionInput input)
        {
            PilotMode.RequirePilot();
            if (AbpSession.TenantId.HasValue) throw new AbpAuthorizationException("Root admission requires authorized host administration.");
            if (input == null) throw new ArgumentNullException(nameof(input));
            using (CurrentUnitOfWork.SetTenantId(input.TenantId))
            {
                await _locks.AcquireCustomerAreaTransitionsAsync(new[] { input.CustomerId });
                var customer = await _customers.FirstOrDefaultAsync(item => item.Id == input.CustomerId && item.TenantId == input.TenantId);
                if (customer == null || !customer.IsActive || !customer.AreaId.HasValue)
                    throw new InvalidOperationException("An active customer in the exact Tenant and Area is required.");
                var entry = await _participations.FirstOrDefaultAsync(item => item.CustomerId == customer.Id);
                if (entry != null)
                {
                    var prior = await _admissions.FirstOrDefaultAsync(item => item.ParticipantId == entry.Id);
                    var attribution = await _attributions.FirstOrDefaultAsync(item => item.ParticipantId == entry.Id);
                    var confirmation = attribution == null ? null : await _confirmations.FirstOrDefaultAsync(item => item.AttributionId == attribution.Id);
                    if (prior == null || prior.TenantId != input.TenantId || prior.Reason != input.Reason?.Trim() ||
                        prior.AdministratorUserId != AbpSession.GetUserId() || attribution == null ||
                        attribution.SourceReferenceId != prior.Id || attribution.AttributionKind != AQGreenRecruitmentAttributionKind.AuthorisedProspectiveRoot ||
                        confirmation == null || confirmation.EvidenceReferenceId != prior.Id)
                        throw new InvalidOperationException("Existing participation cannot be reassigned or repaired by root admission.");
                    return entry.Id;
                }
                var now = DateTime.UtcNow;
                entry = EntryParticipation.StartIndependently(input.TenantId, customer.Id, _terms.GetEntryTerms(), now);
                await _participations.InsertAsync(entry);
                await CurrentUnitOfWork.SaveChangesAsync();
                var admission = AQGreenProspectiveRootAdmission.Record(input.TenantId, entry.Id, AbpSession.GetUserId(), now, input.Reason);
                await _admissions.InsertAsync(admission);
                var rootAttribution = AQGreenRecruitmentAttribution.Create(input.TenantId, entry.Id, null,
                    AQGreenRecruitmentAttributionKind.AuthorisedProspectiveRoot, AQGreenAcquisitionSource.AuthorisedDirectAdmission,
                    admission.Id, now, AbpSession.GetUserId(), admission.Reason, AQGreenRecruitmentAttributionRules.CurrentVersion);
                await _attributions.InsertAsync(rootAttribution);
                await _confirmations.InsertAsync(AQGreenRecruitmentAttributionConfirmation.Confirm(rootAttribution, now,
                    AbpSession.GetUserId(), AQGreenAttributionConfirmationMethod.AuthorisedProspectiveRootConfirmation,
                    admission.Id, AQGreenRecruitmentAttributionRules.CurrentVersion));
                await CurrentUnitOfWork.SaveChangesAsync();
                return entry.Id;
            }
        }

        [AbpAuthorize(AquaPermissions.ProgrammeParticipations.Join)]
        [UnitOfWork]
        public async Task<ProgrammePaymentConfirmationResult> ConfirmTestPaymentAsync(PilotPaymentInput input)
        {
            PilotMode.RequirePilot();
            var tenantId = GetRequiredTenantId("Test joining payment requires a member Tenant.");
            if (input == null || input.CheckoutId == Guid.Empty) throw new ArgumentException("An exact test checkout is required.");
            var checkout = await _checkouts.FirstOrDefaultAsync(item => item.Id == input.CheckoutId && item.TenantId == tenantId);
            var customer = checkout == null ? null : await _customers.FirstOrDefaultAsync(item => item.Id == checkout.CustomerId && item.TenantId == tenantId);
            if (customer == null || !customer.IsActive || customer.UserId != AbpSession.GetUserId())
                throw new AbpAuthorizationException("This test checkout does not belong to the current member.");
            var expected = "PILOT-TEST-" + checkout.Id.ToString("N");
            if (checkout.ProviderCheckoutId != expected)
                throw new InvalidOperationException("Only an exact pilot test checkout may be confirmed.");
            return await _payments.ProcessAQGreenJoiningCheckoutAsync(checkout.Id, "AQGREEN-PILOT-TEST",
                "PILOT-TEST-PAYMENT-" + checkout.Id.ToString("N"), expected, checkout.Amount, checkout.Currency, DateTime.UtcNow);
        }
    }
}
