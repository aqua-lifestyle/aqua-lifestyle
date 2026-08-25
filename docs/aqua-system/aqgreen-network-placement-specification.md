# AQGreen Network Placement V2 specification

- Authority: **Authoritative design specification for resolved AQGreen Placement V2 semantics; implementation and production cutover are separate.**
- Status: **NOT IMPLEMENTED / NOT PRODUCTION-ENABLED / MIGRATION AND CUTOVER DECISIONS OPEN**
- Applies to: AQGreen placement at and after an authorised V2 cutover
- Does not authorise: a production cutover, data migration, financial recalculation, Onyx placement, or manual data repair

## 1. Purpose

This specification defines the authoritative design for the confirmed five-wide AQGreen placement semantics with sponsor-local spillover. Launch-critical business decisions that cannot be derived from current authority are isolated in section 34; implementation of the affected paths must remain blocked until those decisions are resolved. Its purpose is to prevent four different business facts from being collapsed into one relationship:

```text
Acquisition provenance  = how the person came into Aqua
Recruitment credit      = which AQGreen participant is sponsor of record
Network placement       = where the participant is structurally positioned
Current eligibility     = whether the participant currently counts for a decision
```

AQGreen Placement V2 must provide deterministic topology, atomic allocation, permanent historical positions, cutoff-correct qualification, and auditable financial use.

## 2. Scope

### 2.1 Included

- AQGreen recruitment attribution and acquisition provenance;
- the gate for permanent placement;
- five-wide sponsor-local spillover;
- canonical allocation, slots, positions, ordering, and relative depth;
- topology permanence and separate participation/eligibility state;
- concurrency, idempotency, database invariants, and authorization;
- qualification, commission, loan, and graduation integration boundaries;
- legacy analysis, V2 canonicalisation, cutover, and financial-history protection;
- implementation acceptance criteria and required verification.

### 2.2 Excluded

- production or domain implementation in this phase;
- migrations in this phase;
- changing existing tests in this phase;
- Onyx placement implementation;
- inventing monthly due-day, first-liability, refund, chargeback, or post-Active lifecycle policy;
- automatically recalculating settled commissions or reversing graduations;
- allowing customers or ordinary administrators to select placement positions.

## 3. Authority and evidence hierarchy

This document uses these classifications deliberately:

| Classification | Meaning |
| --- | --- |
| `CONFIRMED BUSINESS RULE` | Business intent explicitly confirmed for Placement V2. Implementations may not contradict it. |
| `CURRENT IMPLEMENTATION EVIDENCE` | Behaviour verified by inspection of the current repository. It is not automatically a V2 rule. |
| `ENGINEERING DECISION` | The required technical interpretation selected to implement confirmed intent safely. |
| `DERIVED CONSEQUENCE` | A necessary logical result of confirmed rules and engineering decisions. |
| `RECOMMENDATION` | Preferred sequencing or design detail that may be refined without changing business semantics. |
| `UNRESOLVED BUSINESS DECISION` | Authority is absent or conflicting. Implementation must not manufacture a rule. |
| `CORE BUSINESS DECISION` | Affects normal placement or its primary qualification interpretation. Only the dependent deliverables identified in sections 30 and 34 are blocked. |
| `CUTOVER / MIGRATION DECISION` | Required for authoritative legacy conversion or production activation, not additive groundwork. |
| `OPTIONAL COMPANY-FEATURE DECISION` | Required only if company-assisted acquisition is enabled; otherwise that channel remains disabled. |
| `EXCEPTIONAL WORKFLOW DECISION` | Required only for the corresponding correction, repair, lifecycle, or alternative-priority path. |

Authority, from highest to lowest for this design, is:

1. confirmed Placement V2 business rules in the commissioning brief for this specification;
2. later confirmed business-owner decisions recorded in the repository;
3. [Aqua business rules and workflows](02-business-rules-and-workflows.md), subject to the V1/V2 boundary below;
4. this specification's engineering decisions for implementing the confirmed V2 rules;
5. current code, persistence, tests, and migrations as evidence of existing behaviour;
6. older plans, verification notes, and historical documents as supporting evidence only.

### 3.1 Relationship to the current business-rules document

The current rules document describes V1 at [02-business-rules-and-workflows.md](02-business-rules-and-workflows.md), especially its existing earliest-five recruiter selection. This specification supersedes that adjacency/selection rule **only for AQGreen Placement V2 at and after an authorised V2 effective instant** and, for applicable post-cutover commission weeks, adds the section 24 weekly sales-eligibility rule. V1 remains authoritative for historical decisions made under V1. Existing rates, commission components, holds, release controls, payment controls, Tenant boundaries, Area administration, payment verification, and programme approval remain independently authoritative unless explicitly changed here.

The current repository implements V1 adjacency/selection and does not implement or enable V2 placement or the V2 weekly sales-eligibility gate.

## 4. Terminology

| Term | Definition |
| --- | --- |
| Participant | An AQGreen programme participation, not merely a customer account. Placement identity must use the participation identity. |
| Acquisition source | How Aqua obtained the lead/person, such as member invitation or company marketing. |
| Credited sponsor | The AQGreen participant receiving recruitment/sponsor credit. Also called sponsor of record. |
| Attribution | The authoritative association of a participant with acquisition provenance and, where applicable, a credited sponsor. |
| Attribution confirmed | The point at which required server-side attribution evidence and participant confirmation are complete. |
| Placement eligibility | The one-time approval transition whose preconditions prove attribution, payment, Area authority, sponsor/scope validity, and absence of prior placement; successful allocation and `Active` commit together. |
| Placement parent | The participant immediately above another participant in V2 topology. It does not imply recruitment. |
| Placement child | A participant occupying one of a parent's five slots. |
| Placement slot | An integer from 1 through 5 under a particular parent. |
| Placement position | A semantic, tree-local canonical number identifying topology; never a customer or participation database ID. |
| PlacementTreeScope | The stable identity of one root-specific AQGreen V2 placement tree inside a Tenant and programme. It is not a Tenant, Area, programme, or acquisition source. |
| Placement root | Position 1 of a PlacementTreeScope. Current V1 permits multiple independent roots per Tenant. |
| Sponsor-local subtree | The credited sponsor and every placement descendant beneath that sponsor within the same PlacementTreeScope. |
| Relative depth | Number of placement edges from an ancestor to a descendant. It is contextual, not a member property. |
| Generation occupancy | Number or identity of positions occupied at a relative depth. |
| Generation completion | Whether every required position at a relative depth satisfies the cutoff-effective qualification rule. |
| StructuralCompletionLevel | The highest complete Level 1/2/3 established from qualifying placement positions at a cutoff. It is independent of weekly sales and does not itself create commission entitlement. |
| CommissionCandidate | A participant with a StructuralCompletionLevel that may be evaluated for a commission period. Candidate status is not an earned, released, or paid commission. |
| WeeklySalesEligibility | The period-specific result of applying the versioned weekly sales-eligibility rule to acceptable evidence. It does not alter placement or StructuralCompletionLevel. |
| SalesEligibilityReview | The authorised evidence-review action that confirms, holds, or rejects the evidence used by the weekly sales-eligibility policy. It is not commission approval. |
| SalesEligibilityRulesVersion | The immutable identifier for the period-specific sales policy, initially `AQGreenWeeklySalesEligibilityV1`. |
| PaidAsLevel / CommissionedLevel | The level the system applies to the commission calculation after structural and period-specific eligibility gates. It is a financial result, not a topology fact. |
| Commission payout/release status | The separate held, released, paid, or equivalent state governed by existing financial controls after calculation. |
| Placement sequence | A monotonic ordering assigned by the server when a placement is committed within a PlacementTreeScope. |
| Rules version | The immutable placement/qualification semantics used for a decision, for example `AQGreenPlacementV2`. |
| V1 | Existing recruiter-derived, earliest-five network semantics. |
| V2 | The sponsor/placement-separated, five-wide sponsor-local spillover semantics in this specification. |

To avoid conflict with the repository's existing phrase “programme network,” this document uses two scopes:

```text
Programme network boundary = TenantId + Programme
Placement-tree scope        = one root-specific tree inside that boundary
```

`PlacementTreeScope` and `PlacementTreeScopeId` mean the latter root-specific tree. Under resolved `AQG-V2-D03A`, one Tenant/AQGreen programme may contain multiple explicitly authorised PlacementTreeScopes, each with exactly one explicit root.

## 5. Current-state implementation summary

The following is `CURRENT IMPLEMENTATION EVIDENCE`, verified by repository inspection, not a description of V2:

1. `EntryParticipation.RecruiterCustomerId` is the only persisted AQGreen recruiter/parent field. `StartUnderRecruiter` copies the recruiter's `CustomerId`, and recruiter correction mutates the same current field while appending correction history (`AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Core/Domain/Onyx/EntryParticipation.cs`).
2. AQGreen participation is created under a recruiter before joining payment and approval. It starts in `AwaitingJoiningPayment`; verified completion moves it to `PaymentConfirmedAwaitingApproval`; admin approval sets `Active` and `ActivatedAt`.
3. The domain and application do not enforce five children. EF has a non-unique index on `RecruiterCustomerId`; there is no parent-slot or child-cap invariant (`EntryParticipationConfiguration.cs` and migration `20260723040005_PersistOnyxProgrammeParticipation.cs`).
4. `EffectiveProgrammeNetwork` groups active participants by effective recruiter, orders children by `max(ActivatedAt, effective recruiter-placement time)` and participation ID, and takes five. Sixth and later children remain directly attributed to that recruiter but disappear from that recruiter's selected branch. There is no spillover.
5. `EntryNetworkQualificationEvaluator` recursively requires complete five-child branches for AQGreen Levels 1, 2, and 3. The structural algorithm is sound for a five-ary tree, but its input adjacency is the overloaded recruiter relation.
6. AQGreen commission calculation reconstructs this V1 network at a period cutoff. Loan eligibility and AQGreen-to-Onyx graduation also consume AQGreen Level 2. Progress APIs project depth populations from the same network.
7. Current statuses are `AwaitingJoiningPayment`, `AwaitingActivationPayment`, `PaymentConfirmedAwaitingApproval`, `Active`, and `Rejected`. There is no modeled post-Active suspended, terminated, deceased, refunded, or withdrawn state.
8. Current recruiter correction requires a dedicated permission, mandatory reason, same Tenant/programme, active replacement recruiter, no self-reference, and no cycle. Authorization is scoped to the target participant's current active Area; the same-Tenant replacement recruiter may belong to another Area. It can also clear a recruiter. It is not a V2 placement repair model.
9. At a historical cutoff, a V1 root is an effective Active participation whose recruiter resolves to null after applying cutoff-effective recruiter corrections. A current null `RecruiterCustomerId` is only a root candidate, because activation and correction effective times can change historical component membership. Ordinary self-service can call `StartIndependently`, and clearing a recruiter through correction can create another effective root/component.
10. The programme network boundary is currently Tenant plus programme. Same-Tenant recruitment may cross Areas, and Area does not partition qualification. Cross-Tenant relationships are prohibited. Multiple disconnected roots may exist and are evaluated as separate recruiter-connected components; there is no one-root-per-Tenant constraint.
11. Invitation attribution currently rechecks an Active same-Tenant/programme sponsor, Active sponsor Customer, and active sponsor Area. The legacy direct `RecruiterCustomerId` API path checks an Active same-Tenant programme participation but does not apply the invitation path's Customer/User/Area checks. This is a current contract inconsistency, not authority for weaker V2 sponsor admission.
12. A current monthly/loan financial restriction holds the affected member's own payout. It does not change participation status, recruiter history, topology, or an upline structural result. Customer/User suspension prevents access but does not itself rewrite programme participation or recruiter history.
13. Existing data preserves current recruiter, start/activation times, and effective-dated recruiter corrections. It does not preserve distinct inviter/acquisition source, credited sponsor versus placement parent, slot, canonical position, placement-tree scope, root-admission evidence, or historical selected-five snapshots.
14. The invitation route and authentication flow preserve invite code, Area, and return context through frontend query parameters and the backend proxy (`ProgrammeInvitationLanding`, `SignupForm`, `LoginForm`, and `app/api/backend/[...path]/route.ts`). That context is transient workflow plumbing, not durable V2 acquisition or sponsor attribution.
15. `YocoPaymentNotificationProcessor.EnsureNotificationMatchesCheckout` validates an optional supplied programme-specific merchant reference against the recorded checkout. `YocoPaymentsController` maps `payload.createdDate` to `ConfirmedAt`, but the exact chronology of successful payment remains unresolved; that field is not established as the successful-payment occurrence time.
16. `WeeklyCommissionCalculationLock` provides the existing transaction-owned commission-calculation lock. Separately, `AdminCommissionAppService.ReleaseAsync` and `RecordPaymentAsync` acquire `WeeklyCommissionPayoutMutationLock` by programme and commission before reading or mutating payout state. PostgreSQL conflict races have integration-test evidence in `AdminCommissionPayoutMutationPostgreSqlTests`; SQL Server support is inspection evidence only.
17. Migration `20260821120000_WidenCommissionRulesVersions` widened the Entry and Onyx commission-period and weekly-commission `RulesVersion` columns to `varchar(64)`. Those columns identify commission terms and do not provide separate V2 placement, structural-qualification, or sales-eligibility semantic versions.

Current member progress contracts use “direct recruits” for depth-1 selected recruiter children and use the same derived graph for deeper journey populations. The depth-1 label is not safe as a synonym for V2 placement children or credited recruits: spillover makes those populations different. V2 contracts must name `CreditedRecruits`, `ImmediatePlacementChildren`, and relative-depth occupancy/completion explicitly where each is intended.

### 5.1 Evidence strength

