# PR #105 production readiness assessment and correction

The later unattended-deployment owner instruction supersedes this historical
document's per-release human authority/environment approval requirements. The
[current runbook](safe-automatic-deployment.md) defines standing policy and one-time
setup instead. Runtime/financial safety is retained; no provider transition or
production acceptance is granted by that instruction.

## CI-attempt correction checkpoint

The assessment below is historical evidence at `8185fb5`, not the readiness verdict
for the corrected branch. Subsequent explicit owner authority permits correction,
tests and updating the existing PR, while production remains fail closed. The two
new regressions failed against that source: successful attempt 2 displaced frozen
attempt 1 before Render and after Render/before Vercel. Both now pass within the
expanded 116-case isolated release-safety suite (zero skipped).

The version 2 manifest and owner authority bind repository, source SHA, CI run ID
**and attempt**, release run ID/attempt, environment/profile and provider targets.
All fresh qualifications must match that frozen source identity; attempt-specific
job inventories cannot mix attempts. Partial retry adoption requires an immutable
audit artifact from the same release run and exact original CI identity, with
explicitly renewed release-attempt authority. SHA-only inference is rejected and
partial provider IDs survive failure reporting. See the
[current release contract](safe-automatic-deployment.md) for the executable rules.

Local evidence: 116 tests pass on Node 24 in a network-disabled, read-only container;
actionlint and whitespace checks pass. Final commit, full CI/skip inspection and
fresh independent Agent 2 verdict are recorded in PR #105. Prior independent PASS
is superseded. No provider action was performed during the correction. Provider
and governance blockers below are carried forward observations, not freshly
verified runtime evidence. Deployment authorization and production operational
readiness remain **BLOCKED**; no production acceptance is claimed.

## Historical assessment at 8185fb5

