# AQGreen V2 B6 legacy canonicalisation analysis

> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

## Goal

Make the B6 decision path reviewable without applying a migration: verify the PR
#96 baseline, supply a PII-minimised read-only legacy inventory, supply a
deterministic frozen-input simulator using the accepted V2 sponsor-local allocator
semantics, record the owner-confirmed D03B/D09 rules, and state the exact remaining
representative-data and operational gates.

Excluded: production access or mutation; authoritative migration/backfill; V2
enablement; D10; B7; changes to B5.3/B5.4 business semantics; Onyx combo-policy or
AQGreen commission-eligibility implementation; historical financial or graduation
recalculation.

## Authoritative references

- `AGENTS.md`: Tenant/programme/history boundaries, business authority, stateful
  migration safety, PII protection, and evidence vocabulary.
- `docs/aqua-system/README.md`: current authority order and the distinction between
  confirmed D03B/D09 policy, representative-data validation, and unresolved D10.
- `docs/aqua-system/aqgreen-network-placement-specification.md` sections 3, 8,
  11.3–11.4, 12–13, 26–27, 32–35: resolved target semantics, confirmed D03B/D09,
  whole-scope replay, migration boundaries, remaining decisions, validation, and
  rollout ordering.
- `docs/aqua-system/05-data-history-migrations-and-legacy-members.md`: do not
  manufacture historical facts; read-only inventory before authorised
  reconciliation; PostgreSQL and rollback requirements.
- `docs/aqua-system/07-verification-decision-and-risk-register.md` sections P–Q:
  owner-only policy confirmation convention and durable D03B/D09 authority record.
- `docs/development/validation.md`: proportionate backend/database validation.

## Confirmed decisions

- D02: five-wide sponsor-local parent-major BFS allocation.
- D03A: prospective explicit root per root-specific PlacementTreeScope; multiple
  scopes per Tenant/AQGreen programme are allowed.
- D15: Tenant is the hard topology boundary; Area is not topology.
- D03B: each accepted cutoff-effective legacy root-specific component maps to one
  V2 PlacementTreeScope; independent roots are not merged and a component with a
  topology-affecting anomaly enters exception review in full.
- D09: canonicalisation population, Active-only cutoff qualification eligibility,
  and historical impact inventory are three distinct populations.
- D09 provenance: `LegacyUnknownAcquisition` means only that the acquisition
  channel cannot be reliably recovered. It may be used only when credited sponsor
  and root evidence are deterministic; it cannot conceal unknown sponsor/root or
  ambiguous correction evidence.
- Legacy canonicalisation must preserve sponsor credit separately from placement
  parent and label generated topology `LegacyCanonicalisation`.
- Pre-V2 settled financial and graduation evidence remains historical. A simulated
  topology difference is a review signal, not authority to rewrite it.

D03B and D09 were confirmed by the business owner through the direct written
instruction dated 2026-09-03. That confirmation does not authorise production
access, a concrete final manifest, production persistence for
`LegacyUnknownAcquisition`, migration/apply, cutover, or historical remediation.
D08 remains unresolved where post-Active lifecycle disposition affects topology or
qualification, and D10 remains unresolved/disabled.

## Assumptions

- The requested B6 cutoff/snapshot will be supplied by an authorised operator when
  an authorised pseudonymised/data-minimised production-like snapshot becomes available.
- Synthetic fixtures may prove simulator behavior but cannot establish production
  population counts or root legitimacy.
- Report hashes and pseudonymous snapshot keys are controlled correlation values.
  Raw production Participation IDs are not required in Zone 3 or its manifests.

## Baseline

- `git fetch origin` completed 2026-09-03.
- `main` and `origin/main` were both
  `26803037d9923842db12de621ab6f87c3cd045cc`.
- Tip: `Merge pull request #96 from
  aqua-lifestyle/test/aqgreen-v2-continuous-fresh-data-e2e`.
- PR #96 baseline gate: passed.
- Isolated branch/worktree:
  `feat/aqgreen-v2-b6-legacy-canonicalisation-analysis` at
  `/home/wtc/Downloads/newAqua/aqua-lifestyle-b6`.

## Repository evidence

### Legacy V1

- `EntryParticipation` is the AQGreen compatibility aggregate. It has one current
  `RecruiterCustomerId`, `StartedAt`, optional `ActivatedAt`, five modelled statuses,
  soft-delete auditing, and effective-dated recruiter correction rows.
- `EffectiveProgrammeNetwork.BuildAQGreen` uses only Active participants effective
  by cutoff, rejects missing activation, duplicate customers, dangling recruiter
  edges and cycles, reconstructs effective correction history, and orders selected
  recruiter children by `QualifiedUnderRecruiterAt`, then `ParticipationId`, taking
  five.
- `QualifiedUnderRecruiterAt` is `max(ActivatedAt, effective recruiter placement
  time)`, where the placement time begins at `StartedAt` and changes at a cutoff-
  effective correction.
- Multiple current/effective recruiterless V1 roots are possible. No database rule
  makes one Tenant or Area a root/scope.
- The current lifecycle has no explicit inactive, withdrawn, terminated, deceased,
  refunded, or suspended participation status. Encountered equivalent evidence
  therefore cannot be silently mapped to a valid included/excluded state.

### Prospective V2

- `AQGreenPlacementTreeScope` and `AQGreenNetworkPlacement` persist explicit
  root-specific scopes, parent/slot topology, canonical path, placement time, and
  rules version with Tenant-coherent PostgreSQL constraints and append-only guards.
- The current placement table does not persist a placement-source column or
  placement-sequence column. The B6 manifest can carry these facts, but Phase 4
  would require reviewed additive persistence before apply.
- `AQGreenAcquisitionSource` currently permits only `MemberInvitation` and
  `AuthorisedDirectAdmission`, while domain and PostgreSQL source-shape constraints
  bind sponsored attribution to invitation evidence. The owner has authorised the
  `LegacyUnknownAcquisition` meaning, but not its production representation. A
  future design must update the enum/model, domain source-shape rules, invitation
  evidence relationship and database constraints, migrations/model snapshot,
  readers/reporting/validators, and tests before apply. This task does not make
  those executable changes.
