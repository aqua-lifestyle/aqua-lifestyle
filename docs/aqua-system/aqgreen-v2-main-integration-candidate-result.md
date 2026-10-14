# AQGREEN V2 — MAIN INTEGRATION CANDIDATE RESULT

Local integration only. This derivative does not inherit formal owner acceptance.
No push, PR, main mutation, production access, deployment, real provider request,
real financial operation or B6/B7 operation was authorized or performed.

## Identities and preservation

- Integration branch: `feature/aqgreen-v2-main-integration`, isolated worktree
  `/tmp/aqgreen-main-integration-20261012/candidate`.
- Accepted parent: `2ddaa4977888be7bd91064a500806c0d29c7b5fa`.
- Main parent: `21a41694163ef09628fd84a9f4e12cb0efe1bb85`.
- Accepted branch remains unchanged, with its original implementation/evidence
  commits and eight source-identity records byte-identical to the published tip.
- Original manifest `72219b17d8cd5a55f9c0b542190caa969bb5193131fa2924fdefcdff54d1e26c`
  and accepted derivative manifest
  `5e133be584398b2db14a5c542e27860bd6f02adae0c66c1488ef30e7ee2573e0` unchanged.
- Prior two-file uncommitted dependency candidate remains unchanged in its own
  worktree. Its patch still hashes to
  `a9c46eb893d01686eb3469b4b6921ee449a65cb4fb4b51c8d8927158a1b7c6ae`.
- Accepted archives were not written to or replaced; both archive hashes and all
  2,101 original + 2,105 derivative source checks were verified again successfully. Unrelated original-worktree
  files and other agents' worktrees were not staged or modified.

Main's four newer commits are `cbed24c`, `a5ae950`, `691d565`, and merge `21a4169`.
Both remote references were read again and still matched the exact parents above.

## Eleven textual conflict resolutions

| File | Resolution |
| --- | --- |
| `.github/workflows/ci.yml` | Retain main's complete inventory/result checks, PostgreSQL readiness and required execution markers; subdivide slow jobs without dropping coverage. |
| Frontend `app/layout.tsx` | Retain local fonts and accepted demo banner together with main's pilot boundary. |
| Frontend `package.json` | Retain main's dependency declarations, including the requested patches and its independently updated Axios/ESLint configuration. |
| Frontend `package-lock.json` | Retain main's locked graph, including source-map-js 1.2.2. |
| `AdminWeeklyEarnings.tsx` | Retain both mode checks; test-money wording covers demo and pilot; release/mark-paid controls require known non-test mode plus permission. Tenant scope remains accurate. |
| `member-programmes.tsx` | Retain accepted demo checkout and main's prospective pilot checkout paths and invitation-only boundaries. |
| `programme-invitation-landing.tsx` | Retain both test-mode contracts and attribution-preserving joining. |
| `AdminCommissionAppService.cs` | Retain accepted demo payout denial and main's pilot denial and test-ledger safeguards. |
| `ClubMemberProgrammeParticipationAppService.cs` | Retain transactional accepted joining/confirmation recorder and main's prospective joining/replay checks. Independent pilot roots remain separately privileged. |
| `HealthController.cs` | Retain main's restricted pilot capability set; append the validated demo capability separately. |
| Host `Startup.cs` | Validate both exact environments; choose the pilot gateway only in pilot, otherwise use the accepted validated demo/normal gateway registration. |

The first merged build also exposed an automatic-merge declaration collision in
`ProgrammeInvitationPlacement`. The corrected DTO has one required invitation ID
and sponsor participation ID, rejects empty IDs, and retains the nullable legacy
recruiter-participation alias. Both resolver and main's prospective recorder use
that exact authoritative identity. No default or invented attribution was added.

## Six original backend failures

Five successful-demo startup theory fixtures omitted the mandatory plain-HTTP
loopback client root. Add `http://127.0.0.1:3100/` to those fixtures. All five passed
in the focused rerun; startup validation was not relaxed.

