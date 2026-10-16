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
