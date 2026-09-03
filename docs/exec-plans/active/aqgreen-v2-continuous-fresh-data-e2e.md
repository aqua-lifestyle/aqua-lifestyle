# AQGreen V2 continuous fresh-data backend E2E

> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

## Goal

Prove one fresh dummy AQGreen V2 prospective root continuously through real
root-bootstrap approval, deterministic 5 + 25 placement allocation, B4, B5.1,
Level-2 B5.2 graduation, test-gated B5.3 review, B5.4 R400 durable commission,
historical replay, and exact retry on a new PostgreSQL 16 database. Keep LegacyV1
as the production selector, keep D10 disabled, and exclude B6, admin UI, and member
dashboard investigation.

## Authoritative references

- The current task's explicit continuous-identity acceptance story and exclusions.
- `docs/aqua-system/aqgreen-network-placement-specification.md` for settled V2
  topology, structural, graduation, sales, commission, and selection semantics.
- `docs/aqua-system/04-payments-approval-and-yoco.md` for payment/approval
  separation and atomic activation ownership.
- `docs/development/validation.md` for proportionate PostgreSQL and build evidence.
- Completed root-bootstrap, B5.3, and B5.4 execution records under
  `docs/exec-plans/completed/` and the merged B5.2 record under `active/` as
  implementation provenance only, not business authority.

## Confirmed decisions

- Root bootstrap consumes `AuthorisedProspectiveRoot` plus matching confirmation
  and requires the dedicated host authorization.
- Placement V2 is test-selectable only; production remains `LegacyV1` and D10 is
  disabled.
- B5.2 graduation threshold is structural Level 2 and is independent of B5.3.
- B5.3 uses Friday 00:00 Africa/Johannesburg canonical weeks and computes `Met`
  only when Spray, One Litre, and Five Litre are each at least five.
- B5.4 consumes B4 and finalized B5.3 separately and retains the existing
  cumulative Level-1 R150 plus Level-2 R250 financial terms.

## Assumptions

- The test may use repository-supported dormant selector/gate replacements only
  inside the Web test module; production registrations remain unchanged.
- Because the real approval application path stamps current UTC time, the test
  resolves a canonical closed week from a deterministic later test as-of instant.
  Every activation precedes the cutoff and calculation uses a timestamp after the
  resolved close. The cycle must pass `IsCanonicalCycle`; no arbitrary boundary is
  invented.
- Creating dummy users/customers/participations, invitations, attributions,
  authority, and accepted loan terms through domain factories plus EF persistence
  is acceptable test setup. Placement, approval, B5.3 decisions, graduation,
  commission ledgers, and evidence must be created only by their real application
  or domain service paths, never by direct SQL fabrication.

## Current state

PR #95 merged as `659edd12f118844a5ca8e0ca5ac7aea68ff294cd` after the original
proof at `4d3892f630c5025c0bf2244a7f8d387c5bf2a1d3`. The original dirty work was
preserved without stashing on `test/aqgreen-v2-continuous-fresh-data-e2e`, and that
branch was fast-forwarded mechanically to current `origin/main` at `659edd1`.
Pre- and post-integration SHA-256 hashes of all four recovered files match. The
working tree still contains only the three requested Web-test files and this active
worklog. Production source, migrations, and schema are unchanged. The post-main
continuous backend proof, focused compatibility checks, and Release test-project
build are `TESTED`; no product defect was found.

## Evidence

- `2026-09-03` current-main integration and rerun:
  - fetched `origin/main` at `659edd12f118844a5ca8e0ca5ac7aea68ff294cd`, the
    merge commit for PR #95; its 35-file diff did not overlap the recovered Web test
    module or continuous E2E files;
  - fast-forwarded the protected E2E branch from `4d3892f` to `659edd1` without a
    stash, merge commit, conflict, conflict marker, or recovered-file content change;
  - verified a new task-owned `postgres:16-alpine` database had zero public tables
    before the test; the test later reported PostgreSQL 16.15;
  - `/tmp/aqgreen-v2-current-main-proof.Vwdjye/aqgreen-v2-continuous-current-main.trx`
    records the one authorized primary rerun: 1 passed, 0 failed, 0 skipped, duration
    60.1864020 seconds;
  - `/tmp/aqgreen-v2-current-main-proof.Vwdjye/aqgreen-v2-continuous-e2e.json`
    records the current-main business result and same-participant identity;
  - `/tmp/aqgreen-v2-current-main-proof.Vwdjye/aqgreen-v2-current-main-db-evidence.txt`
    records the read-only durable PostgreSQL cross-check;
  - SHA-256: TRX `eb8d755468f7bc9774f1a35ff807768f4f47ce8aec4c6dd57fdd0126f83dff91`,
    JSON `8a4a931ae4f3651544808727783acdcfd26da9122392c6a81519a0ad1a050f0d`,
    database evidence `22bc83a70f7c34065d5200d5c7017b350e365d7117f8647e792ef58c64baa479`;
  - the task-owned container `aqgreen-v2-continuous-main-20260903-root` was removed
    after retaining the three proof artifacts.