The inactive-progress test asserted an exception despite the accepted pending
journey contract. Its replacement explicitly asserts inactive membership,
awaiting payment, no activation timestamp, upcoming activation, no structural
result/completion, and no V2 gate/evaluator invocation. Level zero describes the
legitimate pending lifecycle, not an evaluated active placement. Active missing
or corrupt selected-V2 placement still fails; no active result is fabricated.
Implementation behavior was not changed to make this assertion pass.

## Additional test-harness finding

REPRODUCTION: selecting administrator API and invitation regressions together on
one fresh PostgreSQL database allowed their collections to initialize concurrently.
First this collided during DDL; later a repair seeder provisioned a customer while
another test was between identity creation and explicit customer creation.

ROOT CAUSE: the existing Authentication web journeys collection lacked the
nonparallel isolation already used by the approval/progress/weekly PostgreSQL
collections. Define that collection with `DisableParallelization = true`.
Explicit `Task.WhenAll` application races remain concurrent. Production and test
startup behavior are unchanged. An intermediate startup-lock experiment did not
cover the fixture-body/repair-seeder race and was removed completely.
Original failed attempts remain in local evidence; they are not reported as passes.

## Dependency and build evidence

Next.js **16.3.8**, sharp **0.35.5**, source-map-js **1.2.2**. Current main already
contains all three; reconciled package and lock match main exactly. This preserves
main's separate dependency work rather than overwriting it with the older patch.
No force fix, audit suppression or new application dependency was added.

Actual execution: Node **24.21.0**, npm **11.19.0**, clean locked offline installation,
Docker `--network none`, production npm audit **0 findings**, ESLint, TypeScript,
**104 focused frontend tests in 17 files**, Next production build, and eight
production HTTP/native dependency regression groups passed. Runtime uses a
synthetic loopback backend, not a browser or real business/backend acceptance.
Sharp SVG/PNG and source-map excessive-offset checks passed; anonymous/origin/session/
Tenant/invitation/local-image checks passed with external image denial.

Backend SDK **8.0.131**, EF **8.0.8**, Release solution build: **0 errors**; existing
warnings remain. Later focused web builds include the final collection correction.
NuGet restore retains the pre-existing test-only AngleSharp 1.1.2 moderate advisory;
the clean npm production audit is not a claim of a clean whole-backend audit.

The host EF CLI is incompatible (10.0.9), so a temporary external .NET probe used
the candidate's existing EF8/Npgsql binaries and `HasPendingModelChanges`:
**54 migrations discovered; no model differences; no database connection**.
The probe adds no application source or package dependency.

## Final validation and runtime

Actual Release execution: **1,289 application cases passed, 0 failed/skipped**.
The final eight application partitions preserve main's Application/EF/remainder
coverage domains and existing 25-minute limits. Exact CI discovery scripts proved
all 1,289 identities equal the eight-shard union; completed TRX identities/counters
match every inventory.

| Application shard | Result | Local test-process wall time |
| --- | --- | --- |
| application-administration | 63/63; no skips | 15m 29s |
| application-membership | 102/102; no skips | 15m 56s |
| application-authentication | 68/68; no skips | 5m 12s |
| application-remainder | 132/132; no skips | 7m 57s |
| entity-framework-migrations | 51/51; no skips | 19m 41s |
| entity-framework-placement | 88/88; no skips | 4m 16s |
| entity-framework-remainder | 144/144; no skips | 5m 56s |
| remainder | 641/641; no skips | 15m 1s |

Web partitions: placement **31 passed** (8m 23s), controllers **24 passed / 14 skipped**
(7m 5s), remaining **38 passed / 9 skipped** (9m 20s). Total **93 passed, 23 skipped,
0 failed**; all 116 discovered cases accounted for. The 23 explicit PostgreSQL-only
skips were exercised in separate PostgreSQL runs, not treated as PostgreSQL passes
from the default suite. Other guarded default-provider facts also do not establish
PostgreSQL execution. The corrected inactive-progress case passed in the final
remaining web shard.

Final focused PostgreSQL execution on internal-network disposable PG **15.19**:

- **52/52** approval/placement/progress, administrator direct API/scope, invitation,
  commission/worker/retry and payout-mutation boundary cases passed, no skips.
