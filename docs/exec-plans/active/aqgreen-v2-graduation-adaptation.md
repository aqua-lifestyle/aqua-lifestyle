# AQGreen V2 graduation adaptation

> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

## Goal

Implement B5.2 so the existing explicit, authorized AQGreen-to-Onyx graduation
transition can select either legacy V1 recruiter/correction Level-2 semantics or
Placement V2 B4 Level-2 semantics without changing V1 history, enabling D10, or
combining AQGreen and Onyx.

## Settled contract

- V1 graduation threshold is Level 2 under legacy recruiter/correction semantics.
- V2 graduation threshold is Level 2 under B4 placement structural semantics.
- Level 3 is maximum AQGreen structural completion, not the graduation threshold.
- Graduation remains an explicit authorized action; structural completion never
  creates Onyx automatically.
- The persisted, accepted loan agreement governs its accepted terms. Graduation
  must not compare it with mutable current catalogue pricing.
- D08 remains conditional and fail-closed only for affected bounded evaluations.
- D10 remains open. Production selection stays LegacyV1/default-disabled, while a
  dormant V2 implementation and focused test selector are permitted.
- AQGreen and Onyx, Tenant and Area, structural qualification, weekly sales,
  commission, payout, D09, D10, and B5.1 presentation remain separate concerns.

## Evidence architecture

The final design is a separate immutable hybrid manifest:

```text
OnyxGraduationDecision
    1 -> 0..1 AQGreenV2GraduationEvidence
AQGreenV2GraduationEvidence
    1 -> N AQGreenV2GraduationEvidenceNode
AQGreenV2GraduationEvidenceNode
    N -> 1 immutable AQGreenNetworkPlacement
```

- Existing immutable placement facts are referenced by source placement ID.
- Mutable/current-only participation, Customer, and User observations used by B4
  are snapshotted at the decision cutoff.
- Existing historical decisions are classified only as LegacyV1; no V2 evidence,
  graduation rules version, loan terms version, or cutoff is fabricated for them.
- A successful bounded Level-2 V2 proof records the anchor, five depth-1 nodes, and
  twenty-five depth-2 nodes. B4/replay owns 5/25 qualification semantics; the
  database owns only manifest integrity and recorded row-count consistency.

## Retention

Evidence is immutable while retained. Normal update, delete, and truncate paths are
prohibited. B5.2 adds no purge workflow or hidden bypass. Evidence retention
duration and an explicit privileged deletion process are D10 production-readiness
dependencies, not blockers for implementation, testing, merge, or dormant
selection.

## Implementation state

- Authority, research, design correction, and final technical design are complete.
- Implementation is complete and is no longer blocked.
- The production selector remains `LegacyV1`; Placement V2 is dormant and can be
  selected only through the explicit injected seam used by focused tests.
- Live bounded capture and replay share the same immutable topology validator,
  structural qualification predicate/version, and completion calculator.
- V2 decisions persist one immutable header and a deterministic 31-node manifest;
  historical V1 rows receive only the honest `LegacyV1` discriminator.
- Graduation now validates that persisted accepted loan terms are already in the
  canonical domain representation, then passes that one representation to Onyx
  and the decision snapshot without consulting current catalogue terms.
- Whole-transaction retry/reconciliation is bounded to three fresh attempts and
  classifies exact PostgreSQL serialization/uniqueness/commit-ambiguity cases.
- PostgreSQL owns graph integrity and append-only enforcement, not the 5/25
  structural formula.
- Branch and origin/main baseline are both
  `f5c4ab45f6fefc75588f7b049cc277bf25d8b4d9`.
- No commit, push, PR, production enablement, or B5.3 work is authorized.

## Planned slices

1. Shared B4 bounded evidence capture, explicit structural version, and replay.
2. Persisted structural model, accepted-terms correction, and disabled selector.
3. Atomic V1/V2 graduation orchestration, focused retries, and reconciliation.
4. Additive PostgreSQL migration, Tenant-coherent integrity, and append-only guards.
5. Focused unit/application/PostgreSQL validation and independent final review.

