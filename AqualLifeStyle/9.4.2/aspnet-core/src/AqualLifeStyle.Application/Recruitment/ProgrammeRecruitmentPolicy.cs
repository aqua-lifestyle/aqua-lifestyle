using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.UI;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.Recruitment
{
    public static class RecruitmentProgrammeKeys
    {
        public const string AQGreen = "AQGREEN";
        public const string Onyx = "ONYX";
    }

    public sealed class RecruiterParticipationReference
    {
        public Guid ParticipationId { get; init; }
        public int TenantId { get; init; }
        public int CustomerId { get; init; }
        public bool IsEligible { get; init; }
    }

    public interface IProgrammeRecruitmentPolicy
    {
        string ProgrammeKey { get; }
        string ProgrammeName { get; }
        Task<RecruiterParticipationReference> FindByCustomerAsync(int customerId);
        Task<RecruiterParticipationReference> FindByParticipationAsync(Guid participationId);
    }

    public interface IProgrammeRecruitmentPolicyResolver
    {
        IReadOnlyCollection<IProgrammeRecruitmentPolicy> GetAll();
        IProgrammeRecruitmentPolicy Resolve(string programmeKey);
    }

    public sealed class ProgrammeRecruitmentPolicyResolver
        : IProgrammeRecruitmentPolicyResolver, ITransientDependency
    {
        private readonly IReadOnlyCollection<IProgrammeRecruitmentPolicy> _policies;

        public ProgrammeRecruitmentPolicyResolver(
            IEnumerable<IProgrammeRecruitmentPolicy> policies)
        {
            _policies = policies.ToArray();
        }

        public IReadOnlyCollection<IProgrammeRecruitmentPolicy> GetAll() => _policies;

        public IProgrammeRecruitmentPolicy Resolve(string programmeKey)
        {
            var normalized = programmeKey?.Trim().ToUpperInvariant();
            var policy = _policies.SingleOrDefault(item => item.ProgrammeKey == normalized);
            if (policy == null)
            {
                throw new UserFriendlyException(
                    "Member invitations unavailable.",
                    "Member invitations are not currently configured for this programme.");
            }

            return policy;
        }
    }

    public sealed class AQGreenRecruitmentPolicy
        : IProgrammeRecruitmentPolicy
    {
        private readonly IRepository<EntryParticipation, Guid> _repository;
        private readonly IUnitOfWorkManager _unitOfWorkManager;

        public string ProgrammeKey => RecruitmentProgrammeKeys.AQGreen;
        public string ProgrammeName => "AQGreen";

        public AQGreenRecruitmentPolicy(
            IRepository<EntryParticipation, Guid> repository,
            IUnitOfWorkManager unitOfWorkManager)
        {
            _repository = repository;
            _unitOfWorkManager = unitOfWorkManager;
        }

        public async Task<RecruiterParticipationReference> FindByCustomerAsync(int customerId)
        {
            using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MustHaveTenant))
            {
                return Map(await _repository.FirstOrDefaultAsync(item => item.CustomerId == customerId));
            }
        }

        public async Task<RecruiterParticipationReference> FindByParticipationAsync(Guid participationId)
        {
            using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MustHaveTenant))
            {
                return Map(await _repository.GetAll()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == participationId));
            }
        }

        private static RecruiterParticipationReference Map(EntryParticipation participation) =>
            participation == null
                ? null
                : new RecruiterParticipationReference
                {
                    ParticipationId = participation.Id,
                    TenantId = participation.TenantId,
                    CustomerId = participation.CustomerId,
                    IsEligible = participation.IsQualifiedForNetwork
                };
    }

    public sealed class OnyxRecruitmentPolicy
        : IProgrammeRecruitmentPolicy
    {
        private readonly IRepository<OnyxParticipation, Guid> _repository;
        private readonly IUnitOfWorkManager _unitOfWorkManager;

        public string ProgrammeKey => RecruitmentProgrammeKeys.Onyx;
        public string ProgrammeName => "Onyx";

        public OnyxRecruitmentPolicy(
            IRepository<OnyxParticipation, Guid> repository,
            IUnitOfWorkManager unitOfWorkManager)
        {
            _repository = repository;
            _unitOfWorkManager = unitOfWorkManager;
        }

        public async Task<RecruiterParticipationReference> FindByCustomerAsync(int customerId)
        {
            using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MustHaveTenant))
            {
                return Map(await _repository.FirstOrDefaultAsync(item => item.CustomerId == customerId));
            }
        }

        public async Task<RecruiterParticipationReference> FindByParticipationAsync(Guid participationId)
        {
            using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MustHaveTenant))
            {
                return Map(await _repository.GetAll()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == participationId));
            }
        }

        private static RecruiterParticipationReference Map(OnyxParticipation participation) =>
            participation == null
                ? null
                : new RecruiterParticipationReference
                {
                    ParticipationId = participation.Id,
                    TenantId = participation.TenantId,
                    CustomerId = participation.CustomerId,
                    IsEligible = participation.Status == OnyxParticipationStatus.Active
                };
    }
}