- Current-main continuous identity and result:
  - root user/customer `5` / `4`, AQGreen participation
    `5ad470f5-8e0a-4a9a-8741-ed29a460a48e`, placement
    `e6a188dc-b092-441d-90b0-f804e04dd7bf`, and placement scope
    `4837f79d-0b03-4a61-8a52-2f4386115d26` remain the same identity through
    placement, B4, root-user B5.1, B5.2, Onyx, B5.3, B5.4, and replay;
  - payment produced awaiting approval; authorized root bootstrap produced `Active`;
    PostgreSQL depth 0/1/2 counts are `1 / 5 / 25`, total structural observations
    are `31`, B4 is Level 2, and B5.1 projects Level 2;
  - B5.2 decision `5eb6b834-e037-4dc9-89b7-3f8577626493` created exactly one Onyx
    participation `8db52a52-9977-4bdc-a46a-4a28cd2200d8`; exact retry returned the
    same decision/Onyx identity, with one durable decision and one Onyx participation;
  - the canonical `Africa/Johannesburg` primary week is
    `2026-09-10 22:00:00Z` through `2026-09-17 21:59:59.9999999Z`; B5.3 decision
    `a8e7ddc7-1cba-4954-870d-93eb28ff403c` is 5/5/5, `Confirmed + Met`;
  - B5.4 ledger `b31c0a81-a128-44ce-a9f2-d8095428bb90` is qualified Level 2,
    commissioned Level 2, one root/week ledger, one evidence header, 31 evidence
    nodes, and exactly two actual-term components: Level 1 R150 plus Level 2 R250,
    total R400;
  - historical replay of that ledger is `PASS` at Level 2 / commissioned Level 2 /
    31 nodes / R400, including after mutable current-user state changes; calculation
    retry is already-calculated, creates zero records, and creates no duplicate money;
  - the separate next-week control is 5/5/4, `Confirmed + NotMet`, qualified Level 2,
    commissioned Level 0, `NotEarned`, no components, and R0.
- PR #95 compatibility on current main:
  - `AQGreenV2Demo` still requires the exact non-production environment, explicit
    opt-in, loopback PostgreSQL, and dedicated disposable database name; Production
    opt-in fails closed;
  - the continuous gates/selectors exist only in `AqualLifeStyle.Web.Tests`, start
    at `LegacyV1`/disabled, and are scoped by exact participant, Tenant/cutoff, or
    `AsyncLocal` clock context;
  - Web.Tests ran under ordinary `Test` with `REPRO_PG=false`, no demo flag, and no
    demo fixture; 15 `HomeController_Tests` passed, 0 failed, 0 skipped. TRX:
    `/tmp/aqgreen-v2-current-main-proof.Vwdjye/web-test-module-boot.trx`, SHA-256
    `41dcd2999b8a5ffeae458c5049c51d787b46995ad2654a50d52c0381ebd65660`;
  - 44 focused production-selector, disabled-gate, demo-boundary, deployment-config,
    and PR #95 B5.3 read tests passed, 0 failed, 0 skipped. TRX:
    `/tmp/aqgreen-v2-current-main-proof.Vwdjye/aqgreen-v2-current-main-focused-regressions.trx`,
    SHA-256 `cddd01c028545ab3c153c129110b6190e7945ba574ba935865ed4b137f05e000`;
  - the Release `AqualLifeStyle.Web.Tests.csproj` build succeeded with 0 errors and
    the pre-existing NU1902 AngleSharp 1.1.2 advisory as its sole reported warning.
  - one bounded current-main self-review found no branch-owned correctness,
    security, semantic-integrity, durability, temporal, idempotency, selector-
    leakage, demo-coupling, production-boundary, or regression defect.
- The original pre-PR-#95 validation remains a historical `PASS` at `4d3892f`; it is
  retained below as carried-forward evidence and is not substituted for the fresh
  current-main rerun above.
- `2026-09-02`: `git fetch origin`, `git switch main`, and `git pull --ff-only`
  left local and origin `main` at `4d3892f`; status was clean.
