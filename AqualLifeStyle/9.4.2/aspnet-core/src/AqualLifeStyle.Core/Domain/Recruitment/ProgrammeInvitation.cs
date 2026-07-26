using System;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;
using AqualLifeStyle.Domain.Common;

namespace AqualLifeStyle.Domain.Recruitment
{
    public class ProgrammeInvitation : FullAuditedAggregateRoot<Guid>, IMustHaveTenant
    {
        public const int CodeLength = 12;
        public const int MaxProgrammeKeyLength = 32;

        public int TenantId { get; set; }
        public string ProgrammeKey { get; private set; }
        public Guid ProgrammeParticipationId { get; private set; }
        public string Code { get; private set; }

        protected ProgrammeInvitation()
        {
        }

        private ProgrammeInvitation(
            int tenantId,
            string programmeKey,
            Guid programmeParticipationId,
            string code)
        {
            if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(programmeKey))
                throw new ArgumentException("A programme key is required.", nameof(programmeKey));
            if (programmeParticipationId == Guid.Empty)
                throw new ArgumentException("A programme participation is required.", nameof(programmeParticipationId));
            if (string.IsNullOrWhiteSpace(code) || code.Length != CodeLength)
                throw new ArgumentException($"The invitation code must contain {CodeLength} characters.", nameof(code));

            Id = Guid.NewGuid();
            TenantId = tenantId;
            ProgrammeKey = programmeKey.Trim().ToUpperInvariant();
            ProgrammeParticipationId = programmeParticipationId;
            Code = code.Trim().ToUpperInvariant();
        }

        public static ProgrammeInvitation Create(
            int tenantId,
            string programmeKey,
            Guid programmeParticipationId) =>
            new ProgrammeInvitation(
                tenantId,
                programmeKey,
                programmeParticipationId,
                SecurePublicCode.Generate(CodeLength));
    }
}
