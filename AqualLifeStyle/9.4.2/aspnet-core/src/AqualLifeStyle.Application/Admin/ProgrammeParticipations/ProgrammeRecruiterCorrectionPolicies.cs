using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.UI;
using AqualLifeStyle.Application.Admin.ProgrammeParticipations.Dto;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.Admin.ProgrammeParticipations
{
    public interface IProgrammeRecruiterCorrectionPolicy
    {
        AdminProgrammeType Programme { get; }
        Task CorrectAsync(
            int tenantId,
            int customerId,
            int? newRecruiterCustomerId,
            long administratorUserId,
            string reason,
            DateTime correctedAt);
    }

    public interface IProgrammeRecruiterCorrectionPolicyResolver
    {
        IProgrammeRecruiterCorrectionPolicy Resolve(AdminProgrammeType programme);
    }

    public sealed class ProgrammeRecruiterCorrectionPolicyResolver
        : IProgrammeRecruiterCorrectionPolicyResolver, ITransientDependency
    {
        private readonly IReadOnlyCollection<IProgrammeRecruiterCorrectionPolicy> _policies;

        public ProgrammeRecruiterCorrectionPolicyResolver(
            IEnumerable<IProgrammeRecruiterCorrectionPolicy> policies)
        {
            _policies = policies.ToArray();
        }

        public IProgrammeRecruiterCorrectionPolicy Resolve(AdminProgrammeType programme)
        {
            var policy = _policies.SingleOrDefault(item => item.Programme == programme);
            if (policy == null)
                throw new UserFriendlyException(
                    "Network placement correction failed.",
                    "The selected programme does not support network placement corrections.");
            return policy;
        }
    }

    public sealed class AQGreenRecruiterCorrectionPolicy
        : IProgrammeRecruiterCorrectionPolicy
    {
        private readonly IRepository<EntryParticipation, Guid> _repository;
        private readonly IUnitOfWorkManager _unitOfWorkManager;

        public AdminProgrammeType Programme => AdminProgrammeType.Entry;

        public AQGreenRecruiterCorrectionPolicy(
            IRepository<EntryParticipation, Guid> repository,
            IUnitOfWorkManager unitOfWorkManager)
        {
            _repository = repository;
            _unitOfWorkManager = unitOfWorkManager;
        }

        public async Task CorrectAsync(
            int tenantId,
            int customerId,
            int? newRecruiterCustomerId,
            long administratorUserId,
            string reason,
            DateTime correctedAt)
        {
            using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MustHaveTenant))
            {
                var target = await _repository
                    .GetAllIncluding(item => item.RecruiterCorrections)
                    .SingleOrDefaultAsync(item =>
                        item.TenantId == tenantId && item.CustomerId == customerId);
                if (target == null) throw NotFound();
                if (!newRecruiterCustomerId.HasValue)
                {
                    target.CorrectToIndependent(administratorUserId, reason, correctedAt);
                    return;
                }

                var recruiter = await _repository.GetAll()
                    .AsNoTracking()
                    .SingleOrDefaultAsync(item =>
                        item.CustomerId == newRecruiterCustomerId.Value &&
                        item.Status == EntryParticipationStatus.Active);
                if (recruiter == null) throw InvalidRecruiter("The new inviting Club Member must have active AQGreen participation.");
                var placements = await _repository.GetAll()
                    .AsNoTracking()
                    .Select(item => new { item.CustomerId, item.RecruiterCustomerId })
                    .ToListAsync();
                RecruiterPlacementCycleValidator.EnsureNoCycle(
                    placements.Select(item => (item.CustomerId, item.RecruiterCustomerId)),
                    customerId,
                    recruiter.CustomerId);
                target.CorrectRecruiter(recruiter, administratorUserId, reason, correctedAt);
            }
        }

        private static UserFriendlyException NotFound() =>
            new UserFriendlyException("Network placement correction failed.", "The AQGreen participation was not found.");

        private static UserFriendlyException InvalidRecruiter(string details) =>
            new UserFriendlyException("Network placement correction failed.", details);

    }

    public sealed class OnyxRecruiterCorrectionPolicy
        : IProgrammeRecruiterCorrectionPolicy
    {
        private readonly IRepository<OnyxParticipation, Guid> _repository;
        private readonly IUnitOfWorkManager _unitOfWorkManager;

        public AdminProgrammeType Programme => AdminProgrammeType.Onyx;

        public OnyxRecruiterCorrectionPolicy(
            IRepository<OnyxParticipation, Guid> repository,
            IUnitOfWorkManager unitOfWorkManager)
        {
            _repository = repository;
            _unitOfWorkManager = unitOfWorkManager;
        }

        public async Task CorrectAsync(
            int tenantId,
            int customerId,
            int? newRecruiterCustomerId,
            long administratorUserId,
            string reason,
            DateTime correctedAt)
        {
            using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MustHaveTenant))
            {
                var target = await _repository
                    .GetAllIncluding(item => item.RecruiterCorrections)
                    .SingleOrDefaultAsync(item =>
                        item.TenantId == tenantId && item.CustomerId == customerId);
                if (target == null)
                    throw new UserFriendlyException("Network placement correction failed.", "The Onyx participation was not found.");
                if (!newRecruiterCustomerId.HasValue)
                {
                    target.CorrectToIndependent(administratorUserId, reason, correctedAt);
                    return;
                }

                var recruiter = await _repository.GetAll()
                    .AsNoTracking()
                    .SingleOrDefaultAsync(item =>
                        item.CustomerId == newRecruiterCustomerId.Value &&
                        item.Status == OnyxParticipationStatus.Active);
                if (recruiter == null)
                    throw new UserFriendlyException("Network placement correction failed.", "The new inviting Club Member must have active Onyx participation.");
                var placements = await _repository.GetAll()
                    .AsNoTracking()
                    .Select(item => new { item.CustomerId, item.RecruiterCustomerId })
                    .ToListAsync();
                RecruiterPlacementCycleValidator.EnsureNoCycle(
                    placements.Select(item => (item.CustomerId, item.RecruiterCustomerId)),
                    customerId,
                    recruiter.CustomerId);
                target.CorrectRecruiter(recruiter, administratorUserId, reason, correctedAt);
            }
        }
    }
}