These findings are **verified by inspection** of current code, mappings, migrations, tests, and the `docs/aqua-system` pack. Focused runtime tests and production data were not executed or inspected for this documentation task. Existing tests demonstrate much of V1 behaviour, but exact earliest-five tie ordering is principally code-inspection evidence.

### 5.2 Decision evidence map

The principal `CURRENT IMPLEMENTATION EVIDENCE` inspected for D02, D03A/D03B, and D14A/D14B is:

| Decision | Repository evidence | What it establishes / does not establish |
| --- | --- | --- |
| D02 | `EffectiveProgrammeNetwork.cs`, `EntryNetworkQualificationEvaluator.cs`, `EntryNetworkQualificationEvaluatorTests.cs`, `EntryParticipationConfiguration.cs`, and `20260723040005_PersistOnyxProgrammeParticipation.cs` | V1 derives earliest-five recruiter adjacency and has no V2 allocator, slot, scope, sequence, or canonical position. D02 now supplies the authoritative V2 ordering. |
| D03A/D03B | `EntryParticipation.cs`, `ClubMemberProgrammeParticipationAppService.cs`, `AdminProgrammeParticipationAppService.cs`, `EffectiveProgrammeNetwork.cs`, `ProgrammeRecruiterCorrectionPolicies.cs`, and `08-area-and-tenant-boundaries.md` | V1 can start independently, can create effective roots through correction, evaluates disconnected components independently, and does not use Area as topology. Recruiter correction authorizes against the target participant's current active Area, while the same-Tenant replacement may be from another Area. D03A now governs prospective roots; this evidence still cannot classify legacy roots under D03B/D09. |
| D14A/D14B | `ProgrammeInvitationResolver.cs`, `ProgrammeRecruitmentPolicy.cs`, `ClubMemberProgrammeParticipationAppService.cs`, `ProgrammePaymentConfirmationProcessor.cs`, `AdminProgrammeParticipationAppService.cs`, `EntryWeeklyCommissionCalculator.cs`, and `02-business-rules-and-workflows.md` | Sponsor checks differ by attribution path; later payment/approval does not uniformly recheck sponsor state; current financial holds affect own payout rather than topology. D14A now governs later temporary restriction; terminal disposition remains D14B. |

The paths above are under `AqualLifeStyle/9.4.2/aspnet-core` unless shown as documentation. Exact implementation line numbers are intentionally not normative; authority comes from the classified rule/evidence, not a mutable source location.

## 6. Problems with the current model

`CURRENT IMPLEMENTATION EVIDENCE` and `DERIVED CONSEQUENCE`:

- one mutable field cannot truthfully represent acquisition, sponsor credit, topology, and correction history;
- a sixth recruit does not spill and therefore cannot obtain the required canonical V2 position;
- the database cannot enforce five-wide topology;
- qualification and money use a derived subset that is not persisted as historical topology;
- changing a recruiter changes both claimed provenance and structural ancestry;
- invitation/channel provenance is not retained on participation;
- no atomic allocator exists, so a naive V2 `SELECT` then `INSERT` would race;
- current financial ledgers record outcomes and commission terms version, but not a placement-rules version or topology evidence;
- migration cannot infer facts that were never stored.

V2 must introduce explicit semantics rather than rename `RecruiterCustomerId` and continue overloading it.

## 7. Confirmed business rules

The following are `CONFIRMED BUSINESS RULE`:

1. Every placed participant has at most five immediate placement children.
2. A sponsor's first five placement-eligible participants occupy the sponsor's own available immediate slots in canonical slot order.
3. Later placement-eligible participants credited to that sponsor search only the sponsor's placement subtree.
4. Placement search uses the resolved `AQG-V2-D02` deterministic sponsor-local parent-major breadth-first order in section 12.
5. Recruitment credit is not rewritten when spillover produces a different placement parent.
6. Acquisition provenance, credited sponsor, placement parent, participation state, and current eligibility are separate facts.
7. Permanent placement occurs only in the authoritative approval transition after attribution and payment evidence are confirmed.
8. The server selects topology. Members and ordinary administrators do not select parent, slot, scope, sequence, or canonical order.
9. Placement is permanent unless an elevated, audited repair workflow explicitly corrects proven error.
10. Temporary financial/programme ineligibility does not remove the participant or descendants from topology, release the slot, or cause compression.
11. Placement topology is the authoritative structural-location graph. Under resolved `AQG-V2-D01`, every cutoff-qualifying occupant contributes to the structural completion of each applicable placement ancestor according to relative depth, regardless of sponsor-credit ownership.
12. Structural completion does not itself create weekly commission entitlement. Weekly sales eligibility, system-calculated PaidAsLevel/CommissionedLevel, and payout/release status are separate facts.
13. Historical V1 money and graduation decisions are not silently reinterpreted under V2.

### 7.1 Economic consequences requiring confirmation

These consequences must be visible to business approvers before V2 qualification, company-assisted enablement, or cutover.

| Consequence | Status | Required interpretation |
| --- | --- | --- |
| **Spillover structural contribution:** another sponsor's spillover can complete a placement ancestor's structural level. | `AQG-V2-D01 — RESOLVED FOR MVP` | Every cutoff-qualifying occupant contributes at its relative depth to every applicable placement ancestor. Recruitment credit does not transfer. |
| **Weekly commission eligibility:** structural completion identifies a potential CommissionCandidate but does not create earning entitlement. | `AQGreenWeeklySalesEligibilityV1 — CONFIRMED MVP RULE` | For each commission week, require acceptable evidence of at least 5 sprays, 5 one-litre units, and 5 five-litre units, with no category substitution or carry-forward. |
| **Company-assigned leads:** assigning sponsor credit selects the subtree that receives the participant and may affect levels, commissions, and graduation. | `AQG-V2-D04`, `D05`, and `D13 — OPTIONAL COMPANY-FEATURE DECISIONS` | Keep company-assisted acquisition disabled until consent, Area ownership, assignment authority, bulk/dual control, and reassignment policy are confirmed. These decisions do not block ordinary member-invitation placement. |
| **Parent-major distribution:** earlier Level-1 branches receive all five upstream spillover placements before the next branch. | `AQG-V2-D02 — RESOLVED` | This deterministic, auditable concentration is accepted; spillover is not evenly distributed across sibling branches. |
| **Permanent occupied slots:** later temporary financial inactivity does not free a valid committed slot. | `CONFIRMED BUSINESS RULE` | Topology remains; separate eligibility rules determine whether the occupant contributes to a later decision. |
| **Deeper occupancy before shallower completion:** a descendant's sponsor-local subtree may spill deeper while an ancestor's shallower generation remains incomplete. | `DERIVED CONSEQUENCE` | Occupancy and level completion must remain separate projections. |

## 8. Acquisition vs sponsor credit vs placement

### 8.1 Required semantic records

`CONFIRMED BUSINESS RULE`: acquisition provenance, recruitment credit, and placement are separate facts. `ENGINEERING DECISION`: V2 requires separate durable records/projections for those facts. Names may follow repository conventions, but semantics may not be merged.

Conceptual attribution record:

```text
RecruitmentAttribution
----------------------
ParticipantId
TenantId
Programme = AQGreen
CreditedSponsorParticipantId?  // null only for an explicitly authorised root
AcquisitionSource
CampaignId? / SourceReference? // invitation/campaign/import reference, not a secret
AttributedAt                   // server time
AttributedBy?                  // system or authorised actor
AssignmentReason?
RulesVersion
```

Conceptual append-only confirmation record:

```text
RecruitmentAttributionConfirmation
----------------------------------
ParticipantId / AttributionId
ParticipantConfirmation
ConfirmedAt
ConfirmedBy / ConfirmationMethod
EvidenceReference
RulesVersion
```

Conceptual placement record:

```text
NetworkPlacement
----------------
PlacementTreeScopeId
ParticipantId
PlacementParentParticipantId?  // null only for position 1
PlacementSlot?                 // null only for position 1; otherwise 1..5
CanonicalPosition / CanonicalOrderKey // persisted or derived engineering representation
PlacedAt                       // authoritative server time
PlacementSequence
RulesVersion
PlacementSource
```

Placement sources are at least:

```text
NormalAllocation
LegacyCanonicalisation
AdministrativeCorrection
```

### 8.2 Provenance requirements

- `AcquisitionSource` must be an explicit controlled value such as `MemberInvitation`, `CompanyMarketing`, `AdminAssigned`, `LegacyCanonicalisation`, or another approved source.
- A source reference must identify persisted evidence where available without storing secrets or raw provider payloads.
- `CreditedSponsorParticipantId` references an AQGreen participation, not merely a customer.
- Attribution history must preserve original acquisition facts. A later sponsor correction must append an audited correction; it must not rewrite acquisition source to imply an invitation that never occurred.
- Placement must never be used to infer inviter or credited sponsor.
- Company-assisted attribution must additionally satisfy the individually auditable assignment rules in section 17. A batch identifier may group records but may not replace participant-level evidence.

### 8.3 Sponsor correction versus placement repair

`ENGINEERING DECISION`: these are separate privileged workflows:

- **Attribution correction** corrects sponsor-of-record/provenance under defined authority and records old value, new value, reason, evidence, actor, and effective time. Whether it can affect already-placed future economics is unresolved in section 34.
- **Placement repair** changes topology only to correct proven erroneous/corrupt placement. It requires elevated permission, impact analysis, append-only audit, and explicit financial/graduation reconciliation. It is not ordinary onboarding.

A repair must not overwrite the original placement as though it never existed. The eventual model must preserve effective-dated placement versions or an append-only correction record from which topology at any historical cutoff can be reconstructed. Exact repair mechanics, including descendant treatment, remain unresolved and must be authorised before that workflow is implemented.

## 9. Placement eligibility

### 9.1 Atomic approval and placement transition

`CONFIRMED BUSINESS RULE` and `ENGINEERING DECISION` are separated below.

Confirmed preconditions for normal V2 approval:

```text
AttributionConfirmed(P)
AND AuthoritativeJoiningPaymentVerified(P)
AND ParticipationStatus(P) = PaymentConfirmedAwaitingApproval
AND valid Area-admin approval command
AND participant is not already permanently placed
```

The payment term means verified, server-side evidence satisfying the existing AQGreen joining purpose, ownership, Tenant, amount, currency, provider identity/finality, and idempotency rules. A browser return, invitation creation, checkout creation, or unverified provider timestamp is insufficient. Current Yoco handling maps `payload.createdDate` into payment confirmation data, but that payload field is not established as the successful-payment occurrence time and does not resolve replay chronology. Sponsor/scope validity is also required. Under resolved `AQG-V2-D14A`, a later temporary financial/earning restriction alone does not invalidate confirmed attribution or block placement; terminal sponsor disposition remains open under `AQG-V2-D14B`.

`ENGINEERING DECISION`: execute one atomic database transaction:

```text
acquire placement-tree lock
recheck attribution, payment, status, Area authority, sponsor topology, and inherited PlacementTreeScope
allocate the approved deterministic placement
set Status = Active
set ActivatedAt from the authoritative server clock
persist placement and approval evidence
commit
```

Postcondition:

```text
Normal V2 Active participant => permanently placed
```

If placement cannot commit, approval does not commit, participation remains `PaymentConfirmedAwaitingApproval`, and the command remains safely retryable. No Yoco/provider, email, or other external call belongs inside the transaction; transactional outbox records may be committed for later delivery.

### 9.2 Timing

- Invitation acceptance may confirm normal member attribution, but does not place.
- Full verified payment may complete the joining obligation and funeral inclusion, but does not place before approval.
- Admin approval to `Active` is the normal final gate and commits atomically with allocation.
- `PlacementEligibleAt`, if retained as a projection, is the server-recorded approval/placement effective instant. It is not a prerequisite requiring `Active` before the transaction and is never client-controlled.
- `PlacedAt` and `PlacementSequence` are assigned by the allocator when placement commits.

Resolved D02, D03A, and D14A combine in the normal prospective flow:

```text
Attribution confirmed -> CreditedSponsor established
  -> PlacementTreeScope inherited from sponsor
  -> payment verified -> valid admin approval
  -> atomic Active + Placement
  -> sponsor-local parent-major BFS -> permanent topology
```

A later temporary financial/earning restriction of the credited sponsor does not recalculate sponsor credit, choose another tree, create a root, or delay this flow solely because of that restriction.

### 9.3 Ordering of concurrently eligible participants

`ENGINEERING DECISION`: canonical position selection is deterministic for a committed topology. The baseline for two truly concurrent approvals is server-authoritative serialized lock/commit order, recorded by `PlacementSequence`; browser or provider timestamps never break the tie. The transaction that acquires the PlacementTreeScope lock and commits first receives the earlier sequence and first canonical vacancy. Once committed, later discovery of another event does not displace it.

This decision favors immutable, auditable order over a false claim that distributed events have a perfectly knowable total wall-clock order. Under `AQG-V2-D12`, serialized commit order is the safe allocator design default and does not itself authorise implementation or production enablement. Production remains gated by implementation evidence and `AQG-V2-D03B`, `D09`, and `D10`. A different commercial priority requires an explicit subsequent decision and a durable server-side ordering fact; browser or provider timestamps are not an acceptable override.

### 9.4 Exceptional Active-but-unplaced reconciliation

`ENGINEERING DECISION`: for normal V2 onboarding, the final approval and placement must commit atomically in one database transaction. The workflow acquires the placement-tree lock, rechecks payment, attribution, and approval authority, records `Active` and placement, and commits both. Failure leaves the participation unapproved rather than Active-but-unplaced. No provider, email, or other external call occurs inside this transaction.

Active-but-unplaced is not a normal onboarding state. It may exist only because of migration, historical data, a deployment/version incident, corruption, or another explicitly classified recovery case. It is an exceptional reconciliation state. Any enabled qualification or dependent financial/graduation decision at a cutoff must fail closed while an unresolved allocation whose authoritative activation was at or before that cutoff could affect the evaluated subtree. A reconciler may use an authorised recovery action idempotently, but it must not invent or backdate placement or silently calculate around missing topology.

