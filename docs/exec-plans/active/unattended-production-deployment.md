# Unattended production deployment

> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

## Current execution — external executor, 2026-10-18

Latest owner decision authorizes credential-free implementation with the existing
account, on isolated branches, without removing Xolani-M administration, secrets,
provider settings, merge or production deployment. Source baseline PR106 at
92eabe0; executor initially empty. Earlier provider/governance investigations are
carried forward, not rerun. Application branch remains
feature/unattended-production-deployment; executor branch is
feature/automatic-qualified-executor. Documentation-only initial executor main
exists solely to support a reviewable PR and contains no workflow.

Reuse the controller with explicit source/executor API routing and version4 identity;
replace application deployment with automatic Actions-only dispatch; keep executor
code/targets/policy independently held, and application Git objects inert. Preserve
provider sequencing, restricted runtime, provenance, immutable receipts and retries.
Focused tests cover actual candidate/admit/deploy entries with real disposable Git
and substituted providers, credential destinations, untrusted input, CI App/attempt,
legacy ambiguity and automatic trigger negatives. No full application CI dispatch
is required for the credential-free local checks; final publication may trigger
the repository's existing CI automatically.

Custody remains BLOCKED: current application administrator also controls executor;
base/App automation access is unqualified. No protected mandatory-test authority
has been installed. Production provisioning must await actual isolation and API
observation permissions. Current setup is in
docs/operations/deployment-executor.md. Next: complete focused checks, pin copied
controller provenance to the final application commit, publish both review branches
and record actual PR/CI evidence without claiming production operation.

## Goal and authority

Latest explicit owner task authorizes investigation, implementation, isolated
testing, documentation, feature branch, commit/push, PR and fresh Agent 2 review.
Qualified main merges must deploy without per-release human approval or a release
token. Production deployment, merge, provider/settings/credential changes, service
resumption, database changes and restricted operation activation remain forbidden.
This supersedes PR #105's manual release-approval requirement, not runtime safety.

## Git context and preserved work

Verified remote main and merged PR #105: `23f176db6eb13af5ee01ff69f1a9f7bbe6b880ff`.
New branch: `feature/unattended-production-deployment`, isolated worktree. Earlier
uncommitted governance documentation remains in its original worktree, untouched.
No Python is used or introduced.

## Investigation and failing-before evidence

- Release 38912818702 qualified/published its candidate, then admission failed with
  ACTIVATION_BLOCKED_AUTO_DEPLOY_NOT_AUTHORIZED. Provider step was skipped.
- Newly auto-created production-release environment has no protection/branch rules,
  secrets or variables. Main still has deletion/non-fast-forward rules only.
- ROOT CAUSE: standing activation switch absent, plus per-release authority JSON
  and mandatory human environment approval built into the controller. Fixing only
  the switch cannot establish unattended safe release readiness.
- Before controller changes, two new desired-behavior regressions fail: no-token
  rollout throws EXACT_RELEASE_AUTHORITY_REQUIRED, and a no-approval environment
  throws PROTECTED_ENVIRONMENT_REQUIRED. Existing absent-switch control passes.
- Fresh authorized provider GETs: Render exact repo/main/root/Dockerfile, empty
  hooks, auto-deploy off, not suspended; live 1251fb8, health Healthy, no recovery
  header. Recovery=false/Bird=true/Yoco=test with configured provider keys;
  financial/weekly/monthly/background/demo/pilot/commissioning/seeding flags false.
  These settings are incompatible with the qualified recovery-only contract.
- Vercel exact existing project/team/domain/root/Node24 confirmed; Git link absent,
  production target has no authoritative Git source; public build inputs remain
  sensitive/redacted and cannot be verified. Local read credentials work; production
  Actions credentials/actual write permissions remain unverified. No writes tested.

## Proposed implementation

Keep one orchestrator, strict CI/source/archive/attempt qualification, provider
sequencing, concurrency, recovery holds and no blind POST retry. Replace per-release
JSON, expiry and required human gates with a standing versioned policy. Its digest
binds the candidate/audit; one qualified bootstrap pair establishes initial
source/schema/backup/rollback evidence. No bootstrap will be fabricated for today's
incompatible runtime. A missing bootstrap remains a visible infrastructure blocker.

Subsequent baselines come from trusted immutable VERIFIED workflow receipts, not
mutable live SHA inference. Same-run retries automatically select the complete
earlier audit chain with exact CI identity; completed/ambiguous/foreign receipts block
new mutations. Receipt expiry/deletion blocks rather than falling back to bootstrap.
Provider IDs and originating release attempts survive partial failure.