- Source mapping confirms real PostgreSQL Web test infrastructure, real V2 root
  approval gate, real B4-backed progress consumer, dormant B5.2/B5.4 selectors,
  disabled B5.3 gate, durable replay validators, and PostgreSQL migration startup.
- The latest E2E source edit is `2026-09-02 05:05:24 +02:00`. Retained proof
  artifacts were created afterward and are current:
  - `/tmp/aqgreen-v2-continuous-proof.4yCR6w/aqgreen-v2-continuous-fresh-e2e-proof.trx`
    ran from `05:06:09` through `05:07:07 +02:00`: 1 passed, 0 failed, 0 skipped.
  - `/tmp/aqgreen-v2-continuous-proof.4yCR6w/aqgreen-v2-continuous-e2e.json`
    records PostgreSQL 16.15 and the exact durable result.
  - Database/container: `aqua_continuous_proof` in
    `aqgreen-v2-continuous-proof-1788318358-31131` (`postgres:16-alpine`).
- Continuous identity and topology:
  - root user/customer: `5` / `4`;
  - AQGreen participation: `75e22d31-59fd-4d56-9ac4-a62ef4adccd2`;
  - root placement: `df28b2b9-1cbb-42c1-8bf8-552dfc5fc80c`;
  - placement scope: `dbdc665b-7092-484c-9ffc-031ac147e5d3`;
  - depth 0/1/2 PostgreSQL counts: `1 / 5 / 25`, total `31`;
  - explicit assertions carry the same participation through placement, B4,
    root-user B5.1 evaluation, B5.2, loan/Onyx, B5.3, B5.4, and replay.
- Continuous business result:
  - root bootstrap reached `Active`; B4 is Level 2; B5.1 projects Level 2;
  - B5.2 graduated once to Onyx
    `3f8cc17f-eb25-4e39-943d-a4942eba08f2`; exact retry returned the same decision
    and Onyx participation; durable graduation and Onyx counts are each one;
  - primary canonical Johannesburg week starts `2026-09-10 22:00:00Z` and closes
    `2026-09-17 21:59:59.9999999Z`;
  - B5.3 decision `b62bb4c1-8362-4463-921e-c55e8ab51917` is 5/5/5,
    `Confirmed + Met`, reviewer `1`, with reviewed-at, current eligibility rules
    version, and one technical evidence reference persisted;
  - B5.4 ledger `9dfe2cbb-1bb5-4f3d-b568-c0af32a300c6` is structural Level 2,
    commissioned Level 2, with Level-1 R150 and Level-2 R250 components resolved
    from effective terms version `continuous-primary-02ec259a71`, total R400;
  - PostgreSQL inspection confirms one primary root/week ledger, one evidence
    header, 31 evidence nodes, and exactly two primary components;
  - replay of that same ledger reconstructs structural Level 2, `Confirmed + Met`,
    commissioned Level 2, 31 nodes, and R400. The replay validator re-resolves the
    recorded terms and checks each ordered component, proving R150 + R250 rather
    than total-only equality; replay remains valid after mutable current-user state
    is changed and restored;
  - B5.4 retry is already-calculated, creates zero records, and retains the single
    ledger, two components, one evidence header, and 31 nodes.
- Control week is the next canonical week, starts `2026-09-17 22:00:00Z`, and is
  separately persisted as 5/5/4, `Confirmed + NotMet`, qualified structural Level 2,
  commissioned Level 0, `NotEarned`, no components, R0. It does not overwrite the
  primary decision, evidence, or ledger.
- Happy-path placement, approval, graduation, B5.3 finalization, commission ledger,
  and commission/graduation evidence are not directly SQL- or EF-inserted. Direct
  fixture persistence is limited to users/customers/participations, authority and
  invitation facts, accepted loan/repayments, active-area baseline, memberships,
  and effective financial terms; business transitions use real processors and
  application/domain services.
- Test-only replacements are registered only in `AqualLifeStyleWebTestModule`.
  Graduation and commission default to `LegacyV1`, B5.3 defaults disabled, and the
  clock delegates to the production PostgreSQL clock unless an E2E `AsyncLocal`
  scope is set. Exact participant/cutoff/Tenant scopes dispose back to defaults.
  The non-parallel PostgreSQL collection prevents overlap with unrelated Web tests.
  Production selectors/gates/clocks are unchanged, D10 remains disabled, and no
  runtime/configured `V2EffectiveAt` exists.
