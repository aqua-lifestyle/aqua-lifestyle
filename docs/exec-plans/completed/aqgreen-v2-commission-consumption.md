# AQGreen Placement V2 commission consumption

> **NOT BUSINESS AUTHORITY.** This execution record tracks B5.4 implementation
> and evidence. It cannot confirm, supersede, or enable a business policy.

## Goal and baseline

Consume B4 cutoff-effective structural completion and the finalized B5.3 weekly
sales decision in AQGreen weekly commission calculation while preserving Legacy
V1, commission arithmetic, holds, release/payment, and immutable historical facts.

- Baseline: `c80c80a161104ea3e84ac23792058e235839c9d9` (`origin/main`).
- Branch: `feat/aqgreen-v2-commission-consumption`.
- Worktree: `/home/wtc/Downloads/newAqua/aqua-lifestyle-aqgreen-v2-commission`.
- Recovered state: uncommitted B5.4 implementation on the baseline commit, with
  14 tracked modified files, 12 untracked files, no staged files, and no commits
  ahead of `origin/main`.
- Production selector: LegacyV1. D10 remains unresolved and PlacementV2 remains
  test-selectable only.
- B6 migration/backfill has not started.

## Independent review disposition

- The initial independent review returned **REVISE**; recommendation:
  **REVISE BEFORE COMMIT**.
- Finding 1 (medium): Level 0 Placement V2 participants improperly required
  finalized B5.3 sales evidence.
- Finding 2 (medium): PostgreSQL proved manifest counts but did not prove that
  `QualifiedStructuralLevel` followed from canonical structural facts.
- Finding 3 (high): validation/provenance was stale because production/migration
  source changed after the previously reported green runs.
- No new business-owner decision was required.
- The independent re-review accepted all three corrections: **ACCEPT**;
  recommendation: **READY TO COMMIT**.

## Recovery classification

- Finding 1 starting state: **PARTIAL**. B4 was already evaluated before B5.3,
  but the reader was still called unconditionally and Level 0 evidence still
  required sales facts. Now **COMPLETE AND VALIDATED**: Level 0 bypasses B5.3,
  persists honest not-applicable evidence, and candidate Levels 1-3 retain the
  exact finalized-decision requirement.
- Finding 2 starting state: **PARTIAL**. EF configuration, migration graph
  hardening, deferred triggers, and manifest count checks existed, but the
  level/count relation was absent. Now **COMPLETE AND VALIDATED**: the defensive
  relation is version-scoped and real PostgreSQL forged-level tests reject
  Levels 1-3 with an anchor-only manifest.
- Finding 3 starting state: **COMPLETE BUT UNVALIDATED**. Earlier artifacts were
  stale. Now **COMPLETE AND VALIDATED** by the post-edit Release build, focused
  suites, PostgreSQL suite, full Application run, solution build, and EF8.0.8
  pending-model check recorded below.

The interrupted agent had already changed the V1/V2 selector and ledger shape,
deferred graph trigger hardening, Tenant/customer/period coherence, replay
centralization, cutoff manifest completeness, and migration/model artifacts.
The current correction added Level 0 applicability/nullability semantics, the
Level 0 reader bypass, explicit reader-call coverage, the versioned SQL
structural relation, and real PostgreSQL forged-level regressions. No unexpected
non-B5.4 source files were found.

## Financial-authority gate

1. **Sales pass — confirmed.** The Placement V2 specification sections 24.4,
   24.6, 29.15, and 34.5 authorize `Confirmed + Met` to apply the existing AQGreen
   commission component/rate rules to the cutoff structural level.
2. **Confirmed + NotMet — confirmed outcome, derived-safe representation.** The
   authoritative outcome is no PaidAsLevel and no commission amount. Preserve a
   finalized `NotEarned`, zero-component ledger decision and immutable evidence;
   do not convert structure to Level 0 or create a payout hold.
3. **Rejected — confirmed outcome, derived-safe representation.** Rejection also
   has no PaidAsLevel or commission amount, but its immutable review status remains
   distinct from confirmed quantity shortfall.
4. **Held/missing/unsupported/corrupt/cross-Tenant — confirmed.** These states
   cannot finalize a PlacementV2 commission decision. Fail the transaction with no
   V1 fallback and no period/ledger/evidence residue.
5. **Structural cutoff — confirmed.** B4 is evaluated at the same canonical closed
   commission-week cutoff used by the ledger.