- **7/7** main prospective pilot cases passed: actual service joining/approval, six
  sponsor-local placements, immutable attribution, parallel approvals/root admission,
  rollback, administrative 5/5/5 evidence, test ledger, direct SQL no-conversion/no-
  payout constraints, and excluded workers/services.
- **Both accepted Lean service cases passed**: the negative case in the original
  two-case run; the positive case retested alone on a new empty database. The mixed
  first run correctly failed its freshness assertion after the negative case had
  already created three participations. No assertion or business rule was relaxed.
  Positive provenance records six members, five concurrent duplicate joins, qualified
  level one, R150 simulated calculation, 5/5/4 control R0, and genuine **Held R150**
  as well as unreleased/no-payment records; no loan/Onyx/provider or human observation.
- **16/16** administrator and invitation regressions repeated on a second fresh
  database, no skips: no DDL/repair-seed/fixture overlap after collection isolation.

EF provider cases also executed on the established disposable PostgreSQL 16-alpine
fixtures. No production/historical B6 reconciliation or cutover was run.

Original combined application/EF parts exceeded their budgets (26m 39s, 26m 45s,
27m 33s). The 200-case remaining application batch passed all cases but took
26m 30s as a process; split it into authentication/account and remaining cases,
which passed in 5m 12s and 7m 57s. Original over-budget runs are not CI success.
Final maximum application process time is **19m 41s**; maximum web is **9m 20s**.
These are local test-process measurements, excluding CI setup/restore/build. The
measured full Release solution build took 2m 1s. Future GitHub runner job time is
not claimed: publication and remote CI are still unauthorized. No job timeout was
increased, hang guard removed, or test case suppressed to achieve these results.

A final completeness check found current VSTest/xUnit emits explicit NotExecuted
records while its redundant TRX notExecuted counter remains zero. The new web
checker therefore requires the exact result-name multiset, every supported outcome,
and total-minus-executed matching explicit skips; the redundant counter must be
zero or the exact skip count. Application checks remain unchanged. Positive
zero/accurate-counter variants pass; missing/duplicate/unexpected identities, unknown
outcomes, inconsistent execution counts and inconsistent skip metadata are rejected.
Local PowerShell execution was unavailable; Node equivalence, real TRX inputs, six
negative mutations and static YAML checks were executed.

No new browser walkthrough, full frontend campaign, image build, provider acceptance,
remote CI or production qualification was executed. The original 17-case/Node24
acceptance remains separately preserved. Raw temporary logs, tokens, caches and
disposable database contents are excluded from the candidate.

[Sanitized evidence](../verification/aqgreen-v2-main-integration-20261012/summary.json)
and [candidate application/CI source manifest](../verification/aqgreen-v2-main-integration-20261012/candidate-application-source.sha256)
identify this derivative's **2,007 files**. Its manifest SHA-256 is

`3bd719f304f1d3d3b423f8c97bbd28b8aa4f1b0a4b1eedb3056b7becb25c2144`.

This application/CI manifest has a different scope from the preserved accepted full
manifest; it is not a replacement or owner-acceptance record.


## Publication and main deployment boundaries

Repository configuration inspection: add only this exact integration branch to
Vercel's `git.deploymentEnabled: false`, retaining the accepted branch suppression.
Main/other branches retain their existing behavior. Main's workflow event triggers,
permissions, PostgreSQL job/readiness/marker requirements, image publish rules,
Render blueprint, worker restrictions and immutable test-ledger controls are retained.
No provider configuration was changed. No new remote CI run or deployment exists
for this local candidate.

Main's Vercel production auto-deployment remains enabled. Publishing this guarded
feature branch and proposing a draft PR are separate from merging main, which can
ship the frontend while the separately controlled backend remains unchanged.
Owner authorization and coordinated deployment handling are required before any
main merge. No production service state or live member/history compatibility is
inferred from local tests. This task does not authorize production migrations,
B6 reconciliation or B7 cutover.

**INTEGRATED and TESTED locally — ready for separate feature-branch publication authorization.**

Remote full CI/containers and review remain outstanding before any claim of MERGE
READY. Main merge/deployment authority remains separate. No formal owner acceptance
is inherited by this corrected integration candidate.
