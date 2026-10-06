# PR #97 backend CI timeout investigation

> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

## Goal

Diagnose the backend deadline breach, compare PR and main, correct only a proven
test-lifecycle or CI defect, and verify complete backend/PostgreSQL coverage plus
both hosted container builds. Publication is authorized; merging is not.

## Authoritative references

- Owner's PR #97 backend-only CI investigation instructions.
- [Repository validation](../../development/validation.md).
- Repository workflow `.github/workflows/ci.yml` and backend test projects.
- [ABP 9.4.1 integrated test disposal](https://github.com/aspnetboilerplate/aspnetboilerplate/blob/v9.4.1/src/Abp.TestBase/TestBase/AbpIntegratedTestBase.cs).
- [ABP 9.4.1 Web test base](https://github.com/aspnetboilerplate/aspnetboilerplate/blob/v9.4.1/src/Abp.AspNetCore.TestBase/AbpAspNetCoreIntegratedTestBase.cs).
- [ASP.NET Core 8 TestServer lifecycle](https://github.com/dotnet/aspnetcore/blob/v8.0.8/src/Hosting/TestHost/src/TestServer.cs).

## Confirmed decisions

No business-policy decisions. Preserve all test coverage and frontend security
fixes. No product, AQGreen, authorization, finance, or unrelated cleanup work.

## Assumptions

None about causality: healthy slow execution, resource leakage, resource waits,
and infrastructure differences remain competing hypotheses until measured.

## Baseline

- Starting branch/remote HEAD: `b6fefc16b06e11e13037a75063a9ecb5f6ba2928`.
- Main/merge-base: `26803037d9923842db12de621ab6f87c3cd045cc`.
- Remote ahead/behind 0/0; main ahead/behind 2/0. PR #97 OPEN, MERGEABLE, UNSTABLE.
- Latest PR run `37333183091`: backend cancelled after 25m21s; tests had 23m25s.
  Restore/build passed; Web project passed 92/92 in 9m17s; application project
  did not finish. Hosted containers skipped. Frontend, PostgreSQL, Vercel passed.
- Main run `37152688401` at the current main SHA: same backend timeout; Web
  project passed 92/92 in 6m5s. Older successful main run `33734267970` at the
  same SHA: application 1247/1247 in 18m34s, Web 92/92 in 6m55s.
- PR and main backend trees have identical Git tree ID
  `33f37a211c4d34f8bbf98e5988df90cf3f22f3da`; workflow also unchanged.
- Local .NET SDK 8.0.131/runtime 8.0.31; Docker available, no running containers
  at investigation baseline.

## Evidence

Hosted job metadata/logs downloaded to temporary storage; inspected successful
and timed-out current-main execution. The 25-minute limit is the GitHub job
budget, increased from 20 minutes by historical commit `1e18748`.

The unmodified diagnostic solution run uses console-normal plus TRX logging.
Application/Web test hosts are PID 791687/791714, parent timer PID 791446;
log `/tmp/opencode/pr97-backend-local-before.log`. At 1m19s RSS was approximately
293/295 MB, at 6m16s approximately 757/956 MB, and at 13m50s approximately
1044/1605 MB. CPU time and passed-test output continue advancing. Stack reports
show active EF compilation, host initialization, seeding and test bodies rather
than an unchanged blocking stack. Containers so far start/stop and are not held.

Initial global-bus hypothesis was refined by primary ABP 9.4.1 source:
`AbpTestBaseModule` already disables the default bus for both test projects.
The application test module's two explicit `EventBus.Default.Register` calls
nevertheless capture its IoC scope and discard their teardown leases.
Separately, `AbpAspNetCoreIntegratedTestBase` does not implement disposal, and the
repository Web test base does not dispose its client/server/IoC scope.
These observations led to the focused regressions and correction below.

The PR diagnostic run was intentionally stopped at approximately 26m45s after
reproducing the deadline breach: 414 application tests completed (not a full
project pass); Web completed 92/92 in approximately 20m42s. Application CPU
remained active, with changing passed-test output. An isolated pristine main
checkout at `/tmp/opencode/pr97-main-diagnostic` passed restore/Release build;
its same diagnostic solution command ran under timer PID 797569, application
PID 797713, Web PID 797712. Main source is unmodified. At 6m9s RSS was approximately
696/913 MB and test counts continued increasing, matching the PR's behavior.

## Open questions

Hosted runtime and container acceptance must be observed at the follow-up HEAD.
Local timing cannot establish that the unchanged hosted job budget is sufficient.

## Current state

Local correction IMPLEMENTED and TESTED through the entire unfiltered solution
and dedicated real-PostgreSQL selection. Hosted acceptance is pending publication;
no hosted success, merge readiness, deployment, or production claim is made here.

## Proven lifecycle defects and focused correction

Main's comparable unmodified local run exceeded 26m: 406 application tests
completed; Web passed 92/92 in approximately 20m. CPU remained active and RSS
grew similarly to PR (application about 1.51 GB; Web peaked above 2.1 GB).
It was intentionally stopped after reproducing the job-budget breach. Neither
incomplete application run is a full-suite pass.

Two new focused regressions exposed the lifecycle defects before correction.
The finalized application regression also failed against unchanged main with
only the regression test temporarily added: a disposed older IoC scope remained
alive after collection. With the two owned global registration leases released
in module Shutdown, it passes. A diagnostic GC-root inspection showed ABP's
mapper legitimately retains its latest localization scope, so the regression
creates a subsequent host to distinguish that bounded reference from unbounded
old-host retention. No heap field values or sensitive material are recorded.

The Web regression exposed the missing IDisposable test-base contract. The fix
disposes client/server/IoC even if an earlier cleanup fails, and is idempotent.
ASP.NET Core 8's TestServer allows CreateClient after disposal but rejects a
request; the final regression checks that actual documented/source contract.
Both finalized regressions now pass. Temporary diagnostic instrumentation was
removed. No product source, test filter, assertion in an existing test, timeout,
frontend dependency, or workflow configuration was changed.

## Completed work

Baseline verified, two backend test projects inventoried, hosted/main comparison
and local matched-budget reproductions established. Two lifecycle corrections
and one regression per test project IMPLEMENTED; focused regressions TESTED.

### Complete local validation (2026-10-06)

- Release restore/build passed on PR and isolated main; final Release solution
  build passed after the correction and finalized regression tests.
- Unfiltered `dotnet test AqualLifeStyle.sln --configuration Release --no-build
  --logger trx --logger "console;verbosity=normal"` completed successfully.
  Application: 1248 passed / 0 failed / 0 skipped, TRX wall time 49m37.60s.
  Web: 93 passed / 0 failed / 0 skipped, TRX wall time 16m26.59s.
  Whole-command wall time: 49m44.47s. Counts increase by exactly one new
  lifecycle regression in each project from the 1247/92 baseline.
- Corrected memory samples: Web about 465 MB at 12m versus baseline above
  2.1 GB by 18m; application approximately 0.55-0.94 GB across most of its
  complete run, versus approximately 1.5 GB before baseline cancellation.
  These are per-process samples, not an inferred hosted memory measurement.
- The largest cumulative application-class durations were programme joining
  (815s), admin participation (684s), programme progress (612s), and payment
  confirmation (463s); these are sums of test durations, not class wall times.
  The slowest individual tests included the intentional 3906-participant Onyx
  simulations (approximately 100s/92s). Repeated host/EF setup and seeding were
  visible in diagnostic stacks. No test was removed or optimized away.
- Existing aggregate PostgreSQL fixtures completed, including placement,
  corruption, concurrency, structural evaluation and migration/rollback tests.
  Docker containers were observed starting/stopping normally; none remained
  after full execution. Both named PostgreSQL collections retain serial scope,
  template/database-lease ownership and connection/lock assertions.
- The dedicated CI filter was executed unchanged against an isolated local
  `postgres:15` service with `REPRO_PG=true`, `REPRO_TRANSACTIONAL=true`, and
  the provenance-marker directory set. 38 passed / 0 failed / 0 skipped;
  whole-command wall time 2m35.60s. All existing named TRX outcome checks,
  the two payout race results, all six required marker files, and the rejection
  of failed/skipped/not-executed results passed.
- Post-run PostgreSQL activity showed only the diagnostic query itself, with
  no lock wait; the owned temporary PostgreSQL service was stopped/removed.
- Aggregate Web execution retains its existing provider guards. The dedicated
  job supplies real PostgreSQL execution for those guarded bodies. Application
  PostgreSQL fixtures are separate from the dedicated Web selection; eliminating
  duplicated expensive PostgreSQL execution was not the remedy.
- `git diff --check` passed. No existing tests, assertions, providers, filters,
  job dependencies, timeouts, workflow logic, backend product source or frontend
  security files changed. Backend test projects still execute concurrently;
  xUnit class/collection scheduling is unchanged.

## Next action

Publish the separately reviewed test-lifecycle follow-up and observe both push
and PR workflows. Require backend completion, all PostgreSQL body/provenance
checks, frontend checks, Vercel, and actual hosted API/web container builds.
This is the pre-publication local evidence record; hosted acceptance must come
from the new PR checks. Do not merge or treat local validation as hosted success.

## Git/branch context

Branch `feature/admin-ui-remediation`; no rebase. Existing unrelated untracked
research files preserved. This investigation owns only its worklog and any
subsequently justified narrow test/CI correction.