- Production commission and graduation structural selectors return `LegacyV1`.
  D10 remains disabled/unresolved.

## Read-only inventory

`docs/operations/aqgreen-v2-b6/legacy-inventory.sql`:

- requires an explicit UTC cutoff;
- starts `REPEATABLE READ READ ONLY`, uses a statement and lock timeout, and ends
  with `ROLLBACK`;
- emits snapshot metadata, stable participation/correction fingerprints, lifecycle
  counts, effective participants/roots, network sizes, >5-child cases, equal
  ordering times, current V1 structural levels, correction classifications, graph
  anomalies, acquisition-provenance gaps, commission/payout states, historical
  structural levels, Onyx loan agreements, graduation decisions, and graduated
  Onyx participations;
- emits no customer IDs, PII, payment references, correction reasons, or operator
  identities; anomaly identities are salted technical hashes.

No authorised pseudonymised/data-minimised production-like snapshot exists in the repository.
The inventory therefore has not been run against representative legacy data. Actual
Tenant, participant, root, network, overflow, correction, commission, graduation,
and anomaly counts remain `UNKNOWN`, not zero.

The exact next-data contract, field allowlist, redactions, deterministic
pseudonymisation, PostgreSQL consistency procedure, acquisition options, and
snapshot fingerprints are defined in
`docs/operations/aqgreen-v2-b6/representative-snapshot-specification.md`.

The repository does not persist a separate historical Onyx loan-eligibility
decision. Current V1 structural levels are the only recomputable eligibility input;
`OnyxLoanAgreement` status 0 is the persisted offer and later statuses are the
persisted agreement lifecycle. The inventory reports these separately without
claiming that mutable current state reconstructs an exact historical eligibility
decision.

### Anomaly matrix

| Category | Actual count | Why it blocks/flags | Correlation output | Required disposition |
| --- | --- | --- | --- | --- |
| `AMBIGUOUS_ROOT` | UNKNOWN | More than one accepted root in a proposed scope, or conflicting root/sponsor evidence | Salted technical hashes in secure anomaly output | Quarantine the component; require authoritative evidence or component-specific owner disposition |
| `MISSING_ROOT_EVIDENCE` | UNKNOWN | No accepted recruiterless root can anchor the component | Component/root candidate hashes | Quarantine pending authoritative evidence or component-specific owner disposition; never invent a root |
| `MISSING_SPONSOR` | UNKNOWN | A non-root has no accepted credited sponsor | Participant hash | Quarantine the component; repair authoritative evidence or obtain component-specific disposition |
| `DANGLING_SPONSOR` | UNKNOWN | Sponsor is outside the accepted same-Tenant population | Participant hash | Quarantine the component; repair authoritative source evidence before reconsideration |
| `CROSS_TENANT` | UNKNOWN | Recruiter evidence crosses the hard Tenant boundary | Participant hash | Data/security review and source remediation; never migrate the edge |
| `CYCLE` | UNKNOWN | Sponsor graph cannot be rooted/topologically replayed | Affected participant/component hashes | Operational investigation and explicit remediation before a new simulation |
| `DUPLICATE_PARTICIPATION` | UNKNOWN | Duplicate Customer/Participation identity destroys deterministic ownership | Representative participant hash plus count | Identity remediation; do not pick an arbitrary duplicate |
| `AMBIGUOUS_EQUAL_CORRECTION_TIME` | UNKNOWN | V1 deliberately rejects equal-time recruiter corrections, so effective sponsor is indeterminate | Participant hashes from correction report | Evidence repair or owner-approved exception disposition |
| `EQUAL_EFFECTIVE_ORDERING_TIME` | UNKNOWN | Review signal, not by itself an anomaly: immutable ParticipationId supplies the versioned total tie-breaker | Count plus the Zone-2-derived ordinal; raw participant IDs are not delivered | Confirm comparator/version; any missing/duplicate immutable ID becomes an exception |
| `LEGACY_UNKNOWN_ACQUISITION` | UNKNOWN | Acquisition channel is unrecoverable while sponsor/root evidence remains deterministic | Participant hashes | Eligible under confirmed D09; retain the controlled meaning and design its production representation before apply |
| `UNKNOWN_OR_AMBIGUOUS_SPONSOR` | UNKNOWN | Credited-sponsor evidence is missing, rejected, or ambiguous | Participant/component hashes plus typed sponsor status | Quarantine the complete component; unknown acquisition cannot substitute for sponsor evidence |
| `UNKNOWN_OR_AMBIGUOUS_ROOT` | UNKNOWN | Reviewed root evidence is missing, rejected, or ambiguous | Root candidate/component hashes plus typed root status | Quarantine the complete component; null recruiter and unknown acquisition cannot substitute for root acceptance |
| `SOFT_DELETED` / `UNSUPPORTED_LIFECYCLE_STATE` | UNKNOWN | D08/D14B and retention intent are not resolved for terminal/current-only evidence | Participant hashes | Owner classifies include/exclude/history-only behavior; default is exception review |
| `INVALID_STRUCTURAL_EVIDENCE` | UNKNOWN | Unsupported historical level must not become a valid-looking default | Participant hashes in secure simulator result | Repair/verify source evidence; otherwise impact remains ambiguous and scope is blocked |

All counts stay `UNKNOWN` until the same inventory is executed on an authorised
representative snapshot. Synthetic absence is not evidence of zero.

## D03B evidence and confirmed rule

### Options

| Option | V1 fidelity | Determinism | Fabricated-history risk | Future V2 effect | Complexity and remediation |
| --- | --- | --- | --- | --- | --- |
| A — one scope per accepted effective V1 root | Highest for valid disconnected cutoff-effective V1 components; preserves each root | Deterministic after a frozen cutoff, correction validation, and D09 population acceptance | Lowest; does not merge components, but still creates future V2 topology rather than historical placement | Independent root-specific V2 trees as allowed by D03A | Scope-local anomaly quarantine, retry, comparison, and forward remediation |
| B — one scope per broader connected legacy network when root-like rows connect through historical corrections | Can preserve a separately evidenced business network, but current code has no authoritative cross-time component identity | Requires an owner-approved effective-time/edge rule beyond current evidence | Material: may merge components that were legitimately independent at the accepted cutoff | Creates a single structural competition/qualification tree where V1 may have had several | Cross-component dependency, rollback, impact, and exception handling are materially harder |
| Tenant- or Area-wide implicit scope | Contradicts current evidence: multiple roots are legal and Area is administrative | Technically simple but semantically arbitrary | Unacceptable | Fabricates scope merging and ordering | Rejected as unsupported, not presented as an owner option |

