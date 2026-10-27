# Status-only preflight amendment validation

The SQL baseline is the accepted status-only amendment. The subsequent launcher-only diagnostic correction is PROPOSED pending Aqua's acceptance and a fresh owner execution window. No diagnostic-candidate production execution or acceptance is asserted. Existing B6, SQL, reset/restore, migration 55 and D10 runtime sources are unchanged.

| Artifact | SHA-256 |
|---|---|
| preflight.sql | `b0b495e30d0f3b910980f6d212b75b57de725c35f56c253ff973f681382264dd` |
| preflight-checks.sql | `1d8845c76fa87843f48fc2e648a57333c94d2417fc018c4b499ff28d25e91651` |
| aqgreen-clean-start-preflight.mjs (diagnostic correction) | `483f9cd595ca6eaa00849f6fce3e417665587c4a2e559d5206e20b2750670f2d` |
| state.sql | `db3d36ac23172c2005aa6edd202ea6f9cb6b4635c75c1bdbd2889f47571fd100` |

## Diagnostic correction evidence

`node --test --test-reporter=spec tools/operations/aqgreen-clean-start-preflight.test.mjs`:
**17 passed, zero failed/skipped**. All original 11 cases remain; six focused cases
exercise exit/status categories, fixed identity/timeout marker recognition, malformed
provider/client output, deadline handling and unchanged PASS/FAIL/UNKNOWN semantics.
Synthetic credentials and financial diagnostic canaries never appear in receipts.
The existing .NET CI wrapper requires all 17 Node cases with zero failures/skips.
No dependencies, production SQL or new environment were used. Fresh exact-source
CI is attached to the published diagnostic commit; prior CI alone does not qualify it.

The 09:29:25.975 UTC authorized execution used the old launcher
`02cfc986a3435234308be8595a4775ad7d490b5c67e8380d87c983e93231c525`
and returned UNKNOWN/exit 1. Its cause remains UNDETERMINED. The new categories
are evidence about future observed failure stages, not a diagnosis of that attempt.
Privacy/source self-review confirms only allowlisted categories, stages, bounded
numeric child exit statuses and UTC timestamps are added. Client diagnostics are
examined only in memory and never printed or saved. No SQL predicates, reset/D10
guards, TLS requirements, retries or business-result rules changed.

## Carried-forward status-only SQL evidence

Before the diagnostic correction: 21 focused xUnit cases passed, zero failed/skipped, on the existing PostgreSQL 18.4 fixture. This includes the eight existing reset/recovery cases and 13 added status-screen/supporting cases. The supporting Node case executed the original 11 launcher/privacy checks; those 11 also passed standalone. These are separate evidence categories, not 32 independent database executions.

Artifacts: `/tmp/aqgreen-status-preflight-tests/status-preflight-reviewed.trx`, `/tmp/aqgreen-status-preflight-reviewed.log`, `/tmp/aqgreen-status-preflight-node-tests-final.log`. The Release build passed with warnings; no warning-free build or dependency changes are claimed. One development fixture initially omitted its monthly due-policy FK; that fixture was corrected, without changing guard logic or assertions.

The SQL tests prove predicate parity with state.sql, exact EF migration identity, unchanged source fingerprint, protected-record conservation, actual payment/loan/monthly exposure rejection, deleted and small/large payment populations, invalid scope/history, RLS-filtered UNKNOWN and read-only enforcement. A SELECT-only test role without pg_control_system EXECUTE still runs the database screen. Launcher tests cover endpoint substitution, SQL changes/races, inherited connection options, strict certificate settings, expired windows, partial/error redaction and rejection of raw counts or fabricated business acceptance.

Technical self-review followed the repository benchmark: source and Tenant boundaries, unknown-versus-zero semantics, client/server TLS roles, rollback-before-output, immutable SQL snapshots, deadlines and strict output shape. Review found and corrected per-check false PASS under RLS filtering and a file-change race between hash verification and execution. No unresolved branch-owned defect was identified in that review. This is not independent human amendment acceptance.

Actual provider TLS delivery is not newly production-verified. The 26 October logical target/TLS evidence is carried forward; the subsequent metadata-only connection attempt returned no verified result. The amended launcher reports UNKNOWN on connection/certificate/access failure and never retries or downgrades TLS. Mocked launcher tests do not prove a current Render handshake.

The new logical identity mode needs explicit owner/reviewer acceptance for this read-only screen. It does not prove physical cluster continuity or waive native binding/private audit custody in the unchanged reset/D10 procedure. Fresh source-bound CI and human authorization remain release gates; earlier application adff7d9 CI and executor 136d467 CI are carried-forward evidence for those revisions only.

## Earlier clean-start implementation evidence (before this amendment)
# Clean-start non-production evidence — 27 October 2026

These are local implementation tests on synthetic data. They do not establish
production classification, protected custody, human acceptance, live Yoco behavior
or release authorization. Final revisions remain unpublished in the two clean-start
worktrees; base revisions are recorded in README.md.

This is the local validation record made before owner-authorized CI publication.
Published candidate identities and actual CI results are attached to the commits'
GitHub check runs; a CI pass cannot supply production-data or human acceptance.

## Executable SQL identity

Paths are relative to
`AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.EntityFrameworkCore/Operations/CleanStart/`.

