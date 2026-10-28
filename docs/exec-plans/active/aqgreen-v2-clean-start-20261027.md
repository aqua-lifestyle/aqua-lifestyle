# AQGreen V2 clean-start release — 27 October 2026

Owner authority: the direct 27 October clean-start instruction supersedes mandatory
historical migration only for an explicitly classified disposable Tenant 1 dataset.
It authorizes implementation and non-production validation, not production reads,
reset, DDL, provider mutation, deployment or financial enablement.

Application base: `73890aa1b7c843238bbca1b450000d43d8ef8b5b`.
Executor base: `caf8b4bb34572feecccad30fdd8eb5b19b8116a9` in the adjacent
`aqua-deployment-clean-start` worktree. Neither worktree contains production data.
Accepted B6 source `82fc64894bcc9cc459e4077c17af6aa714d7f747` is untouched.

Implementation scope: transactional archival of classified test participations,
zero existing immutable V2 placements, preservation of source/audit/
financial records, a separately selected CLEAN_START D10 contract, and executor
admission preserving exact-source CI, replay, additive-schema and hold controls.
No dummy B6 history or B6 acceptance is generated. Missing classification, genuine
financial exposure, source drift or unavailable recovery evidence must stop.

Validation: focused admission/reset tests on existing PostgreSQL tooling, existing
joining/placement/commission regressions, and executor tests. Record actual commands
and results here when complete. Production classification and recovery remain
UNVERIFIED until protected operator evidence exists; owner assertion is a basis
for classification, not a claim that SQL eligibility has passed.

## Completed implementation and validation

Workstream state: **IMPLEMENTED**, **TESTED** for the focused scope below.
Not MERGED, DEPLOYED, ENABLED or PRODUCTION VERIFIED. Nothing was staged,
committed or pushed. No production connection, reset, backup/export, DDL, provider
mutation or deployment was performed. No Python, new package, paid resource or
privileged workstation configuration was used.

The coordinated execution package is
`docs/operations/aqgreen-v2-clean-start/README.md`; actual test evidence, SQL hashes
and reproduction filters are in its `validation.md`. Source bases above plus the
application `candidate-files.sha256` and executor
`docs/aqgreen-clean-start-candidate-files.sha256` manifests identify the prepared changes.
Final published commit identities and complete exact-source CI are still required.

- Release solution build: passed, 18 warnings, zero errors.
- Focused backend/PostgreSQL: **126 passed, zero failed/skipped**. Includes actual
  PostgreSQL 18.4 transactional archive/recovery and retained migration-55 SQL,
  identity/source-drift and protected-exposure rejection; existing five-wide
  allocation, Level 1–3, invalid-sales/commission release and historical D10 tests.
  Initial financial workers remain held; a separate verified worker-commissioning
  record can authorize later processing without authorizing automatic payouts.
- Actual PostgreSQL 18.4 empty-Tenant commissioning/root/payment/approval/six-member
  invitation and spillover flow: **one passed, zero skipped**, execution marker
  present. Uses synthetic authority and a fake Yoco gateway, not a real payment.
- Executor: **137 passed, zero failed, one existing skipped**. The skipped
  PostgreSQL concurrent signed-authorization test requires AQUA_TEST_DATABASE_URL;
  its protection is not claimed as executed. New test exercises the real inert
  Git reader for every pinned clean-start source, not just mocked source digests.
- Existing application deployment workflow tests: **five passed**.
- All 20 schema/hold/clean-start source pins match the worktree; both 240-case
  security inventories and policy/readback digests match. Actual schema readback
  remains UNVERIFIED. No mandatory security case was removed.
- B6 worktree is clean at its accepted source. Migration SQL retains SHA-256
  `1f45ef7b67fa6eae747ca70edfed05679ef63cf4cfe2f2cf308b9d31b8c1faea`.

The full backend run begun during development was cancelled after source changes;
it is not current passing evidence. The global EF 10 CLI check is discarded;
the pinned EF 8 model comparison in the PostgreSQL migration test passed. NuGet
advisory enumeration timed out without output: UNAVAILABLE, not a clean advisory
result. No dependencies changed. Full frontend/provider/browser CI was not run.
One local application-flow launch omitted CI's transactional/development flags
and failed during Tenant setup; the final run with those flags passed. It is the
only application-flow run claimed for the final source.
Tracked/new-file whitespace checks pass except the byte-preserved reviewed
migration SQL's existing extra EOF blank line; do not rewrite its accepted bytes.