All five slices are implemented. Before the independent correction pass, focused
validation had passed 54/54 tests and the full unfiltered backend had passed
1,197/1,197 tests (1,113 application plus 84 web). That carried-forward evidence
covered B4, B5.1 regressions, V1/V2 application paths, accepted-agreement versus
mutable-catalog drift, evidence replay and version rejection, PostgreSQL
migration/append-only invariants, provider-generated 40001 and known/unknown 23505
handling, controlled provider-level post-commit connection failure followed by
fresh durable reconciliation, no partial durable state after tested failure stages,
authorization, and D08 fail-closed behavior. The existing PostgreSQL uniqueness
invariant permits only one loan agreement per AQGreen participation, so two valid
different-loan requests for the same AQGreen participation cannot coexist; a
provider test verifies the second loan is rejected rather than weakening that
constraint to manufacture the race.

## Independent review correction pass

The independent B5.2 review returned `REVISE BEFORE COMMIT` with three supported
findings. The accepted architecture was not reopened.

1. Accepted contractual strings could be normalized while constructing Onyx and
   then differ ordinally from the persisted loan on retry. Graduation now requires
   the persisted accepted agreement to already match the domain's canonical
   representation: terms versions are nonblank and unchanged by trimming, and
   currencies are unchanged by trimming and invariant uppercase canonicalization.
   One validated accepted-terms value is used to construct Onyx; the decision
   snapshots that value. Versioned LegacyV1 and PlacementV2 retries also validate
   canonical Loan/Decision/Onyx equality. Pre-B5.2 LegacyV1 decisions with both
   version snapshots absent use a separate historical terminal-graph validation;
   they no longer receive an unchecked same-loan return.
2. Standalone evidence replay did not own `GraduationRulesVersion` validation.
   `OnyxGraduationRules.IsSupportedVersion` is now the shared supported-version
   predicate used by standalone V2 replay and public reconciliation. Unsupported
   non-null versions fail reconciliation for both current LegacyV1 and PlacementV2
   decisions; no current version is substituted for persisted history.
3. The initial migration `Down` used `defaultValue: 0` while restoring
   `EvaluatedNetworkLevel`. That default was removed. PostgreSQL `Down` locks and
   preflights the authoritative tables, refuses PlacementV2 evidence, refuses any
   remaining null legacy level, and restores `NOT NULL` with no database default.
   It neither updates nor coalesces historical values.

## Historical/current reconciliation correction

The follow-up correction preserves genuine pre-B5.2 LegacyV1 history without
allowing a current row to masquerade as that history:

- Migration-added columns leave existing decisions as `LegacyV1` with null
  `GraduationRulesVersion` and `EvaluatedLoanTermsVersion`. No versions are
  fabricated.
- PostgreSQL adds
  `CK_OnyxGraduationDecisions_VersionSnapshots_Required` as `CHECK ... NOT VALID`.
  Existing violations remain readable, while PostgreSQL enforces the constraint
  for future inserts and for updates of existing rows. Current factories populate
  both snapshots, so new LegacyV1 and PlacementV2 decisions are versioned.
- The historical compatibility branch validates the durable Decision -> AQGreen ->
  Loan -> Onyx graph: Tenant/customer coherence, persisted AQGreen and loan links,
  the exact resulting Onyx link, `EntryGraduation`, terminal start/activation
  evidence, amount/currency coherence, and the effective-time bounds actually
  guaranteed by the pre-B5.2 factory. It does not replay current structural level,
  B4, current AQGreen or loan status, current catalogue terms, membership
  configuration, or Customer/User eligibility. Normal current caller Tenant/Area
  authorization remains in force.
- Historical Onyx terms came from the then-current catalogue while loan
  `EffectiveAt` came from approval, so equality between those two old timestamps
  was never guaranteed. Historical reconciliation honestly verifies that each was
  effective no later than the recorded graduation instead of fabricating the new
  accepted-agreement representation.