6. **Structural historical evidence — derived-safe engineering design.** B4's
   ordinary result explicitly is not a durable financial snapshot. B5.4 therefore
   captures a commission-specific immutable structural manifest at the weekly
   cutoff. It does not reuse persisted graduation evidence.
7. **Rules-version ownership — confirmed.** Existing commission `RulesVersion`
   identifies financial terms. Placement, structural qualification, weekly sales,
   and commission-decision rules retain separate version fields.
8. **Historical V1 honesty — confirmed.** Existing/current LegacyV1 ledgers receive
   only an explicit LegacyV1 discriminator; V2-only evidence/version fields remain
   absent rather than fabricated.

## Architecture

- Add an explicit AQGreen commission structural-model selector whose production
  implementation always returns `LegacyV1` until D10 authority exists.
- Keep the existing V1 network/evaluator/calculator path unchanged and free of B4
  and B5.3 reads.
- For PlacementV2, capture B4 structural evidence at the closed-week cutoff, read
  the finalized B5.3 decision for the exact week/version only for candidate
  Levels 1-3, derive commissioned level only for `Confirmed + Met`, and reuse
  `EntryWeeklyCommission` component arithmetic. Level 0 is structurally
  incomplete, not a candidate, and records no sales evidence.
- Extend the AQGreen ledger with an honest structural-model discriminator and a
  separately owned commission-decision rules version for new decisions.
- Store V2-only facts in a one-to-one immutable evidence companion plus canonical
  structural manifest nodes referencing immutable placements and the exact B5.3
  decision.
- Preserve payout lifecycle mutability on the ledger while protecting the finalized
  V2 decision evidence from update/delete/truncate.
- Keep the PostgreSQL structural level/count relation as defense-in-depth only;
  it applies explicitly to `AQGreenStructuralQualificationV1` and does not
  replace the domain B4 evaluator.
- Retain the current transaction-owned calculation lock and period uniqueness. All
  B4/B5.3/terms/hold reads and ledger/evidence writes occur inside that transaction.

## Validation plan

Run focused domain and application tests first, then real PostgreSQL migration,
integrity, concurrency, rollback, and historical-preservation tests. Follow with
V1, B4, B5.3, and touched B5.2 regressions; full Application/Web suites; Release
build; EF Core CLI 8.0.8 pending-model check; diff/Git/untracked review; and
container hygiene.

## Recovery and review record

- The financial-authority gate passed. `Confirmed + Met` earns at the eligible
  structural level; `Confirmed + NotMet` and `Rejected` persist distinct
  `NotEarned` decisions; held/missing/unsupported/corrupt states fail closed.
- The first recovered PostgreSQL graph test passed before hardening. The first
  review then fixed LegacyV1 decision-version fabrication, made the four deferred
  graph triggers `ENABLE ALWAYS`, tightened Tenant/participant/customer/period
  coherence and NULL-safe shapes, and retained payout lifecycle mutability.
- The second review found that replay did not independently rerun the centralized
  B5.3 evaluator or fully validate final B5.3 audit evidence, the evidence header
  lacked the commission-decision version, and a sales review could postdate the
  commission. Those defects were fixed and covered by replay tests.
- The final branch self-review additionally fixed runtime/migration model parity,
  made the LegacyV1 enum default sentinel explicit, required a cutoff-bounded
  structural manifest to be complete, and added an explicit PostgreSQL
  participant/customer mismatch mutation assertion. A PostgreSQL syntax error in
  that new manifest guard was found by migration execution and corrected before
  final validation.

## Corrected validation record

All results below were generated after the latest relevant source/test edit at
2026-09-01 10:03:10 +0200. No production or test source was edited afterward.

- Affected Release build: `dotnet build test/AqualLifeStyle.Tests/AqualLifeStyle.Tests.csproj --configuration Release --no-restore -v:q /p:WarningLevel=0`; passed, 0 warnings, 0 errors, 28.54s; build output timestamp 10:03:50 +0200.
- B5.4 domain/replay: 8 passed, 0 failed, 0 skipped, Release; TRX
  `test/AqualLifeStyle.Tests/TestResults/b54-domain-replay-current.trx`.
- B5.4 application/infrastructure: 6 passed, 0 failed, 0 skipped, Release; TRX
  `test/AqualLifeStyle.Tests/TestResults/b54-application-focused-current.trx`.
