using System;
using Abp.Domain.Entities;

namespace AqualLifeStyle.Domain.AQGreen
{
    // Immutable evidence of the privileged D03A admission action; not AR1 authority.
    public sealed class AQGreenProspectiveRootAdmission : AggregateRoot<Guid>, IMustHaveTenant
    {
        public int TenantId { get; private set; }
        public Guid ParticipantId { get; private set; }
        public long AdministratorUserId { get; private set; }
        public DateTime AuthorizedAt { get; private set; }
        public string Reason { get; private set; }
        int IMustHaveTenant.TenantId { get => TenantId; set => TenantId = value; }
        private AQGreenProspectiveRootAdmission() { }

        public static AQGreenProspectiveRootAdmission Record(
            int tenantId, Guid participantId, long administratorUserId,
            DateTime authorizedAt, string reason)
        {
            if (tenantId <= 0 || participantId == Guid.Empty || administratorUserId <= 0)
                throw new ArgumentException("Exact root admission identities are required.");
            if (authorizedAt.Kind != DateTimeKind.Utc || authorizedAt == default)
                throw new ArgumentException("An authoritative UTC admission time is required.");
            if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
                throw new ArgumentException("A root admission reason of at most 500 characters is required.");
            return new AQGreenProspectiveRootAdmission
            {
                Id = Guid.NewGuid(), TenantId = tenantId, ParticipantId = participantId,
                AdministratorUserId = administratorUserId,
                AuthorizedAt = authorizedAt, Reason = reason.Trim()
            };
        }
    }
}
