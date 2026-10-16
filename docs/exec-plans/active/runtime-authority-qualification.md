# Runtime authority qualification

> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

## Goal and authority

Current owner mission: qualify effective runtime capabilities across startup,
restart, health, workers, callbacks and retries; correct demonstrated unsafe source
paths; retain unattended releases without recurring approval. Scripture governs
Christian ethical application, not a technical-immunity claim. Preserve Tenant/Area,
financial, messaging and data-integrity boundaries. Source at start:
700fadb58ced50ae0d63319ad66db3f0674687ad, existing PR106 feature branch, clean dedicated
worktree. No production deployment, provider/settings/credential mutation, database
session/mutation, service resume, external delivery or restricted activation is authorized.

References: AGENTS.md; docs/aqua-system/README.md and documents04–08; ADR001–003;
docs/operations/cicd-safety-contract.md; production-infrastructure-plan.md;
docs/development/validation.md. Resolve authority and stateful-change skills apply.

## Investigation and hypotheses

OBSERVATION / PRE-EXISTING: normal host unconditionally registers four periodic
workers, independently of ABP's BackgroundJobs.IsJobExecutionEnabled. Bird worker
checks Bird only; weekly/monthly workers check their individual flags but not the
financial or background holds. Yoco monitor has neither recovery nor background
body guard. Outbox processor can be resolved/called directly without checking Bird,
recovery or background holds before claiming/mutating messages. Bird gateway checks
snapshot Enabled but not a later recovery suspension. Financial referral event
handlers and live Yoco processing need separate tracing; do not assume a commission
entry-point check protects every financial side effect.

HYPOTHESES: (1) global ABP execution flag also suppresses independent workers;
(2) registration-only guard is sufficient; (3) direct service calls and per-worker
flags can bypass holds; (4) API image migrator availability implies actual DB DDL
authority. Source falsifies(1); controlled tests must discriminate(2)/(3). For(4),
binary packaging and role grants are separate; production role authority is UNKNOWN.

Production evidence from the previous exact-source assessment is carried forward
until fresh read-only observations are collected: Render normal profile, Bird=true,
Yoco=test, other checked holds=false; DB grants/IAM unverified. No live DB access
is inferred from provider inspection permission.

## Execution plan

1. Inventory all automatic registrations, entry points, credentials and persistence/
   network effects; produce a ten-question capability matrix with source references.
2. Add discriminating negative tests before correction; retain failures. Apply the
   smallest complete registration/execution/provider restriction, without pretending
   configuration guards are independent identity separation.
3. Qualify actual application restart and held outbox behavior against disposable
   PostgreSQL/Redis and controlled providers; prove restricted DB role denials. Keep
   API/migrator/runtime/worker identities and live grants separate from fixture proof.
4. Run focused tests, mandatory security probes and complete applicable hosted CI.
   Update current configuration/runbooks for changed behavior; no live changes.
5. Commit/push only this branch (standing authorization persists), obtain fresh
   Agent2 review on exact final commit and report source/local/CI/provider boundaries.

## Current state and next action

ROOT CAUSE established by17 failing negatives: independent timers and direct
dispatch/financial paths bypassed partial flags. Guards now enforce existing holds
at registration, bodies and relevant direct provider/orchestrator entry points.
Referral-bearing conversion rejects before deferred award publication; direct handler
also rejects. A further direct-service regression failed before its correction.
Positive fixtures retain assertions and explicitly scope synthetic execution grants.

128 initial focused application cases and13 gateway cases passed, followed by69
cases after narrowing test-only dispatch grants. New direct-orchestrator and demo
composition coverage is still being finalized. Two no-match discovery attempts did
not count as executed evidence; explicit rebuild produced the expected direct-service
failure before correction. Missing configuration never grants authority.

Fresh Render read-only evidence19:18UTC remains live1251fb8, normal/Bird/test-Yoco,
financial/background enablement=false. Runtime-specific source matches the starting source
for the inspected paths. No running-binary attestation, production DB session or
role/IAM evidence exists. Known NuGet advisories were inspected; versions unchanged,
not claimed clean or uniformly test-only.

Actual-image isolated PostgreSQL18/Redis probe17/17 passed before final demo-marker
follow-up: normal Production startup, container restart and replacement preserve held
outbox/business fingerprints beyond dispatch interval; restricted API role receives
SQLSTATE42501 for DDL, role assumption and protected mutation. This read/audit/key/
enqueue fixture is not a production grants recipe or full member/admin write proof.
Synthetic webhook secret length initially failed real startup validation; fixture
was corrected, validator retained. Final runtime-source API/maintenance images were
rebuilt; the probe rerun passed17/17, including fresh replacement and missing-cutoff
startup denial. Safe output is retained in the verification package. Latest focused
test execution is pending at publication; the demo positive's test-container accessor
compile error was corrected without changing its assertions.

Agent2 interim review found Compose profile interpolation still requiring migration
credentials; corrected with a separately loaded maintenance overlay. API image omits
migrator; separate maintenance image has separate connection. Agent2 also identified
demo calculator compatibility: resolved with a default-disabled calculation marker,
installed only by validated disposable demo host and rechecked against dedicated
loopback DB/client/environment. No ordinary Boolean exception or global financial
enablement added. Exact final-source review remains pending.

209 mandatory Node cases include appended0209 for required container-probe CI wiring;
all208 previous identities retained. Explicit workflow/script/Docker/Compose pins
updated; external custody remains UNVERIFIED. Mutation fixture still170pass/1skip
exit0→BLOCKED. Runtime matrix is docs/operations/runtime-authority-qualification.md.
Next: finish final focused checks/imageprobe, publish only this feature branch, run
complete hosted CI/PG and obtain exact-final-commit Agent2 review. Production authority
and readiness remain BLOCKED; no source guard is promoted to independent privilege.

Follow-up to1ed5c48: expanded local suite151/151 passed, including real calculator
demo composition. Inspection of the opt-in pilot regression exposed masked explicit
authorization denial; five added negatives all failed before correction. Restored
recovery no-op → pilot denial → shared hold ordering in four workers, and pilot denial
before financial hold in the scheduler. Agent2 independently confirmed causality.
First hosted PG run39passed/1failed: payment/approval race now stopped its synthetic
payment before the lock under the new hold. Added an explicit test-only financial
grant to that isolated positive, preserving all lock/transaction/outcome assertions.
Final focused, actual-image, complete CI and exact-commit review are rerunning; do
not promote earlier results to the final source.

Final runtime-source focused suite156/156, zero skips, and rebuilt API actual-image
probe17/17 passed. The4d25f72 hosted PG job passed base40+memberArea24; D10had13pass/
1fail at the first synthetic payment's financial hold. Agent2 established exact
TRX/source causality. Added explicit financial authority only in that isolated D10
case after its PostgreSQL-mode assertion; all D10 scope, no-write and call-count
assertions remain. Runtime code is unchanged by this last fixture correction.
Complete final hosted CI and exact-head review remain required; final results are
recorded on PR106 to avoid falsely self-attesting the source commit in its own tree.

The first D10 grant placement in d1e549a was incorrect: ConfigureD10Async installs
a replacement IConfiguration after the grant. Both hosted runs still failed13/1 in
D10. The correction now grants only on the returned case-local configuration and
asserts resolved instance identity before service construction. Rebuilt web tests;
all14 D10 cases passed locally against fresh PostgreSQL18 using an internal-network,
read-only SDK container without host credential mounts. Repeated execution passed14/14
again; local evidence is console summary only (logger options produced no retained TRX);
final hosted TRX identity/count verification remains required;
no runtime guard, D10 assertion or production configuration was changed.