Validate main PR/CI governance and unattended main-only credential environment before
the environment job and again before provider mutation. A standing enable/kill switch
is a one-time setup control, not per-release authority. No reviewer/wait/custom
approval gate is accepted. Existing runtime remains unchanged; recovery-only is the
safe qualified alternative, not permission to transition the current normal service.

## Validation and next actions

Implement focused positive/negative policy, receipt, CI drift, concurrency, provider
failure and runtime-preservation tests; run complete release suite and actionlint.
Push/open a focused PR, inspect complete final-head CI inventory/skips and obtain
Agent 2's exact-commit independent review. Report code, CI, infrastructure and
production readiness separately; no production acceptance inferred from mocks.

Agent 2 preliminary architecture review agrees with this separation and identifies
receipt retention, bootstrap qualification, stale partial releases and trusted
environment custody as material requirements. Final review is pending implementation.

## Implementation checkpoint

- Per-release authority JSON/expiry and mandatory human environment review removed.
  Standing switch, credential custody, main-only environment and complete source/CI
  protections remain. Version3 candidates/audits bind the standing-policy digest.
  Bootstrap stays null: no safe production baseline is invented.
- Read-only bypass-actor visibility is insufficient, so no admin credential was
  added. Visible governance plus concrete exact-head premerge PR approval/current
  reviewer permission is checked before each POST. Hidden bypass custody, separate
  humans and historical/last-pusher authority remain explicit evidence limits.
- Verified predecessors are selected by historical successful attempt, preserving
  earlier success after a failed rerun. Every earlier own-run attempt receipt forms
  the retry chain; original provider IDs survive intervening preflight failure.
  Missing/deleted/ambiguous evidence blocks. Workflow run history gaps are rejected.
- Agent 2 preliminary review identified retry evidence loss, failed-rerun baseline
  loss and provider concurrency/terminal verification gaps. Corrections and executable
  cases now cover each: fresh provider history/configuration near both POSTs and
  terminal verified receipt, plus multi-attempt chain and historical-success lookup.
- Expanded suite: 156 passed, zero failed/skipped locally, including actual workflow
  candidate/archive/admit/deploy entry with real Git and synthetic providers; no
  per-release token. Two original new regressions failed before controller changes.
  A first Node24 Alpine attempt lacked Git for the new real-archive test; its failed
  infrastructure result is preserved. A local Node24+Git image run as the source
  owner passes in a network-disabled read-only container; an earlier wrong-user
  repository-object read failure is also retained. Tests were not skipped.
- Request intent receipts fence cancellation/lost response before a recorded ID;
  a different release can supersede a prior failure only with trusted proof that
  its provider step was unexecuted. Hidden accepted operations cannot be inferred
  absent from eventually consistent provider history.
- actionlint and diff checks passed. Fresh Render ancillary GETs confirm Blueprint
  autoSync=false, no environment groups and no service secret files. Runtime and
  frontend blockers remain; no provider/configuration mutation was performed.

Next: publish the exact corrected commit/PR, complete all current CI jobs and inspect
TRX/discovery skips, then obtain fresh Agent 2 review at that immutable SHA. Record
final publication evidence in the PR to avoid recursive evidence-only commits.

## Scripture-governed safety contract follow-up

Latest owner task requests Scripture as the Christian ethical foundation, a
risk-by-risk verifiable contract and independently enforced CI/CD safety; engineering
choices must not be presented as divine commands. Continue PR #106/its branch from
6a5950d; no merge, provider changes, deployment, secrets or restricted activation.

Investigation: root instructions, validation/stateful/workflow skills, system index,
operations/decision register, administrator security, ADR001-003, current workflows,
controller, runbooks and primary Scripture/NIST SSDF/OWASP/SLSA sources inspected.
Fresh read-only GitHub checks still show only deletion/non-fast-forward main rules,
production-release with no protection/branch policy and admin bypass=true. Provider
observations from the previous phase are carried-forward, not newly verified here.

Agent 2 independently identifies same-repository test/control editability, unknown
runtime DB/provider privilege separation, unauthenticated bootstrap evidence
references and source-vs-build provenance limitations. None demonstrates exploitation
or authorizes infrastructure changes. Plan: document all ten contract fields for each
risk; implement an assessment-only immutable-base change classifier plus executable
synthetic PostgreSQL restore/least-privilege negative fixtures; keep live independent
enforcement BLOCKED until actual outside-candidate controls/identities are verified.
The new restore fixture must use only its own disposable local container, never an
existing application database or production backup. Add it to the existing required
PostgreSQL CI job, without adding a competing release path. Run relevant tests/full
CI inventory and obtain fresh Agent 2 review at the resulting exact SHA. No Python.

