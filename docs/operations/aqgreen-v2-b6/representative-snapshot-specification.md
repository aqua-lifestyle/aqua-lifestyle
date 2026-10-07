# AQGreen V2 B6 representative snapshot specification

## Authority and boundary

This specification defines the minimum representative data needed to apply the
owner-confirmed `AQG-V2-D03B` and `AQG-V2-D09` analysis rules. It does not authorise
production access, an export, a migration, a final manifest, a cutover, financial or
graduation remediation, D10, or `V2EffectiveAt`.

Business-owner confirmation of D03B/D09 is separate from production-access and
data-handling authority. An authorised database operator must create the snapshot;
the B6 analyst must receive only the pseudonymised and data-minimised isolated
result and an approved snapshot-identity manifest. Stable technical pseudonyms are
still linkable inside this bounded dataset, so the result remains controlled data;
this specification does not make a legal anonymity or compliance determination.

## Required analysis boundary

`AnalysisSnapshotCutoff` is the explicit UTC instant whose legacy state is being
analysed. It is not `V2EffectiveAt`, does not activate V2, and does not select a
financial cutover. Record it to microsecond precision with its declared precision.
It must be no later than the transactionally captured `SourceObservedAt`; a future
cutoff is rejected at every artifact trust boundary. Equality is valid when all
other evidence is valid.

Because `EntryParticipation.Status`, soft-delete state, Customer state, and some
lifecycle facts are mutable current projections, the safest requirement is:

```text
AnalysisSnapshotCutoff = the transactionally consistent source observation instant
```

If an earlier cutoff is requested, every required current-only fact must have
authoritative effective-dated evidence at that earlier instant. Otherwise the
affected result is `UNKNOWN`/`EXCEPTION_REVIEW`; current state must not be projected
backward.

Each package execution requires one explicitly authorised Tenant and the versioned
`ALL_TENANT_AQGREEN_PARTICIPATIONS` population selector. The extractor starts from
that Tenant's complete AQGreen compatibility population and mechanically includes
only the minimal referential closure described below. A referenced recruiter or
sponsor outside the declared Tenant is not exported as a valid relationship: it
causes delivered-snapshot verification to fail as a scoped ownership violation.
If a future review genuinely needs the foreign historical row, it requires a
separate explicit quarantined-raw-evidence contract; this package does not provide
one or let such evidence masquerade as a valid edge.

## Minimum relational data

Only the named columns are required for the B6 analysis dataset. A physical restore
may initially contain more columns inside the database operator's controlled zone,
but the copy released to B6 analysts must conform to this allowlist or document and
approve every additional field.

The current `legacy-inventory.sql` directly reads `EntryParticipations`,
`EntryRecruiterCorrections`, `AQGreenRecruitmentAttributions`,
`EntryWeeklyCommissions`, `OnyxLoanAgreements`, `OnyxGraduationDecisions`, and
`OnyxParticipations`. The simulator reads only reviewed derived candidate rows, not
database tables. The remaining allowlisted tables below are the minimum read-only
extensions needed to enforce the newly confirmed approval/provenance rules, detect
existing V2 conflicts, and validate Tenant/Area ownership before representative
analysis. The snapshot package consumes them and attaches `SnapshotId`, versioned
allowlist identity, and canonical SHA-256 digests before a representative run.

### A. Required for topology