### CONFIRMED D03B

Decision status: `CONFIRMED` by the business owner on 2026-09-03.

Recorded rule: at the authorised analysis cutoff, map each accepted AQGreen V1
root-specific component to exactly one V2 PlacementTreeScope. An accepted component
must be single-Tenant, based on approved cutoff-effective sponsor evidence, have
exactly one deterministic accepted root, be acyclic, have no dangling or
cross-Tenant sponsor edge, and have deterministic ordering/correction evidence.
Never merge independent roots merely because they share a Tenant, Area, Aqua
affiliation, unrelated history, or non-joining historical corrections. Never
manufacture a global, Tenant, Area, or company root.

Why: this follows the current V1 component boundary and resolved D03A root-specific
scope model while making no unsupported historical connection.

Evidence: V1 permits independent starts and recruiter-clearing corrections;
`EffectiveProgrammeNetwork` treats Tenant/programme as the outer boundary and does
not impose one root per Tenant/Area; D03A explicitly permits multiple root-specific
scopes.

Counterexample considered: two currently root-like records may be linked by old
correction history. That proves a relationship existed at some time, not that the
two current components are one scope at the authorised cutoff. Such a case remains
exception review unless stronger owner/source evidence authorises a merge.

Failure mode: a falsely accepted root changes every descendant scope, placement,
future structure, and consumer input.

Exception handling: a component with no deterministic root, competing/overlapping
root evidence, a cycle, dangling sponsor, cross-Tenant relationship, irreconcilable
correction history, or unsupported topology-affecting lifecycle evidence enters
`EXCEPTION REVIEW` and produces no accepted-manifest rows. Quarantine the complete
component; do not guess, merge, compress, skip an ancestor, reflow descendants,
invent a root, or search globally for a replacement sponsor.

Historical claim boundary: generated rows are `LegacyCanonicalisation` proposals
identified against the analysis cutoff. A later manifest timestamp requires
separate authority; none is selected here. The rows do not claim V2 topology
existed historically and do not replace historical V1 interpretations.

Confidence: confirmed policy; its real-data consequences remain unknown until the
authorised representative inventory is run.

## D09 evidence and confirmed rule

### Candidate population matrix (CONFIRMED)

| V1 evidence/state at frozen cutoff | Classification | Reason |
| --- | --- | --- |
| Active, activated by cutoff, valid continuous correction history, same-Tenant accepted sponsor/root graph, no applicable D08 exception | INCLUDE | Crossed the authoritative gate and provides deterministic ordering/topology evidence in an accepted D03B component |
| Valid active recruiterless candidate | INCLUDE only after D03B root acceptance | Null recruiter is a root candidate, not self-authorising root evidence |
| Awaiting joining or activation payment | EXCLUDE from the frozen legacy population | Not effective V1 structural participants; post-fence activation must use the authorised V2 writer |
| Payment-confirmed awaiting approval | EXCLUDE from the frozen legacy population | Payment is not participation activation; post-fence approval must atomically use V2 |
| Rejected | EXCLUDE | Never Active under current model; rejection history remains untouched |
| Soft-deleted/deleted, or apparently suspended/withdrawn/terminated/deceased/refunded/disputed/inactive after Active | EXCEPTION_REVIEW | D08 remains unresolved; if the row is an ancestor, quarantine the complete affected component and do not reflow descendants |
| Valid recruiter correction chain | INCLUDE using cutoff-effective sponsor and derived ordering time | Preserve the complete chain digest; do not overwrite corrections |
| Equal correction timestamps, discontinuous chain, current-state mismatch, correction before start, or no-op correction | EXCEPTION_REVIEW | Effective sponsor/order is ambiguous or corrupt |
| Missing recruiter | D03B ROOT_REVIEW or MISSING_SPONSOR exception | Do not turn missing evidence into an accepted root |
| Duplicate customer/participation, cycle, dangling sponsor, cross-Tenant sponsor | EXCEPTION_REVIEW | Violates identity, graph, or Tenant invariants |
| Unknown acquisition channel with deterministic sponsor/root/correction evidence | INCLUDE as `LegacyUnknownAcquisition`, subject to every other population rule | The confirmed value describes acquisition provenance only; production representation is a later design surface |
| Unknown/ambiguous sponsor or root, or corrupt/ambiguous correction evidence | EXCEPTION_REVIEW | `LegacyUnknownAcquisition` cannot stand in for sponsor, root, placement, or evidence quality |
| Existing V2 scope/placement for a candidate | EXCEPTION_REVIEW unless it exactly equals the approved manifest under defined idempotency rules | Prevent conflicting or duplicate topology |
| Participant with historical calculated/held/released/paid commission or graduation/Onyx evidence | HISTORICAL IMPACT INVENTORY ONLY; canonicalisation classification is decided independently | Historical evidence measures impact and never makes a participant eligible for canonicalisation |

### CONFIRMED D09

Decision status: `CONFIRMED` by the business owner on 2026-09-03.

Canonicalisation population: AQGreen participations that crossed the authoritative
approval/activation gate, are effective at the approved B6 analysis cutoff, have
deterministic Tenant, credited-sponsor/root, and ordering/correction evidence, and
belong to an accepted D03B root-specific component. Inclusion is component-safe,
not a generic `all EntryParticipations` query and not a per-sponsor batch choice.

Included: rows meeting the confirmed matrix conditions, with complete Tenant,
ParticipationId, effective sponsor/root, normalized ordering, correction-chain,
activation, lifecycle, and source-evidence digests.

Excluded: pending-payment, awaiting-approval, and rejected rows at the frozen
cutoff. They retain their V1 history and, if they later become eligible, require the
post-fence V2 path.

Qualification eligibility: separate from canonicalisation; Active-only at the
authoritative cutoff. Eligibility changes never delete placement, reuse a slot,
compress topology, move descendants, or re-parent them.

