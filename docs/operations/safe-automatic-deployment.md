# Automatic qualified production deployment

Current owner instruction: **merge → CI PASS → Render → API verification → Vercel
→ production verification**, without a release activation switch, human environment
approval, hand-authored bootstrap, per-release secret/token, manual dispatch or
additional approval after merging. Repository implementation supports this sequence.
Initial persistent infrastructure configuration is currently incomplete; no production
deployment or settings change was performed during preparation.

Normal PR review remains merge governance. Restricted financial, messaging, worker
and migration operations retain their separate boundaries. Deploying code never
authorizes those operations. The [safety contract](cicd-safety-contract.md) and
[independent verifier assessment](mandatory-security-verification.md) describe known
security limits. Candidate-controlled CI is not independent security enforcement.

## Automatic trigger and exact source

`ci.yml` runs on every push to main. `release.yml` consumes completed Continuous
integration runs on main through `workflow_run`, with no workflow-dispatch entry.
Its candidate job admits only a successful same-repository main push. Failed,
neutral, cancelled, unfinished, fork, PR and manually dispatched CI cannot deploy.
The controller independently fetches the exact CI run and attempt-specific jobs,
requires all 17 named jobs to succeed, checks current main, and binds the result to
immutable source bytes. Missing, skipped, duplicate or mixed-attempt jobs fail.

Global production-release concurrency has cancel-in-progress=false. A queued source
that becomes stale stops; its newer qualified main commit has its own automatic
release event. No provider POST is blindly retried. A candidate/receipt freezes
repository, source SHA, CI run/attempt, release run/attempt, policy digest, profile
and exact provider targets. Persistent Render/Vercel authentication is necessary,
but no credential grants per-release authorization.