- Progress/journey: 18 passed, 0 failed, 0 skipped, Release; TRX
  `test/AqualLifeStyle.Tests/TestResults/b54-progress-final.trx`.
- B5.4 PostgreSQL 16: the complete current weekly commission class had 8
  passed, 0 failed, 0 skipped, Release, 1m02s; TRX
  `test/AqualLifeStyle.Tests/TestResults/b54-weekly-postgresql-current-final.trx`.
  This includes valid Level 0 and Level 1 persistence plus anchor-only forged
  Level 1, Level 2, and Level 3 rejection tests, each against a fresh database.
- V1: 42 passed, 0 failed, 0 skipped, Release; TRX
  `test/AqualLifeStyle.Tests/TestResults/b54-v1-final.trx`.
- B4: 32 passed, 0 failed, 0 skipped, Release; TRX
  `test/AqualLifeStyle.Tests/TestResults/b54-b4-final.trx`.
- B5.3: 54 passed, 0 failed, 0 skipped, Release; TRX
  `test/AqualLifeStyle.Tests/TestResults/b54-b53-final.trx`.
- Targeted B5.2: 19 passed, 0 failed, 0 skipped, Release; TRX
  `test/AqualLifeStyle.Tests/TestResults/b54-b52-final.trx`.
- Full Application (`dotnet test test/AqualLifeStyle.Tests/AqualLifeStyle.Tests.csproj --configuration Release --no-build`): 1,221 passed, 0 failed, 0 skipped, 37m29s, exit 0; TRX
  `test/AqualLifeStyle.Tests/TestResults/b54-full-application-final.trx`.
- Web: **NOT RERUN IN THIS CORRECTION PASS; NO RELEVANT WEB SOURCE/TEST
  CHANGE.**
- Solution Release build: passed, 0 errors, exit 0, 42.16s. Existing Web test
  AngleSharp advisory and missing generated `wwwroot` library warnings remain.
- EF Core tool: exact path
  `/home/wtc/.nuget/packages/dotnet-ef/8.0.8/tools/net8.0/any/dotnet-ef.dll`;
  `dotnet .../dotnet-ef.dll --version` reported 8.0.8. Exact command:
  `dotnet .../dotnet-ef.dll migrations has-pending-model-changes --project src/AqualLifeStyle.EntityFrameworkCore/AqualLifeStyle.EntityFrameworkCore.csproj --startup-project src/AqualLifeStyle.Web.Host/AqualLifeStyle.Web.Host.csproj --context AqualLifeStyleDbContext --configuration Release --no-build`; output: `No changes have been made to the model since the last migration.`; exit 0. Retained at
  `test/AqualLifeStyle.Tests/TestResults/b54-ef8-pending-model-current.txt`.

## Delivery state

- D08 remains unresolved and is fail-closed when it prevents safe cutoff
  evaluation.
- D10 remains disabled; production selection remains LegacyV1.
- B6 has not started. Verified Commerce remains future work.
- Accepted validation is current: domain/replay 8/8, application 6/6,
  progress/journey 18/18, PostgreSQL 8/8, V1 42/42, B4 32/32, B5.3 54/54,
  B5.2 19/19, full Application 1,221/1,221, Release PASS, and EF8 no model
  drift.
- Independent re-review: **ACCEPT**. Recommendation: **READY TO COMMIT**.
- Delivery is authorized; no business-policy cutover or production enablement is
  implied.

## Open future risks and exclusions

- D10 production cutover/effective scope remains unresolved and disabled.
- D08 remains a conditional fail-closed policy boundary when affected state is
  encountered by B4.
- D11 correction/adjustment authority remains unresolved; B5.4 does not mutate
  settled history or add reconciliation payouts.
- Evidence retention duration/deletion authority remains a production prerequisite.
- B6 canonicalisation/backfill, Verified Commerce, frontend, sales-policy expansion,
  product mapping, refund/chargeback policy, and worker enablement remain out of
  scope.

## Current state

Implementation recovery, current validation, and the bounded self-review are
complete. The independent re-review found no remaining issue in the three
reviewed findings: Level 0 avoids B5.3 and fabricates no sales evidence;
Levels 1-3 require finalized sales; candidate missing/Held states fail closed;
PostgreSQL rejects forged structural levels through the versioned relation; V1
and D10 remain untouched; and the merged B5.3 migration remains outside the
diff. The accepted execution plan is complete and is moved to `completed/` for
delivery.