- Current versioned LegacyV1 and PlacementV2 retries use the one canonical accepted
  agreement representation and compare terms version, principal/entry amount,
  currency, and `LoanAgreement.EffectiveAt` against
  `OnyxParticipation.TermsEffectiveFrom` before any V2 structural replay can return
  success.
- `OnyxGraduationDecision` has creation factories and private decision-state
  setters; application code only inserts or reads decisions. Repository search
  found no update/delete workflow or maintenance migration that mutates a recorded
  decision. This supports the grandfathering invariant's deliberate consequence
  that any attempted update of an old null/null row is rejected.

The takeover applied the repository `review-change`, `verify-stateful-change`, and
`validate-evidence` procedures. No remaining branch-owned correctness or stateful
integrity finding was identified, and no implementation patch was required.

## Fresh final evidence

- Fresh PostgreSQL graduation/evidence/migration validation passed 32/32, failed 0,
  skipped 0 in 39 seconds. It includes a real pre-B5.2 row migrated forward and
  retried through the public service, future null/null insert rejection, current-row
  null/null corruption rejection, corrupt historical graph rejection, both
  effective-date divergence models, valid LegacyV1 and PlacementV2 retries, later
  loan lifecycle evolution, concurrency, append-only evidence, and migration Down.
  Artifact:
  `TestResults/b52-takeover-postgresql/b52-takeover-postgresql.trx`.
- The inclusive B5.2 focused matrix passed 121/121, failed 0, skipped 0 in
  7 minutes 49 seconds. This supersedes the earlier 101-test matrix by including
  the directly relevant PostgreSQL evidence/migration class. Artifact:
  `TestResults/b52-focused-takeover/b52-focused-takeover.trx`.
- The full unfiltered backend passed 1,222/1,222, failed 0, skipped 0: 1,138
  application tests in 37 minutes 23 seconds and 84 web tests in 7 minutes
  40 seconds. Separate artifacts:
  `TestResults/full-backend-application-takeover/backend-application-takeover.trx`
  and `TestResults/full-backend-web-takeover/backend-web-takeover.trx`.
- `dotnet build AqualLifeStyle.sln --configuration Release --no-restore` passed in
  53.9 seconds with 0 errors and 88 existing unrelated warnings: the AngleSharp
  advisory, Web Host XML-documentation warnings, and two xUnit analyzer warnings.
  No warning points to a B5.2 file.
- EF Core CLI 8.0.8 reported no pending model changes for
  `AqualLifeStyleDbContext` using the Release `--no-build` model check. The global
  EF 10.0.9 tool was not used.
- `git diff --check` passed. All TRX files are under the ignored `TestResults/`
  convention. No Aqua/B5.2/PostgreSQL test container remains. No files are staged;
  no B5.2 commit or push exists.
- Current status is **TESTED** and ready for the required final independent
  re-review. It is not marked `MERGE READY`, `MERGED`, `DEPLOYED`, `ENABLED`, or
  `PRODUCTION VERIFIED`.

Evidence labels are intentionally precise:

- Different-loan/same-participation: **NOT APPLICABLE — PREVENTED BY STRONGER
  INVARIANT** (`IX_OnyxLoanAgreements_EntryParticipationId`).
- Multiple AQGreen participations for the same Tenant/customer are likewise
  prevented by the unique `EntryParticipations(TenantId, CustomerId)` index.
- Commit ambiguity coverage proves a controlled provider-level post-commit
  connection failure and fresh durable reconciliation; it is not a literal TCP
  acknowledgement-loss test.
- “Onyx tracked” and “decision tracked” identify ChangeTracker stages inside one
  `SaveChanges` graph, not separately persisted database stages. No tested failure
  leaves partial durable state.

## Open production prerequisites

- D10 cutover/effective boundary and production selection authority.
- D08 policy for remaining affected post-Active states.
- Evidence retention duration and authorized retention-deletion process.
- Any future evidence-audit access surface.

These prerequisites do not authorize fallback, fabricated history, or production
enablement, and they do not block dormant B5.2 implementation.