## 10. Five-wide topology

`CONFIRMED BUSINESS RULE`:

```text
0 <= immediatePlacementChildren(parent) <= 5
slot in {1,2,3,4,5}
one occupant per parent slot
one permanent placement per participant
```

For participant `X`, relative capacity is a consequence of a complete five-ary tree:

| Relative depth | Positions at exactly that depth |
| ---: | ---: |
| 1 | 5 |
| 2 | 25 |
| 3 | 125 |
| 4 | 625 |
| 5 | 3,125 |

These are topology capacities, not standalone qualification tests. In particular, `descendantCount >= 25` does not prove Level 2.

## 11. Sponsor-local spillover semantics

`CONFIRMED BUSINESS RULE`:

For an eligible participant `P` with credited sponsor `S`:

1. `S` must already have a V2 placement in the same Tenant, programme, and PlacementTreeScope.
2. Search begins at relative depth 1 under `S`.
3. Search never leaves `S`'s placement subtree.
4. A vacancy in a sibling branch outside `S`'s subtree is irrelevant.
5. The selected placement parent does not become the credited sponsor merely because it receives spillover.

`DERIVED CONSEQUENCE`: allocations credited to an ancestor and allocations credited to a descendant may compete for the same vacancy in the descendant's subtree. The PlacementTreeScope lock serializes both because they share one placement root.

`AQG-V2-D03A — RESOLVED CORE BUSINESS DECISION`: prospective V2 root and PlacementTreeScope policy is:

1. Every PlacementTreeScope has exactly one explicit root, and every placed participant belongs to exactly one PlacementTreeScope.
2. One Tenant/AQGreen programme may contain multiple explicitly authorised PlacementTreeScopes. There is no one-root-per-Tenant rule.
3. A root may be created only by an explicit privileged, audited bootstrap/root-creation operation. It requires dedicated authorization, explicit root intent, Tenant/programme authority, audit actor and server timestamp, reason/bootstrap reference, and a stable `PlacementTreeScopeId`.
4. Ordinary signup, missing recruiter/sponsor data, sponsor-resolution failure, company marketing source, and normal allocation cannot create a root or PlacementTreeScope.
5. For every normal non-root participant:

```text
Participant.PlacementTreeScopeId
  = PlacementParent.PlacementTreeScopeId
  = CreditedSponsor.PlacementTreeScopeId
```

6. The root has `PlacementParentParticipantId = null` and `PlacementSlot = null`. Every non-root has `PlacementParentParticipantId != null` and `PlacementSlot in 1..5`.
7. Normal allocation cannot cross PlacementTreeScope. Failure to resolve a valid credited sponsor, sponsor placement, or sponsor scope fails the complete approval/placement transaction closed; it never creates an implicit root.
8. Cross-Tenant placement remains prohibited. Area remains administrative scope, not topology identity.

`CURRENT IMPLEMENTATION EVIDENCE`: V1 supplies no durable root-admission fact. Null recruiter, independent start, and recruiter-clearing correction can each produce a historical root candidate, while qualification follows disconnected components independently. Those V1 mechanisms are not prospective V2 root creation.

`AQG-V2-D03B — OPEN / CUTOVER-MIGRATION DECISION`, related to `AQG-V2-D09`: legacy classification and mapping remain unresolved. D03B/D09 must decide which historical recruiterless participants are legitimate roots versus anomalies, how accepted populations map to PlacementTreeScopes, and how malformed, ambiguous, dangling, cyclic, deleted, or cross-Tenant evidence is classified. Prospective D03A implementation is not blocked by D03B; authoritative legacy canonicalisation and migration remain blocked.

`CURRENT IMPLEMENTATION EVIDENCE`: V1 sponsor admissibility is path-dependent. Invitation resolution rechecks an Active same-Tenant/programme participation, Active Customer, and active Area. The legacy raw recruiter-ID path checks only the Active same-Tenant programme participation. AQGreen stores attribution before payment/approval, payment confirmation does not revalidate the sponsor, and Area approval validates the invitee and approving Area authority rather than the sponsor's later state.

V2 must distinguish three questions:

```text
May S receive new recruitment credit?
Does S retain previously confirmed recruitment credit?
May a previously attributed participant still enter S's permanent subtree?
```

`AQG-V2-D14A — RESOLVED CORE BUSINESS DECISION`: when attribution to `S` was authoritatively confirmed while `S` was eligible to receive it, a later temporary financial/earning restriction does not change `CreditedSponsor`, invalidate attribution, block eventual placement, move the participant to another subtree, or create a root. After verified payment and valid approval, the participant inherits `S.PlacementTreeScopeId` and is placed normally in S's permanent subtree using resolved D02 ordering. S's temporary restriction affects S's own financial/earning state, not the existence of S's topology.

D14A applies to a commission/payment hold, temporary financial restriction, or temporary account restriction where programme placement and the original attribution remain valid. It does not authorise new attribution while a sponsor is otherwise ineligible, and it does not apply an ambiguous generic `Inactive` label to terminal states. Blocking an already-attributed participant solely because of such a temporary restriction is rejected because it can strand a paid/approved participant and stall downstream onboarding indefinitely.

```text
Placement topology         != Current earning eligibility
Confirmed attribution      != Current sponsor financial state
```

`AQG-V2-D14B — OPEN / EXCEPTIONAL WORKFLOW DECISION`: terminal sponsor disposition remains unresolved for death, permanent programme termination or exit, fraud removal, voluntary withdrawal, final refund/chargeback consequences, deletion/anonymisation, and other irreversible states. Preserve attribution history and committed topology while unresolved; affected pending placements fail closed, and the system must not silently reassign, invent another tree/root, or infer a terminal outcome from D14A. D14B governs pending placement after such a terminal event; D08 separately governs whether an already placed occupant contributes at a later qualification cutoff.

## 12. Canonical BFS allocation algorithm

### 12.1 Rule

`AQG-V2-D02 — RESOLVED CONFIRMED BUSINESS RULE`: use **sponsor-local breadth-first placement with deterministic parent-major / lowest-canonical-position ordering**.

**D02 decision box**

- **Decision:** fill A slots 1-5, then B slots 1-5, in canonical parent order, and continue parent-major breadth-first within the credited sponsor's subtree.
- **Accepted consequence:** earlier sibling branches may receive multiple upstream spillovers before later sibling branches receive one. This can change structural-completion timing under resolved D01.
- **Reasoning:** deterministic, reproducible, auditable, retry-stable, and suitable for deterministic replay once migration inputs and controls are authorised and verified.
- **Excluded order/policies:** round-robin (`A1, B1, C1...`), shortest-leg or dynamic fairness balancing, randomness, member choice, and ordinary-admin topology choice.

### 12.2 Formal algorithm

Given approval-ready participant `P` satisfying section 9 preconditions and confirmed credited sponsor `S`:

```text
Allocate(P, S):
  1. Resolve S's immutable AQGreen V2 PlacementTreeScope and placement.
  2. Acquire the transaction-scoped lock for that PlacementTreeScope/root.
  3. Re-read all authoritative gate and scope facts inside the transaction.
  4. If P is already placed, return exactly that placement.
  5. Reject conflicting attribution, cross-Tenant/programme scope, invalid topology,
     unplaced sponsor, or a participant already placed in another scope.
  6. Enumerate parents in S's subtree by:
       a. increasing relative depth from S, with S itself first as parent depth 0;
       b. increasing canonical PlacementPosition within the same depth.
  7. For each parent, inspect slots 1, 2, 3, 4, 5 in ascending order.
  8. Select the first unoccupied valid slot.
  9. Derive/validate the child canonical position.
 10. Assign the next PlacementTreeScope PlacementSequence and server PlacedAt.
 11. Insert one placement with the applicable RulesVersion and source.
 12. Commit. Database uniqueness is the final invariant guard.
```

The search is parent-major:

```text
A1, A2, A3, A4, A5,
B1, B2, B3, B4, B5,
...
```

The authoritative order is not round-robin:

```text
A1, B1, C1, D1, E1, ...  // not permitted by D02
```

Distribution consequence:

```text
X's 6th  -> A slot 1
X's 7th  -> A slot 2
X's 8th  -> A slot 3
X's 9th  -> A slot 4
X's 10th -> A slot 5
X's 11th -> B slot 1
```

This accepted order deliberately does **not** distribute spillover evenly across A through E. Determinism and the persisted topology/concurrency controls make it reproducible and auditable without discretionary placement.

### 12.3 Why this algorithm

Parent-major BFS is authoritative and independent of frontend/database return order. Round-robin, balancing, randomness, member-selected placement, ordinary-admin-selected placement, and post-placement optimization are not V2 allocator modes.

### 12.4 Search implementation

`RECOMMENDATION`: query persisted positions ordered by canonical position and find the first missing child position in the sponsor's descendant interval/set appropriate to the depth. A recursive CTE is acceptable if bounded and indexed; iterative breadth levels are also acceptable. Correctness and explainability take priority over speculative optimization. The implementation must prove that returned candidates are descendants of `S`, not merely low global positions.

## 13. Placement-position numbering

### 13.1 Canonical formula

`ENGINEERING DECISION`: define a one-indexed semantic canonical position within each `PlacementTreeScope`, with root position 1, for ordering and reproducibility:

```text
childPosition(parentPosition, slot)
    = 5 * parentPosition - 4 + slot

slot in 1..5
```

Equivalent children of parent `n` are:

```text
5n - 3, 5n - 2, 5n - 1, 5n, 5n + 1
```

Example:

```text
parent position 30
slot 1 -> 147
slot 2 -> 148
slot 3 -> 149
slot 4 -> 150
slot 5 -> 151
```

### 13.2 Topology authority and physical representation

`ENGINEERING DECISION`: `PlacementParentParticipantId + PlacementSlot` is the authoritative relational topology. Canonical ordering semantics are required; a persisted numeric position is not business semantics and is not independently editable.

Trade-off:

- a persisted exact canonical number improves ordering, migration comparison, visualization, and audit queries but adds redundant-data validation and numeric-mapping complexity;
- a materialized path can preserve canonical order and ancestry efficiently but requires path-integrity enforcement;
- parent plus slot with derived canonical position minimizes storage but may make subtree ordering and audit queries more expensive;
- another representation is acceptable only if it proves identical parent-major order, sponsor-subtree membership, replay determinism, overflow/representation failure handling, and historical auditability.

`RECOMMENDATION`: evaluate persisted arbitrary-precision non-negative numeric position, materialized path, and derived position during Phase B2. If numeric position is selected, use a verified exact .NET/Npgsql/PostgreSQL mapping because no maximum placement depth is authorised. Every candidate representation must fail rather than wrap, truncate, reorder, or bind topology to database identity generation.

### 13.3 Multiple roots

Position 1 is unique only within a `PlacementTreeScope`. A stable scope identifier prevents collisions between independent AQGreen roots. Tenant plus programme alone is not sufficient because multiple explicitly authorised roots are allowed.

## 14. Relative depth and generation

Placement depth is contextual:

```text
RelativeDepth(ancestor, descendant)
    = number of placement-parent edges between them
```

Example:

```text
X
└── A
    └── F
```

```text
RelativeDepth(X, F) = 2
RelativeDepth(A, F) = 1
```

`CONFIRMED BUSINESS RULE`: do not store an authoritative global `Participant.Level` for placement topology. A cached depth from the network root may be a validated projection, but it must not be presented as a universal generation and must be rebuilt/verified after privileged repair. Onyx programme level/tier is a separate concept.

## 15. Generation occupancy vs completion

`CONFIRMED BUSINESS RULE`:

- **Occupancy** asks which relative positions currently contain permanent placement records.
- **Structural completion** asks whether all required positions at a relative depth satisfy the authoritative cutoff-effective qualification rule.

Sponsor-local spillover can create deeper occupancy before an ancestor's shallower generation is complete. Therefore this assertion is false and prohibited:

> No depth-3 participant can exist until all depth-2 positions relative to every ancestor are full.

For participant `X`:

```text
Depth2Occupied(X) may be < 25
AND Depth3Occupied(X) may be > 0
```

Under resolved `AQG-V2-D01`:

```text
Level2Complete(X, cutoff)
iff all 25 required relative-depth-2 positions qualify at cutoff
```

APIs and UI must label occupancy/counts and completed levels separately.

## 16. Qualification semantics

### 16.1 Spillover structural contribution

`AQG-V2-D01 — RESOLVED FOR MVP`:

> A qualifying occupied placement position contributes to the structural completion of each applicable placement ancestor according to relative depth, regardless of sponsor-credit ownership.

Placement topology is the single structural qualification graph. Recruitment credit remains with the credited sponsor and is not transferred to a spillover placement parent.

```text
Placement topology determines the structural qualification relationships;
every occupant must still satisfy the authorised cutoff-effective eligibility rule.

A spillover participant can contribute to the placement parent's
5/25/125 completion even when another sponsor received recruitment credit.
```

Derived consequence: a member can obtain StructuralCompletionLevel 1, 2, or 3 partly or entirely through spillover, including participants credited to an ancestor. Company-generated acquisition remains feature-gated by D04, D05, and D13.

The resolved rule is recursive for every placement ancestor. One occupant can contribute to different relative levels simultaneously:

```text
X
└── A
    └── F
        └── K

F: CreditedSponsor = X, PlacementParent = A
K: CreditedSponsor = A, PlacementParent = F
```

- Qualifying F contributes to A's Level-1 depth-1 set and X's Level-2 depth-2 set, even though A did not recruit F.
- Qualifying K contributes to F's Level-1 depth-1 set, A's Level-2 depth-2 set, and X's Level-3 depth-3 set.
- Completing all five child positions under A can therefore complete A's Level 1; completing every required position at depth 2 from X can complete X's Level 2 regardless of which intermediate occupants received sponsor credit.
- No contribution skips a position or substitutes an aggregate descendant count: every required relative position must exist and satisfy the cutoff-effective eligibility rule.

