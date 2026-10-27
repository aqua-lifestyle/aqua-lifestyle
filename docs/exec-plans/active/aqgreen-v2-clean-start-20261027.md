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