Historical impact inventory: independently include all relevant historical/current
commission and payout decisions, holds/releases/paid outcomes, loan offers and
agreements, graduations, resulting Onyx participations, deleted/problem records,
and historically relevant qualification evidence. These facts do not grant
canonicalisation eligibility and remain unmodified V1 history.

Exception review: every other topology-affecting matrix category. An anomaly blocks
its complete root-specific component unless later authoritative evidence resolves
it. Post-Active/deleted states remain exception review under D08.

Provenance preservation: retain current sponsor and complete correction evidence as
legacy sponsor evidence. Do not create historical invitation/admin/marketing facts.
Keep source table/snapshot identifiers, canonical source-field digest, comparator,
precision, cutoff, and rules/simulation versions.

Unknown provenance treatment: use the controlled semantic name
`LegacyUnknownAcquisition` only when authoritative retained evidence cannot recover
the acquisition channel and credited sponsor/root/correction evidence is otherwise
deterministic. It does not mean unknown sponsor/root, invitation, marketing, direct
admission, consent, inferred placement, administrator assignment, or evidence
waiver. Its future production representation must be designed and approved before
apply; do not map it to either current enum value.

Historical financial boundary: calculate a read-only V1-versus-simulated-V2 impact
flag. Never update or recalculate historical commission, payout, loan, or graduation
records. Any remediation requires separate authority, including D11 where relevant.

Confidence: confirmed policy; prevalence, quarantined component counts, and impact
remain unknown until a sanctioned representative snapshot is analysed.

## Simulator and canonical ordering

- `AQGreenLegacyCanonicalisationSimulator` accepts exactly one frozen,
  already-classified scope and refuses to decide D03B/D09 itself.
- It validates exactly one explicit `AcceptedRoot`, Tenant, identity uniqueness,
  separately typed sponsor/root evidence, sponsor presence, dangling edges, cycles,
  UTC ordering evidence, source digest, and acquisition provenance. A recruiterless
  row without reviewed acceptance remains `RootCandidate`; any anomaly yields no
  topology and no manifest digest.
- The analysis adapter represents `Known` and `LegacyUnknownAcquisition`
  provenance independently from `SponsorEvidenceStatus` and `RootEvidenceStatus`.
  `LegacyUnknownAcquisition`, independent start, and recruiter clearing never
  establish root acceptance. No production enum/schema change is authorised here.
- Accepted root evidence is an external reviewed disposition bound to the snapshot
  participant, `ReviewedLegacyRootEvidenceV1`, evidence SHA-256, controlled decision
  reference, review time, and `AuthorisedB6RootReviewer` role. Missing evidence
  quarantines the complete component.
- Replay is scope-wide Kahn traversal: a candidate becomes ready only after its
  accepted legacy sponsor has placement; the smallest ready immutable replay key is
  processed; newly ready candidates retain their original key.
- Replay key is `QualifiedUnderRecruiterAtUtc`, then the positive
  `LegacyOrderingTieBreakOrdinal` generated in Zone 2 with the explicit versioned
  `DotNetGuidCompareToOrdinalV2` comparator. That comparator matches the logical
  field ordering of `.NET Guid.CompareTo`, not lexical text or the mixed-endian
  `Guid.ToByteArray()` representation. Equal recruiter-correction timestamps remain
  a distinct inventory anomaly.
- Placement uses `AQGreenSponsorLocalVacancySelector`, which is also called by the
  live PostgreSQL allocator. This prevents the simulator from owning a subtly
  different vacancy rule.
- Output contains Tenant, accepted scope, participant, legacy sponsor, input
  ordinal, proposed parent, slot, canonical path/position, sequence,
  `PlacementSource=LegacyCanonicalisation`, placement/simulation/comparator versions,
  and source-evidence digest.
- The deterministic manifest SHA-256 excludes recording/report timestamps and
  includes the authorised canonicalisation effective instant.
- Synthetic X -> A..H evidence produces X slots A..E and overflow F/G/H under A
  slots 1..3 while sponsor credit remains X.

## Structural, financial, and graduation impact

The inventory reports grouped current V1 and historical ledger levels. After an
authorised pseudonymised/data-minimised snapshot and accepted D03B/D09 candidate manifest exist, the
secure analysis runner must compare each participant's cutoff-effective V1 level
with the simulator's V2 level using resolved D01 and classify `SAME`, `CHANGED`, or
`AMBIGUOUS`.

For every `CHANGED` participant, join read-only to commission ledgers/payout states,
Onyx loan agreements, graduation decisions, and graduated Onyx participations. Flag
participants with any settled financial/graduation evidence for review. Counts are
currently `UNKNOWN`; historical ledgers and graduations were not modified.

## Approved-manifest/apply design (not implemented)

### Manifest

The future approved manifest should contain:

- manifest identity and SHA-256;
- Tenant and AQGreen programme;
- accepted root and deterministic PlacementTreeScope identity;
- ParticipationId and accepted legacy sponsor evidence;
- source snapshot/high-water mark and per-table canonical fingerprints/counts;
- normalized ordering timestamp, declared precision, input ordinal, and UUID
  comparator version;
- proposed parent, slot, canonical path/position, and scope sequence;
- `PlacementSource=LegacyCanonicalisation`;
- placement, qualification, allocator, simulation, manifest-schema, and source-query
  versions;
- per-row source-evidence identifier/digest and anomaly disposition reference;
- authorised canonicalisation effective time, distinct from recording time;
- owner approval evidence locators for D03B/D09 and operational approval locator.

PII is excluded. Secure internal technical IDs remain necessary for exact apply;
published review reports use hashes.

### Drift and equality

1. Inventory and simulation execute in one repeatable-read pseudonymised/data-minimised snapshot and
   record snapshot/high-water metadata, source query/version, table counts, and
   canonical source-field SHA-256 fingerprints.
2. Approval names the exact manifest hash and exception disposition set.
3. At apply, stop AQGreen starts, approvals, and recruiter corrections; drain
   in-flight work; record the fenced database high-water mark.
4. Recompute the source fingerprints and simulation from the fenced source. Every
   count, candidate, root, sponsor edge, ordering fact, exception disposition, and
   proposed row must byte-match the approved manifest.