| Table/domain | Required fields | Purpose |
| --- | --- | --- |
| `EntryParticipations` | pseudonymised `Id`, `TenantId`, `CustomerId`, `RecruiterCustomerId`; `Status`, `StartedAt`, `ActivatedAt`, `IsDeleted`, `DeletionTime`; derived `LegacyOrderingTieBreakOrdinal` | AQGreen identity, cutoff population, effective sponsor/root input, preserved immutable-ID ordering outcome, and current/deleted evidence |
| `AQGreenRecruitmentAttributions` | `Id`, `TenantId`, `ParticipantId`, `CreditedSponsorParticipantId`, `AttributionKind`, `AcquisitionSource`, `SourceReferenceId`, `AttributedAt`, `RulesVersion`; replace actor ID and assignment reason with `ActorPresent` and `AssignmentReasonPresent` booleans | Validate the current production source shapes and distinguish acquisition provenance from sponsor/root evidence. `MemberInvitation` requires a credited sponsor, no administrative assignment reason, and retained AQGreen invitation reference; `AuthorisedDirectAdmission` requires its actor/reason/direct-reference shape and does not require an invitation. A row alone does not prove confirmed/cutoff-effective provenance |
| `AQGreenRecruitmentAttributionConfirmations` | `Id`, `TenantId`, `AttributionId`, `ConfirmedAt`, `ConfirmationMethod`, `EvidenceReferenceId`, `RulesVersion`; replace user ID with an `ActorPresent` boolean if needed | Validate whether existing attribution evidence was confirmed without exporting user identity |
| `AQGreenPlacementTreeScopes` | `Id`, `TenantId` | Detect pre-existing or conflicting V2 scopes |
| `AQGreenNetworkPlacements` | `Id`, `TenantId`, `PlacementTreeScopeId`, `ParticipantId`, `PlacementParentParticipantId`, `PlacementSlot`, `CanonicalPath`, `PlacedAt`, `RulesVersion` | Detect existing V2 placement conflicts; these rows are observations, not migration authority |
| `ProgrammeInvitations` | `Id`, `TenantId`, `ProgrammeKey`, `ProgrammeParticipationId`, `IsDeleted`; exclude `Code` | Retain only non-deleted AQGreen invitations actually referenced by in-scope `MemberInvitation` attribution. Unrelated and Onyx invitations are excluded |

The simulator additionally requires one reviewed input row per accepted participant:
Tenant snapshot key, `ParticipantSnapshotKey`, separately typed sponsor and root
evidence statuses, accepted legacy sponsor snapshot key where applicable,
normalised `QualifiedUnderRecruiterAt`,
`LegacyOrderingTieBreakOrdinal`, effective V1 structural level when
recoverable, explicit acquisition classification including
`LegacyUnknownAcquisition`, and a SHA-256 source-evidence digest. Accepted scope ID,
fixed canonicalisation-effective time, rules/simulator/comparator versions, and the
snapshot identity are scope-level inputs. These values must be derived from the
snapshot; they are not separate production facts. Null/effective-null recruiter is
only `RootCandidate`. `AcceptedRoot` additionally requires a reviewed disposition
that identifies the participant snapshot key, uses
`ReviewedLegacyRootEvidenceV1`, supplies a SHA-256 evidence digest and controlled
decision reference, records review time, and names the
`AuthorisedB6RootReviewer` role. Independent start, recruiter clearing, and
`LegacyUnknownAcquisition` are never sufficient root authority.

### B. Required for recruiter/correction history

| Table/domain | Required fields | Purpose |
| --- | --- | --- |
| `EntryRecruiterCorrections` | `Id`, shadow FK `EntryParticipationId`, `PreviousRecruiterCustomerId`, `NewRecruiterCustomerId`, `CorrectedAt`; replace administrator ID with `ActorPresent`; exclude `Reason` | Reconstruct the complete correction chain and detect equal-time, pre-start, no-op, discontinuous, current-state mismatch, and cutoff-effective sponsor evidence |
| `EntryParticipations` | Topology fields above | Supplies the original/current recruiter endpoints and start/activation boundary |
| `Customers` identity projection | `Id`, `TenantId` | Resolve in-Tenant participant/recruiter CustomerIds without exposing customer attributes; a foreign recruiter cannot enter the valid closure and therefore fails verification |

Correction rows after `AnalysisSnapshotCutoff` remain required when they are already
present in the source snapshot because the current V1 implementation validates the
complete retained chain before reconstructing an earlier effective recruiter.

A currently Active participant whose authoritative approval or activation is
conclusively after the cutoff does not enter frozen topology merely because they are
Active at observation. They are harmlessly excluded when no cutoff-effective edge
depends on them. A cutoff-effective sponsor edge or correction chain that requires
such a participant is contradictory topology and quarantines the complete affected
component; the analysis must not skip, reconnect, re-parent, or reflow around them.
Any financial or graduation evidence for that participant remains in historical
impact inventory only and does not establish canonicalisation eligibility.

### C. Required for participation lifecycle