- Retained root-bootstrap/B4 regression:
  `/tmp/aqgreen-v2-shared-regression.DqsgRs/aqgreen-v2-root-bootstrap-and-b4.trx`
  ran from `05:08:47` through `05:10:43 +02:00`: 32 passed, 0 failed, 0 skipped
  (31 approval/root-bootstrap PostgreSQL cases plus one B4/progress PostgreSQL
  case). `aqgreen-v2-progress-pg.ran` confirms the B5.1 PostgreSQL body executed.
- Recovered focused Application tail:
  `/tmp/aqgreen-v2-validation-tail.l9rUvW/aqgreen-v2-focused-application-tail.trx`
  ran from `06:46:20` through `06:55:40 +02:00`: 121 passed, 0 failed, 0 skipped.
  It covers B5.2 application/PostgreSQL graduation, B5.3 safety/application/
  PostgreSQL durability and concurrency, B5.4 selectors/terms/evidence/replay and
  PostgreSQL decision graphs, and canonical commission-week resolution.
- Current Release solution build completed after the latest E2E edit at about
  `06:56 +02:00`: succeeded with 0 errors and one pre-existing NU1902 warning for
  test dependency AngleSharp 1.1.2. Bundler also reported pre-existing missing
  legacy Web MVC assets; no dependency or Web MVC asset file changed in this task.
- One bounded review found no branch-owned correctness, security, semantic-integrity,
  durability, retry, temporal, selector-leakage, or regression defect. No production
  code or architectural correction was required.
- After retaining and hashing the proof artifacts and completing the read-only
  durable database inspection, the two task-owned containers
  `aqgreen-v2-continuous-proof-1788318358-31131` and
  `aqgreen-v2-shared-regression-1788318516-746` were stopped and removed. The
  pre-existing `aqgreen-v2-continuous-e2e-pg` and
  `aqgreen-v2-e2e-validation-pg` containers were left untouched.

## Open questions

- Admin B5.3 sales-review UI gap: `OPEN` and intentionally not investigated here.
- Member commission dashboard visibility: `NOT YET ASSESSED`.
- D10 remains disabled and the backend E2E does not authorize B6 or production
  cutover.
- Global catalogue combo mappings, remaining Onyx mappings, AQGreen-to-Onyx
  transition-obligation semantics, recurring combo fulfilment, and any future
  combo-fulfilment/commission relationship are explicitly deferred. Sales remain
  separate from combos. None of these policy TODOs is implemented or confirmed by
  this worklog.

## Completed work

- Protected the original dirty work on a dedicated branch and integrated merged PR
  #95/current main without a stash, content loss, overlap, or conflict resolution.
- Re-ran the primary continuous E2E exactly once on a fresh PostgreSQL 16.15 database
  and independently cross-checked the same-participant durable database graph.
- Verified PR #95 demo isolation, ordinary Web.Tests boot without a demo flag,
  production selector/gate dormancy, focused B5.3 compatibility, and the Release
  Web test-project build.
- Completed one bounded current-main self-review with no branch-owned finding.
- Verified current merged baseline and loaded repository workflow, stateful-change,
  and evidence-validation procedures.
- Mapped the existing stage-specific test infrastructure and identified the test-
  only selector/gate seams needed for one continuous scenario.
- Added and compiled the bounded Web PostgreSQL continuous scenario and default-safe
  test-only selectors/gate/clock.
- Credited the timestamp-current fresh PostgreSQL 16 continuous pass instead of
  recreating its 31-person network, and independently cross-checked its durable
  database graph.
- Credited the completed 32-test root-bootstrap/B4 regression and finished the
  interrupted 121-test focused Application regression tail.
- Completed the current Release build, bounded self-review, and final Git/diff
  hygiene checks without staging, committing, pushing, or opening a PR.

## Next action

Complete final Git/staged-scope checks, commit only the three continuous Web-test
files and this worklog, push the dedicated branch, open the PR to `main`, and monitor
required CI. Do not merge without explicit human authorization, and do not mark B6
ready from this backend E2E.

## Git/branch context

- Repository/worktree: `/home/wtc/Downloads/newAqua/aqua-lifestyle`
- Branch: `test/aqgreen-v2-continuous-fresh-data-e2e`
- Base and current `HEAD`: `659edd12f118844a5ca8e0ca5ac7aea68ff294cd`
- `origin/main`: `659edd12f118844a5ca8e0ca5ac7aea68ff294cd`
- Dirty state: one modified Web-test module plus two new Web-test files and this
  active worklog; all are test/documentation scope. Nothing is staged.
- Production/migration/schema changes: none.
- Commit/push/PR state: authorized by the current delivery task but not yet done.