5. Any drift fails closed before topology insertion. Applying yesterday's approval
   to today's different population is prohibited.
6. The migration reads approved rows; it does not rerun an independent topology
   decision. After insert, compare exact row count and canonical row digest with the
   approved result before commit.

### Atomicity and idempotency

- Prefer one caller-owned PostgreSQL transaction for the complete accepted manifest
  (or a separately reviewed durable-manifest resumability protocol if size proves
  that impractical).
- Acquire deterministic Tenant/scope locks, validate D03B/D09 approval and source
  drift, insert scopes and roots/children in manifest sequence, validate exact
  equality, then commit.
- A complete rerun verifies existing scope/placement rows against the same manifest
  and performs no insert. Any existing mismatch is a conflict, never an update.
- Database unique/Tenant/parent-slot constraints remain final guards.
- An interruption before commit rolls back. A resumable alternative must durably
  identify the manifest and verify every applied prefix against recomputation before
  continuing; it cannot skip or re-decide rows.
- `Down` must not erase consumed topology; use verified restoration before use or
  reviewed forward remediation after use.

## AQGreen combo correction

- AQGreen combo requirement: `NONE`.
- The previous assumption that AQGreen Levels map to combo requirements is
  `SUPERSEDED` by the owner clarification dated 2026-09-03.
- Onyx combo policy is separate and out of B6 scope.
- AQGreen commission-eligibility business authority is a separate TODO.
- Sales remain separate. No combo, Onyx, sales, commission-eligibility, or
  B5.3/B5.4 executable semantics changed in this task.

## Evidence and validation

- Baseline Git commands: passed at
  `26803037d9923842db12de621ab6f87c3cd045cc`.
- Repository snapshot search: no authorised pseudonymised/data-minimised production-like database
  dump found; actual legacy inventory unavailable.
- Current focused simulator plus exact shared V1-ordering tests: 31 passed, 0
  failed, 0 skipped in Release on 2026-09-04.
- Existing live allocator tests after extracting the shared vacancy selector:
  27 passed, 0 failed, 0 skipped against disposable PostgreSQL 16.
- The .NET-native real-EF-schema snapshot-package E2E passed 39 package contract
  tests and one test-only reviewed-root fixture step with 0 failures and 0 skips on
  the final fresh current-tree run. It ran the
  full source-migrate/extract/sanitize/base-verify/analysis-restore/schema-verify/
  inventory/D03B/D09/simulate/impact/final-verify/receipt chain twice.
- B6 runner Release build: succeeded with 0 errors and 0 warnings.
- Full solution Release build on 2026-09-04: succeeded with 0 errors. The final
  recompilation emitted 2,605 repository warnings, none from B6-changed files;
  these included the known `AngleSharp` advisory in the unchanged web-test project.
- `git diff --check`, including explicit checks of every untracked deliverable:
  passed.

Empty-schema PostgreSQL and synthetic test evidence prove provider syntax,
read-only execution, deterministic tooling, and the tested allocator/anomaly cases.
They do not prove representative legacy population results, authoritative migration
safety, cutover, deployment, or production behavior. The full test suite was not
run; validation was selected for this analysis-only boundary.

## Remaining gates and decisions

No further owner policy confirmation is required for D03B or D09. The next gate is
operational authorization and delivery of the representative pseudonymised/data-minimised snapshot.
Real-data anomalies may require component-specific owner disposition under the
confirmed rules. D08 remains unresolved wherever post-Active lifecycle evidence is
topology- or qualification-affecting. If representative evidence proves a confirmed
rule impossible or catastrophically mismatched, report `OWNER DECISION
RECONSIDERATION REQUIRED` with exact evidence; do not silently change D03B/D09.

D10 is intentionally not requested or resolved in B6 and V2 remains disabled.

## Representative snapshot package (2026-09-04)

`docs/operations/aqgreen-v2-b6/snapshot-package/` now contains an allowlist-only
operator acquisition package. The terminology is intentionally
`PSEUDONYMISED AND DATA-MINIMISED REPRESENTATIVE SNAPSHOT`: stable technical keys
remain linkable and still require controlled-data handling. The package does not
claim anonymity or legal compliance.

Identity and business order are separate. `ParticipantSnapshotKey` is a
relationship-preserving, namespaced keyed-HMAC pseudonym. Zone 2 derives
`LegacyOrderingTieBreakOrdinal` from the original immutable Participation UUID
comparator before raw IDs are removed. The Zone 3 simulator now orders equal-time
inputs by that ordinal and does not require production Participation IDs. The HMAC
secret/mapping stays operator-controlled outside Git, logs, reports, manifests, and
the delivered snapshot. No production acquisition/provenance enum was added.

The package includes a Tenant/population-scoped read-only extractor, per-table
minimal-closure rules, exact allowlist, sanitizer, base/final manifests, fail-closed
file and restored-database verifier, reviewed root-disposition contract, three D09
populations, ancestry-preserving D03B component adapter, fixed simulator invocation,
recomputed impact report, verified receipt, and package-owned PostgreSQL 16
analysis runtime. `AnalysisSnapshotCutoff` is recorded solely as an observation
instant and is never named or used as `V2EffectiveAt`.

Package entry points enforce `umask 0077`, `0700` directories, `0600` files,
exclusive no-follow output creation, secure temporary files, a generated >=32-byte
HMAC key, fatal pseudonym collisions, and no credential-bearing destination DSN.
The analysis runtime is a unique loopback-only Postgres container identified by
package ownership labels, nonce, and database identity; cleanup can target only
that verified resource. The worker claim is correspondingly narrow: the package
attaches no Aqua application worker to its Postgres-only container.

The production-valid synthetic fixture now uses the actual current EF migrations
and PostgreSQL UUID/schema constraints. It covers `MemberInvitation` and
`AuthorisedDirectAdmission` source shapes, required approval evidence, paid-status
coherence, mixed-endian GUID ordering, Tenant-minimal extraction, reviewed roots,
exception ancestors, final-manifest verification, impact binding, and receipt
generation. Impossible production rows are tested only as malformed delivered-
snapshot negatives.