| Table/domain | Required fields | Purpose |
| --- | --- | --- |
| `EntryParticipations` | `Id`, `TenantId`, `Status`, `StartedAt`, `ActivatedAt`, `IsDeleted`, `DeletionTime` | Separate canonicalisation population from qualification eligibility and lifecycle exceptions |
| `EntryParticipationApprovalDecisions` | `Id`, shadow FK `EntryParticipationId`, `Approved`, `DecidedAt`; replace administrator ID with `ActorPresent`; exclude `Reason` | Verify retained approval/activation-gate evidence where it exists and distinguish approval from payment confirmation |
| `Customers` | `Id`, `TenantId`, `AreaId`, `IsActive`, `IsDeleted`, `DeletionTime` | Minimum closure for participant/recruiter identities and retained financial/loan/graduation/Onyx Customer references; current-only Area/lifecycle integrity and D08 exception classification; never treat as exact earlier state |
| `Areas` | `Id`, `TenantId`, `IsActive`, `IsDeleted`, `DeletionTime` | Verify that Area evidence remains inside Tenant; Area is not a root or topology key |

Joining/payment identifiers and payment rows are not required to include an already
Active participant: payment confirmation is not activation, while the Active state,
`ActivatedAt`, and retained approval decision are the relevant gate evidence. A
missing or contradictory approval history is reported; it is not repaired from a
payment record.

Soft-deleted, deleted, suspended, terminated, withdrawn, deceased, refunded,
chargeback/dispute, or other unsupported post-Active evidence is
`EXCEPTION_REVIEW`. When such a row is a required ancestor, the whole D03B component
is quarantined; descendants are not reflowed.

### D. Required for historical financial impact

| Table/domain | Required fields | Purpose |
| --- | --- | --- |
| `EntryCommissionPeriods` | `Id`, `TenantId`, `PeriodStart`, `PeriodEnd`, `TimeZoneId`, `CalculatedAt`, `RulesVersion` | Locate the authoritative financial cutoff and terms/rules boundary for each ledger |
| `EntryWeeklyCommissions` | `Id`, `TenantId`, `EntryParticipationId`, `CustomerId`, `CommissionPeriodId`, `StructuralModel`, `CommissionDecisionRulesVersion`, `HighestCompletedLevel`, `PayoutStatus`, `CalculatedAt`, `ReleasedAt`, `PaidAt`, `RulesVersion`, `IsDeleted`, `DeletionTime` | Inventory not-earned, earned, held, released and paid outcomes and validate participant/customer identity without changing money |

`EntryCommissionComponents`, amounts, currencies, hold/release free text, and payment
references are not needed for topology-intersection counts. If a later remediation
asks for monetary totals or component replay, that is a separately authorised data
scope and D11 decision; it is not part of this B6 snapshot.

### E. Required for loan/graduation/Onyx impact

| Table/domain | Required fields | Purpose |
| --- | --- | --- |
| `OnyxLoanAgreements` | `Id`, `TenantId`, `EntryParticipationId`, `CustomerId`, `Status`, `OfferedAt`, `MemberAcceptedAt`, `ApprovedAt`, `EffectiveAt`, `RepaymentDeadlineAt`, `LastAssessedAt`, `SettledAt`, `TermsVersion`, `IsDeleted`, `DeletionTime`; actor IDs become presence booleans; exclude member confirmation | Identify offers, agreement lifecycle, relationship integrity, and intersections with changed AQGreen structural inputs |
| `OnyxGraduationDecisions` | `Id`, `TenantId`, `CustomerId`, `EntryParticipationId`, `LoanAgreementId`, `OnyxParticipationId`, `DecidedAt`, `StructuralModel`, `GraduationRulesVersion`, `EvaluatedNetworkLevel`, `AQGreenWasActive`, `LoanWasActive`, `LoanWasAccepted`, `LoanWasAdministratorApproved`, `EvaluatedLoanTermsVersion` | Preserve the historical decision model and validate graduation relationships; exclude actor and justification |
| `OnyxParticipations` | `Id`, `TenantId`, `CustomerId`, `EntryParticipationId`, `LoanAgreementId`, `AdmissionRoute`, `Status`, `StartedAt`, `ActivatedAt`, `IsDeleted`, `DeletionTime` | Identify and validate Onyx participations created through AQGreen graduation without interpreting Onyx topology |

The repository has no separately persisted historical loan-eligibility decision.
The B6 report may state current V1 Level 2/3 eligibility as a clearly labelled
recalculation and may inventory persisted offers (`OnyxLoanAgreement.Status = 0`),
but must not fabricate a historical eligibility decision. Loan requirement and
repayment-allocation tables are not required for intersection-only counts; include
them only under separate authority if an as-of loan-hold replay is requested.

