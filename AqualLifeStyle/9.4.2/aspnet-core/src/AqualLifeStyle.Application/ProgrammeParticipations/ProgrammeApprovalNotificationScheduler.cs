using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp;
using Abp.Authorization;
using Abp.Dependency;
using Abp.Domain.Repositories;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Authorization.Users;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Areas;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.Email;
using AqualLifeStyle.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AqualLifeStyle.Application.ProgrammeParticipations
{
    /// <summary>
    /// Resolves the active Area Administrators who are authorised to decide a
    /// programme participation and writes one idempotent outbox alert per
    /// administrator. The participation state remains the durable queue source.
    /// </summary>
    public sealed class ProgrammeApprovalNotificationScheduler : ITransientDependency
    {
        private readonly IRepository<User, long> _userRepository;
        private readonly IRepository<Area, Guid> _areaRepository;
        private readonly IRepository<AreaAdminAssignment, Guid> _areaAdminAssignmentRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly IPermissionChecker _permissionChecker;
        private readonly ITransactionalEmailOutbox _emailOutbox;
        private readonly TransactionalEmailTemplateBuilder _emailTemplates;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ProgrammeApprovalNotificationScheduler> _logger;

        public ProgrammeApprovalNotificationScheduler(
            IRepository<User, long> userRepository,
            IRepository<Area, Guid> areaRepository,
            IRepository<AreaAdminAssignment, Guid> areaAdminAssignmentRepository,
            ICustomerRepository customerRepository,
            IPermissionChecker permissionChecker,
            ITransactionalEmailOutbox emailOutbox,
            TransactionalEmailTemplateBuilder emailTemplates,
            IConfiguration configuration,
            ILogger<ProgrammeApprovalNotificationScheduler> logger)
        {
            _userRepository = userRepository;
            _areaRepository = areaRepository;
            _areaAdminAssignmentRepository = areaAdminAssignmentRepository;
            _customerRepository = customerRepository;
            _permissionChecker = permissionChecker;
            _emailOutbox = emailOutbox;
            _emailTemplates = emailTemplates;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<int> ScheduleAsync(
            MemberPayment payment,
            Guid participationId,
            ProgrammeParticipationKind participationKind,
            decimal confirmedJoiningAmount)
        {
            if (payment == null) throw new ArgumentNullException(nameof(payment));
            if (payment.Status != MemberPaymentStatus.Confirmed || !payment.ConfirmedAt.HasValue)
                throw new InvalidOperationException(
                    "Administrator review can be scheduled only for a confirmed programme payment.");
            if (participationId == Guid.Empty)
                throw new ArgumentException("A participation is required.", nameof(participationId));
            if (confirmedJoiningAmount <= 0m)
                throw new ArgumentOutOfRangeException(
                    nameof(confirmedJoiningAmount),
                    "The confirmed joining amount must be positive.");

            var customer = await _customerRepository.GetAsync(payment.CustomerId);
            if (customer.TenantId != payment.TenantId || !customer.AreaId.HasValue)
                throw new InvalidOperationException(
                    "The programme participant has no valid Area assignment.");
            var area = await _areaRepository.FirstOrDefaultAsync(item =>
                item.Id == customer.AreaId.Value &&
                item.TenantId == payment.TenantId &&
                item.IsActive);
            if (area == null)
                throw new InvalidOperationException(
                    "The programme participant's Area is not active in the payment Tenant.");
            var assignedUserIds = await _areaAdminAssignmentRepository.GetAll()
                .Where(assignment =>
                    assignment.TenantId == payment.TenantId &&
                    assignment.AreaId == area.Id &&
                    !assignment.RevokedAt.HasValue)
                .Select(assignment => assignment.UserId)
                .Distinct()
                .ToListAsync();
            var candidates = await _userRepository.GetAll()
                .AsNoTracking()
                .Where(user =>
                    user.TenantId == payment.TenantId &&
                    user.IsActive &&
                    !user.IsDeleted &&
                    assignedUserIds.Contains(user.Id) &&
                    !string.IsNullOrWhiteSpace(user.EmailAddress))
                .OrderBy(user => user.Id)
                .ToListAsync();

            var administrators = new List<User>();
            foreach (var candidate in candidates)
            {
                if (await _permissionChecker.IsGrantedAsync(
                        new UserIdentifier(payment.TenantId, candidate.Id),
                        AquaPermissions.Admin.ProgrammeParticipations.Approve))
                {
                    administrators.Add(candidate);
                }
            }

            if (administrators.Count == 0)
            {
                _logger.LogWarning(
                    "ProgrammeApprovalOperationsAlert AlertType=no_responsible_area_administrator TenantId={TenantId} ParticipationId={ParticipationId} Programme={Programme}",
                    payment.TenantId,
                    participationId,
                    participationKind);
                return 0;
            }

            var programmeName = participationKind == ProgrammeParticipationKind.Onyx
                ? "Onyx"
                : "AQGreen";
            var portalUrl = BuildPortalUrl();
            var scheduled = 0;
            foreach (var administrator in administrators)
            {
                var key = $"programme-approval:{participationKind}:{participationId}:administrator:{administrator.Id}";
                if (await _emailOutbox.EnqueueAsync(
                        payment.TenantId,
                        "ProgrammeParticipationAwaitingApproval",
                        key,
                        _emailTemplates.ProgrammeParticipationAwaitingAdministratorReview(
                            administrator.Name,
                            administrator.EmailAddress,
                            customer.Name,
                            customer.ClubMemberNumber,
                            area.Name,
                            programmeName,
                            confirmedJoiningAmount,
                            payment.Currency,
                            payment.ConfirmedAt.Value,
                            portalUrl,
                            key)))
                {
                    scheduled++;
                }
            }

            return scheduled;
        }

        private string BuildPortalUrl()
        {
            var root = _configuration["App:ClientRootAddress"]?.TrimEnd('/');
            return Uri.TryCreate(root, UriKind.Absolute, out var uri) &&
                   (uri.Scheme == Uri.UriSchemeHttps || uri.IsLoopback)
                ? $"{root}/admin/programme-participations"
                : null;
        }
    }
}