Follow-up implementation: six-verse contextual mapping and eight ten-field risk
contracts now distinguish ethical authority, interpretation, engineering evidence,
standards and implementation. NIST1.1final/1.2draft, OWASP risk taxonomy and SLSA1.2
source/build requirements compared without compliance claims. Existing actual-schema
PostgreSQL recovery/restore test is reused; the new standalone shell fixture adds
15 real-role/ACL/FK/restore-conflict/truncated-archive checks in its own hardened local
container, also wired into the existing required PG job. No business/schema/release
controller/policy activation code changed. Classifier is assessment-only, comparing
immutable Git revisions with narrow regular presentation paths; external gate and
custodian identities remain unconfigured, not invented.

Validation checkpoint:171/171 release/classifier tests passed in isolated read-only
network-disabled Node24+Git;15/15 disposable PostgreSQL checks passed in repeated
local runs; bash syntax/actionlint/diff/link checks passed. Agent2 preliminary review
found no material defect; clarified path-only classification and data/schema-history
snapshot claim. An isolated archive of6a5950d with only a critical test changed to
test.skip returned exit0/155passed/1skipped, proving the current job-success signal
does not establish mandatory security-case execution. No mutant source was pushed.
Final exact-commit CI/artifact/skip accounting and fresh review will be recorded in
PR106, preserving the independent-enforcement FAIL and production UNKNOWN/BLOCKED.

## Finalization owner task from 64cbb5c (current execution scope)

Latest explicit owner instruction, 2026-10-16: complete code for every qualified
main merge → CI PASS → Render → API verification → Vercel → production verification,
without a standing activation switch, hand-authored release/bootstrap token,
workflow dispatch dependency or post-merge approval. Persistent provider credentials
and initial infrastructure setup remain necessary. This supersedes the earlier
standing-switch/null-bootstrap implementation choice, not recovery/runtime holds.
Independent verifier custody remains a disclosed limitation, not an assertion of
independent protection. Authorized: repository edits/tests/docs/commit/push on PR106
and fresh Agent2 review. Forbidden: merge, production deployment, live provider
settings/credentials changes, restricted operation activation.

EXPERIMENT RESULT before patch: two newly added regressions fail on the starting
implementation with DEPLOYMENT_DISABLED_STANDING_SWITCH and
QUALIFIED_RECOVERY_BOOTSTRAP_REQUIRED. ROOT CAUSE: explicit activation comparison
and mandatory source-authored bootstrap in validatePolicy. Removal is owner-directed;
no safety failure is converted into success. New policy version2 uses automatic
provider baseline discovery only when no verified predecessor exists; partial
retries retain their original immutable baseline. Missing/corrupt history still
blocks rather than resetting a floor or discarding receipts. All operational holds,
source/CI/attempt/provenance and current-main checks remain.

Fresh read-only provider/GitHub observations at 2026-10-16T17:33Z:
- Render target/repo/main/Dockerfile/health path match, native deployment off,
  hooks/command empty, not suspended. Live1251fb8; API200 and DB/Redis/key-store
  Healthy. Recovery=false, Bird=true, Yoco=test and Bird/Yoco keys present; all other
  checked financial/worker/demo/pilot/seed flags false. Recovery header absent.
  Runtime is incompatible with recovery-only automated release. No flags/keys changed.
- Vercel expected project/team/root/Node24, no custom build overrides; Git link
  absent. Current production target READY but no authoritative Git SHA. Public
  API/Tenant variables are sensitive and cannot be verified from returned values.
  No values, credentials or raw provider responses retained in repository.
- Repository Actions secrets and production-release secrets inventories are empty.
  Environment is unprotected, branch policy absent and admin bypass=true. Main
  governance remains missing required PR/CI controls. These are persistent setup
  blockers, not recurring release approval.
- Historical real main-push CI38911525858 passed for23f176d and automatically
  started workflow_run release38912818702, which stopped before provider mutation
  on the old authorization requirement. Its known legacy provider step was skipped.
  No new main push, production release or live provider mutation was initiated.

Next: complete code/regressions/docs; qualify legacy skipped release history without
silently ignoring potentially executed attempts; update explicit mandatory inventory
under this owner-authorized control change; run complete CI once final source is
published and obtain Agent2 review. Report initial infrastructure blockers and the
independent verifier/runtime privilege limitations separately from code readiness.