### F. Required for Tenant/Area integrity

| Table/domain | Required fields | Purpose |
| --- | --- | --- |
| `AbpTenants` | `Id`, `IsActive`, `IsDeleted`, `DeletionTime` | Include exactly the declared Tenant; names and tenancy names are unnecessary |
| `Customers` | `Id`, `TenantId`, `AreaId`, `IsActive`, `IsDeleted`, `DeletionTime` | Validate participant/recruiter Tenant ownership and current Area references |
| `Areas` | `Id`, `TenantId`, `IsActive`, `IsDeleted`, `DeletionTime` | Validate same-Tenant Area ownership without making Area topology authority |
| `__EFMigrationsHistory` | `MigrationId`, `ProductVersion` | Prove the exact database-schema migration state consumed by inventory/simulation |

Tenant, Customer, Area, Participation, attribution, loan, graduation, scope, and
placement keys must preserve referential joins after sanitisation. The verifier
requires exactly the declared Tenant and rejects dangling or cross-Tenant
relationships. Related tables are bounded by intersection with the in-scope
AQGreen population; this does not authorise table-wide exports.

### G. Not required / must be excluded or redacted

- names, surnames, emails, telephone numbers, club-member numbers;
- physical/postal addresses, geolocation, identity/passport numbers, dates of birth;
- user names, password hashes, security stamps, authentication/refresh tokens,
  roles, claims, sessions, data-protection keys, and invitation codes;
- Yoco/provider credentials, signatures, checkout URLs, raw callback/request/response
  bodies, webhook receipts, and payment/provider references;
- correction reasons, rejection reasons, hold/release reasons, graduation
  justifications, member confirmations, profile/free-text content, and email/outbox
  bodies;
- product/order/enquiry/catalogue data, combo data, savings, funeral/travel benefits,
  monthly obligations, and Onyx topology/commission data not needed for the named
  graduation intersections;
- production host names, connection strings, database credentials, backup keys, and
  sanitisation secrets.

## Relationship-preserving pseudonymisation

Technical keys may remain only if the data/security approval explicitly permits
them in the secured isolated environment. Otherwise use an operator-controlled,
versioned deterministic mapping:

1. For identities that do not affect business ordering, generate pseudonyms with
   keyed HMAC-SHA-256 over `namespace || original-value`; never use an unsalted
   public hash for low-cardinality integer IDs.
2. Use one mapping namespace for every occurrence of the same identity type. For
   example, `Customer.Id`, `RecruiterCustomerId`, correction previous/new recruiter,
   and CustomerId foreign keys must all use the same Customer mapping. Participant
   IDs and every ParticipationId/sponsor/parent foreign key must use the same
   Participation mapping.
3. Preserve data types: use deterministic HMAC-derived integer surrogates for
   integer keys and UUID surrogates for UUID keys. A collision anywhere in a
   delivered output type/domain is fatal; never choose a counter-derived alternate,
   because that would make identity depend on traversal order. Apply the map before
   constraint validation.
4. Keep identity and ordering separate. `ParticipantSnapshotKey` is the shared,
   relationship-preserving HMAC-derived pseudonymous Participation identity. It is
   deliberately **not** order preserving. Inside Zone 2, before removing the raw
   production Participation ID, compute a positive per-Tenant
   `LegacyOrderingTieBreakOrdinal` from the exact original immutable-ID comparator.
   The Zone 3 comparator is `QualifiedUnderRecruiterAt` followed by that ordinal.
   Raw production Participation IDs are prohibited in Zone 3 and are not required
   by the simulator.
5. Map Tenant, Area, attribution, invitation/acquisition evidence, commission,
   loan, graduation, Onyx participation, scope, and placement identifiers
   consistently across all related tables. Do not hash foreign-key columns
   independently. Preserve namespace labels in the delivered contract wherever
   equal output shapes might otherwise be confused.
6. Keep exact timestamp values and precision because order and cutoff semantics
   depend on them. Do not independently date-shift rows. Any approved uniform shift
   must also shift the cutoff and every related timestamp and must be declared in
   the sanitisation manifest.
7. Keep the HMAC key and reversible lookup, if any, outside the delivered snapshot,
   Git, logs, and reports under operator control. Record only the scheme version.
   Aqua must approve key/mapping retention, access, and destruction; this document
   does not invent a corporate retention period.

