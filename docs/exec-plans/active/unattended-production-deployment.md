# Unattended production deployment

> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

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
