> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

# Safe automatic deployment

Historical workstream: the later owner instruction for unattended deployment
supersedes per-release human authorization. See the
[unattended implementation worklog](unattended-production-deployment.md) and
[current contract](../../operations/safe-automatic-deployment.md). Runtime and
financial restrictions remain; earlier review does not accept the new implementation.

## Goal and authority

Owner task: investigate, implement, test, independently review and open a PR for
qualified main-to-production automation. No production deployment, service resume,
merge, credential/configuration mutation or operational activation is authorized.
No Python. Baseline is verified main `ac7dfe968ec13fd8c743504e4b531ab5afd2e5e7`
(merged PR #104). Branch: `feature/safe-automatic-deployment`.

References: repository AGENTS; deployment and validation guides; system documents
06/07; provider-test-operational-transition worklog. Historic recovery statements
are not substituted for current provider observations or new owner authority.

## Investigation evidence

- GitHub main CI run 38889852541 succeeded at the baseline. The only workflows
  validate code and publish containers on version tags; neither deploys main.
- Existing GitHub environments have no protection rules or branch restrictions;
  repository Actions secrets/variables listings are empty for this credential.
- Read-only Render CLI/API: existing API tracks main in the correct repository,
  native Docker build at root, expected Dockerfile, empty pre-deploy command,
  autoDeployTrigger off, not suspended. Latest live deployment is an API-triggered
  `1251fb82979e18be53d1d0e7f5c6fab2d0c50a58`, not the baseline merge.
- Render declares Production, recovery false, Bird true, Yoco test with credentials
  present, and explicit financial/weekly/monthly/background/demo/pilot/D10/seed
  holds false. Values/secrets are not copied into Git. These are current provider
  configuration observations, not comprehensive proof of effective process state.
- Read-only Vercel API: existing project has no Git link, correct frontend root,
  Node 24, no custom build/install command. Latest deployments are CLI production
  deployments; latest target has custom metadata declaring frontend source
  `6c81fb91afbac0cedb63092cc736e857eb544d82` without authoritative Git source.
  Its production branch cannot be verified while Git is disconnected. No evidence
  establishes a failing webhook or ignored-build failure as the cause.
- Vercel production public API/Tenant values are sensitive/redacted even through
  the available authenticated environment reads. Their presence is verified, their
  values are UNKNOWN. The pipeline explicitly rejects this unverifiable state.
- Live API health returned 200, Healthy database/Redis/Data Protection, Production,
  build `1251fb82979e18be53d1d0e7f5c6fab2d0c50a58`, no recovery header. No business
  or messaging/payment request was made. The main ruleset protects deletion and
  non-fast-forward only; required PR review/CI protection is an owner prerequisite.

ROOT CAUSE (repository and live configuration): no qualified main release path,
Render explicitly off, Vercel Git disconnected. Integration permissions and
historical reasons for disabling them remain UNKNOWN. Render declaration matches
its off/root/build/pre-deploy settings; Vercel source declarations do not establish
a live Git integration. No provider settings were changed.

## Implemented design and safety boundary

One orchestration after completed successful push CI on the exact current main
commit. Publish an immutable source candidate and qualification manifest, then
admit deployment through a separately protected production environment. Native
provider builds use the exact commit, Render before Vercel, with outcome/revision
and readiness reconciliation. A source archive is qualification/source evidence,
not proof that a provider runs a particular container digest.

Native independent Git auto-deploys cannot establish cross-provider sequencing or
the recovery/authorization gate. Do not enable Render auto-deploy or Blueprint
synchronization. Disable main's native Vercel trigger before reconnecting its Git
access for exact-source API deployment. Version-tag publication must not bypass
qualification and never serves as production authorization.

Initial automated API profile is recovery-only: existing active service, no
provider keys/mode, Bird off, recovery on, all operational holds retained, no
migrations or configuration edits. Current live settings do not meet that profile.
Activation therefore remains BLOCKED unless the owner separately authorizes and
establishes a matching safe baseline; a normal-operation profile needs separate
qualification. Never transition today's running service merely to satisfy a gate.
The baseline merge is explicitly excluded from deployment.

## Implementation and validation checkpoint

- New single `workflow_run` consumer qualifies all 17 current CI job identities and
  exact current main commit, archives deterministic source and records SHA-256/
  producing artifact identity. Downloaded source is independently compared with Git.
- Protected exact-attempt/time-bounded owner evidence, actual environment rules,
  existing targets, baseline, safe profile and public build inputs gate all provider
  calls. Main's native Vercel trigger is explicitly off. Version-tag image publishing
  now also requires qualified exact-main CI and remains publication-only.
- Sequential exact-commit Render/Vercel operations retain IDs and revalidate source,
  expiry, recovery/health, provider source and production target. Failed/partial or
  ambiguous operations never cause resume, migration, rollback or blind POST retry.
- Before implementation, all three workflow regression checks failed against the
  original missing release path/native main trigger/unqualified tag publication.
- 73 isolated Node tests passed, zero failed/skipped, on local Node and Node 24 in
  a read-only, network-disabled container. Cases include source/authority tampering,
  failed/missing/skipped CI, runtime holds, target drift, stale main, lost POST,
  partial failure, authority expiry, pagination and retry adoption. These are mock/
  contract tests, not actual provider deployment or rollback execution.
- actionlint 1.7.12 passed for all workflows (optional shellcheck/pyflakes disabled;
  no Python execution). `git diff --check` passed. No application/runtime business
  code, image Dockerfile, migration, package dependency or provider setting changed.
- Read-only snapshots of actual live Render/Vercel objects were supplied to the
  admission validators: Render rejected `ACTIVE_RECOVERY_BASELINE_REQUIRED` and
  Vercel rejected `VERCEL_TARGET_OR_GIT_CONFIGURATION_DRIFT`, as intended. Raw private
  provider JSON was deleted after safe observation capture; no configuration or
  secret response is tracked.
- Agent 2's preliminary independent source review found no remaining material
  verified defect after archive binding, public-build validation and expiry checks.
  This does not substitute for its exact-commit final review or formal GitHub review.

Status: IMPLEMENTED and locally TESTED; production activation BLOCKED. Remaining:
commit/push and focused PR, complete final-head CI/skip inspection, fresh independent
Agent 2 review at that SHA. Record publication results in the PR so final CI/review
can identify an immutable commit without recursive evidence-only commits. The
[current runbook](../../operations/safe-automatic-deployment.md) contains exact owner
prerequisites, effective-runtime evidence limits, safe rollback/recovery procedure
and observability custody. No merge, production deployment or activation occurred.

## Read-only production-readiness follow-up, 16 October 2026

The owner authorized read-only inspection, isolated testing and documentation at
`8185fb5742fd341b6465ccb435004bc26a046503`, not implementation/provider changes.
PR #105 remains open; main remains ac7dfe9. Full publication CI and independent
preparation review are recorded in the PR. A new independent adversarial probe
found one CI-attempt binding gap inside an already admitted rollout: later qualify
results are discarded, so successful attempt2 can replace frozen attempt1 while
Vercel proceeds. A failed attempt2 correctly blocks Vercel. Both agents reproduced
this using existing mocks, with no provider POST. The original 73-case suite still
passes; it does not cover this edge. Code readiness for frozen-attempt acceptance
is FAIL until separately authorized correction/regressions/review. No product code
was changed. Earlier preparation PASS is not production acceptance or closure of
this finding.

Fresh provider/governance observations and exact owner actions are in the
[production-readiness matrix](../../operations/pr105-production-readiness.md).
Recovery cannot preserve today's normal Bird/Yoco/business capabilities. Main lacks
required PR/CI rules; release environment is absent; two admin identities do not
establish independent humans; Vercel remains disconnected/public values redacted.
Render auto-deploy remains off and Blueprint auto-sync false. Deployment authority
and production operational readiness remain BLOCKED. No credential or configuration
change, merge, deployment, resume, payment, email or restricted operation occurred.

## Authorized CI-attempt binding correction

Owner task: correct the independently confirmed gap on the existing PR #105 branch;
commit, push and update that PR, with complete CI and fresh Agent 2 review. No
production/provider operation, merge or activation is authorized. No Python used.

- Before changing the implementation, two new regressions failed with missing
  expected rejection: successful CI attempt 2 replaced frozen attempt 1 before
  Render and after Render/before Vercel. The original deploy discarded returned
  requalification identities and authority omitted CI attempt. Retry inference from
  source SHA also lacked sufficient attempt provenance.
- Version 2 candidates and authority now bind the complete source/CI/release/target
  identity. Latest-run reads bracket attempt-specific job inventories; every fresh
  qualification must equal the frozen candidate before further provider mutation.
- Retries require renewed current-attempt authority plus an immutable prior audit
  receipt from the same release run and CI attempt. Exact provider IDs/original
  requesting attempts survive partial failures. Contradictory, unavailable, legacy,
  completed or ambiguous evidence fails closed; matching SHA alone cannot authorize
  adoption. Rerun all release jobs to produce a current-attempt candidate.
- Expanded release-safety suite: 116 passed, zero failed/skipped, including both
  formerly failing cases, failed reruns, changed identity, stale approval, partial
  Render completion, concurrent releases, lost IDs and ambiguous responses. Node 24
  also passed in a read-only, network-disabled container. These are mocks/contract
  tests, not provider deployment, recovery or rollback execution.
- actionlint 1.7.12 passed all workflows (optional shellcheck/pyflakes disabled;
  no Python execution). Full final-head CI/skip evidence and fresh exact-commit
  Agent 2 review will be recorded in PR #105 after publication. Earlier PASS is
  superseded; preliminary source review found no material defect.

Status: correction IMPLEMENTED and locally TESTED. Production activation remains
BLOCKED by the carried-forward governance, recovery-baseline and Vercel prerequisites
in the current runbook/readiness matrix. No settings or production state changed.