The owner-directed architectural refactor makes the authoritative B6 executable
package C#/.NET-native at
`aspnet-core/tools/AqualLifeStyle.B6SnapshotRunner`. It uses Npgsql/EF Core,
System.Text.Json, System.Security.Cryptography, and native `System.Guid`, and calls
the shared C# simulator and production V1 recruiter-ordering primitive. Shell is
limited to orchestration. The former B6 Python implementation and Python tests
were removed only after equivalent .NET contract and real-schema coverage passed.
This is a B6-specific architecture decision, not a repository-wide language ban.

### Bounded correction self-review

Completed once across root authority/component preservation, scoped
extraction/schema fidelity, authoritative manifest chain, destination safety,
file/key/pseudonym safety, and required fixtures/tests/docs. The review corrected
the final canonicalisation-population digest to include only accepted D03B
components, aligned the SQL inventory version with the manifest, made unsupported
root/sponsor evidence fail closed, and permitted explicitly allowlisted
presence-only booleans without permitting their source free text. Required focused,
PostgreSQL, package, runner, Release-build, syntax, JSON, and diff checks passed.
The continuation of that same bounded review corrected the interrupted impact path:
every participant in a quarantined D03B component is now explicitly represented as
`STRUCTURALLY_AMBIGUOUS` / `QUARANTINED`, receives no simulated V2 placement, and
retains its separate commission, payout, loan, graduation, and resulting Onyx
intersection evidence. The final manifest recomputes and binds that impact digest,
reverification reproduces it, and the receipt remains bound to the reverified final
manifest. The pre-correction validation at that point passed 27 package tests plus
the one test-only root-disposition step, 31 focused simulator/V1-ordering tests, and 27 live
allocator PostgreSQL tests, all with 0 failures and 0 skips. Both runner projects
built with 0 warnings/errors; the full Release solution built with 0 errors and one
pre-existing `AngleSharp` advisory warning. The candidate remains uncommitted and
requires one fresh independent review.

The subsequent four-finding integrity correction binds executable semantics to a
canonical runner/Core/EntityFrameworkCore assembly identity and separates dirty
precommit synthetic validation from clean operational evidence. Operational mode
also requires those executing DLLs to byte-match a fresh non-incremental build from
the exact clean recorded source commit. It rejects an
analysis cutoff later than source observation across raw, sanitised, base,
restored-database, and final trust boundaries. Cutoff topology now excludes a
conclusively post-cutoff participant only when no cutoff-effective topology depends
on them; cutoff-effective sponsor or correction dependence quarantines the complete
component without reflow, while historical financial/graduation evidence remains
impact-only. Inventory now requires a verified base, exact repository SQL digest,
recomputed semantic/result/complete digests, and a restored-schema marker bound to
SnapshotId, source snapshot digest, and base-manifest digest. Reviewed root evidence
binds the same authority, and the final manifest/reverification bind and independently
rerun inventory.

Fresh correction evidence on 2026-09-04: 8/8 primitive contracts; 9/9 focused F1-F4
artifact regressions; the final full real-EF-schema package run passed 39/39
contract tests plus one fixture-authoring step; earlier correction runs passed 38/38
before the fresh-build F1 regression was added. Simulator/V1 ordering passed 31/31;
27/27 live allocator PostgreSQL; runner and runner-test Release builds with 0
warnings/errors; full solution Release build with 0 errors. Its final recompilation
emitted 2,605 repository warnings, none from B6-changed files, including the known
`AngleSharp` advisory. Bash syntax, JSON parsing/contracts, tracked and
untracked whitespace, Python-dependency audit, direct dirty operational-mode
rejection, and package-owned container cleanup passed. Representative production
data remains unavailable and no production extraction, migration, enablement,
commit, push, or PR occurred.

Final C1/C2 continuation evidence on 2026-09-05: operational provenance uses a
private detached Git worktree created from the exact reviewed commit, verifies the
materialized commit and clean source set, performs a fresh non-incremental build in
that worktree, and compares the executing runner/Core/EntityFrameworkCore assembly
bytes with that reviewed-tree build. It does not copy or overlay files from the
live checkout. The ignored-source contamination regression passed, as did stale
assembly and dirty-operational-evidence rejection. Dirty candidate execution
remains `PRECOMMIT_SYNTHETIC_VALIDATION` and does not establish operational
provenance.

The package test wrapper now validates the exact TRX produced in its fresh private
results directory before printing PASS. Valid filtered execution passed; explicit
zero-match and practical `Name~SomeExistingTest` no-match filters, an expected-count
mismatch, and a controlled selected failing test all failed closed without the
package PASS banner. Missing, corrupt, inconsistent, and ambiguous structured TRX
evidence also failed closed. The focused F1-F4 gate reported 9 discovered/executed/
passed with 0 failed/skipped; the C1/C2 gate reported 10/10/10 with 0 failed/skipped;
primitive contracts reported 14/14/14 with 0 failed/skipped; and the final full
real-schema package run reported 45/45/45 with 0 failed/skipped plus the separate
fixture-authoring step. Simulator/V1 ordering passed 31/31 and the live allocator
PostgreSQL suite passed 27/27. Fresh runner and runner-test Release builds each had
0 errors and 0 B6-owned warnings (three pre-existing Core warnings were visible);
the full solution Release rebuild had 2,608 repository warnings, 0 B6-owned
warnings, and 0 errors. Representative production data remains unavailable, real
legacy counts remain `UNKNOWN`, the final migration manifest is not ready,
migration/apply remains unauthorised, Docker egress still blocks representative-
data operation, D10 remains disabled/unresolved, and B7 remains not started.

Final C2 fail-closed correction evidence on 2026-09-05: direct repository VSTest
17.8 output established `Completed` as the successful `ResultSummary` outcome and
showed the current counter vocabulary and the authoritative
`http://microsoft.com/schemas/VisualStudio/TeamTest/2010` `TestRun` -> direct
`ResultSummary` -> direct `Counters` structure. The exact-TRX verifier now requires
that qualified root and those exact qualified direct-child locations; foreign
namespaces and nested extension content cannot mint test-result authority. It also
requires the explicit successful outcome and full emitted counter set; permits only positive, reconciled
total/executed/passed values; and requires `failed`, `error`, `timeout`, `aborted`,
`inconclusive`, `passedButRunAborted`, `notRunnable`, `notExecuted`, `disconnected`,
`warning`, `completed`, `inProgress`, and `pending` all to be zero. Unknown
unqualified execution-status counters fail closed, while namespace-qualified
extension metadata is not misclassified as a status counter. B6 evidence permits
no skipped/not-executed tests.