Use a CSPRNG-generated key of at least 32 bytes. Reject weak/all-zero keys and keep
the key outside Git, command arguments, snapshots, manifests, receipts, stdout, and
ordinary logs. After sanitisation, validate primary/foreign keys, unique
constraints, Tenant joins, row counts, null distributions for required fields, and
collision absence.

## Transactional acquisition requirements

### Rank 1 / recommended: Option A — operator-produced pseudonymised restore/copy

An authorised database operator creates one transactionally consistent PostgreSQL
snapshot/restore in a restricted staging zone, applies the allowlist and deterministic
sanitisation, validates relationships, and releases only the pseudonymised,
data-minimised artifact. The B6 package itself creates and owns the loopback-only
PostgreSQL 16 analysis container. It attaches no Aqua application process or
provider configuration. This narrow claim does not attest the state of an
operator-supplied external environment.

- Security risk: lowest practical analyst-side risk; transient source PII remains
  confined to the already-authorised operator zone.
- Direct-identifier exposure: excluded after allowlist/transformation; linkable
  pseudonyms remain controlled data and no raw copy is released to B6.
- Fidelity: highest; PostgreSQL types, constraints, nulls, ordering and joins remain.
- Referential integrity: strongest when sanitisation runs before restore validation.
- Repeatability: inventory and simulation can be rerun against an immutable clone.
- Operational burden: moderate; requires DBA, security/privacy approval, secured
  staging, validation, retention, and destruction controls.

### Rank 2: Option B — transactionally synchronised column export

An authorised operator exports only the required tables/columns from one PostgreSQL
MVCC snapshot, sanitises within the controlled export process, and restores them to
the matching schema in isolated PostgreSQL.

- Security risk/PII: smallest extracted field set, but custom export code increases
  omission, logging, and relationship-mapping risk.
- Fidelity: high only if types, nulls, timestamp precision, enum values and complete
  cross-table scope are preserved.
- Referential integrity: must be revalidated explicitly after restore.
- Repeatability: good after the export artifact and identity manifest are frozen.
- Operational burden: highest because ordering, shared pseudonym maps, dependency
  order, and schema compatibility are operator-owned.

For multiple export sessions, the operator must hold one `REPEATABLE READ, READ
ONLY` transaction open, call `pg_export_snapshot()`, and import that snapshot with
`SET TRANSACTION SNAPSHOT` in every worker before any query. A single `pg_dump`
consistent snapshot is also acceptable when its included-table/column and
sanitisation process meets this specification. Independently exported tables from
different snapshots are prohibited.

### Rank 3 / insufficient alone: Option C — aggregate-only production inventory

The current repository has a read-only aggregate inventory but no sanctioned
production sanitisation/export pipeline. Running only aggregate SQL in production
would minimise exported PII, but it cannot provide the participant-level sponsor
graph needed to reproduce the simulator or reviewed manifest. It is therefore not a
qualifying B6 representative snapshot and is not recommended as a substitute.

## Snapshot identity and fingerprints

The package computes two non-secret manifest stages. `BASE_SNAPSHOT` contains:

- opaque `SnapshotId` and approved Tenant scope;
- `AnalysisSnapshotCutoff`, snapshot observation instant, declared timestamp
  precision, and PostgreSQL MVCC/export snapshot evidence;
- source environment represented by an approved opaque identifier, PostgreSQL
  server major/full version, required extensions, and source encoding/collation;
- repository commit and inventory/simulator/source-query versions;
- complete ordered `__EFMigrationsHistory` values and latest migration, plus their
  SHA-256 digest;
- allowlisted table/column list, per-table row counts, primary-key range/cardinality
  metadata where non-identifying, sanitisation version, pseudonym namespace version,
  and export tool/version;
- per-table SHA-256 over canonical, primary-key-sorted, length-prefixed required
  fields with explicit null tokens, UTC timestamp representation, and enum numeric
  values;
- dedicated participant, recruiter/correction-chain, current lifecycle, existing
  V2, financial, and loan/graduation/Onyx digests;
- one overall SHA-256 of the canonical identity manifest.