## Production stop and smallest next action

**BLOCKED BY REAL DATA OR UNSAFE RESET**: actual reset eligibility is unverified;
this is not evidence that genuine production exposure exists. Production records
were not inspected under this implementation authorization. The previous fixed-SHA
aggregate-query authorization does not cover the new preflight.

Next: authorize the exact new read-only preflight plus shared state SQL through
the existing protected TLS channel. Bind its actual native source identity and
fingerprint to the protected disposable-test classification and Aqua's independent
clean-start acceptance. Missing/nonzero exposure, genuine membership, existing
immutable placement, unknown classification or source drift aborts the reset.
Verify existing recovery/cost, protected audit custody and the actual write fence;
then obtain separate exact reset, migration, release and D10/live-payment authority.
The existing B6 implementation remains available for any genuine history.

## Final read-only preflight request

Prepared `docs/operations/aqgreen-v2-clean-start/preflight-authorization.md` for
explicit owner authorization. Inspected the unchanged preflight/state pair,
enumerated its 16 public tables and metadata/pg_control_system access, confirmed
server-enforced read-only transaction/timeouts/rollback, and distinguished raw
aggregate output from the required small-cell-safe conversation report. No SQL
or application/executor source was changed and no tests were rerun.

No production/Yoco/business-record access occurred. Actual provider and off-system
obligation cross-checks are NOT PERFORMED; no genuine exposure finding or absence
claim is established. Request names the precise protected custodian evidence and
separate access authorization needed before classification can be accepted.
Recovered local validation is carried forward, not promoted to production proof.
The local concurrency test remains skipped; existing executor CI supplies the
test database, but these uncommitted changes have no exact-source CI. Recovery,
zero incremental costs, custody, retention and effective write fence remain
operation-specific UNVERIFIED requirements. SQL-only preflight authorization
cannot authorize reset, migration, deployment or live-payment validation.

## Owner-authorized exact-source CI publication

The final release-gates instruction authorizes committing these reviewed changes
and running existing mandatory CI, including real PostgreSQL replay concurrency.
Publish the application feature branch for its existing push CI and an executor
draft PR for its existing PostgreSQL-backed test workflow. Do not merge, dispatch
production execution, change tests/gates or perform production reads. Commit/check
run identities are the authoritative execution evidence; preparation-time
uncommitted/skipped descriptions above are historical, not claims about later CI.

Both reviewed SQL hashes were reconfirmed unchanged before publication. Xolani-M's
actual native source identity, protected reporting channel and UTC execution
window are still required before explicit owner preflight execution approval.
No Yoco/business source review, backup, reset, migration or deployment is implied
by CI publication or a successful CI result. Exact production admission also
requires independently reviewed main-source evidence and separate human gates.

### First hosted CI findings and mandatory dependency correction

Application `84bf271a18e4cc3f60c46b2a6d64a6bfed0c0bea`, run `40450407066`,
executed all 240 mandatory release-safety cases: passed, zero failed/skipped.
Executor `136d467f9e27bc64441315f8efadadeeb178ccc8`, draft PR 9, run
`40450464399`: **138 passed, zero failed/skipped**, including actual PostgreSQL
concurrent signed-authorization use. The PR merge tree equals the source tree;
this is existing CI execution, not production or independent-human acceptance.

The application production dependency audit failed on unchanged baseline Next.js
16.3.8 (critical) and Zod 4.4.3 (high). Branch ownership: PRE-EXISTING, exposed by
the mandatory current audit. No clean-start application code caused this failure.
The smallest correction updates existing Next.js/matching ESLint configuration to
16.4.3 and raises the existing Zod floor to 4.6.6 (lock resolves 4.6.7). No business
logic, SQL, CI gate, automatic payout flag or new dependency family changes.
The Next.js registry peer range remains compatible with retained React 19.2.4.
No broad/forced audit fix or codemod is used. Full exact-source CI must run again.