The expanded primitive/parser gate passed 50/50/50 with 0 failed/skipped. Actual
`verify-test-run` CLI probes accepted the observed successful TRX with exit 0 and
rejected foreign or replaced root namespaces, a wrapped `TestRun`, foreign or
nested `ResultSummary`/`Counters`, duplicate authoritative elements, Failed
outcome, aborted, timeout, inconclusive, not-executed, unknown counters,
contradictory accounting, and expected-count mismatch with exit 2. The valid
filtered wrapper and focused F1-F4 gate passed 9/9/9 with 0 failed/skipped; explicit
zero-match, practical typo/no-match, and controlled selected-test failure wrapper
runs exited nonzero without the package PASS banner. The exact ignored-source C1
regression passed 1/1 and left no detached worktree. The final real-schema package
run passed 81/81/81 with 0 failed/skipped plus its separate fixture-authoring step,
with structured TRX acceptance printed before the package PASS banner. Simulator/
V1 ordering passed 31/31 and the live allocator PostgreSQL suite passed 27/27.
Fresh runner and runner-test Release builds each completed with 0 errors, three
pre-existing Core warnings, and 0 B6-owned warnings; the full solution Release
rebuild completed with 0 errors, 2,608 repository warnings, and 0 B6-owned warnings.
Bash syntax, all four package JSON files/contracts, tracked and untracked
whitespace, and the zero-Python operational/test/implementation dependency audit
passed. This correction does not alter C1, F2-F4, production isolation, business
authority, migration/apply authorization, or disabled V2 state.

Final supported-namespace `TestRun` cardinality correction evidence on 2026-09-06:
the pre-fix focused regression accepted all four additional same-namespace
`TestRun` variants while accepting the intended foreign-namespace control, directly
reproducing the fail-closed document-cardinality gap without demonstrating authority
substitution. The parser now rejects any descendant with the authoritative
`TestRun` expanded name after first validating the exact document root; authority
selection remains limited to that root, its exact direct `ResultSummary`, and that
summary's exact direct `Counters`. Five focused cardinality/namespace tests passed,
and the full primitive/parser gate passed 55/55/55 with 0 failed/skipped. Direct
`verify-test-run` probes returned 0 for ordinary valid TRX and foreign-namespace
`TestRun` metadata, and 2 for direct, deep, success-shaped, and alternate-prefix
same-URI `TestRun` additions and for a DTD/entity payload.

The focused F1-F4 real-schema package gate passed 9/9/9; the complete package gate
passed 86/86/86 plus its separate fixture-authoring step. Both accepted their exact
TRX before printing the package PASS banner. Explicit zero-match and practical
`Name~SomeExistingTest` probes exited 2 without PASS, while the controlled selected-
test failure exited 1 without PASS. The exact ignored-compile-source C1 regression
passed 1/1. Simulator/V1 ordering passed 31/31, and the live allocator PostgreSQL
suite passed 27/27. Fresh runner and runner-test Release builds each completed with
0 warnings and 0 errors. The B6 runner projects are not members of the solution, so
the prior full-solution Release evidence is carried forward rather than presented
as fresh evidence for this isolated parser correction. Bash syntax for all three
scripts, all four package JSON contracts, tracked and all 37 untracked-file
whitespace, and the zero-Python operational/test/implementation dependency audit
passed. No production access, migration/apply, V2 enablement, commit, push, PR, or
staging occurred.

## Post-commit pre-authority provenance correction (2026-09-06)

`e4a25bc773cacc3a03e86a0ec4fda6b8fbcfcfe1` retained a branch-introduced
operational trust-boundary defect. The earlier C1-A/B/C correction established that
ignored source, physical checkout roots, and prior build state could not become
trusted evidence, but its live Runner started while the operator wrapper already
exported DSN and output authority. An ignored SDK-consumed module initializer was
observed executing before `ToolchainIdentity.Capture()`. Exact-byte rejection after
process start was therefore too late to prove that unreviewed code lacked sensitive
authority. The accepted engineering contract is C1-D,
`TRUST-BEFORE-SENSITIVE-OPERATION`; it does not claim that no trusted bootstrap or
build process executes before artifact verification.

The selected correction removes the assumption that operational evidence must
execute a live-worktree Runner. The operator-controlled shell streams the exact
`authoritative-runner.sh` blob from the selected full reviewed Git commit into Bash.
No mutable repository script is an operational entry point. The reviewed bootstrap
validates the Git authority with replacement objects disabled, captures then
unexports all B6 operational variables, creates two independent private clones
without checkout, isolates their Git configuration/templates, disables hooks and
replacement objects, checks out the reviewed commit, rejects ignored/untracked
state, verifies every materialized regular file against its raw no-filter Git blob,
and builds both. Mutable source-repository hooks, `info/attributes`, checkout
filters, replace objects, and live files cannot enter either source tree.
Restore/build use
minimal isolated environments and one new private package cache shared only by the
two builds; they receive no DSN, snapshot path, output path,
pseudonym key, provider token, or synthetic sensitive sentinel. The retained
canonical build contract still provides explicit `NuGet.Config`, forced restore
reevaluation, no-incremental compilation, build-server suppression, isolated
outputs/intermediates, and deterministic SourceLink/path normalisation.

Runner, Core, and EntityFrameworkCore bytes must match exactly between reviewed
build A and reviewed build B. Build A is selected inside the private owner-only
temporary root, the selected tree is made read-only, and hashes are rechecked
immediately before launch. The exact reviewed operation adapter receives only its
operation-specific capability environment after provenance and rechecks all three
assemblies before every Runner invocation. `ToolchainIdentity` records the actual
selected bytes/commit and requires the bootstrap's matching pre-authority binding;
that binding is defence-in-depth, not a substitute for the Git-object bootstrap.
Ignored/live source and all live `bin`/`obj`/artifact state are outside the
operational selection path.