After base verification and reviewed root disposition, the package computes
`FINAL_ANALYSIS` from the verified snapshot through the three D09 populations,
D03B component classification, simulator input/output, and impact analysis. It
first verifies an inventory artifact bound to the SnapshotId, source snapshot
digest, base-manifest digest, inventory rules/semantic versions, exact external
inventory-query digest, and recomputed result/complete digests. The restored schema
carries the same snapshot/base authority marker. It seals the base-manifest hash,
inventory bindings, root-disposition digest, canonicalisation,
qualification, historical-impact, accepted/quarantined component, simulator input,
simulator output, and impact digests. Those values are never accepted as arbitrary
caller inputs. Final verification reruns the chain; impact reporting recomputes it,
and a handoff receipt can be created only from that verified final stage.

The reviewed root-disposition artifact independently binds the same SnapshotId,
source snapshot digest, and base-manifest digest. A reused opaque SnapshotId cannot
make review evidence from a different source/base authoritative.

The MD5 values emitted by the current exploratory inventory are useful diagnostic
drift checks but are not sufficient as the approved snapshot/manifest identity.
Both inventory and simulator reports must name `SnapshotId`, overall SHA-256,
`AnalysisSnapshotCutoff`, repository commit, and tool/rules versions.

Executable tool identity is the canonical SHA-256 list of the runner and every
first-party assembly materially used by B6 evidence: the runner, Core, and
EntityFrameworkCore. Operational evidence additionally requires a clean reviewed
Git worktree at the recorded commit and byte-equality with a fresh build whose
source was mechanically materialized from that exact Git object tree, not copied
or globbed from the live filesystem. Dirty local execution is classified only as
`PRECOMMIT_SYNTHETIC_VALIDATION` and cannot verify as operational evidence.

## Repeatability and required output

Against the immutable isolated snapshot:

1. Run `legacy-inventory.sql` twice using the same cutoff and stable report format.
   Compare logical output after excluding declared report-generation metadata.
2. Build the three separate D09 sets: canonicalisation population, current
   qualification eligibility, and historical impact inventory.
3. Classify each D03B component as accepted or quarantined. An anomaly never changes
   the confirmed rule automatically.
4. Run the reviewed simulator twice over the same accepted component manifests.
5. Require exact equality of candidate population, components, roots, sponsor
   inputs, ordinals, parent, slot, path, canonical position, placement sequence,
   anomaly classifications, structural impacts, and all semantic digests.

The direct-identifier-free, pseudonymised review report must include real counts for:

- Tenants and all AQGreen participations in the approved scope;
- canonicalisation candidates, candidate roots, and root-specific components;
- accepted and quarantined components, and accepted and blocked participants;
- parents with more than five effective recruiter children;
- recruiter-correction chains, equal-time corrections, and discontinuous chains;
- cycles, dangling and cross-Tenant sponsor relationships, and duplicate
  Customer/Participation evidence;
- `LegacyUnknownAcquisition`, unknown/ambiguous sponsor, and unknown/ambiguous root
  evidence;
- deleted/post-Active lifecycle exceptions and conflicting existing V2 evidence;
- V1 structural levels, simulated V2 structural levels, and `SAME`, `CHANGED`, and
  `AMBIGUOUS` impact classifications;
- historical commission and payout-state intersections; and
- loan, AQGreen graduation, and resulting Onyx participation intersections.

If evidence shows a confirmed rule is impossible or catastrophically mismatched,
report `OWNER DECISION RECONSIDERATION REQUIRED` with exact counts and digests. Do
not silently modify D03B/D09.

## Required operational authorisation

Before any acquisition, all of the following must be recorded:

1. Production system/data owner and security/privacy approval naming the exact
   source, Tenant scope, allowlisted columns, purpose, recipients, retention,
   storage, transfer, and destruction deadline.
2. DBA/platform-operator approval naming the operator, method, cutoff/window,
   connection role, expected source load, transaction/snapshot mechanism, staging
   location, and recovery/abort procedure.
3. Isolated-environment approval naming access principals, encryption, network
   restrictions, audit logging, outbound-connectivity prohibition, backup policy,
   and destruction evidence.
4. Acknowledgement that D03B/D09 business approval is not production-access,
   migration, manifest, D10, cutover, financial-remediation, or graduation-remediation
   approval.

No agent or B6 analyst should connect to production. The authorised operator hands
off the pseudonymised/data-minimised snapshot identity and read-only connection details through the
approved secret channel, never through source, prompts, logs, or reports.
