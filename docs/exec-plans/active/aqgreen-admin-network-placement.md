# AQGreen administrator network placement — PR #139

> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

## Goal and authority

Use the existing AQGreen system for historical transfers and ongoing company
acquisition. The owner's PR #139 continuation authorizes investigation,
integration and non-production development and prohibits merge and production
operations. Follow [placement V2](../../aqua-system/aqgreen-network-placement-specification.md),
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

- Existing scoped `Add to Network` roster preview from the first PR commit flags
  active placement gaps and dependent rows. The company route is unavailable.
- The continuation shares the existing membership-evidence join between roster
  preview and reconciliation/export. The roster now flags same-Tenant and shared
  legacy AQGreen assignments (including records with no current participation)
  and unverifiable assignments without disclosing a foreign plan or writing data.
- See the [operator guide](../../development/aqgreen-existing-member-preview.md)
  and [PR #139](https://github.com/aqua-lifestyle/aqua-lifestyle/pull/139) for
  command and test evidence. Reconciliation tests use synthetic records on
  disposable PostgreSQL; there is no provider or production acceptance evidence.

## Blocked write paths and next action

- **Historical:** Obtain the approved source identity/evidence mapping,
  root/parent/slot and business-effective dates (D03B/D09). Decide which reviewed
  scopes remain held and who can clear them. An eventual apply should use the
  existing account, topology and audit boundaries, with admission and automatic
  allocation interlocked under the existing tree lock. Decide historical payment
  and activation treatment from evidence; do not synthesize settled transactions.
- **Company:** Confirm whether admission into an established tree without an
  individual sponsor is required, what proves acquisition/member consent, and
  what authorizes the target Area and placement. For established-tree admission,
  the two-value attribution constraint plus the allocator's invitation-only
  branch prove a model gap. Proposed only: extend the existing attribution and
  approval model, reusing the checkout, Area validation, placement persistence and
  constraints. Do not impersonate the member, synthesize an invitation, or create
  an independent root as an implicit fallback. Any schema successor needs
  migration and D10 review before production authority can be requested.

Branch `feat/aqgreen-admin-network-placement`, based on protected `main`
`32a6852` (merged #138). Continue in the same draft PR; owner decisions come
before write-path implementation and acceptance tests.