This structural result is intentionally not automatic earning entitlement:

```text
StructuralCompletionLevel = Level 2
WeeklySalesEligibility    = NotMet
PaidAsLevel               = None
```

The participant retains structural Level 2. Section 24 defines the separate weekly sales gate, system-calculated commission result, and payout/release controls.

### 16.2 Conditional structural formula

Under resolved `AQG-V2-D01`, member `X` at authoritative cutoff `T` under rules version `R` is evaluated position-by-position:

```text
Level1Complete(X,T,R)
iff every one of the 5 relative-depth-1 positions exists
    and its occupant qualifies under R at T

Level2Complete(X,T,R)
iff Level1Complete(X,T,R)
    and every one of the 25 relative-depth-2 positions exists
    and its occupant qualifies under R at T

Level3Complete(X,T,R)
iff Level2Complete(X,T,R)
    and every one of the 125 relative-depth-3 positions exists
    and its occupant qualifies under R at T
```

`CONFIRMED BUSINESS RULE`: no descendant total can replace the position-by-position structural test, and AQGreen ends at Level 3. A placement parent does not become inviter or sponsor for qualification reporting. An occupant must satisfy the cutoff-effective structural contribution eligibility rule; D08 remains unresolved only for the listed post-Active lifecycle states.

### 16.3 Cutoff projection

`ENGINEERING DECISION`: V2 qualification consumes immutable placements whose `PlacedAt <= T`, plus the cutoff-effective participant eligibility rule. A later placement cannot qualify an earlier cycle. A later correction must not be projected backward.

### 16.4 Current eligibility evidence

`CURRENT IMPLEMENTATION EVIDENCE`: only `Active` AQGreen participations contribute to the current network. Current monthly overdue and applicable loan delinquency hold the member's own payout; they do not remove that participant from topology or currently impose an upline qualification penalty.

`CURRENT IMPLEMENTATION EVIDENCE`: Active-only participation is the V1 baseline. Whether future V2 suspension, restriction, termination, refund, or chargeback changes an occupant's qualification contribution is blocked by `AQG-V2-D08`; no new rule is inferred. Placement permanence remains separate from contribution eligibility.

## 17. Admin/company-assisted recruitment

### 17.1 Conditional assisted flow shape

`CONDITIONAL FLOW SHAPE` after `AQG-V2-D04`, `D05`, and `D13` resolution; this is not a confirmed enabled business workflow:

```text
Company marketing obtains participant
  -> authorised admin records AcquisitionSource = CompanyMarketing
  -> authorised admin assigns CreditedSponsor = X
  -> D04-authorised attribution/participant confirmation
  -> authoritative payment verification
  -> valid Area-admin approval command
  -> canonical allocator searches only X's placement subtree
  -> server selects topology and approval + placement commit atomically
  -> AQGreen becomes Active
```

The admin chooses recruitment credit, not topology.

### 17.2 Required controls

- treat sponsor assignment as a financially significant administrative action because it selects the placement subtree and may affect qualification, commission, and graduation;
- use a dedicated permission distinct from customer creation, programme approval, sponsor correction, and placement repair;
- validate sponsor eligibility, Tenant, programme, and placement-tree scope server-side under the authorised attribution policy;
- preserve one immutable participant-level assignment record containing `ParticipantId`, `AcquisitionSource = CompanyMarketing`, `CreditedSponsorParticipantId`, campaign/source reference, `AssignedBy`, `AssignedAt`, assignment reason/basis where applicable, and `RulesVersion`;
- preserve participant confirmation as a separate append-only record containing confirmation, `ConfirmationAt`, confirmation actor/method, evidence reference, and `RulesVersion`; a read model may present assignment and confirmation together without rewriting the assignment event;
- require participant confirmation under `AQG-V2-D04`; a client checkbox or administrator assertion is not authoritative unless that decision explicitly allows it;
- make every bulk assignment individually auditable and deterministically linked to a batch manifest, actor, reason, input hash, and per-participant outcome; a batch summary does not replace individual facts;
- prohibit silent sponsor reassignment; append corrections under the separately authorised policy;
- use the same payment, approval, and allocator path as member recruitment;
- do not let assisted onboarding bypass Area approval or provider verification;
- do not let ordinary admins select placement parent, slot, order, or position;
- do not describe the sponsor as inviter when company marketing acquired the participant.

The current repository has customer creation/import and post-participation recruiter correction, but no authoritative company-assisted AQGreen onboarding workflow. V2 implementation must not repurpose recruiter correction as that workflow.

`AQG-V2-D13 — OPTIONAL COMPANY-FEATURE DECISION` governs whether one authorised administrator is sufficient, whether high-value or bulk assignments require dual approval, and assignment or reassignment before permanent placement. `AQG-V2-D06` exclusively governs sponsor correction after placement. Company-assisted production enablement remains blocked until `D04`, `D05`, and `D13` are authorised; ordinary member-invitation placement is unaffected.

### 17.3 Company-assisted Area assignment

`CURRENT IMPLEMENTATION EVIDENCE`: invitation onboarding inherits and re-resolves the inviting participant's current active Area. The repository defines no company-marketing campaign Area or company-assisted sponsor-assignment path.

`AQG-V2-D05 — OPTIONAL COMPANY-FEATURE DECISION`: determine whether a company-assisted participant receives the credited sponsor's Area, a campaign Area, an explicitly authorised Area, or another authoritative Area source. `CreditedSponsor = X` does not by itself prove `Area = X.Area` for this new acquisition channel. The decision must define initial assignment evidence, who may assign it, and failure behavior when the Area is inactive or ambiguous. Once assigned, subsequent Area movement retains the repository's existing effective-dated history rules. Until then, the company-assisted channel remains disabled without affecting ordinary member invitations.

Area remains administrative ownership/approval scope; placement-tree scope remains network topology. Resolving Area must not let an administrator select topology.

## 18. Placement permanence

`CONFIRMED BUSINESS RULE`: a valid permanent placement is historical topology.

After placement:

- status or financial changes do not delete placement;
- a vacated-looking position is not reused;
- descendants are not compressed or silently reparented;
- a member cannot request movement because a later position is preferable;
- retry cannot move the participant;
- historical reports and decisions resolve topology by cutoff/rules version.

Example:

```text
Before restriction       After restriction
X                        X
└── A                    └── A [Restricted]
    └── F                    └── F
```

The topology is identical.

## 19. Participation, eligibility, and inactivity model

### 19.1 Separate dimensions

`ENGINEERING DECISION`: do not add one ambiguous `Inactive` flag. Model or project at least:

```text
Placement state:     Unplaced | Placed
Participation state: Pending | Active | ...future authorised states
Eligibility state:   Eligible | Grace | Restricted | Ineligible
```

Exact post-Active transitions are not authorised by this specification. Current persisted statuses remain the implementation baseline until a separate lifecycle decision is made.

This eligibility dimension concerns whether an occupant contributes structurally at a cutoff. It is not `WeeklySalesEligibility`, which is recalculated independently for each commission week under section 24.

### 19.2 Monitoring

`ENGINEERING DECISION`:

```text
authoritative domain/payment/admin event
  -> append auditable state transition
  -> update current eligibility projection

periodic reconciliation
  -> detect missed/stale/contradictory evidence
  -> retry deterministic transition or flag reconciliation
```

The scheduler is a recovery mechanism, not the sole source of truth. Each material transition must preserve:

```text
PreviousState
NewState
Reason
EffectiveAt
RecordedAt
Actor/System
EvidenceReference
RulesVersion
```

Do not invent monthly due-day or first-liability policy in placement code.

## 20. Concurrency

### 20.1 Threats

The design must handle:

- simultaneous eligibility for two participants;
- duplicate provider/admin/domain events;
- request and worker retries;
- multiple application instances;
- ancestor-sponsored and descendant-sponsored allocation competing in one subtree;
- admin-assisted and member-assisted allocation at the same time;
- process failure before commit or after commit before acknowledgement.

### 20.2 Baseline transaction

`ENGINEERING DECISION`: serialize allocation at the stable AQGreen V2 `PlacementTreeScope`/root boundary with a transaction-scoped PostgreSQL advisory lock. V2 allocation and this lock do not currently exist. The design follows the current transaction-owned `WeeklyCommissionCalculationLock` pattern; current payout release/payment mutations separately use `WeeklyCommissionPayoutMutationLock` keyed by programme and commission before read/mutate. Those existing patterns are evidence for lock ownership and scope, not evidence that V2 is implemented.

```text
BEGIN TRANSACTION

resolve stable PlacementTreeScope/root
acquire pg_advisory_xact_lock(derived PlacementTreeScope key)
re-read eligibility, attribution, sponsor placement, and existing target placement
validate topology and boundaries
if target already placed: return existing placement
find first canonical vacancy in sponsor subtree
insert placement and allocate sequence

COMMIT
```

Requirements:

- the lock key derivation must be stable, collision-aware, and shared by every V2 writer;
- do not hold the lock across provider calls, email, or other external work;
- all placement/repair writers must use the same lock boundary;
- database constraints remain authoritative if a writer is defective or a lock is missed;
- lock timeout/cancellation must roll back the complete approval/placement transaction and leave the still-unapproved approval command safely retryable;
- deadlock and transient database failure must be observable and safely retryable.

Row/range locking with equal or stronger proven semantics may replace advisory locking only through an explicit design review and PostgreSQL integration evidence.

## 21. Idempotency

`CONFIRMED BUSINESS RULE`:

```text
Allocate(P) once = Allocate(P) repeatedly
```

After successful placement, all retries return/preserve the same scope, parent, slot, position, sequence, source, and rules version. They do not rerun search and move `P`.

`ENGINEERING DECISION`:

- use `ParticipantId` within programme/network semantics as the natural idempotency identity;
- check existing placement after acquiring the lock;
- if the existing placement matches the request's authoritative attribution/scope, return it;
- if it conflicts, fail closed to reconciliation rather than overwrite;
- duplicate eligibility requests may be marked completed against the same placement;
- an insert unique violation must be classified: same participant means re-read/idempotent success if facts match; occupied slot means retry search only while still in the protected transaction and after proving no invariant breach.

## 22. Database invariants

`ENGINEERING DECISION`:

The eventual migration and EF configuration must enforce the semantic equivalents of:

```text
UNIQUE (TenantId, Programme, ParticipantId)
UNIQUE (PlacementTreeScopeId, PlacementParentParticipantId, PlacementSlot)
UNIQUE (PlacementTreeScopeId, PlacementSequence)
CHECK  (PlacementSlot BETWEEN 1 AND 5)
```

If the selected representation persists a canonical position/order key, it must also enforce semantic uniqueness within `PlacementTreeScopeId` and exact consistency with parent plus slot.

Root-specific constraints must ensure exactly one position-1 root per scope and require:

```text
root     -> Parent IS NULL, Slot IS NULL, CanonicalOrder = root
non-root -> Parent IS NOT NULL, Slot IS NOT NULL, CanonicalOrder follows parent/slot
```

Additional required invariants:

- scope belongs to exactly one Tenant and AQGreen programme, and each participant has exactly one permanent placement-tree scope;
- null parent/slot is legal only for the one root in a scope and requires durable authorised root-admission or accepted-legacy evidence;
- placement participant, parent, sponsor, and scope belong to the same Tenant and AQGreen programme;
- non-root parent has a placement in the same scope;
- under normal prospective V2 placement, participant, placement parent, and credited sponsor have the same `PlacementTreeScopeId`;
- ordinary allocation inherits the credited sponsor's established scope and cannot create a root or another PlacementTreeScope;
- participant cannot parent itself;
- canonical ordering is exactly reproducible from the parent/slot topology under the approved allocator version;
- cycles are impossible under normal immutable insert-only allocation and must be rejected by repair;
- attribution source and confirmation evidence satisfy required non-null rules;
- placement and attribution history are not hard-deleted to change financial facts;
- optimistic concurrency/audit fields follow repository conventions but do not replace uniqueness.

Database enforcement is mandatory, not optional:

- composite foreign keys must bind each placement participant and parent to the same Tenant, AQGreen programme, and placement-tree scope;
- attribution must bind its credited sponsor to the same Tenant and AQGreen programme through composite foreign keys, and database-side allocation validation must prove the sponsor's placement is in the target placement-tree scope;
- database constraints, and a trigger/constraint function where cross-row validation is required by the selected representation, must reject missing parents, root-shape violations, and canonical-order data inconsistent with parent plus slot;
- database permissions and immutable-history triggers must reject direct update/delete/truncate operations that would erase or rewrite attribution, placement, or correction evidence outside the authorised append-only repair procedure;
- the trigger must be installed by the migration, operate under the same transaction, and be covered by direct PostgreSQL negative tests;
- repair writers cannot bypass these constraints.

Application/domain checks provide clear errors and protect invariants before persistence, but they do not replace these database guards.

## 23. Authorization and security

### 23.1 Prohibited client authority

`CONFIRMED BUSINESS RULE`:

No customer/client input may select or override:

- placement parent;
- slot;
- canonical position;
- PlacementTreeScope/root;
- placement sequence;
- `PlacedAt`;
- rules version;
- another credited sponsor after authoritative attribution.

Do not accept these as hidden fields and then “validate” them. Derive them server-side.

### 23.2 Administrative authority

Confirmed/current boundaries:

- if company-assisted onboarding is authorised after D04, D05, and D13 are resolved, it may assign credited sponsor but not topology;
- programme approval remains Area-scoped under current authoritative policy;
- host authority does not permit a cross-Tenant network;
- payment confirmation remains provider/server-authoritative.

`ENGINEERING DECISION` controls, subject to `AQG-V2-D07` and `D13` authority decisions:

