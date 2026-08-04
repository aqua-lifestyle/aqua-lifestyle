using System;
using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;
using AqualLifeStyle.Domain.Enums;

namespace AqualLifeStyle.Domain.Accounts
{
    public enum InternalAccountInvitationStatus
    {
        Pending = 0,
        Accepted = 1,
        Expired = 2,
        Revoked = 3
    }

    public class InternalAccountInvitation : CreationAuditedEntity<Guid>, IMustHaveTenant
    {
        public const int HashLength = 64;
        public const int MaxEmailAddressLength = 256;
        public const int MaxRevocationReasonLength = 500;

        public int TenantId { get; set; }
        public long UserId { get; private set; }
        public AquaUserRole Role { get; private set; }
        public string InvitedEmailAddress { get; private set; }
        public string PublicCodeHash { get; private set; }
        public string SetupTokenHash { get; private set; }
        public InternalAccountInvitationStatus Status { get; private set; }
        public DateTime ExpiresAt { get; private set; }
        public DateTime? EmailConfirmedAt { get; private set; }
        public DateTime? AcceptedAt { get; private set; }
        public DateTime? RevokedAt { get; private set; }
        public long? RevokedByUserId { get; private set; }
        public string RevocationReason { get; private set; }
        public Guid? PreviousInvitationId { get; private set; }
        public int Version { get; private set; }

        protected InternalAccountInvitation()
        {
        }

        public static InternalAccountInvitation Create(
            int tenantId,
            long userId,
            AquaUserRole role,
            string invitedEmailAddress,
            string publicCodeHash,
            string setupTokenHash,
            DateTime createdAt,
            DateTime expiresAt,
            Guid? previousInvitationId = null)
        {
            if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));
            if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));
            if (!Enum.IsDefined(typeof(AquaUserRole), role))
                throw new ArgumentOutOfRangeException(nameof(role));
            if (string.IsNullOrWhiteSpace(invitedEmailAddress) ||
                invitedEmailAddress.Trim().Length > MaxEmailAddressLength)
                throw new ArgumentException("A valid invited email address is required.", nameof(invitedEmailAddress));
            ValidateHash(publicCodeHash, nameof(publicCodeHash));
            ValidateHash(setupTokenHash, nameof(setupTokenHash));
            if (createdAt == default) throw new ArgumentException("The invitation creation time is required.", nameof(createdAt));
            if (expiresAt <= createdAt) throw new ArgumentException("The invitation expiry must follow its creation time.", nameof(expiresAt));

            return new InternalAccountInvitation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = userId,
                Role = role,
                InvitedEmailAddress = invitedEmailAddress.Trim(),
                PublicCodeHash = publicCodeHash,
                SetupTokenHash = setupTokenHash,
                Status = InternalAccountInvitationStatus.Pending,
                CreationTime = createdAt,
                ExpiresAt = expiresAt,
                PreviousInvitationId = previousInvitationId,
                Version = 1
            };
        }

        public InternalAccountInvitationStatus GetEffectiveStatus(DateTime now)
            => Status == InternalAccountInvitationStatus.Pending && now >= ExpiresAt
                ? InternalAccountInvitationStatus.Expired
                : Status;

        public void MarkExpired(DateTime now)
        {
            if (Status != InternalAccountInvitationStatus.Pending || now < ExpiresAt) return;
            Status = InternalAccountInvitationStatus.Expired;
            Version++;
        }

        public void ConfirmEmail(DateTime confirmedAt)
        {
            EnsurePendingAndCurrent(confirmedAt);
            if (EmailConfirmedAt.HasValue) return;
            EmailConfirmedAt = confirmedAt;
            Version++;
        }

        public void Accept(DateTime acceptedAt)
        {
            if (Status == InternalAccountInvitationStatus.Accepted) return;
            EnsurePendingAndCurrent(acceptedAt);
            if (!EmailConfirmedAt.HasValue)
                throw new InvalidOperationException("Email ownership must be confirmed before accepting the invitation.");
            Status = InternalAccountInvitationStatus.Accepted;
            AcceptedAt = acceptedAt;
            Version++;
        }

        public void Revoke(DateTime revokedAt, long? revokedByUserId, string reason)
        {
            if (Status == InternalAccountInvitationStatus.Revoked) return;
            if (Status == InternalAccountInvitationStatus.Accepted)
                throw new InvalidOperationException("An accepted invitation cannot be revoked.");
            if (Status == InternalAccountInvitationStatus.Expired)
                throw new InvalidOperationException("An expired invitation cannot be revoked.");
            if (revokedAt == default) throw new ArgumentException("The revocation time is required.", nameof(revokedAt));
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A revocation reason is required.", nameof(reason));
            var normalizedReason = reason.Trim();
            if (normalizedReason.Length > MaxRevocationReasonLength)
                throw new ArgumentException($"The revocation reason cannot exceed {MaxRevocationReasonLength} characters.", nameof(reason));
            Status = InternalAccountInvitationStatus.Revoked;
            RevokedAt = revokedAt;
            RevokedByUserId = revokedByUserId;
            RevocationReason = normalizedReason;
            Version++;
        }

        private void EnsurePendingAndCurrent(DateTime now)
        {
            if (Status != InternalAccountInvitationStatus.Pending)
                throw new InvalidOperationException("The invitation is no longer pending.");
            if (now >= ExpiresAt)
                throw new InvalidOperationException("The invitation has expired.");
        }

        private static void ValidateHash(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != HashLength)
                throw new ArgumentException("A SHA-256 hash is required.", parameterName);
        }
    }
}