GitHub's trigger is observable in existing history: main-push CI
[38911525858](https://github.com/aqua-lifestyle/aqua-lifestyle/actions/runs/38911525858)
completed successfully for `23f176d` at 14:04:22 UTC on 2026-10-16; release
[38912818702](https://github.com/aqua-lifestyle/aqua-lifestyle/actions/runs/38912818702)
started automatically at 14:04:24 UTC with event=workflow_run and the same source.
That historical controller stopped before provider deployment on its old authorization
requirement. This proves the platform trigger, not a deployment with this branch's
new code. Updated trigger/controller behavior is exercised with real Git and
synthetic provider clients; a new live main event cannot be induced without the
forbidden merge/main mutation. GitHub requires the consumer workflow on main; the
branch version takes effect after merge. See [GitHub workflow_run documentation](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#workflow_run).

## Baseline, deployment and final verification

Policy version 2 is checked in with baseline=automatic. There is no bootstrap object,
release floor, evidence-reference token, activation variable or expiry renewal.

1. Check merged-PR provenance/premerge review, required main governance and the
   main-only unattended credential environment. These are persistent merge/security
   configuration, not an approval requested after merge.
2. Read complete release history from run 1. Only a successful **production job**
   with an immutable verified audit can be a predecessor; an all-skipped workflow
   that GitHub concludes successfully is not a deployed baseline. Known legacy
   failures are superseded only when their exact provider step was positively skipped.
   Unknown, deleted, truncated or ambiguous execution remains a technical failure.
3. When no verified predecessor exists, automatically select the single live Render
   deployment and the current Vercel production target. Validate exact IDs, source
   SHAs, Git repository/project, READY/live state, current configuration and API
   recovery/dependency health. Missing provenance blocks; current SHA is not fabricated.
   The selected baseline is recorded before any provider mutation.
4. Preserve that original baseline and exact accepted provider IDs across partial
   retries. Do not rediscover an advanced Render SHA after a partial rollout. A
   verified predecessor remains authoritative; missing/expired receipts do not
   permit resetting to a newly discovered baseline.
5. Reject active foreign deployments, existing unreceipted candidate operations,
   migration changes/renames, incomplete source comparisons, source not descended
   from the API baseline, or drift. Recheck immediately before mutation.
6. POST Render with commitId equal to the qualified SHA. Poll the returned deployment
   ID; treat the documented queued state as pending and reject foreign queued work.
   Require live status, exact Git revision, recovery-only configuration, API
   health 200 and exact buildId, Healthy database/Redis/data-protection key store.
   Only then may Vercel be requested. [Render documents commitId and deployment states](https://api-docs.render.com/reference/create-deploy).
7. POST Vercel for the same repository/SHA and production target, with exact CI/release
   provenance metadata. Poll the returned ID; require READY, exact Git source,
   project identity, production alias assignment, verified public API/Tenant build
   values and HTTP 200 at the production URL. Recheck both providers and API readiness.
8. Record a verified audit containing renderSha, vercelSha, both deployment IDs,
   source/CI/release identities and originating attempts. Preserve partial IDs and
   request intent even on failure. Providers rebuild Git source; this is source and
   provider-state verification, not cryptographic attestation of running binaries.

Backend startup does not migrate/repair/seed Production. The release path never runs
EF Down, restores data, resumes services, enables workers, changes provider settings,
replays payments/outbox work or loosens Tenant/Area authorization. This path supports
code-only compatible changes; schema changes stop automatically rather than acquiring
migration authority.

## Persistent initial setup and current read-only findings

Fresh GET-only inspection on 2026-10-16 at 17:33 UTC:

| Surface | Observation | Persistent setup required |
| --- | --- | --- |
| GitHub main | Only deletion/non-fast-forward rules | Required PR review, latest-head approval and strict checks for all 17 jobs; qualify settings/bypass custody |
| production-release | No protection rules/branch policy; admin bypass=true | Main-only branch policy, no human/wait/custom approval gate, admin bypass disabled |
| Actions credentials | Repository and production-release secret inventories empty | Persistent RENDER_API_KEY and VERCEL_TOKEN in the intended environment; github.token supplies read-only GitHub access |
| Render integration | Correct existing repository/main/Docker context/API health path; native auto-deploy off; hooks and command empty; not suspended | Retain single orchestrator; qualify actual deploy permission using separately authorized non-production capability tests |
| Render runtime | Live `1251fb8`; API/DB/Redis/key store Healthy; recovery=false and recovery header absent; Bird=true; Yoco=test and Bird/Yoco keys present | Current runtime fails the unchanged recovery-only contract. A separately authorized infrastructure task must establish a compatible profile; this preparation does not change flags or remove keys |
| Other restrictions | Financial/weekly/monthly/jobs/demo/pilot/commissioning/seed flags false | Keep held; deployment never enables them |
| Vercel integration | Expected existing project/team/root/Node24; no build overrides; Git link absent | Restore Git access to the same repository, main production branch, native main deployment disabled |
| Vercel baseline | Current target READY without authoritative Git SHA | Establish an actual Git-provenance baseline; do not invent a source SHA for the existing deployment |
| Public frontend inputs | API/Tenant values sensitive/redacted and unverifiable | Make expected non-secret build inputs verifiable to the deployment identity; unknown values fail admission |

These blockers are disclosed initial configuration. There is no additional deploy
activation variable to turn on after setup, no release-specific bootstrap to author,
and no recurring release authorization. Current merge behavior is therefore explicit:
main CI runs and a successful trusted event starts CD automatically; **the candidate
currently stops at missing main PR/CI governance**, before production credentials or
provider mutation. After that is configured, environment/credential/runtime/Git-source
blockers still stop automatically until their checks genuinely succeed.

Recovery-only settings checked by the controller are Production, recovery=true,
and all of App__WeeklyCommissions__Enabled, App__EntryMonthlyObligations__Enabled,
App__FinancialOperations__Enabled, App__BackgroundJobs__Enabled,
AQGreenV2Demo__Enabled, AQGreenV2Pilot__Enabled,
App__AQGreenV2__Commissioning__Enabled, SEED_DEMO_DATA and Bird__Enabled=false.
Yoco mode/secrets/webhook secret and Bird key must be absent. This list is a read-only
contract, not an instruction or authorization to reconfigure the live service.

## Failure, retry and security limits

Failures emit redacted codes and GitHub job summaries. The production audit artifact
is named production-release-audit-RUN-ATTEMPT and retained 90 days; partial failures
retain accepted IDs, request intent and original attempts. API failure prevents
Vercel; later failures never report a verified pair. An ambiguous POST, lost receipt,
expired evidence or unknown history blocks further mutation. Cancellation does not
prove an accepted provider request stopped. Reconciliation/rollback uses separate
operational authority; it is exceptional failure recovery, not normal release approval.
Disable the Actions workflow or revoke persistent deploy capability through the
separate custodian if containment is needed; no application activation flag is used.

Independent security-test custody, immutable external audit retention, no-bypass
credential issuance, production database/provider IAM separation and authenticated
production backup/restore/rollback evidence remain unestablished. Static source
hashes and 208 mandatory case checks improve candidate CI but can be edited by that
candidate. The external verifier/broker design remains documented, not installed.
This task does not claim full security acceptance or production privilege isolation.

## Validation

```bash
node tools/deployment/run-security-tests.mjs --local
node tools/deployment/mandatory-security-probe.mjs
bash tools/deployment/disposable-recovery.sh
```

Run actionlint for workflow changes and complete hosted CI on the exact final commit,
including application PostgreSQL regressions and both container builds. Preserve
failed/cancelled attempts honestly. Local/synthetic execution does not prove actual
provider deploy permission or end-to-end production success. Record exact final
commit, CI run/attempt, fresh Agent 2 verdict and evidence limits in the final report.