- company sponsor assignment uses a dedicated permission;
- manual attribution correction and placement repair use distinct elevated permissions;
- placement repair requires mandatory reason, evidence reference, before/after topology impact, actor, server times, and affected financial/graduation decision inventory;
- audit/query APIs must avoid exposing unnecessary personal or provider data.

### 23.3 Tenant and Area

`CURRENT IMPLEMENTATION EVIDENCE` adopted for V2:

- Tenant plus programme is the hard network/security boundary;
- same-Tenant placement may cross business Areas;
- Area scopes administration, not network topology;
- current invitation onboarding initially inherits/re-resolves recruiter Area, but later Area movement does not sever a same-Tenant network edge.

`ENGINEERING DECISION`: these existing Tenant/programme and Area boundaries are retained as V2 normative constraints. “Placement-tree scope” is the root-specific allocation/lock unit inside the broader Tenant-plus-programme network; it does not create a new security Tenant or Area boundary.

`AQG-V2-D07 — EXCEPTIONAL WORKFLOW DECISION`: decide whether elevated V2 attribution correction and placement repair are Tenant-wide or also require active Area authority over all affected participants. Current V1 recruiter correction is authorized against the target participant's current active Area; a same-Tenant replacement recruiter may belong to another Area. That target-Area/cross-Area pattern does not resolve V2 repair or correction authority. Manual V2 topology repair remains disabled until D07 is resolved; normal immutable placement is unaffected.

## 24. Commission dependencies

AQGreen weekly commissions are a direct financial consumer of StructuralCompletionLevel, but structural completion is only one input. The following facts must remain separate:

```text
StructuralCompletionLevel  // topology-derived and cutoff-effective
WeeklySalesEligibility     // commission-week-specific commercial gate
PaidAsLevel                // CommissionedLevel calculated by the system
Commission amount          // calculated by versioned commission rules
Payout/release status      // existing held/released/paid controls
```

A structurally qualified participant is a potential `CommissionCandidate`; that status is not an earning entitlement and does not make funds available.

### 24.1 AQGreenWeeklySalesEligibilityV1

`CONFIRMED BUSINESS RULE — AUTHORISED MVP RULE`:

For each applicable AQGreen commission week, the participant satisfies the quantity gate only when acceptable qualifying-sale evidence proves all three independent thresholds:

```text
QualifyingSpraysSold >= 5
AND QualifyingOneLitreUnitsSold >= 5
AND QualifyingFiveLitreUnitsSold >= 5
```

No product category substitutes for another:

```text
5 sprays, 5 x 1L, 5 x 5L   -> quantity gate satisfied
15 sprays, 0 x 1L, 0 x 5L -> NotMet
7 sprays, 8 x 1L, 4 x 5L  -> NotMet
```

The 5/5/5 threshold is the authorised MVP weekly sales-eligibility rule. It must be identified by `SalesEligibilityRulesVersion = AQGreenWeeklySalesEligibilityV1` so a future validated change does not rewrite historical commission decisions. The threshold belongs to commission-period eligibility policy, not `Participant`, `NetworkPlacement`, placement topology, or structural qualification.

Each commission week stands on its own. There is no MVP carry-forward, banking, borrowing, or substitution of excess sales between products or periods:

```text
Week 1: StructuralCompletionLevel = Level 2
        Sprays = 5, 1L = 6, 5L = 5
        WeeklySalesEligibility = Confirmed

Week 2: StructuralCompletionLevel = Level 2
        Sprays = 4, 1L = 8, 5L = 5
        WeeklySalesEligibility = NotMet
```

The Week-2 result does not remove Level 2, change placement, move descendants, or rewrite Week 1.

### 24.2 Qualifying sale and evidence

For MVP, a qualifying sale is a completed sale of the applicable water product supported by acceptable sales evidence and not cancelled or refunded at the time of the weekly eligibility decision.

```text
member obtains or buys inventory != automatic qualifying customer sale
inventory transfer                != automatic qualifying customer sale
member self-purchase for inventory != automatic qualifying customer sale
```

Because the current water-sales system is outside Aqua, MVP evidence review is manual. Acceptable evidence policy remains extensible and may include a sales receipt, invoice, payment evidence, sales register, or other authorised proof. This specification does not invent tax, invoice, or customer-identification rules. Evidence references/digests and minimum facts should be retained without unnecessary customer PII; raw evidence access must follow applicable privacy and retention controls.

### 24.3 Sales eligibility review

The authorised admin action is **Confirm Weekly Sales Eligibility**, not “approve commission.” The admin verifies whether acceptable evidence and independently counted product quantities satisfy `AQGreenWeeklySalesEligibilityV1` for the stated participant and commission week.

The conceptual `SalesEligibilityReview` supports repository-appropriate equivalents of:

| Outcome | Meaning |
| --- | --- |
| `Confirmed` | Evidence is acceptable and all three quantity thresholds are met. `WeeklySalesEligibility = Confirmed`. |
| `HeldForEvidence` | Evidence is missing, ambiguous, or awaiting review. Eligibility is not confirmed and no commission becomes available while held. |
| `Rejected` | Evidence is unacceptable, invalid, or does not support the claimed sales. `WeeklySalesEligibility = NotMet`. |

A verified quantity shortfall also produces `WeeklySalesEligibility = NotMet`; the admin cannot waive a product threshold or substitute quantities. Exact enum/entity allocation is an implementation-design choice, but these semantics are mandatory.

The admin does not determine structural level, placement, commission formula, commission amount, placement parent, placement slot, or payout release. The system derives structure and calculates the financial result from versioned rules.

### 24.4 MVP commission workflow

```text
Placement topology at commission cutoff
            -> system calculates StructuralCompletionLevel
            -> structurally qualified member becomes CommissionCandidate
            -> weekly water-sales evidence is available/submitted
            -> authorised admin performs SalesEligibilityReview
            -> sprays >= 5 AND 1L >= 5 AND 5L >= 5?
            -> evidence acceptable?
            -> Confirmed
                 -> system applies versioned commission rules
                 -> PaidAsLevel / CommissionedLevel and amount recorded
                 -> existing release/payment controls operate separately
            -> HeldForEvidence / Rejected
                 -> no PaidAsLevel or commission amount becomes available
```

Sales review and payout release must not be collapsed into one operation or one generic `AdminApproved` state.

### 24.5 Audit and separation of duties

The audit model must record separately, even where one authorised person performs more than one role:

```text
who assigned recruitment credit
who approved programme participation
who reviewed weekly sales eligibility
who released or paid commission
```

Each record requires its own action type, actor, server time, applicable period/effective time, reason or evidence reference where applicable, and rules version. This specification does not introduce an unauthorised dual-approval requirement.

Sales-eligibility review and commission release/payment require separately granted permissions; authority for one action does not grant authority for the other. The same person may hold both permissions if current authorization policy allows it, but each action remains separately authorized and audited. Different human actors or dual approval are not required for MVP unless separately authorised.

### 24.6 Financial recording and corrections

`ENGINEERING DECISION`:

- cycles before `V2EffectiveAt` use `AQGreenNetworkV1` semantics;
- cycles/cutoffs at or after `V2EffectiveAt` use `AQGreenPlacementV2` only when cutover is authorised and canonicalisation is accepted;
- the cutoff projection uses placements and structural contribution eligibility effective at that cutoff;
- the commission result records `StructuralCompletionLevel`, `WeeklySalesEligibility`, `SalesEligibilityRulesVersion`, `PaidAsLevel`/`CommissionedLevel`, amount/components, commission week/cutoff, financial terms version, placement/qualification rules version, review actor/time/outcome, and durable evidence references/digests sufficient for reproduction without PII-heavy snapshots;
- `WeeklySalesEligibility != Confirmed` cannot produce a PaidAsLevel or make a commission available for that week;
- payout/release remains subject to existing holds, authorization, and payment controls after calculation;
- no existing `Paid`, `Released`, `Held`, or `Earned` ledger is overwritten merely because V2 or later evidence gives a different result.

If evidence later proves a reviewed sale was cancelled, refunded, duplicated, fabricated, or otherwise invalid, preserve the original review and financial evidence, flag the period for reconciliation, and use an authorised linked correction/adjustment if required. Do not silently mutate history or design around unresolved `AQG-V2-D11` correction authority.

Current component/rate logic and own-payout hold mechanics remain independently valid; this specification does not redesign the commission formula or release process.

`CURRENT IMPLEMENTATION EVIDENCE`: `AdminCommissionAppService.ReleaseAsync` and `RecordPaymentAsync` acquire the per-programme/per-commission `WeeklyCommissionPayoutMutationLock` before loading and mutating the commission. PostgreSQL conflict races are integration-tested by `AdminCommissionPayoutMutationPostgreSqlTests`; SQL Server application-lock behavior is verified by inspection only. These controls preserve current payout mutations and do not authorise V2 correction or reconciliation.

### 24.7 Validation risk and scope boundaries

The 5/5/5 rule is the authorised design rule for a future MVP weekly gate. This specification does not implement or enable it and does not legally or economically validate it; its long-term commercial economics remain a validation hypothesis rather than an eternal invariant.

Commission source-of-funds and the relationship between participant payments, water/product sales, programme economics, and commission funding require separate business, legal, and economic validation. Weekly sales eligibility is not a substitute for that analysis. This specification makes no legal conclusion.

The weekly sales rule does not resolve D04, D05, or D13. Company-assisted acquisition remains disabled until its existing decisions are resolved, although an enabled company-assisted participant would later use the same structural and weekly sales rules. The weekly commission gate also does not enable or redefine loan offers, repayments, monthly due-day, first-liability month, or monthly worker automation.

## 25. Graduation dependencies

Current AQGreen-funded Onyx graduation revalidates AQGreen Level 2, so it is a transitive consumer of V2 placement.

`CONFIRMED BUSINESS RULE`: `AQGreenWeeklySalesEligibilityV1` is a weekly commission gate, not a graduation or permanent structural-completion gate. Graduation consumes the authorised StructuralCompletionLevel semantics and its separately authorised eligibility rules; this task does not add weekly water-sales evidence as a graduation condition.

Requirements:

- eligibility at/after cutover must use the authorised V2 cutoff/current decision semantics;
- authorization to graduate remains separate from eligibility and retains Tenant/Area/permission controls;
- the graduation decision must snapshot `RulesVersion`, decision/cutoff time, evaluated AQGreen level, and reproducible placement evidence reference;
- V2 must not silently invalidate an existing historical graduation;
- a historical correction requiring graduation remediation must be explicit, privileged, and audited;
- the resulting Onyx participation remains separate. V2 does not redesign Onyx placement.

The repository contains a permission-gated graduation endpoint that evaluates AQGreen Level 2. Before V2 cutover, that existing endpoint must either use the authorised V2 qualification semantics or be explicitly disabled; this adaptation does not require completing the broader loan/repayment journey.

Loan offer eligibility is also modeled as a Level 2 consumer, but the complete loan-offer and provider-repayment lifecycle is documented as `PLANNED / NOT ENABLED`. Placement V2 does not require completing or enabling it. If another such consumer is actually enabled at V2 cutover, that enabled path must use the authorised V2 qualification rules; otherwise its integration contract and future adaptation point are sufficient.

The same scope rule applies to monthly obligations: due-day, first-liability, worker enablement, and unresolved monthly automation are outside Placement V2. Existing monthly/loan cutoff facts may continue to hold a member's own payout where already implemented, but this specification neither enables those workflows nor invents a new upline effect.

## 26. Migration and canonicalisation strategy

No placement migration is authorised or migration-ready by this document alone. Read-only analysis may proceed, but authoritative canonicalisation or data application requires acceptance of `AQG-V2-D03B` and `D09`, followed by the applicable cutover controls.

### 26.1 Phase 1: read-only analysis/dry-run

Inventory, without exposing PII:

- effective AQGreen participants and roots by Tenant;
- participants with more than five V1 effective recruiter children;
- V1 selected-five ordering and cutoff evidence;
- affected networks and ambiguous scope/root cases;
- current and historical qualification levels;
- calculated, held, released, and paid commissions;
- loan eligibility/offers/agreements and graduations;
- recruiter correction chains;
- cycles, dangling references, mixed-Tenant evidence, deleted rows, duplicate customers, and discontinuous/equal correction times;
- records missing a recoverable distinct credited sponsor or acquisition source.

The analysis artifact must distinguish database observation from business interpretation.

### 26.2 Phase 2: deterministic V2 canonicalisation simulation

Canonicalisation operates over one complete accepted `PlacementTreeScope`, not independently per sponsor. Its frozen migration manifest contains accepted roots, population, sponsor edges, normalized ordering times, immutable participation identities, Tenant/programme/scope ownership, allocator/rules version, comparator version, and timestamp policy.

For candidate `P`, define one immutable replay key from accepted V1 evidence:

```text
ReplayKey(P) = (
    QualifiedUnderRecruiterAt normalized to UTC and declared precision ASC,
    ParticipationId under a versioned canonical UUID-byte comparator ASC
)
```

Do not assume PostgreSQL `uuid` and .NET `Guid` default comparators are equivalent. The implementation must define test vectors for the canonical comparator.

`ENGINEERING DECISION`: replay each scope as a deterministic priority-queue topological traversal:

```text
1. Establish only explicitly accepted roots at their authorised root placements.

2. Mark candidate P ready only when AcceptedLegacySponsor(P)
   already has a proposed V2 placement in this scope.

3. Put every ready candidate in one scope-wide priority queue ordered
   by its original immutable ReplayKey.

4. Remove the unique smallest ready candidate P.

5. Place P using the exact accepted/versioned V2 sponsor-local allocator
   over the complete topology produced by all earlier replay steps.

6. Assign the next deterministic scope-local PlacementSequence and
   PlacementSource = LegacyCanonicalisation.

7. Add candidates newly made ready by P, retaining their original
   ReplayKey; readiness never creates a new artificial priority.

8. Repeat until no ready candidate remains.

9. If any non-root candidate remains unplaced, fail the complete scope
   for dependency/cycle/missing-sponsor review. Never invent a root,
   sponsor, timestamp, or global placement fallback.
```