Assessment: 16 October 2026, read-only observations completed at approximately
12:24 UTC. Source: `8185fb5742fd341b6465ccb435004bc26a046503`, PR
[#105](https://github.com/aqua-lifestyle/aqua-lifestyle/pull/105), still open.
Main remains `ac7dfe968ec13fd8c743504e4b531ab5afd2e5e7`.

**Code readiness: FAIL for exact CI-attempt binding (one verified finding).**
The mechanism remains IMPLEMENTED and TESTED preparation, with a new edge case
outside the original passing suite. **Deployment authorization: BLOCKED. Production
operational readiness: BLOCKED.** Earlier independent preparation PASS and green
CI are not production acceptance and do not resolve this new finding.

Owner authority for this assessment covers read-only inspection, isolated tests
and documentation. No implementation, workflow, provider, secret, integration,
service or database setting was changed. No merge, deployment, resume, email,
payment, financial operation or commissioning was performed. No Python used.

## Historical release-readiness matrix at 8185fb5

PASS means the scoped observation/control is established, not that production is
accepted. FAIL means a required control is missing or an executable counterexample
exists. BLOCKED means further evidence, owner authority or provider action is needed.

| Prerequisite | Status | Evidence / limitation | Exact resolution and authority |
| --- | --- | --- | --- |
| Existing code CI | PASS, scoped | Both exact-head workflows passed 17/17; fresh isolated rerun passed 73/73 without skips. The newly discovered attempt-drift case is not covered | Retain historical evidence; obtain new regressions, full final-head CI and independent review after correction. Green existing tests do not close the finding |
| Main review and CI enforcement | FAIL | Effective rules contain only deletion and non-fast-forward protection; required status checks/PR reviews are absent despite branch API reporting protected=true | Repository/org administrator must enable the minimum rules below under owner authorization; no bypass actors |
| Production release environment | FAIL | `production-release` absent. Four existing environments have no protection/branch rules and all permit administrator bypass | Administrator creates/protects the exact environment below; do not reuse an unprotected similarly named environment |
| Independent reviewer | BLOCKED | `Xolani-M` and `aqualifestyleclub` are the two listed admin collaborators; no repository teams. PR author/active CLI identity is `Xolani-M`; zero formal PR reviews | Owner designates an eligible different human, confirms account custody and acceptance responsibility, or grants an independent reviewer suitable access. Two accounts do not prove two people. Agent review is not GitHub approval |
| Render identity/build/stop controls | PASS, scoped | Correct main repository, root Docker context, existing API Dockerfile, empty pre-deploy/start overrides, auto-deploy off, not suspended. Existing Blueprint autoSync=false; no workspace env groups returned | Retain these values. No resume/configuration mutation is part of deployment. Custodian must freeze unrelated provider deployments/settings during an authorized release |
| Recovery baseline without losing current capabilities | BLOCKED; incompatible | Running build `1251fb8` has healthy dependencies and no recovery header; declared recovery=false, Bird=true, Yoco=test with credentials present. Recovery-only admission rejects it | No capability-preserving recovery qualification is possible under this contract. Owner must choose a separately authorized recovery transition that suspends capabilities, or authorize a separately implemented/tested normal-operation profile. Do not disable today's providers merely to pass admission |
| Runtime operational holds | PASS for declarations; BLOCKED for full effective-state proof | Financial, weekly, monthly, framework jobs, demo, pilot, D10 commissioning and demo-seed declarations are false. Public health does not expose every effective flag, worker registration or queue state | Authorized operations custodian supplies redacted effective-process/worker and startup evidence for the approved baseline; no database/queue mutation or provider test is authorized here |
| Vercel project/root | PASS, scoped | Existing expected project/team, frontend root `AqualLifeStyle/9.4.2/aqua-frontend`, Node24; no custom project build/install override | Retain exact existing identifiers; repository vercel.json specifies npm ci / npm run build |
| Vercel Git / production trigger | BLOCKED | Project link=null; production branch not verifiable. GitHub Vercel app check suite exists, which does not prove a connected project or deployment. Main trigger exclusion exists only in unmerged PR source | With separate integration authority, team/project owner and GitHub org/app custodian verify repository access, then connect the existing project to the exact repo/main only after main's native deployment exclusion is effective. Keep native main deployment disabled; verify no ignored-build override |
| Public frontend build inputs | BLOCKED | API URL and Tenant variable exist as sensitive/redacted production values; decrypt=true still returns no values | Owner verifies and makes only these public build values verifiable to the deployment identity, under separate configuration authority; never expose server secrets. Expected values below |
| Release commit/run/environment context | PASS, scoped | Commit, CI run ID, release run/attempt, expiry and profile checked; archive binds source and CI attempt; workflow and targets hardcode the repository/environment | Retain exact-context binding. Old release run/attempt or commit approvals fail. Environment/repository are implicit fixed context, not separate validated JSON fields; explicit fields are proposed clarity, not a demonstrated cross-environment bypass |
| CI attempt frozen throughout rollout | FAIL | Successful CI attempt2 can replace frozen attempt1 during API rollout and the frontend simulated POST still occurs | Code correction needed: bind owner authorization to ciRunAttempt, compare every requalification identity with the frozen candidate, stop before any further provider POST on drift, add regression and fresh review |
| Release activation / credential custody | BLOCKED | Environment absent; repository secret/variable listings empty. No eligible protected release authority or admitted production release exists | Custodian provisions least-privilege environment-only provider credentials and exact-release authority, enables AUTO_DEPLOY_ENABLED only under separate owner authority, then an independent eligible human reviews that exact deployment. No credentials requested or changed here |
| Single authoritative trigger | PASS for current observed holds; BLOCKED for commissioned integration | Render off/Blueprint sync false; Vercel disconnected; repository hooks listing empty. App check suites are not evidence that all app/provider hooks are absent | Custodian inventories provider hooks/manual automations and freezes competing deployments before reconnecting/enabling the qualified path. Repository hook count does not inventory private app/provider hooks |
| No automatic activation | PASS for current source/mocks; BLOCKED for production execution evidence | Guard checks nine explicit holds, provider isolation and effective recovery header. No migration, environment PATCH, resume, payment or Bird request in deploy logic | Retain guards. Current source still needs the attempt correction; any future normal-operation profile requires separate operational authority and tests, not removing recovery checks |
| Backup/schema/rollback | BLOCKED | Startup preflight only checks exact migration history; workflow blocks changed migration files and empty hooks are verified. No backup restoration or actual rollback executed in this task | Custodian independently verifies schema compatibility/drift, restorable backup and exact prior provider revisions, and supplies referenced evidence. Health/history matching is insufficient. Rollback requires separate authority |
| Provider delivery and monitoring acceptance | BLOCKED | Actual deployment, protected approval, rollback and notification receipt unexecuted; Render declares notify-on-fail only | After all earlier gates, separately authorize a bounded exact-release rehearsal/rollout, independently reconcile revisions/readiness/audit and alert receipt. Do not treat HTTP READY or CI as business-operation acceptance |

## Minimum GitHub governance

Owner-authorized repository/org administrator action is required; nothing below
was configured in this assessment.

For main, use an active ruleset targeting `refs/heads/main` (or verified default
branch), no exclusions/bypass actors, including administrators. Retain deletion and
force-push protection. Require a PR, at least one approval from an eligible
independent human, dismissal of stale approvals, approval after the latest push,
and resolved review conversations. Require the branch to be up to date and all
17 current CI job names in `tools/deployment/release-control.mjs:requiredJobs`,
with expected source **GitHub Actions app 15368**. Do not substitute the queued
Render/Vercel app checks for CI. No merge queue was qualified by this workflow;
do not enable one without implementing its CI event contract.

Designate independent owners/reviewers for workflows, deployment scripts/targets,
Docker/startup/recovery/worker guards and other operational safety code. No
CODEOWNERS file exists currently. Enforced code-owner review needs owner-designated
identities and a separately reviewed CODEOWNERS addition; do not invent reviewers.
Restrict permission to change the rules/environment or provider deployments to
the custodian. No repository setting prevents an authorized administrator from
later editing/removing the controls: restricted custody and audit are part of the
trust boundary, not a claim of immunity from a malicious administrator.

For **production-release**:

- Required eligible independent reviewer, `prevent_self_review=true`,
  `can_admins_bypass=false`; exactly one custom deployment **branch** policy, main,
  no tags/wildcards. The script verifies actual provider metadata after approval.
- Keep environment `AUTO_DEPLOY_ENABLED` absent/false until separately authorized.
  Store RENDER_API_KEY, VERCEL_TOKEN and PRODUCTION_RELEASE_AUTHORIZATION only
  in this protected environment, not repo/org-wide broadly inherited secrets.
- Approval evidence must reference exact source, successful push CI run and frozen
  attempt, release workflow run/attempt, approved baselines, profile, expiry within
  24 hours and independent schema/rollback/runtime/single-trigger evidence. The
  CI-attempt field/check was absent at this historical source and is implemented
  by the version 2 correction above; actual protected approval remains unexecuted.
- A new commit, release run, release attempt or expired window requires fresh
  authority and review. Failed/changed CI must stop subsequent provider mutations.
  Revoke unused authority; inspect already accepted operations before retry/rollback.

The repository is public, so the documented public-repository reviewer controls
apply; actual protection effectiveness must still be independently verified.
GitHub self-review prevention distinguishes accounts, not human ownership.
Primary contracts: [rulesets](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/available-rules-for-rulesets),
[environment protection](https://docs.github.com/en/actions/how-tos/deploy/configure-and-manage-deployments/manage-environments),
[deployment review](https://docs.github.com/en/actions/how-tos/deploy/configure-and-manage-deployments/review-deployments).

## Render capability decision and safety assessment

The observed live release is `1251fb82979e18be53d1d0e7f5c6fab2d0c50a58`,
deployment `dep-db8av3dg1s2s739m6sgg`, not PR #105's source. Production health
reports Healthy database/Redis/Data Protection and no recovery header. All eight
operational holds are declared false, but Bird is enabled and Yoco Test Mode keys
are present. The recovery-only contract requires recovery=true, Bird=false and
Yoco mode/key/webhook/Bird-key absence. Recovery also denies normal business routes
and provider processing. It therefore cannot preserve current capabilities.

This is a genuine owner decision, not drift to repair automatically. The
capability-preserving option needs an authorized **normal-operation deployment
profile** with precisely approved Bird delivery boundary/outbox behavior, Yoco
Test Mode safeguards and all financial/job/D10 holds, then implementation, isolated
validation and independent review. This task neither approves nor implements it.
The alternative recovery transition needs explicit downtime/capability-suspension
authority. No flag/key was removed or changed here.

For the current recovery code: Production/recovery startup sets SkipDbSeed;
DatabaseReleasePreflight reads migration history and throws on mismatch without
applying migrations. Recovery latches before bootstrap, disables framework jobs,
does not register provider/financial workers, forces Bird options disabled, and
worker bodies also return in recovery. The release preflight rejects any hold
that is true/missing, any provider setting, suspended service or nonempty
pre-deploy/start override. These source/mocked checks do not prove every current
running-process setting, backup recoverability or a future arbitrary code change.

## Exact Vercel prerequisites

Use existing project `prj_6qL1e43KhsWrRshY5GxdmGl3HGMz` / team
`team_Hc3BWfhTLfwLzwbgbtaasBUY`, repository `aqua-lifestyle/aqua-lifestyle`
(GitHub repository ID 1276688422), production branch main, frontend root
`AqualLifeStyle/9.4.2/aqua-frontend`, Node24. No alternate project or hook.

Expected production public variables, with no branch override or duplicate:

| Variable | Required value |
| --- | --- |
| NEXT_PUBLIC_ABP_API_URL | https://aqualifestyle-api.onrender.com |
| NEXT_PUBLIC_DEFAULT_TENANT_NAME | Default |

Vercel team/project owner must verify GitHub app repository authorization and
connect the existing project only after native main deployment is disabled in
the effective main configuration (`git.deploymentEnabled.main=false`). Keep the
existing npm ci/npm run build contract and no ignored-build override. Do not
reconnect first: current main has not received this exclusion. The deployment
token needs only the appropriate existing team/project read/deploy authority;
successful provider source retrieval/deployment remains unverified. Public values
may need separately authorized re-entry as non-sensitive public configuration to
permit validation. Their actual current values are UNKNOWN, not demonstrated wrong.

## Verified defect: CI attempt changes during an admitted rollout

REPOSITORY EVIDENCE: `validateAuthority` binds ciRunId but not ciRunAttempt.
Candidate entry binds the manifest to the current CI attempt. Later `deploy`
checks at release-control.mjs lines 265 and 278 call `qualify` but discard its
returned identity. A same-run successful rerun can therefore substitute a different
CI attempt after initial admission. Frozen qualification/audit still describes
attempt1 while the later check accepts attempt2.

EXPERIMENT RESULT (Agent 2 independently found, Agent 1 reran): existing synthetic
deployment fixture, no network, same commit/run ID:

| Scenario | Requalifications | Simulated provider POSTs | Observed outcome |
| --- | --- | --- | --- |
| Successful CI rerun | attempt1 success, then attempt2 success | Render, Vercel | verified (unexpected for frozen-attempt authority) |
| Failed-rerun control | attempt1 success, then attempt2 failure | Render only | CI_NOT_SUCCESSFUL |

This is **one MEDIUM code-level provenance/authorization-evidence gap**, not proof
of failed-CI bypass, another commit deployment, cross-environment escape or actual
production exposure. Earlier approval still cannot authorize a different release
run/attempt; the defect is requalification drift inside the admitted rollout.

Required code change: compare each fresh qualification's repository, SHA, CI run
ID and attempt to the frozen candidate before any new provider POST. Include and
validate ciRunAttempt in exact owner authority/audit; bind CI attempt in adoption
metadata as appropriate so a retry cannot reinterpret provenance. Add regressions
for drift before Render and between Render/Vercel, preserving failed-CI rejection
and no further POST on mismatch. Document renewal/reconciliation rather than blind
retry. Explicit repository/environment authority fields are sensible schema clarity
but currently fixed workflow/source context already binds them; no second bypass
finding is claimed. Implementation changes are outside this read-only task.

The historical probe below requires a separate checkout of exactly `8185fb5`;
it depends on that version's fixture and is not a command for the corrected branch.
Current executable regressions live in `tools/deployment/release-control.test.mjs`.
The historical probe loads
only existing synthetic fixture definitions, disables test registration, replaces
the mock CI responses and asserts the required safe behavior. The final assertion
**fails on 8185fb5**, after printing both observations; all POSTs are mock records.

```sh
node --input-type=module <<'NODE'
import fs from 'node:fs';
import assert from 'node:assert/strict';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
let source = fs.readFileSync('tools/deployment/release-control.test.mjs', 'utf8')
  .replace("import { test } from 'node:test';", 'const test = () => {};')
  .replace("'./release-control.mjs'",
    JSON.stringify(pathToFileURL(resolve('tools/deployment/release-control.mjs')).href));
source += '\nexport { fixture };';
const { fixture } = await import('data:text/javascript;base64,' +
  Buffer.from(source).toString('base64'));
const observations = [];
for (const secondConclusion of ['success', 'failure']) {
  const f = fixture();
  const original = f.api.gh;
  let calls = 0;
  f.api.gh = async route => {
    const result = await original(route);
    if (route !== '/actions/runs/123') return result;
    calls++;
    return { ...result, run_attempt: calls === 1 ? 1 : 2,
      conclusion: calls === 1 ? 'success' : secondConclusion };
  };
  let outcome;
  try { await f.execute(); outcome = f.reports.at(-1).phase; }
  catch (error) { outcome = error.message; }
  observations.push({ secondConclusion, outcome,
    simulatedPosts: f.posts.map(post => post.provider) });
}
console.log(JSON.stringify(observations));
assert.equal(observations[1].outcome, 'CI_NOT_SUCCESSFUL');
assert.equal(observations[0].simulatedPosts.includes('vercel'), false,
  'CI attempt drift must block the subsequent frontend deployment');
NODE
```

## Historical evidence and recommended commissioning sequence

Carried-forward exact-head CI: [push 17/17](https://github.com/aqua-lifestyle/aqua-lifestyle/actions/runs/38894471291),
[PR 17/17](https://github.com/aqua-lifestyle/aqua-lifestyle/actions/runs/38894566039),
1364 application passed; web95 passed/84 default opt-in skips (50 unrelated opt-in
cases unexecuted after dedicated coverage); PostgreSQL24+40+14 passed; frontend556
passed. These runs were queried again, not rerun in this documentation task.
Fresh Node24 network-disabled execution: 73 tests passed, zero failed/skipped.
The additional attempt-drift probe exposes the missing case despite those passes.
Existing dependency advisories remain unchanged.

1. Keep all activation/integration/runtime holds unchanged. Under separate code
   authority, correct the attempt binding, add failing-before/passing-after tests,
   run final-head CI and obtain independent review. Do not carry old approval to
   a changed commit. No merge is authorized by this assessment.
2. Owner chooses normal-capability preservation versus explicit recovery suspension.
   Qualify the selected profile independently before any production transition.
3. Administrator designates genuinely independent reviewer custody; configures main
   and protected release environment while activation remains off. Verify controls
   read-only, including no administrator bypass and source-bound required CI.
4. After separate merge authority, integrate the reviewed correction/mechanism.
   Verify native production trigger exclusion on actual main. Then, under separate
   integration/configuration authority, establish Vercel's exact Git/build inputs.
   Retain Render off/Blueprint auto-sync off; inventory/freeze competing operations.
5. Custodian independently verifies effective runtime holds, source/schema/backup,
   approved baseline revisions, rollback procedure and owned alert delivery. Supply
   evidence references, not invented true booleans or sensitive records.
6. Only then obtain separate exact release/attempt deployment authorization and
   independent environment approval. Reconcile actual provider commits, recovery or
   approved profile, readiness, production alias and retained audit. Test rollback
   only under its own bounded authority. Deployment remains distinct from any
   financial/provider/worker/D10 activation or business-operation acceptance.

Independent Agent 2 review confirmed the reproduced attempt gap and the limits of
account/environment binding. Production acceptance remains BLOCKED; there has been
no actual deployment/rollback/notification acceptance by either agent.
