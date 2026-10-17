# Mandatory CI/CD security verification — PR 106

> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

## Goal and authority

Owner task: prevent skipped/deleted/renamed/weakened mandatory security tests from
qualifying automated deployment; retain merge → CI → automatic deployment without
per-release approval. Work only on `feature/unattended-production-deployment`, PR
106, starting at `1b662546de119edd4dd78514bd655a639444fdc5`. No merge, production
operation, credential change, provider configuration or restricted activation.
References: repository AGENTS.md, docs/development/validation.md,
docs/operations/cicd-safety-contract.md (especially R6), release.yml and ci.yml.

## Confirmed decisions / assumptions

The owner requires independent custody; a same-candidate verifier is insufficient.
No eligible external verifier repository, signing authority, credential broker or
organization enforcement has been designated. Do not invent one or activate it.
Repository administration permission is not authorization to change credentials or
production settings. Ordinary source review remains distinct from release approval.

## Current state and investigation

OBSERVATION: existing 171 security tests pass locally. The release-safety CI job
uses Node exit status; release-control validates job names/statuses, not individual
security case results. Candidate discovery/inventory can shrink with deletions.
Hypothesis: Node treats an explicit skip as a successful test process; verify with
a throwaway source archive, leaving the PR worktree unchanged.

Read-only GitHub observation, 2026-10-16: active repository ruleset 19085283 only
contains deletion and non_fast_forward, with no bypass actors. Classic main branch
protection returns 404. production-release environment has no protection rules or
branch policy. Account reports repository admin permission. Organization rulesets
endpoint is unavailable (404 / missing admin:org scope); no organization authority
is inferred. No settings were changed. These conditions cannot establish an
external required verifier or prevent a candidate workflow reading environment
credentials. Current controller already blocks for missing governance/bootstrap;
that controller is itself candidate-controlled.

## Plan and evidence

1. Reproduce exact 170-pass/one-skip success and trace qualification gap.
2. Add a stable, explicit security-case inventory and strict local execution/result
   classification, with exact source/run/attempt binding and immutable artifact output.
3. Exercise missing/skipped/deleted/renamed/forged/misattributed results and candidate
   edits to inventory/gate using a separately retained verifier/policy fixture.
4. Specify independent policy/test custody, trusted execution/evidence and the
   machine-enforced credential boundary. Do not call fixtures live enforcement.
5. Run security suite, applicable complete hosted CI including PostgreSQL, inspect
   final diff, commit on the existing branch, and request fresh Agent 2 review of
   the final commit. Report independent enforcement BLOCKED unless actually proven.

## Open questions and next action

An accountable security custodian must provision external enforcement and remove
ordinary workflow access to deployment credentials through a separately authorized
change. No per-release human approval is proposed. Next: reproduce and implement
local hardening, then assess the required external handoff with negative evidence.

## Implementation and local evidence checkpoint

EXPERIMENT RESULT: the exact starting-source throwaway skip probe returned exit 0,
170 passed, one skipped, zero failed. ROOT CAUSE confirmed: Node permits skips;
job-success classification does not prove individual execution. BRANCH-EXPOSED.

IMPLEMENTED: explicit 193-case inventory with stable IDs, source-byte pins, strict
report classifier, CI SHA/run/attempt binding, immutable artifact upload, signature
consumer primitives and adversarial custody fixture. Two duplicated parameterized
baseline display names were made unique; assertions were preserved. Same-candidate
control files and inventory remain editable and are not independent enforcement.

TESTED locally: 193/193, zero failed/skipped on Node 26.6.0 and isolated read-only,
network-disabled Node 24.21.0; 171-case positive and skip/delete/rename probes;
removed/replaced workflows/gate, modified inventory, forged/missing evidence and
wrong commit/attempt all denied within the retained-policy fixture. Actionlint and
diff check passed. Disposable PostgreSQL restore/role-denial fixture: 15 passed.
Initial development probes exposed an empty NODE_TEST_CONTEXT recursion issue and
a syntactically invalid delete mutant; corrected and rerun without weakening tests.
Versioned local probe output: docs/verification/mandatory-security-20261016/.

Required external authority remains unprovisioned. The existing CD credential path
is not independently protected and has not been represented as fixed. The custody
contract specifies required workflow/App producer, separately protected policy and
signed exact-attempt evidence, isolated unprivileged execution and a broker or
protected external deployment identity denying candidate access to production keys.
No per-release approval is proposed. No GitHub/provider/credential setting changed.

Next: publish this local-hardening commit only on PR 106's branch; run final-head
complete CI (including application PostgreSQL); request fresh Agent 2 review on that
commit; report overall BLOCKED regardless of local/hosted tests until independent
credential and verification custody is established and adversarially demonstrated.