This is a deterministic Kahn topological traversal with a total-order priority queue. At each step the frozen sponsor graph and already-produced prefix determine the ready set; the canonical comparator selects one unique candidate; and the accepted allocator selects one unique vacancy. By induction, the next row and complete output are unique. Machine scheduling, input order, database return order, process count, batch size, and retry timing cannot alter output if they preserve this logical dequeue order.

Separate scopes may be computed in parallel only after `AQG-V2-D03B`/`D09` accepts the legacy roots, populations, and scope ownership. Canonical reports sort scopes by an accepted immutable scope/root key. Within a scope, workers must not claim ready records independently outside the logical priority queue.

The manifest must use a fixed authorised canonicalisation effective timestamp distinct from recording time. Retries may have different `RecordedAt` values but must produce the same effective placement facts and manifest hash. A crash either recomputes from the frozen manifest and verifies the complete applied prefix, or resumes from a persisted prefix whose rows/hash exactly match recomputation.

This output is a proposed V2 topology, not reconstruction of original historical placement. Unknown acquisition source remains unknown/legacy; do not fabricate `MemberInvitation`. Authoritative topology output requires population/provenance authority under `AQG-V2-D09` and legacy root mapping under `AQG-V2-D03B`; resolved D02 supplies the allocator order but does not authorise the migration population. Resolved `AQG-V2-D01` permits structural-completion simulation. `AQGreenWeeklySalesEligibilityV1` applies to post-V2 candidate periods; using it against V1 history is permitted only as explicitly labeled counterfactual analysis, never as a historical rule or result, and must not rewrite historical V1 results.

### 26.3 Phase 3: anomaly and impact review

Produce before/after reports such as:

```text
V1 effective direct recruiter children: 14
V2 immediate placement children:         5
V2 spillover placements:                  9
```

and, using resolved D01 structural semantics:

```text
V1 qualified level: Level 1
V2 simulated level: Level 2
Financial/graduation impact: review required
```

Reports must cover changes in current/future interpretation and identify already settled history separately. Ambiguous records enter an exception queue; no “best guess” row is inserted.

### 26.4 Phase 4: controlled cutover

Only after explicit acceptance:

- approve legacy root/PlacementTreeScope mapping and unresolved exceptions;
- take and verify a restorable backup;
- deploy additive schema/code in a safe order;
- run reviewed canonicalisation with source/version provenance;
- verify invariants and compare exact simulation output;
- establish `V2EffectiveAt` at an authorised financial boundary;
- switch qualification consumers atomically/feature-gated;
- retain V1 readers/evidence only for historical cutoffs with a concrete need;
- monitor eligible-unplaced records, allocator failures, anomalies, and consumer version use.

### 26.5 Live-write fence

`ENGINEERING DECISION`: accepted dry-run output cannot be applied while V1 topology keeps changing. Final canonicalisation requires a controlled Tenant/programme write fence:

1. stop new AQGreen starts, final approvals, and recruiter corrections that could alter the migration population;
2. allow in-flight transactions to finish and record a database high-water mark/snapshot identifier;
3. rerun the deterministic simulation from that exact snapshot and compare it with the reviewed result;
4. apply canonicalisation from the same fenced state and verify exact row/count/hash equality;
5. deploy and verify V2-capable writers/readers before lifting the fence;
6. route every post-fence final approval through atomic V2 placement; do not resume V1-only writes.

If operational policy cannot tolerate this maintenance fence, a separately reviewed dual-write/catch-up design with equivalent evidence is required. A best-effort dry run followed by live backfill is prohibited.

## 27. Historical financial integrity and rules versioning

### 27.1 Cutover rule

`CONFIRMED BUSINESS RULE`:

```text
financial cutoff < V2EffectiveAt
  -> AQGreenNetworkV1

financial cutoff >= V2EffectiveAt
  -> AQGreenPlacementV2
```

`V2EffectiveAt` is an authorised business/financial instant, not automatically migration completion or deployment time. Prefer a canonical AQGreen weekly-cycle boundary to avoid one cycle mixing topology rules.

### 27.2 Required snapshots

Material decisions/ledgers must preserve, where applicable:

```text
PlacementRulesVersion
StructuralQualificationRulesVersion
StructuralCompletionLevel
SalesEligibilityRulesVersion
WeeklySalesEligibility
SalesEligibilityReview outcome/actor/time/evidence reference
FinancialTermsVersion
DecisionAt / Cutoff
PaidAsLevel / CommissionedLevel
Amount and components
Payout/decision status
Placement/eligibility evidence reference or digest
Correction/adjustment linkage
```

Migration `20260821120000_WidenCommissionRulesVersions` makes the current Entry and Onyx commission-period and weekly-commission `RulesVersion` columns `varchar(64)`. They mainly identify commission terms and remain semantically insufficient as proof of separate placement, structural-qualification, or weekly-sales rules.

### 27.3 Corrections

Never mutate an original settled ledger to make it look as though V2 always applied. Use:

```text
original immutable ledger
+ authorised adjustment/correction entry
+ reason, actor, evidence, and rules version
```

Historical topology repair does not by itself authorize financial correction.

## 28. Onyx reuse seams

AQGreen is the only implementation target.

`RECOMMENDATION`: expose small seams that Onyx could later reuse:

- recruitment-attribution semantics;
- parent/slot/position value rules;
- sponsor-local candidate enumeration;
- allocator lock/idempotency contract;
- relative-depth queries;
- rules-version evidence.

Do not create a speculative generic network framework or merge AQGreen and Onyx aggregates. Onyx has separate participation, payments, recruitment, qualification depth, tiers, and financial rules. Prefer composition behind explicit domain/application interfaces within the current ABP dependency direction.

## 29. Behavioural examples

### 29.1 Scenario 1: first five

X's first five eligible sponsored participants occupy X slots 1 through 5:

```text
X
├── A [slot 1]
├── B [slot 2]
├── C [slot 3]
├── D [slot 4]
└── E [slot 5]
```

All have `CreditedSponsor = X` and `PlacementParent = X`.

### 29.2 Scenario 2: X's sixth participant

F is credited to X after X's five slots are occupied:

```text
X
├── A
│   └── F [A slot 1]
├── B
├── C
├── D
└── E
```

```text
CreditedSponsor(F) = X
PlacementParent(F) = A
RelativeDepth(X,F) = 2
RelativeDepth(A,F) = 1
```

The system does not claim A recruited F.

If F satisfies the cutoff-effective structural contribution rule, F simultaneously contributes to A's depth-1 structure and X's depth-2 structure. This is one placement record interpreted at two relative depths, not transferred recruitment credit.

### 29.3 Scenario 3: continued X spillover

Under resolved `AQG-V2-D02`, X's 7th through 10th eligible participants continue under A; the 11th starts B:

```text
X
├── A
│   ├── F [slot 1, X's 6th]
│   ├── G [slot 2, X's 7th]
│   ├── H [slot 3, X's 8th]
│   ├── I [slot 4, X's 9th]
│   └── J [slot 5, X's 10th]
├── B
│   └── L [slot 1, X's 11th]
├── C
├── D
└── E
```

This accepted behavior is not round-robin across A through E. Earlier sibling A receives all five of these upstream spillovers before B receives one.

F through J all have `CreditedSponsor = X`; A personally recruited none of them. Under resolved `AQG-V2-D01`, A has `StructuralCompletionLevel = Level 1` when all five occupants satisfy the cutoff-effective structural contribution rule. A's zero personal recruits do not prevent structural completion, but this result alone does not create weekly commission entitlement.

### 29.4 Scenario 4: A recruits after upstream spillover

After X's spillover filled A's five immediate slots, A sponsors K. Search is local to A:

```text
X
├── A
│   ├── F
│   │   └── K [F slot 1]
│   ├── G
│   ├── H
│   ├── I
│   └── J
├── B
├── C
├── D
└── E
```

```text
CreditedSponsor(K) = A
PlacementParent(K) = F
RelativeDepth(A,K) = 2
RelativeDepth(X,K) = 3
```

K does not move into B's branch even if B has empty positions relative to X.

If K is qualifying at the cutoff, K contributes simultaneously to F's Level-1 structure, A's Level-2 structure, and X's Level-3 structure, subject to complete 5/25/125 position-by-position requirements.

### 29.5 Scenario 5: mixed acquisition

Assume X's immediate slots are full and canonical vacancies under A are next. X personally recruits F, company marketing supplies G for X, and A personally recruits H after F and G exist:

```text
X
├── A
│   ├── F
│   ├── G
│   └── H
├── B
├── C
├── D
└── E
```

| Participant | Acquisition source | Credited sponsor | Placement parent | Depth from X | Depth from A |
| --- | --- | --- | --- | ---: | ---: |
| F | MemberInvitation | X | A | 2 | 1 |
| G | CompanyMarketing | X | A | 2 | 1 |
| H | MemberInvitation | A | A | 2 | 1 |

H searches A's subtree. Because A still has an immediate vacancy, H occupies A's next slot even though F and G arrived as X's spillover. If all five of A's immediate slots were occupied, A's next recruit would search parent-major beneath those children as shown in scenario 4.

### 29.6 Scenario 6: concurrency

P and Q receive simultaneous valid final approval commands while the first apparent vacancy is A slot 1:

```text
Transaction P                       Transaction Q
BEGIN                               BEGIN
lock PlacementTreeScope R acquired waits for R
recheck payment/approval/P         -
insert P at A slot 1               -
set P Active; sequence 106         -
COMMIT approval + placement        lock R acquired
                                    recheck payment/approval/Q
                                    A slot 1 now occupied
                                    insert Q at A slot 2
                                    set Q Active; sequence 107
                                    COMMIT approval + placement
```

The placement-tree lock serializes both transitions and database uniqueness prevents duplicate occupancy. If either placement fails, that transaction does not commit `Active`. Under the D12 default, whichever transaction commits first receives the earlier vacancy; no additional commercial-priority rule is required unless the business explicitly requests one.

### 29.7 Scenario 7: duplicate eligibility event

P is placed at A slot 1, canonical order N, sequence 106. The same approval command is delivered again:

```text
allocator acquires PlacementTreeScope lock
finds ParticipantId P already placed
validates matching scope/attribution
returns/preserves A slot 1, canonical order N, sequence 106
does not search again
```

### 29.8 Scenario 8: inactive/restricted participant

```text
Before                         After eligibility change
X                              X
└── A [Eligible]               └── A [Restricted]
    └── F [Eligible]               └── F [Eligible]
```

Placement rows, slots, positions, and ancestry do not change. Whether A counts for a future qualification cutoff is the D08 decision; restriction never compresses F upward. D14A separately permits an already-attributed, not-yet-placed invitee to enter A's subtree after a later temporary financial/earning restriction; D14B terminal disposition remains open.

### 29.9 Scenario 9: legacy parent with more than five children

V1 data:

```text
X -> A, B, C, D, E, F, G, H
```

Dry-run V2, using accepted V1 effective ordering only as canonicalisation input:

```text
X
├── A
│   ├── F
│   ├── G
│   └── H
├── B
├── C
├── D
└── E
```

Every generated placement is marked `LegacyCanonicalisation`. The simulation does not claim A recruited F/G/H and does not claim this topology existed historically.

### 29.10 Scenario 10: financial cutover

```text
Cycle cutoff 2026-09-03  -> V1 Level 1 -> original ledger remains V1
V2EffectiveAt 2026-09-04 00:00 Africa/Johannesburg
Cycle cutoff 2026-09-10  -> V2 Level 2 -> new ledger records V2
```

If V2 would have produced Level 2 for the earlier cycle, that observation does not mutate the V1 ledger. Any authorised correction is a linked adjustment.

### 29.11 Scenario 11: company marketing fills spillover

Company marketing supplies M1 through M5 and authorised attribution assigns all five to X. Under resolved D02 parent-major ordering, they occupy A's next five vacancies:

```text
X
├── A
│   ├── M1
│   ├── M2
│   ├── M3
│   ├── M4
│   └── M5
├── B
├── C
├── D
└── E
```

For every `Mi`:

```text
AcquisitionSource = CompanyMarketing
CreditedSponsor = X
PlacementParent = A
```

Each assignment requires the participant-level immutable audit in section 17, even if one deterministic batch groups them. Under resolved D01, qualifying M1-M5 structurally complete A's Level 1 despite being credited to X. This does not itself create A's weekly commission entitlement. Assignment/consent governance and Area remain blocked by `AQG-V2-D04`, `D05`, and `D13`; D01 does not enable the company-assisted channel.

### 29.12 Scenario 12: company lead and member recruit race

A company lead credited to X and a normal member recruit credited to X reach valid final approval concurrently for the same apparent vacancy. Both use the same approval transaction, placement-tree lock, sponsor-local allocator, database constraints, and idempotency rules. Acquisition channel grants no placement priority. The D12 default uses serialized commit order; neither administrator nor member can reserve the slot.

### 29.13 Scenario 13: temporary sponsor restriction after attribution

Day 1: S is eligible and F's attribution to S is authoritatively confirmed. Day 5: S becomes temporarily financially restricted. Day 8: F completes required payment and receives valid admin approval. Under resolved `AQG-V2-D14A`:

```text
CreditedSponsor(F) = S
PlacementTreeScopeId(F) = PlacementTreeScopeId(S)
S's committed placement remains occupied
F is placed in S's sponsor-local subtree using resolved D02
```

S's temporary restriction affects S's financial/earning state, not S's topology or F's confirmed attribution. Placement is not delayed solely for that restriction. If S is later restored, F is not reassigned, placed again, moved, or converted into a root; the existing placement remains authoritative and retries return it.