Finalization implementation checkpoint:206 local cases pass, including switch-free
first baseline, exact completed-event positives and six untrusted-event negatives,
legacy skipped provider step, lost baseline retry rejection, explicit final provider
revisions and all-skipped workflow handling. The skipped-workflow regression failed
before its production-job check and now passes. All17 required CI names remain;
no backend/frontend business behavior or runtime restriction changed. Stable mandatory
IDs preserved and13 new cases appended, with owner-directed activation/bootstrap
assertions replaced by real credential/context/provenance checks. No automatic
candidate discovery shrinks the normal mandatory inventory. The focused mutation
fixture uses171 exact reviewed identities to preserve the170-pass/one-skip negative.

Validation checkpoint:206/206 current cases pass with zero skips on Node26.6.0 and
isolated network-disabled/read-only Node24.21.0. The focused171-case custody fixture
passes its positive and all mutation denials, including170 passed/one skipped with
raw exit0 but blocked classification. Disposable PostgreSQL fixture:15 checks pass.
Actionlint and diff checks pass. Preliminary Agent2 code review found no additional
actionable defect; final exact-commit verdict and complete hosted CI remain pending.
No live provider/GitHub configuration or production data was modified.

Provider protocol follow-up: Render's current official create-deploy API documents
queued as a valid pending state. Two regressions failed before adding it to the
active-state set; ours now polls and foreign queued work blocks. Final local suite
is208/208 on Node26 and isolated Node24 with zero skips. This is an integration
correction in scope, not relaxed unknown-state handling. Source/inventory pins were
refreshed and original IDs preserved with15 append-only cases since64cbb5c. Current
runbook/evidence records distinguish initial setup, technical failures, independent
verifier limitation and actual untested production writes.

## Infrastructure integration assessment — 2026-10-16

Owner-authorized scope: authenticated read-only GitHub/Render/Vercel inspection,
repository corrections on PR106, isolated tests, documentation, commits and PR
updates. Production deployment, main merge, database access/mutation, live credential
or provider-setting changes and restricted capability activation remain unauthorized.
Starting qualified source:9581655e4ba0ab22c8578a7536439eda1bbf6fbc; dedicated feature
worktree initially clean. Detailed current configuration and proposed persistent
actions are recorded in docs/operations/production-infrastructure-plan.md.

FACT: authenticated GETs at18:05–18:08UTC confirm absent main PR/required-check rules,
unrestricted production-release environment, empty deployment-secret inventories,
Render normal profile with Bird=true/Yoco=test, Vercel link=null and missing baseline
Git source. Sensitive public Vercel variables return empty strings with decrypted=false;
their actual values are UNKNOWN, not proven incorrect. Both listed database export
inventories are empty. Follow-up18:13UTC recovery GET reports PITR AVAILABLE for the
application DB, startsAt2026-10-13T02:44:00Z; exercised restore readiness and runtime
role privileges remain UNKNOWN. Organization-ruleset GET404 explicitly requires the
unavailable admin:org scope.
No raw environment payload, secret value, export URL or database content was retained.

REPOSITORY EVIDENCE: BackgroundJobs=false controls ABP job execution, but normal host
startup separately registers periodic workers. Bird outbox delivery checks Bird=true,
not that ABP flag. Recovery also suspends business routes. Removing recovery checks
to match the live service would not preserve demonstrated holds; no such patch is
justified. Normal-operation profile qualification remains an explicit design/owner
decision, not a recurring release authorization.

GitSource can be supplied explicitly to Vercel's create API; its schema alone does not
prove the unlinked project has GitHub access. File upload avoids Git linking but
requires a new attested source/artifact path and real baseline provenance. Neither
was silently substituted for the currently implemented Git-source path. Candidate CI
cannot protect itself or credential custody; main-only environment matching alone
does not block pull_request_target or a malicious main workflow. Keep deploy secrets
absent until the external trust boundary is qualified.

Fresh isolated validation:208 mandatory cases pass without failures/skips; all retained
tampering negatives pass, including exit0/170passed/1skip→BLOCKED. Disposable PostgreSQL
recovery/privilege fixture15/15 passes. This is not production DB/provider evidence.

Next: publish the evidence and actionable configuration proposal, complete hosted CI
on the documentation commit, obtain
fresh Agent2 assessment, and report BLOCKED infrastructure separately from tested
controller behavior. No provider or production mutation is part of this task.