| File | SHA-256 |
|---|---|
| preflight.sql | `12f2e7cee40682e6bf1d031211842143d0d0a8de5dcf79d5333a06555a1152d6` |
| state.sql | `db3d36ac23172c2005aa6edd202ea6f9cb6b4635c75c1bdbd2889f47571fd100` |
| reset.sql | `29535d77b81021e5d2ca76369818c35b0d9cd74e0fe6ef7a618b63e335ea5f52` |
| restore.sql | `1f9e1e8493037151e3d96d8c3a76b8f09e622854e703b838171fcef0a7bd33e1` |
| migration-55.reviewed.sql | `1f45ef7b67fa6eae747ca70edfed05679ef63cf4cfe2f2cf308b9d31b8c1faea` |

The first preflight invocation includes both preflight.sql and state.sql; approve
their exact pair, native source identity, boundary and aggregate reporting rules.
No preflight or reset was executed against production.

## Results and artifacts

| Validation | Actual result | Local artifact |
|---|---|---|
| Release solution build | passed; 18 warnings, zero errors | `/tmp/aqgreen-clean-start-build-current.log` |
| Focused backend/PostgreSQL | 126 passed, zero failed/skipped | `/tmp/aqgreen-clean-start-tests/clean-start-current.trx` |
| PG 18.4 clean-start application flow | one passed, zero skipped; execution marker present | `/tmp/aqgreen-clean-start-web-tests/clean-start-e2e-current.trx` |
| Executor including real Git-reader regression | 137 passed, zero failed; one existing skipped | `/tmp/aqgreen-clean-start-executor-final-with-git-reader.log` |
| Application deployment workflow | five passed | `/tmp/aqgreen-clean-start-workflow-tests.log` |

Backend result SHA-256:
`ad98af3bffe47b7aed252c1d431643051a5c48b40842c72103e5c620f55514ef`.
Application-flow result SHA-256:
`9b14eda4fc055ae6d581aebc2af29f63600b646bfa86889432f7a39d0bab96cd`.
Its `clean-start-commissioning.ran` marker SHA-256:
`3828db6f763dabb35b5c4261659dd6769ea2e1a422434a9c933fa1b40d920f1e`.
Executor log SHA-256:
`b97133c2e2fb74a55d3d94d94359e9eb21dfc7ebbe1b8f9edf57ba810b4466a6`.

The 126 cases comprise 8 clean-start SQL, 23 clean-start admission, 22 historical
D10, 27 PostgreSQL placement allocator, 16 PostgreSQL structural completion,
12 domain structural completion, 8 weekly commission evidence and 10 PostgreSQL
sales-invalidation cases. Clean-start SQL uses the existing cached PostgreSQL 18.4
fixture; existing allocator/structural regressions retain their PostgreSQL 16
fixture. Migration 55 is executed from retained SQL, rejects duplicate application,
and subsequent EF Migrate is a no-op with exactly one history entry. The pinned
EF 8 model comparison reports no pending model changes.

The full application flow uses a fresh synthetic PostgreSQL 18.4 database, actual
D10 admission and application services, synthetic independently bound inputs and
a fake checkout gateway. It proves the owner-selected unpaid root, ordinary
payment/approval separation, six genuine-path synthetic invitations, five-wide
spillover and retry protection. It does not prove actual human independence or
provider payment delivery. CI verifies both the exact test result and its marker.
A local launch that omitted CI's transactional/development flags failed during
Default-Tenant setup; the final run uses those existing CI flags and passed.

The skipped executor test is `disposable PostgreSQL admits at most one concurrent
use of a signed authorization` because AQUA_TEST_DATABASE_URL was not configured.
Its concurrent database execution is not claimed. Full backend/frontend/provider
qualification and fresh remote exact-source CI remain required for release.
NuGet advisory enumeration was unavailable after a bounded timeout; no dependency
changes were introduced. Accepted B6 tests are carried forward, not rerun here.

## Reproduction using existing test tooling

From `AqualLifeStyle/9.4.2/aspnet-core`, after the existing Release build:

```bash
dotnet test test/AqualLifeStyle.Tests/AqualLifeStyle.Tests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~AQGreenCleanStartPostgreSqlTests|FullyQualifiedName~AQGreenCleanStartCommissioningTests|FullyQualifiedName~AQGreenD10CommissioningTests|FullyQualifiedName~AQGreenPlacementAllocatorPostgreSqlTests|FullyQualifiedName~AQGreenStructuralCompletionEvaluatorPostgreSqlTests|FullyQualifiedName~AQGreenStructuralCompletionCalculatorTests|FullyQualifiedName~AQGreenV2WeeklyCommissionEvidenceTests|FullyQualifiedName~AQGreenSalesInvalidationPostgreSqlTests'
```

For the existing fresh isolated test database, set ASPNETCORE_ENVIRONMENT=Development,
REPRO_PG=true, REPRO_TRANSACTIONAL=true, REPRO_PG_CONNECTION,
ConnectionStrings__Default and REPRO_MARKER_DIR using its synthetic fixture values,
then run (never substitute a production connection):

```bash
dotnet test test/AqualLifeStyle.Web.Tests/AqualLifeStyle.Web.Tests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~CleanStart_EmptyTenant'
```

From the executor worktree:

```bash
node --test --test-reporter=spec tools/deployment/*.test.mjs
```

From the application root:

```bash
node --test --test-reporter=spec tools/deployment/workflow.test.mjs
```
