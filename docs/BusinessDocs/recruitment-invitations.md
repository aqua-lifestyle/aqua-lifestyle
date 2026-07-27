# Programme recruitment invitations

## Purpose

The invitation experience makes programme recruitment discoverable without
changing the confirmed recruitment rules. A Club Member recruits through an
active participation in a specific programme. The invitation is therefore
owned by the programme participation, not by the customer account.

The lifecycle remains deliberately separated:

`Customer → Programme participation → Recruitment relationship → Network placement → Activation → Qualification → Commission eligibility → Commission payment`

Accepting an AQGreen invitation creates its pre-activation participation and
records the recruiter placement; one confirmed R1,200 joining payment then governs
activation. Confirming a direct-Onyx invitation records only the member's joining
intent for checkout. It does not create an Onyx participation or placement.
After Yoco confirms the R6,120 payment, the backend revalidates the invitation
and atomically creates the active Onyx participation and recruiter placement.
Neither flow directly qualifies a network or pays a commission.

## Customer workflow

1. An active AQGreen or Onyx participant opens **Invite Club Members**.
2. The system returns one stable invitation for each eligible participation.
3. The Club Member copies its code or link, or uses the device share action.
4. The invitee opens `/i/{inviteCode}`.
5. The public preview shows only the recruiter name, immutable Club Member
   number, Area, programme, and current recruitment eligibility.
6. An unauthenticated invitee creates an account or signs in and returns to the
   same invitation.
7. The invitee explicitly confirms the programme and intended recruiter.
8. For AQGreen, the normal joining workflow creates the participation and
   recruiter placement in its pre-activation state; Yoco confirmation of the full
   R1,200 joining payment activates it. For direct Onyx, confirmation proceeds to the
   R6,120 checkout without creating programme state. Only a confirmed payment
   creates the active Onyx participation and recruiter placement.

Joining independently remains supported. Manually entering internal customer
or participation identifiers is not part of the normal customer experience.

## Domain and application design

- `ProgrammeInvitation` stores a unique, stable, 12-character public code,
  programme key, Area, and programme-participation reference.
- Codes use a cryptographically secure random-number generator and an
  unambiguous uppercase alphabet. Internal IDs are never encoded into a code.
- A unique database constraint on `(ProgrammeKey, ProgrammeParticipationId)`
  enforces one invitation per participation. A second unique constraint protects
  the code namespace.
- Programme-specific eligibility and participation lookup live behind
  `IProgrammeRecruitmentPolicy`. Adding a programme requires a policy and DI
  registration; invitation resolution and joining logic do not change.
- AQGreen and Onyx continue to use their existing activation and eligibility
  rules. The invitation layer does not contain branch-width, depth, payment, or
  commission rules.
- Jasper and BusinessPremier have no confirmed recruitment policy. They do not
  receive invitation actions, codes, placements, or qualification behaviour.
  An unsupported programme fails closed with a clear configuration message;
  there is no generic membership-type fallback.
- Legacy recruiter-ID inputs remain backend-compatible for existing clients,
  but the customer UI uses invitation codes.

## Validation and security

The backend is authoritative. On every acceptance it verifies that:

- the invitation exists and its code is well formed;
- the invitation belongs to the requested programme;
- the referenced participation still exists and remains eligible;
- the recruiter's participation remains eligible under the selected programme;
- an Area difference does not invalidate an otherwise valid recruitment;
- the invitee is not accepting their own invitation; and
- existing idempotency and recruiter-reassignment protections still hold.

The public lookup does not return customer IDs, participation IDs, entity IDs,
payment information, network structure, qualification details, or commission
information. Invitation creation and acceptance use ABP auditing. Creation and
administrator correction also emit structured application log entries.

## Administrator correction workflow

Administrators with the dedicated correction permission identify Club Members
by immutable Club Member number, select AQGreen or Onyx, enter the corrected
recruiter (or choose an independent network), and provide a mandatory reason.

The backend validates active same-programme participation, Area scope, self
placement, and cycles. A successful change appends an immutable history record
containing the previous recruiter, new recruiter, reason, administrator, and
timestamp. Repeating the already-applied correction is idempotent and does not
create duplicate history.

## Operational notes and future improvements

- Technical debt: the AQGreen/Entry domain classes currently remain under the
  `Domain.Onyx` namespace and directory. This does not affect programme-policy
  resolution or network isolation, but a future naming-only migration should
  move them to a programme-neutral or AQGreen-specific location.
- Invitation codes are stable, not expiring, because the confirmed requirement
  is one stable invitation per participation. Eligibility is checked on every
  preview and acceptance, so an inactive recruiter cannot create a placement.
- Concurrent first-time requests are protected by database uniqueness. A future
  high-volume deployment may add a short distributed lock or retry around the
  unique-constraint race to make the losing concurrent request transparent.
- Revocation, expiry, delivery analytics, QR codes, and rate-limiting are useful
  future capabilities but are intentionally outside this first complete flow.
- If cross-Area recruitment rules change, that decision belongs in programme
  policy. It must not be inferred from an invitation code.
