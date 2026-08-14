# Programme recruitment invitations

## Customer-facing language

The product calls this feature **Member invitations**. Customer and
administrator interfaces use **Inviting Club Member**, **Member network**, and
**Network placement** instead of recruitment terminology. Existing internal
class names, API properties, permissions, database columns, and audit records
retain their current recruiter/recruitment names until a separately planned
technical migration is justified. This terminology boundary avoids breaking
contracts while keeping the customer experience clear and professional.

## Purpose

The invitation experience makes programme recruitment discoverable without
changing the confirmed recruitment rules. A Club Member recruits through an
active participation in a specific programme. The invitation is therefore
owned by the programme participation, not by the customer account.

The lifecycle remains deliberately separated:

`Customer → Programme participation → Recruitment relationship → Network placement → Activation → Qualification → Commission eligibility → Commission payment`

Accepting an AQGreen invitation creates its pre-activation participation and
records the recruiter placement; confirmed joining payment moves the participation
to Area Administrator review, and only approval activates it. Confirming a direct-Onyx invitation records only the member's joining
intent for checkout. It does not create an Onyx participation or placement.
After Yoco confirms the R6,120 payment, the backend revalidates the invitation
and atomically creates the awaiting-approval Onyx participation and recruiter placement.
Neither flow directly qualifies a network or pays a commission.

## Customer workflow

1. An active AQGreen or Onyx participant opens **Invite Club Members**.
2. The system returns one stable invitation for each eligible participation.
3. The Club Member copies its code or link, or uses the device share action.
4. The invitee opens `/i/{inviteCode}`.
5. The public preview shows only the recruiter name, immutable Club Member
   number, current business Area, programme, and current recruitment eligibility.
6. An unauthenticated invitee creates an account or signs in and returns to the
   same invitation. Registration inherits the inviting Club Member's current
   active business Area but does not create programme placement.
7. The invitee explicitly confirms the programme and intended recruiter.
8. For AQGreen, the normal joining workflow creates the participation and
   recruiter placement in its pre-activation state and re-resolves the inviting
   Club Member's current Area. Yoco confirmation of the full R1,200 joining
   obligation moves it to Area Administrator review; only approval activates it.
   For direct Onyx, confirmation proceeds to the R6,120 checkout without creating
   programme state. Verified payment creates the awaiting-approval Onyx
   participation and placement using the recruiter's current Area; Area
   Administrator approval activates it.

Joining independently remains supported. Manually entering internal customer
or participation identifiers is not part of the normal customer experience.

## Domain and application design

- `ProgrammeInvitation` stores a unique, stable, 12-character public code,
  programme key, Tenant, and programme-participation reference. Business Area is
  resolved from the recruiter Customer when the invitation is used, not copied
  into the invitation. For host-context registration, the invitation's persisted
  Tenant is the server-authoritative registration Tenant; browser workspace
  routing does not grant Tenant authority.
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
- the invitation, recruiter participation, and invitee belong to the same ABP
  Tenant;
- the invitation belongs to the requested programme;
- the referenced participation still exists and remains eligible;
- the recruiter's participation remains eligible under the selected programme;
- the recruiter Customer is Active and has a current active Area in the same
  Tenant;
- the invitee inherits that server-resolved Area at the programme placement
  transition, with effective-dated Area history preserved;
- the invitee is not accepting their own invitation; and
- existing idempotency and recruiter-reassignment protections still hold.

Tenant is the hard security and programme-network boundary. Recruitment and
qualification never cross Tenants. Area is the business and administrative
subdivision inside a Tenant. Invitation recruits inherit the recruiter's current
Area, while the qualification graph remains Tenant-and-programme scoped.

The public lookup does not return customer IDs, participation IDs, entity IDs,
payment information, network structure, qualification details, or commission
information. Invitation creation and acceptance use ABP auditing. Creation and
administrator correction also emit structured application log entries.

## Administrator correction workflow

Administrators with the dedicated correction permission identify Club Members
by immutable Club Member number, select AQGreen or Onyx, enter the corrected
recruiter (or choose an independent network), and provide a mandatory reason.

The backend validates active same-programme participation, same-Tenant
placement, administrator authority, self placement, and cycles. Host authority
may operate on more than one Tenant, but it cannot create a recruiter
relationship across Tenants. A successful change appends an immutable history
record containing the previous recruiter, new recruiter, reason,
administrator, and timestamp. Repeating the already-applied correction is
idempotent and does not create duplicate history.

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
- Future programme-specific cross-Area recruitment restrictions belong in
  programme policy. They must not be inferred from Tenant identity or an
  invitation code.