If S instead dies, permanently exits/is terminated, is removed for fraud, or has another irreversible state before F's placement, `AQG-V2-D14B` remains **OPEN**. This specification preserves history and does not invent F's terminal disposition.

### 29.14 Scenario 14: legacy cross-sponsor competition

Frozen V1 evidence contains candidates F and G credited to ancestor X and K credited to descendant A. Their immutable replay keys are:

```text
ReplayKey(F) < ReplayKey(K) < ReplayKey(G)
```

K is initially unready until A has a proposed placement. The scope-wide replay queue processes the smallest **currently ready** original key. Once A is placed, K enters with its unchanged key and may compete with F/G for vacancies in A's subtree according to the accepted allocator version. Arbitrary sponsor-by-sponsor batches are prohibited. Input permutations, batch sizes, process schedules, and retries must produce the same placement sequence and manifest hash; unresolved dependencies fail the complete scope.

### 29.15 Scenario 15: structural Level 2 with period-specific sales eligibility

```text
A.StructuralCompletionLevel = Level 2

Commission Week W:
Sprays sold: 5 / 5  PASS
1L sold:     7 / 5  PASS
5L sold:     3 / 5  FAIL

WeeklySalesEligibility = NotMet
PaidAsLevel = None
Commission eligibility = not satisfied for W
```

A retains structural Level 2 and the placement tree is unchanged.

```text
A.StructuralCompletionLevel = Level 2

Commission Week W+1:
Sprays sold: 8 / 5  PASS
1L sold:     6 / 5  PASS
5L sold:     5 / 5  PASS

SalesEligibilityReview = Confirmed
WeeklySalesEligibility = Confirmed
PaidAsLevel = Level 2
Commission = calculated by the system under the applicable commission rules
```

A does not rebuild the placement structure between weeks. Excess Week-W+1 quantities are not banked or substituted into another product category or period.

### 29.16 Scenario 16: explicit roots and multiple prospective trees

Under resolved `AQG-V2-D03A`, one Tenant may contain multiple explicitly authorised AQGreen PlacementTreeScopes:

```text
Tenant T
└── AQGreen
    ├── PlacementTreeScope R1 -> explicit Root R1
    └── PlacementTreeScope R2 -> explicit Root R2
```

S belongs to R1 and F is credited to S. Therefore F must inherit R1. If S's placement or R1 cannot be resolved, F does not become a root, move to R2, or create a new PlacementTreeScope; approval/placement fails closed and remains retryable.

This prospective rule does not classify V1 recruiterless participants. Historical root legitimacy and population-to-scope mapping remain open under `AQG-V2-D03B` and `AQG-V2-D09`.

## 30. Acceptance criteria

The project is intentionally separable. Technical groundwork may begin where it cannot encode an unresolved economic policy; dependent paths remain blocked by stable decision IDs.

### 30.1 Can implement independently

Resolved `AQG-V2-D01` now permits structural qualification work. The following can proceed without enabling deferred products or resolving unrelated optional/exceptional decisions:

1. Acquisition provenance, credited sponsor, and placement parent are separate queryable/auditable facts.
2. Attribution persistence and immutable participant-level company-assignment audit support the unresolved governance choices without selecting them.
3. PlacementTreeScope, explicit-root, parent-plus-slot, and sponsor-scope invariants enforce one root per scope, at most five children per parent, and one permanent placement per participant.
4. A sponsor-local allocator implements the resolved D02 parent-major order; production cutover remains separately gated by D09/D10.
5. Concurrent allocation cannot duplicate a slot or participant, and retry preserves the original placement.
6. The disabled/non-production integration path proves normal final approval and placement commit atomically; failure leaves participation non-Active and retryable. Production use remains subject to the applicable decision IDs.
7. Relative depth, occupancy, ancestry, and canonical order can be reproduced without treating them as qualification.
8. Ordinary clients/admins cannot choose topology, and cross-Tenant placement fails closed.
9. Audit history, exceptional Active-but-unplaced detection, and read-only V1 inventory tooling are available.
10. Deterministic tree-wide replay mechanics produce identical manifests for identical frozen test inputs independent of process order, machine, batch size, or retry; final legacy population/scope inputs remain subject to D03B and D09.
11. StructuralCompletionLevel is calculated from qualifying placement positions at relative depth, including recursive spillover contribution, without changing sponsor credit.
12. A CommissionCandidate's weekly commission calculation requires `AQGreenWeeklySalesEligibilityV1` and an auditable admin evidence review; the system then calculates PaidAsLevel/CommissionedLevel and amount, after which existing payout/release controls apply separately.
13. An already-confirmed attribution continues through normal placement when the credited sponsor later has only a temporary financial/earning restriction under D14A; affected pending placements encountering a D14B terminal state fail closed until D14B is resolved.

### 30.2 Resolved and remaining dependency boundaries

- **Structural qualification, progress, and structural graduation input:** no longer blocked by D01; use the resolved relative-depth contribution rule. D08 still governs contribution at cutoffs involving its unresolved post-Active states.
- **MVP weekly commission eligibility:** no longer blocked by D01; use StructuralCompletionLevel plus `AQGreenWeeklySalesEligibilityV1` and confirmed evidence, subject to existing commission and payout controls.
- **Prospective allocator and root/scope model:** no longer blocked by D02 or D03A. Use the resolved parent-major order, explicit privileged root creation, and sponsor-scope inheritance. Production enablement remains subject to implementation evidence and D10.
- **Legacy root classification and final placement-tree scope mapping:** blocked by `AQG-V2-D03B` and `AQG-V2-D09`; prospective root creation and normal placement are not.
- **Company-assisted production enablement:** blocked by `AQG-V2-D04`, `AQG-V2-D05`, `AQG-V2-D13`, and D14B only when a terminal sponsor case is encountered; keep the channel disabled without blocking ordinary member invitations.
- **Sponsor reassignment after placement:** blocked by `AQG-V2-D06`.
- **Manual topology repair:** blocked by `AQG-V2-D07` and `AQG-V2-D11` where historical decisions are affected.
- **Post-Active contribution for affected states:** blocked by `AQG-V2-D08`; topology remains immutable.
- **Pending placement after sponsor state changes:** temporary financial/earning restriction is resolved by D14A and does not block placement; terminal disposition remains blocked only for affected participants by D14B.
- **Canonicalisation population and provenance:** blocked by `AQG-V2-D09`.
- **Migration cutover and V2 financial effective boundary:** blocked by `AQG-V2-D10`, `AQG-V2-D03B`, and `D09`; resolved D01, D02, and D03A supply target semantics but do not authorise migration or cutover.
- **Alternative concurrent commercial priority:** requires `AQG-V2-D12`; absent such a request, serialized commit order is the non-blocking default.

Deferred loan/repayment and monthly-obligation automation are not Placement V2 acceptance criteria. Only an actually enabled consumer at cutover must be adapted so it cannot continue using contradictory V1 topology.

## 31. Tests required for implementation

### 31.1 Domain tests

- authorised-root shape and evidence, exactly one root per scope, and rejection of ordinary null-parent allocation;
- parent/slot invariants and the selected canonical-order representation, including root and representation-failure cases;
- first five and continued parent-major spillover;
- sponsor-local search when ancestor sibling branches are empty;
- contextual relative depth;
- occupancy versus structural completion;
- recursive 5/25/125 structural completion under resolved D01, including one qualifying occupant contributing to multiple ancestors at different relative depths;
- no qualification from aggregate descendant count alone;
- topology permanence under eligibility transitions;
- cycle/self-parent/cross-scope rejection for repair.

### 31.2 Application tests

- explicit root admission is permissioned/audited and separate from normal allocation; normal allocation inherits sponsor scope and cannot create or cross a scope;
- invitation attribution confirmation without early placement;
- payment-confirmed-awaiting-approval remains unplaced;
- approval and placement commit atomically, with failure leaving the approval retryable and participation non-Active;
- company marketing source plus admin-assigned sponsor plus canonical server placement;
- company-assisted Area assignment, consent, single/bulk audit, and reassignment behavior after `AQG-V2-D04`, `D05`, and `D13` are resolved;
- ordinary admin/client topology fields rejected or absent from contracts;
- attribution and placement correction permissions are distinct;
- same-Tenant cross-Area success and cross-Tenant denial;
- invitation and legacy recruiter-ID sponsor admission cannot bypass the accepted V2 sponsor-state policy;
- D14A preserves confirmed attribution and permits placement after a later temporary restriction, including retry/restoration without reassignment, duplicate placement, topology movement, or root creation;
- D14B terminal states fail closed without invented disposition or silent reassignment;
- failure/retry around the atomic approval-and-placement transaction;
- duplicate payment and approval commands, plus exceptional reconciliation retries;

### 31.3 PostgreSQL integration tests

- root-admission evidence and exactly-one-root-per-scope constraints, including concurrent root attempts;
- every unique/check/foreign-key invariant against the real provider;
- two connections racing for one vacancy;
- ancestor and descendant sponsor allocations racing in one network;
- advisory lock behavior across application instances;
- rollback after lock, sequence allocation, or insert failure;
- idempotent replay after commit-before-acknowledgement;
- the selected candidate query/representation returns exact accepted canonical vacancies and proves sponsor-subtree boundaries;
- representation-specific ordering, overflow/failure, persistence, and EF/Npgsql/PostgreSQL round-trip tests; arbitrary-precision tests apply only if that representation is selected;
- direct update/delete/truncate attempts cannot erase or rewrite placement, attribution, or correction history;
- repair and normal allocator share the lock;
- tree-wide migration replay permutation, database-return-order, batch-size, machine/process schedule, crash/restart, and retry invariance;
- canonical timestamp normalization and UUID comparator conformance vectors across .NET and PostgreSQL;
- newly ready candidates retain original priority, including a candidate whose key predates its sponsor and cross-sponsor subtree competition;
- migration simulation/apply row, count, order, and manifest-hash equality, with dependency/cycle/missing-sponsor anomalies failing the complete scope.

### 31.4 Financial and workflow tests

- V1 cutoff before effective instant and V2 cutoff after it;
- no mixed rules in one cycle;
- placement after cutoff excluded from old qualification;
- a pre-cutoff unresolved exceptional allocation causes qualification and every dependent financial/graduation decision to fail closed;
- structural completion persists across weeks with different sales-eligibility results;
- all three `AQGreenWeeklySalesEligibilityV1` thresholds pass independently, with no category substitution, carry-forward, or banking;
- 15/0/0 and 7/8/4 product mixes fail while 5/5/5 passes when evidence is acceptable;
- inventory acquisition alone is rejected as proof of a qualifying customer sale;
- `Confirmed`, `HeldForEvidence`, and `Rejected` reviews have distinct effects and audit evidence;
- the admin cannot set StructuralCompletionLevel, PaidAsLevel, commission formula, amount, or payout/release status through sales review;
- commission result records placement/structural, sales-eligibility, and financial rules versions plus the review evidence;
- later cancellation, refund, duplication, fabrication, or invalidation flags reconciliation and preserves original evidence;
- settled ledger remains unchanged after V2 canonicalisation;
- adjustment workflow links rather than overwrites;
- the existing graduation endpoint consumes accepted V2 Level 2 semantics and retains authorization controls, or is explicitly disabled for cutover; deferred loan/repayment paths need only preserve an explicit future integration seam;
- progress/reporting APIs distinguish credited recruits, immediate placement children, relative-depth occupancy, and completed levels;
- frontend visualization and labels do not infer sponsor identity from placement parent or call deeper structural populations direct recruits;
- end-to-end first-time member journey, including D14A temporary-restriction continuation; company-assisted journey only after `AQG-V2-D04`, `D05`, `D13`, and any encountered D14B disposition are resolved;
- interrupted workflow, delayed notification/reconciliation, refresh, retry, duplicate action, and direct API attempts.

### 31.5 Existing tests requiring replacement or rework

Fixtures/assertions that construct structural trees with `StartUnderRecruiter` or assume `RecruiterCustomerId` is parent include:

- `EntryNetworkQualificationEvaluatorTests.cs`;
- `EntryWeeklyCommissionTests.cs`;
- `OnyxLoanAgreementTests.cs`;
- `OnyxLoanAppServiceTests.cs`;
- `OnyxProgrammePersistenceTests.cs`;
- `AdminCommissionAppServiceTests.cs`;
- `WeeklyCommissionCalculationPostgreSqlTests.cs`;
- `ClubMemberProgrammeProgressAppServiceTests.cs`;
- programme journey/progress frontend fixtures and tests.

Preserve independently valid assertions for 5/25/125 completion, rates/components, payout holds, payment verification, Area authorization, loan accounting, and immutable outcomes. Replace only their V1 topology setup/expectations when implementation begins.

## 32. Future migration validation prerequisites

Before a future V2 migration can be considered for merge or application, after `AQG-V2-D03B` and `D09` acceptance:

- run the read-only inventory against a sanitized production-like copy;
- publish counts and hashes, not personal data;
- prove deterministic dry-run repetition;
- compare every proposed placement with accepted input ordering;
- classify ambiguous, dangling, cyclic, cross-Tenant, duplicate, and deleted evidence;
- inventory all affected commissions, payouts, loans, and graduations;
- verify additive deployment ordering and mixed-version compatibility;
- verify EF model/snapshot alignment;
- test PostgreSQL `Up`, rollback consequences, uniqueness, and fail-closed guards;
- prove rerun idempotency and crash recovery;
- verify backup restoration;
- require explicit sign-off for financial/graduation interpretation changes;
- refuse cutover while unexplained differences remain.

`Down` must not pretend it can safely erase V2 topology after production decisions consume it. Prefer reviewed forward remediation or verified restoration once historical use exists.

## 33. Operational and deployment considerations