Primary advisory evidence:
[Next.js image optimization](https://github.com/advisories/GHSA-2464-fhr4-w9xw),
[Next.js cached typed arrays](https://github.com/advisories/GHSA-673q-mjm6-r5xr),
[Zod constrained collections](https://github.com/advisories/GHSA-mrhx-c7xc-m3w5).
SQL and production-read/human gates remain unchanged and unexecuted.

The same first CI run exposed one D10 fixture that enabled financial operations
after binding configuration authority. ROOT CAUSE: the new configuration digest
correctly rejects that post-binding mutation. Correct only the synthetic fixture
to select the capability before creating/binding evidence; retain every original
Area/financial/side-effect assertion and all production configuration checks.
This is BRANCH-EXPOSED fixture incompatibility, not permission to relax D10.

Validation of these bounded CI corrections: production `npm audit --omit=dev`
reports **zero vulnerabilities** against the updated lock; Release web-test build
passes (2847 warnings, zero errors); all **14 D10 PostgreSQL cases** and the **one
empty-Tenant clean-start joining/spillover case** pass on the existing PostgreSQL
15 test fixture with zero skipped. Artifacts are in
`/tmp/aqgreen-clean-start-ci-correction-tests`; existing PG18.4 evidence is carried
forward. No application business-logic, SQL or CI assertion was changed. The next
hosted run must qualify the exact corrected commit; none is presumed successful.

## Proposed status-only preflight amendment

The owner subsequently authorized the smallest status-only preflight change,
subject to review/testing and new production execution authorization. The prior
raw-count SQL hash and its UTC window do not authorize the amendment.

Changes are confined to the operational preflight/launcher, targeted tests and
current operator documentation. Shared state.sql, reset/restore, migration 55,
AQGreen/D10 business logic and accepted B6 are unchanged. Canonical population and
financial CTEs remain byte-equivalent and are regression-checked. All counters
stay inside PostgreSQL; the client receives only PASS/FAIL/UNKNOWN and whitelisted
non-identifying hashes/time/source metadata. No new raw-results storage is needed
for that receipt. External business classification remains UNKNOWN, not zero.

Proposed logical identity mode RENDER_RESOURCE_TLS_DATABASE_SESSION_V1 combines
authenticated provider-resource lookup, its exact external hostname/database/login,
system-CA verify-full/SNI and server session/TLS/read-only checks. This is scoped
to a current logical-source observation; physical-cluster continuity is not proven
and the unchanged reset/D10 native binding is not waived. Approval of that narrower
identity amendment remains a human decision.

Targeted tests: 21 focused xUnit cases (including the original eight reset cases)
and 11 launcher checks passed with zero skips. SQL uses existing PostgreSQL18.4
fixtures. Technical self-review corrected per-check PASS under incomplete RLS
visibility and froze verified SQL bytes to prevent a later file replacement.
Deadline checks, strict output keys and error redaction fail closed. No new tool,
dependency, paid resource, privileged host change or production query was used.
Full details and exact executable hashes are in the existing preflight authorization
and validation documents. Independent human amendment acceptance and new owner
execution authorization are not fabricated by these tests or self-review.

## Launcher-only diagnostic correction

The one authorized status-only attempt began 2026-10-27T09:29:25.975Z and
returned execution/overall UNKNOWN with launcher exit 1. The operator's direct
acceptance was verified at executor PR9 comment 6438280453 before execution.
The previous launcher discarded child diagnostics and completion time, so its
cause is UNDETERMINED; the 34.025-second remainder does not prove cancellation.

Current owner direction authorizes only a minimal launcher diagnostic amendment,
focused local validation and existing CI, Aqua acceptance and a new window request.
All SQL, reset/restore, D10, TLS, financial predicates and business-result semantics
remain unchanged. Safe allowlisted failure categories, observed provider/client
exit statuses and a completion timestamp now distinguish failed execution from a
completed database screen with unresolved business evidence. Raw diagnostics stay
in memory; no counts, records, secrets or arbitrary diagnostic messages are emitted.

Focused launcher validation: 17 passed, zero failed/skipped. The existing .NET CI
wrapper now requires all 17 instead of 11; its exact zero-failure/skip assertions
remain. Independent acceptance and fresh production authorization are pending;
there has been no production retry, reset, migration, deployment or new resource.

## Current owner directive: owner-attested test-only retirement

The latest explicit owner instruction supersedes the requirement to independently
classify the four payment groups before implementing the clean start. The owner
formally classifies the old AQGreen cohort as TEST DATA. Record that declaration
as OWNER_ATTESTED_TEST_ONLY_V1, with provider test/live classification explicitly
unverified. Do not invent independent financial verification or repeat that request.

Production evidence already obtained: the 13:05 UTC read-only diagnostic completed;
payments alone failed, through MemberPayments, Yoco receipts, joining checkouts and
participation payment references. All nine other original exposure guards passed.
No production reset, DDL or deployment is authorized by the new direction.

Implementation scope: source-bound owner attestation; unchanged nine guards;
before/after financial and audit conservation; soft-retire old participations and
AQGreen membership assignments while retaining all source relationships; no old
cohort commissions/payouts; preserve late verified payment evidence in a held
historical path without activation; keep new-payment/approval integrity; bind the
same evidence through reset, D10 and the existing executor. Migration 55 SQL stays
unchanged. Validate using existing PostgreSQL/application/executor tests and prepare
one coordinated, human-authorized cutover proposal. The unexecuted protected
payment-review draft and earlier diagnostic artifacts remain preserved, not reused
as invented provider clearance. No additional infrastructure or Python.

Targeted validation: initial backend run 196 passed, zero failed/skipped; final
PostgreSQL 18.4 application flow passed with its execution marker and no skips.
The last preservation expansion exposed a whitespace error in the synthetic
trigger fixture (BEGIN concatenated with UPDATE/NEW); only the test fixture was
corrected, without weakening assertions or changing release SQL. The corrected
rollback cases and final source-bound CI must pass before release qualification.
The executor's missing existing pg dependency was restored from its unchanged
lockfile; real PostgreSQL replay/concurrency then passed all three focused cases.
No new dependency, production query, mutation, resource or human approval resulted.

## Receive-only Yoco correction — owner-authorized implementation

Implement a PostgreSQL-backed receive-only fence using the existing 54-schema
AbpBackgroundJobs table. Capture verified envelopes independently of business
processing and commit before acknowledgement. Retain events, serialize duplicate
capture and replay, drain HTTP writers through shared/exclusive advisory leases,
and hold all background/automatic payout activity. Replay requires separately
pinned authoritative-Yoco human reconciliation; unknown events stay held.
Build a baseline-1251fb8 receiver-only artifact and a separate full-V2 artifact;
merge ancestry must preserve the 54-to-55 migration delta. No migration-55/reset
rewrite, production action, new dependency, resource or charge is authorized.
Validate relevant negative/concurrent/crash/replay/fence/54-schema cases using
existing synthetic PostgreSQL fixtures, then existing exact-source CI. Any new
source amendment requires independent acceptance before production authorization.

Receive-only correction validation: 35/35 focused tests passed, zero skipped,
including the real Production-mode schema-54 receiver, host/operator versus
Tenant authorization, signed capture and duplicate receipt, abrupt process
restart, and receipt continuity through migration 55. The earlier 101/101 focused
payment/recovery tests passed before the final MVC-attribute-only correction;
this is carried-forward evidence, not exact-source CI. Independent agent review
closed its controller activation finding and reported technical ACCEPT; it is
not Aqua's human acceptance. The amended source still needs human admission and
separate deployment/cutover authorization.

The separate receiver is pinned in `tools/operations/yoco-receiver54.json` and
built from the actually deployed 54-migration source with no migration changes.
[The executable receive-only cutover procedure](../../operations/yoco-receive-only-cutover.md)
retains conservation acceptance before migration 55, authoritative Yoco
reconciliation before Normal, and disabled payouts/workers. Main-push CI and
provider baseline readback remain execution gates; no new operational facts,
provider acceptance, merchant records or deployment IDs have been fabricated.
No production access or mutation was performed for this correction.

CI packaging follow-up: the first exact-source run correctly failed the unchanged
mandatory source-integrity check because CI's newly reviewed receiver-build steps
changed its pinned workflow bytes. Only that workflow digest is refreshed in the
candidate inventory; no mandatory case, assertion or verifier is changed or
waived, and human review of this control change remains pending. The receiver
build helper also needed `git -C <repository root> archive`: CI invokes it from
the backend subdirectory, where plain `git archive` emits a stripped subtree.
Direct archive listings reproduced that exact missing-project cause. Runtime,
receiver pin, reviewed SQL and previously passing tests remain unchanged.
The additional restricted-file/pin/duplicate-JSON test passed separately (1/1).