The minimum trusted base is the operating system and private-directory semantics,
the operator-controlled invocation and `PATH`, Git executable/object database,
Bash/core utilities and SHA-256 implementation, and the canonical .NET
SDK/MSBuild/NuGet build base. Package restore network is classified separately from
operational/database authority. The operator-selected reviewed SHA remains a human
review/authorization input; the repository does not infer that a commit was
reviewed merely because Git can resolve it.

The evaluated alternatives were: B, a separately installed external verifier;
C, an unprivileged candidate plus parent-mediated capability transfer; and D, the
live candidate with stripped authority. B added a second distribution/trust truth
without improving the Git-object bootstrap. C required a new secure IPC/capability
protocol and still needed an independently trusted parent. D could not safely add
authority to the same already-running candidate without such a protocol. Option A
proved claims 2–5 directly: executed bytes derive from reviewed source, two physical
roots reproduce them, trust precedes authority, and operational evidence is emitted
by the selected artifact. Claim 1, live-filesystem equivalence, is not a B6
requirement and is no longer asserted.

All validation in this uncommitted state is `PRECOMMIT CORRECTION EVIDENCE`. A
post-commit trusted bootstrap does not exist until these exact changes have a new
reviewed SHA. The final correction fingerprint, reviewed A/B assembly hashes,
C1-A/B/C/D adversarial results, path/TOCTOU evidence, complete package checks, and
cleanup are recorded at handoff after fresh validation. No production or
representative data, credentials, migration/apply, V2 enablement, D10, B7, commit,
push, PR, or staging is authorised by this work.

Controlled probes established that the canonical restore/build succeeds with an
empty minimal environment and no B6 operational variables. A fully fresh restore
also exposed transient MyGet/NuGet resets; the reviewed builds now share one newly
created private package cache, while their independent clones and intermediate/output
trees,
tool homes, and no-incremental compilations remain independent. Unavailable feeds
are ignored only when every exact required package can still be resolved; a missing
package remains fatal. The current precommit implementation passed its focused
synthetic capability regression, three replacement/symlink artifact-selection
attacks, the 59-test primitive/provenance class, and the 90-test real-schema
PostgreSQL package plus its separate fixture-authoring step. One disposable exact
candidate-tree bootstrap also completed both real canonical builds, exact-byte
comparison, selected-artifact execution, and structured TRX validation. Final
fingerprints/hashes and a fresh exact-tree rerun remain the handoff evidence because
subsequent test/document hardening changed the uncommitted correction bytes.

The final narrow independent review then established a further branch-owned C1-A/C1-D
blocker in the precommit correction: linked `git worktree add` ran mutable shared
`post-checkout` hooks and honored shared repository checkout attributes/filters. An
attacker could inject the same ignored SDK-consumed module initializer into A and B,
leave both ordinary statuses clean, and obtain equal contaminated assemblies. The
correction now uses independent no-checkout clones with empty templates and isolated
Git configuration, hook-free checkout, replacement objects disabled, ignored-state
rejection, and a raw no-filter hash comparison of every materialized file to its
reviewed blob. Its regression first demonstrates that a mutable shared hook and
`info/attributes` smudge filter both contaminate an ordinary linked worktree while
status remains clean, then proves neither mechanism runs or enters either authoritative
clone and the sentinel is released only to the verified reviewed artifact. This
paragraph and all rerun evidence remain `PRECOMMIT CORRECTION EVIDENCE`.

## Current state

- B6 policy/design: `DECISION-READY / CONFIRMED FOR D03B+D09`.
- B6 .NET-native inventory/snapshot/canonicalisation tooling: `IMPLEMENTED / TESTED`
  on current EF migrations in package-owned disposable PostgreSQL 16.
- B6 canonicalisation simulator: `IMPLEMENTED / TESTED` on synthetic inputs; the
  representative adapter implements the confirmed analysis-only legacy-unknown
  provenance state and ordering ordinal.
- Representative legacy-data analysis: `BLOCKED — AUTHORISED SNAPSHOT REQUIRED`.
- Final migration manifest: `NOT READY`.
- Migration implementation/apply: `NOT AUTHORISED`.
- D08: `UNRESOLVED WHERE APPLICABLE`.
- D10: `DISABLED / UNRESOLVED`.
- B7: `NOT STARTED`.

The requested B6 .NET-native refactor is complete. B6 production-data analysis,
migration/cutover, D08 exceptions, D10, and B7 remain separately gated as above.

## Completed work

- Verified and isolated the PR #96 main baseline.
- Traced current authority, V1 effective-network evidence, V2 persistence/allocator,
  provenance enum, financial/graduation models, and disabled selectors.
- Added the read-only, PII-minimised PostgreSQL inventory script.
- Added a deterministic fail-closed frozen-scope simulator and shared live/simulator
  vacancy selector.
- Added focused deterministic, overflow, anomaly, provenance, privacy, and
  non-mutation tests.
- Added and validated the allowlist-only representative snapshot acquisition,
  sanitisation, manifest, restore-verification, analysis, and handoff package.
- Recorded the owner-confirmed D03B root-component rule, D09 three-population
  model, and `LegacyUnknownAcquisition` semantics without implementing apply.
- Defined the bounded representative snapshot requirement and retained the Phase 4
  manifest, drift, atomicity, and idempotency design as not authorised.

## Next action

Obtain one narrow independent review of the pre-authority provenance correction. If
accepted, create a new commit on top of `e4a25bc`, then rerun complete
`OPERATIONAL_EVIDENCE` from that clean new SHA. Do not push or open a PR unless that
clean-SHA evidence passes. The separately gated representative-snapshot operation
still requires production/data-owner/security/privacy and database-operator
authorization; this correction grants none.

## Git/branch context

- Repository/worktree: `/home/wtc/Downloads/newAqua/aqua-lifestyle-b6`
- Branch: `feat/aqgreen-v2-b6-legacy-canonicalisation-analysis`
- Base `origin/main`: `26803037d9923842db12de621ab6f87c3cd045cc`
- Accepted B6/current `HEAD`: `e4a25bc773cacc3a03e86a0ec4fda6b8fbcfcfe1`
- Changes: local unstaged narrow build/provenance correction, regression, affected
  operations documentation, and this worklog.
- Correction commit/push/PR: none; prohibited for this task.