- deploy schema support before writers and readers that require it;
- ensure old application instances cannot write incompatible V1 relationships after V2 enablement;
- feature-gate V2 by Tenant/network and authorised effective instant;
- do not enable automatic placement until canonicalisation and consumer readiness are verified;
- observe exceptional Active-but-unplaced count/age, lock wait/failure, unique violations, retry count, reconciliation count, and allocator latency;
- alert immediately on Active participants without placement under the exceptional reconciliation service-level objective;
- expose read-only audit diagnostics for sponsor, parent, slot, position, sequence, source, rules version, and evidence references;
- redact PII and never log provider secrets/raw callbacks;
- document allocator pause/resume and incident recovery;
- prohibit ad hoc SQL placement repair;
- coordinate cutover with commission cycle closure and worker enablement;
- retain V1 historical projection capability for old cutoffs as long as financial/audit retention requires it.

## 34. Business decision register

Every decision has a stable identifier, status, and bounded dependency category. Resolved D01 remains in the register for traceability. Implementation work must cite open IDs rather than silently selecting a policy, but an open optional or exceptional decision must not be promoted into a blocker for unrelated normal placement.

### 34.1 Core placement decisions

| ID | Status | Decision | Exact dependency / safe state |
| --- | --- | --- | --- |
| `AQG-V2-D01` | **RESOLVED FOR MVP** | **Spillover structural contribution:** every cutoff-qualifying occupied position contributes to each applicable placement ancestor according to relative depth, regardless of sponsor credit. Structural completion is separate from weekly earning eligibility. | Structural qualification/progress may proceed. MVP commission eligibility uses `AQGreenWeeklySalesEligibilityV1`; long-term threshold economics remain a non-blocking validation risk. |
| `AQG-V2-D02` | **RESOLVED** | **Sponsor-local placement ordering:** deterministic parent-major BFS, A1-A5 then B1-B5, continuing breadth-first in canonical parent order within the credited sponsor's subtree. Earlier siblings receiving multiple spillovers before later siblings receive one is accepted. | Prospective allocator work may proceed; D09/D10 still govern migration/cutover. |
| `AQG-V2-D03A` | **RESOLVED** | **Prospective root/scope policy:** one explicit privileged/audited root per PlacementTreeScope; multiple scopes per Tenant/AQGreen programme are allowed; every normal non-root inherits the credited sponsor's scope. | Prospective root, scope, topology, locking, and normal allocation work may proceed. Missing sponsor/scope fails closed and cannot create a root. |
| `AQG-V2-D03B` | **OPEN / MIGRATION-BOUND** | **Legacy root-to-PlacementTreeScope mapping:** classify legitimate historical roots/anomalies and approve population/scope mapping. | Blocks authoritative legacy canonicalisation/migration only; related to D09. It does not block prospective D03A behavior. |
| `AQG-V2-D14A` | **RESOLVED** | **Temporary sponsor restriction after confirmed attribution:** preserve sponsor and scope; after valid payment/approval, place normally in the original sponsor's subtree using D02. | Temporary financial/earning restriction alone no longer blocks prospective placement. Sponsor earning/release remains separate. |
| `AQG-V2-D14B` | **OPEN** | **Terminal sponsor disposition:** treatment after death, permanent exit/termination, fraud removal, withdrawal, final refund/chargeback, or other irreversible state. | Blocks only pending placements that encounter a terminal state. Preserve history and fail closed without reassignment or an invented root. |

### 34.2 Cutover and migration decisions

All decisions in sections 34.2 through 34.4 remain **OPEN** unless a later authoritative record explicitly resolves them.

| ID | Decision | Exact blocked scope | Safe state while open |
| --- | --- | --- | --- |
| `AQG-V2-D09` | **Legacy migration population and provenance:** accepted records, sponsor evidence, unknown source classification, ordering evidence, D03B root/scope mapping, and exception authority. | Authoritative legacy manifest and migration of unresolved records. | Run read-only inventory and synthetic/frozen-input replay tests only. |
| `AQG-V2-D10` | **V2 effective cutover:** financial instant, Tenant/global scope, cycle alignment, and activation authority. | Production cutover and post-cutover financial use. | Keep V2 disabled; retain V1 authority. |

### 34.3 Optional company-feature decisions

| ID | Decision | Exact blocked scope | Safe state while open |
| --- | --- | --- | --- |
| `AQG-V2-D04` | **Company-assisted attribution confirmation:** participant consent/evidence and confirmation authority. | Company-assisted onboarding only. | Disable company-assisted acquisition. |
| `AQG-V2-D05` | **Company-assisted Area assignment:** authoritative Area source and inactive/ambiguous behavior. | Company-assisted ownership and approval routing only. | Disable company-assisted acquisition. |
| `AQG-V2-D13` | **Company-assignment governance:** single/dual approval, bulk controls, batch audit, and assignment/reassignment boundaries before placement. | Company-assisted pre-placement assignment operations only; D06 exclusively governs post-placement sponsor correction. | Disable company-assisted acquisition and pre-placement reassignment. |

### 34.4 Exceptional and correction decisions

| ID | Decision | Exact blocked scope | Safe state while open |
| --- | --- | --- | --- |
| `AQG-V2-D06` | **Sponsor correction after placement:** reporting, future economics, topology, or prohibition pending reconciliation. | Post-placement sponsor correction. | Reject the correction; preserve original facts. |
| `AQG-V2-D07` | **Placement repair authority:** permission/scope, evidence, dual approval, descendants, outcomes, and audit visibility. | Manual topology repair. | Disable repair; surface the anomaly for investigation. |
| `AQG-V2-D08` | **Post-Active lifecycle contribution:** cutoff contribution after suspension, termination, withdrawal, death, refund/dispute/chargeback, inactivity, deletion, or security disablement. | Qualification and dependent decisions involving affected states, not topology. | Preserve placement; fail closed for an affected unresolved cutoff. |
| `AQG-V2-D11` | **Historical financial correction authority:** adjustment treatment for commission, loan, and graduation decisions. | Historical financial remediation after an accepted correction. | Do not rewrite settled history or issue an adjustment. |
| `AQG-V2-D12` | **Concurrent commercial priority override:** whether the business requires an ordering fact other than serialized lock/commit order. | Only implementation of an alternative priority policy. | Use recorded serialized commit order; this is not a normal allocator blocker. |

### 34.5 Dependency matrix

`Required` means the decision must be resolved for that deliverable. `Conditional` means it applies only when that optional/exceptional path is included or encountered.

| Deliverable | Required decisions | Conditional decisions | Explicitly not required |
| --- | --- | --- | --- |
| Additive topology model, constraints, lock/idempotency mechanics | None; D02 and D03A are resolved | None | Open migration, company, correction, and terminal-state decisions |
| Prospective parent-major allocator and explicit-root workflow | None; D02 and D03A are resolved | D14B only for an encountered terminal sponsor state | D03B/D09 legacy mapping, D04/D05/D13 company channel |
| Normal member placement in an established scope | None; D02, D03A, and D14A are resolved | D14B after terminal sponsor state | D03B, D04-D13 otherwise |
| V2 StructuralCompletionLevel and progress | None; D01 is resolved | D08 for affected post-Active states | D02, D03A/D03B, D04-D07, D09-D13, D14A/D14B unless that path also runs |
| MVP weekly commission eligibility | No open decisions; D01 and `AQGreenWeeklySalesEligibilityV1` are resolved authorities | D08 for affected structural inputs; existing commission rules after the gate and payout controls after calculation | Unrelated optional decisions |
| Company-assisted onboarding | D04, D05, D13 | D14B after terminal sponsor state | D01, D02, D03A, D14A |
| Final legacy canonicalisation and structural comparison | D03B, D09 | D08 for affected cutoff contribution | D01, D02, D03A, D04-D07, D10, D14A/D14B otherwise |
| Production cutover | D03B, D09, D10 | D04/D05/D13 for company channel; D08/D14B for affected lifecycle paths | D01, D02, D03A, D14A, D06, D07, D11, D12 when exceptional alternatives are disabled |
| Correction or repair workflow | None universally | D06, D07, D11 according to the requested action | Unrelated normal-placement decisions |

Non-launch policy still requiring documentation: exact history-retention duration/evidence digest. It must be resolved before production retention is configured, but it does not prevent relational-model and allocator prototype work.

The monthly due day, first-liability month, and monthly worker enablement remain separate unresolved rules owned by the deferred monthly workflow. They do not block Placement V2 unless a future `AQG-V2-D08` eligibility policy explicitly depends on them. Current policy infers no upline penalty from monthly delinquency.

## 35. Recommended implementation phases

Dependency sequence:

```text
Additive model + invariants
          |
          +--> lock/idempotency + resolved D02 allocator
          |                         |
          |                         +--> D03A explicit root/scope + normal placement
          |                                      |
          |                         D14B only for terminal sponsor cases
          |
          +--> topology/occupancy projections --> StructuralCompletionLevel (D01 resolved)
          |                                      |
          |                    AQGreenWeeklySalesEligibilityV1 --> weekly commission gate
          |
          +--> read-only inventory -- D03B + D09 --> final simulation
                                                                  |
                             resolved structural + weekly rules --> financial comparison/sign-off
                                           D10 --> controlled production cutover

Company channel: D04 + D05 + D13 (+ D14B if a terminal sponsor case is encountered), otherwise disabled.
Corrections: D06/D07/D08/D11 only when the corresponding exceptional path is enabled or encountered.
D12: serialized commit order is the default; decide only if an alternative commercial priority is requested.
```

### Phase B1: decisions and data evidence

- retain resolved D01 evidence and obtain each remaining open decision only before the dependent deliverable identified in sections 30 and 34; do not wait for all decisions before independent groundwork;
- run read-only production-like inventory;
- approve the exception taxonomy and financial review process.

### Phase B2: domain and persistence model

- add explicit attribution, placement, scope, source, rules-version, and audit semantics;
- add database invariants and immutable history;
- select and prove a canonical-order storage/query representation without changing parent-plus-slot authority;
- add no production backfill yet.

### Phase B3: allocator

- implement resolved sponsor-local parent-major BFS; keep production activation subject to the remaining cutover gates;
- add PostgreSQL lock, idempotency, uniqueness, and concurrency tests;
- integrate atomic approval-to-placement orchestration behind a disabled/non-production boundary and add exceptional Active-but-unplaced reconciliation detection.

### Phase B4: read projections and qualification

- build cutoff-effective V2 topology/occupancy queries independently of qualification;
- implement resolved D01 StructuralCompletionLevel and adapt qualification/progress APIs without changing V1 historical readers;
- distinguish direct sponsor credit, placement children, occupancy, and completion in contracts/UI.

### Phase B5: transitive financial consumers

- implement `AQGreenWeeklySalesEligibilityV1`, auditable SalesEligibilityReview, and system-derived PaidAsLevel/CommissionedLevel for commissions;
- version and adapt every structural consumer actually enabled at cutover;
- adapt the existing graduation endpoint to accepted V2 Level 2 semantics or explicitly disable it for cutover;
- preserve explicit future integration seams for deferred loan-offer/repayment paths without implementing or enabling them;
- add decision evidence snapshots and V1/V2 cutoff tests;
- preserve authorization and payout controls.

### Phase B6: simulation and controlled migration

- after `AQG-V2-D03B` and `D09` approve legacy roots/population, run authoritative deterministic whole-scope canonicalisation dry-runs using resolved D02 ordering;
- apply resolved D01 structural semantics to qualification/graduation comparison and the weekly sales rule to commission comparison;
- implement and validate an additive, idempotent migration only after approval.

### Phase B7: cutover and operations

- deploy in compatible order;
- reconcile exceptions;
- enable V2 at the authorised boundary;
- monitor and independently verify complete member/admin/financial workflows.

## 36. Definition of done

Placement V2 is not done merely because an allocator unit test passes. It is done only when:

1. all section 30 acceptance criteria are met;
2. every decision ID applicable to the enabled launch scope is resolved and recorded; unresolved company-assisted decisions prohibit that channel rather than silently defaulting;
3. domain, application, EF, API, and UI semantics no longer overload sponsor and placement;
4. PostgreSQL proves atomic five-wide allocation under concurrency and retry;
5. a first-time member can complete the workflow, including D14A temporary-restriction continuation; company-assisted actors can do so only if that channel is included and decisions `D04`, `D05`, `D13`, and any encountered D14B disposition are resolved;
6. qualification, commission, progress, reporting, and every structural consumer actually enabled at cutover use the accepted rules version; the existing graduation endpoint is adapted or explicitly disabled, while deferred loan/repayment/monthly workflows are not made launch prerequisites;
7. V1 historical decisions remain reproducible and unmodified;
8. dry-run output exactly predicts accepted migration output;
9. all anomalies have an authorised disposition and audit trail;
10. operational monitoring, recovery, deployment order, backups, and rollback/forward-remediation plans are verified;
11. security tests prove clients and ordinary admins cannot manipulate topology;
12. implementation and workflow evidence are independently reviewed at a level proportionate to financial risk;
13. D01 structural contribution is implemented recursively by relative depth without transferring sponsor credit or treating StructuralCompletionLevel as automatic earning entitlement;
14. `AQGreenWeeklySalesEligibilityV1` enforces independently evidenced 5/5/5 thresholds per week, no substitution/carry-forward, auditable review outcomes, system-calculated PaidAsLevel/amount, and separate payout release;
15. later-invalid sales evidence preserves history and enters authorised reconciliation rather than silent mutation; and
16. the unvalidated long-term 5/5/5 economics and separate commission source-of-funds review remain explicitly reported rather than being treated as proven by weekly sales eligibility.

This specification alone never changes implementation status. `IMPLEMENTED`, `TESTED`, `INTEGRATED`, migration acceptance, `ENABLED`, and `PRODUCTION VERIFIED` each require separate evidence; none is implied by this documentation change.
