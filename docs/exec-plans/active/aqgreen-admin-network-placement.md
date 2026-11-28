# AQGreen administrator network placement — PR #139

> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

## Goal and authority

Use the existing AQGreen system for historical transfers and ongoing company
acquisition. The owner's latest PR #139 message approves the historical hold and
clearance rules, company acquisition without sponsor credit, consent, verified
payment, Area approval and existing placement rules. It prohibits merge and
production operations. Follow [placement V2](../../aqua-system/aqgreen-network-placement-specification.md),
[historical evidence](../../aqua-system/05-data-history-migrations-and-legacy-members.md),
[payment and approval](../../aqua-system/04-payments-approval-and-yoco.md), and
[D10 admission](../../operations/d10-api-availability-and-independent-root-release.md).
Acquisition, recruitment credit and structural placement are separate facts.

## Bounded implementation trace

| Concern | Existing implementation and limit |
| --- | --- |
| Account creation/reuse | `AccountAppService` handles self-service registration. `AdminCustomerAppService.CreateAsync`/`RestoreAsync` and `CustomerImportAppService` reuse `AdminCustomerAccountManager`: duplicate identity checks, explicit removed-account handling, current Area authority and Customer/User creation. `Customer.MembershipId` is a plan assignment; neither it nor the `Customer.ClubMemberNumber` generated at account creation establishes legacy identity or AQGreen participation. `AreaAssignmentResolver` proves a current eligible Area, not which one a company recruit should receive. |
| Historical facts | `PreviewLegacyAQGreenRosterAsync` compares original references and recorded links, read-only. `GetLegacyAQGreenReconciliation`, frozen export and `AQGreenLegacyCanonicalisationSimulator` are inventory/review/proposal tools. `EffectiveProgrammeNetwork` can reconstruct a cutoff-effective V1 recruiter graph from evidenced corrections; it is not immutable five-wide placement history. No write path binds a verified source ID, historical lifecycle and exact topology; no reservation/hold is derived from a preview. |
| Participation and attribution | `ClubMemberProgrammeParticipationAppService.StartEntryAsync` creates an `EntryParticipation`. V2 requires a real `ProgrammeInvitation` (resolved and recorded by `ProgrammeInvitationResolver` and `AQGreenInvitationAttributionRecorder`) or a member-authenticated `AQGreenIndependentRootRequest`. `AQGreenRecruitmentAttributionConfiguration` restricts source/kind/credited sponsor to those two shapes; sponsored attribution also has an invitation FK. `AuthorisedDirectAdmission` is root provenance, not company acquisition. `EntryParticipation.RecruiterCustomerId` remains V1 recruitment evidence; it is not a placement-parent field. |
| Payment and approval | `ClubMemberProgrammeParticipationAppService.CreateAQGreenJoiningCheckoutAsync` binds the signed-in customer and participation, persists `AQGreenJoiningCheckout`, and calls `YocoCheckoutGateway`. `YocoPaymentsController` and `ProgrammePaymentConfirmationProcessor` apply authenticated provider evidence into `PaymentConfirmedAwaitingApproval`. `ApproveProgrammeParticipationAsync` rechecks payment and current Area authority before activation. The existing customer's checkout is reusable once an authorized company participation exists; there is no admin equivalent for that channel today. The legacy membership guard instructs the member to reconcile rather than join and pay again. |
| Topology and safeguards | `AQGreenNetworkPlacement` stores Tenant, tree scope, participant, parent, slot, canonical path and effective placement time; it is separate from attribution. The existing bootstrap creates a fresh scope for an approved independent root. `AQGreenPlacementAllocator` requires confirmed sponsored attribution and searches the sponsor subtree in canonical five-wide order under `IAQGreenPlacementTreeLock` and PostgreSQL uniqueness constraints. `AQGreenD10ProductionSelectors` checks provenance, historical population and the CLEAN_START baseline; a missing or mismatched population/schema is not admitted by updating an account. |

## Completed and tested on the PR branch

- Existing scoped `Add to Network` roster preview flags active placement gaps
  and dependent rows.
- The continuation shares the existing membership-evidence join between roster
  preview and reconciliation/export. The roster now flags same-Tenant and shared
  legacy AQGreen assignments (including records with no current participation)
  and unverifiable assignments without disclosing a foreign plan or writing data.
- See the [operator guide](../../development/aqgreen-existing-member-preview.md)
  and [PR #139](https://github.com/aqua-lifestyle/aqua-lifestyle/pull/139) for
  command and test evidence. Reconciliation tests use synthetic records on
  disposable PostgreSQL; there is no provider or production acceptance evidence.

- The owner-approved continuation adds scoped historical holds and atomic
  placement/clearance for existing paid active records in established trees.
  Company registration extends the existing attribution plus member acceptance
  records, then reuses member checkout, Area approval and canonical allocation.
- Migration 58 changes the controlled attribution shape and adds historical hold
  and reference tables. The fully migrated disposable PostgreSQL column digest
  is `68aa40dd42169d4c52ed03011bc070bea4b319af7015cadf7e84c8981d788159`.
  Application startup and D10 keep exact migration/preflight checks; the
  receive/replay store also retains the bounded set of older recovery prefixes.
- The administrator PostgreSQL application test covers held approval, invalid
  historical evidence, exact placement and replay, company Area denial, consent,
  checkout and payment processor replay, approval and follow-on spillover.

## Remaining verification and release boundaries

- Owner review of `e92ea03` confirmed the missing terminal path for an incomplete
  historical review. The recovery change adds guarded cancellation of a precautionary
  hold and binds clearance to a separately reviewed register's reference-set digest.
  Known conflicts and attempted approvals stay protected. The same tree lock and
  PostgreSQL checks reject cancellation after durable reference or topology changes.
  Migration 59 leaves earlier evidence intact and pins the measured test schema.
  The fresh migrated PostgreSQL recovery/continued-allocation test, injected partial
  reference/topology denial test, and CLEAN_START journey passed locally. Source
  register authenticity and whole-scope completeness remain reviewer responsibilities.
- Membership whose identity, paid activation, root or historical dates cannot be
  proved using the retained record is not activated or placed here. The historical
  UI is for an established tree and requires reviewed placement data, not just a
  roster preview. New historical topology in a commissioned Tenant requires the
  separate D10 reconciliation path. A previously calculated commission period
  is never rewritten by transfer.
- Offer the signed-in member's existing checkout, with explicit consent. Exact
  manual slots for new company acquisitions and admin-sent checkout links are
  not used in this implementation. A live provider end-to-end run is not authorized.
- Use protected CI evidence at the exact candidate; it is not production release
  authority. No production migration, import, payment or deployment is authorized.

Branch `feat/aqgreen-admin-network-placement`, based on protected `main`
`32a6852` (merged #138). Continue in the same draft PR.
