> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

# Owner-controlled GitHub governance alignment — 2026-10-17

## Goal and authority

Owner instruction, “CLOSE THE REMAINING GITHUB GOVERNANCE CONFLICT”: accept the
installed 17-check ruleset #25297647 without mandatory human PR reviews or last-push
approval; preserve exact source/CI/run-attempt/App checks; reject weakened or
bypassable checks; prepare but do not apply the environment administrator-bypass
change. Update existing PR #106 and obtain fresh Agent 2 technical review. This
explicit instruction supersedes earlier human-review proposals; this worklog does
not create authority. Scripture supplies stewardship, prudence and examination
principles; code, tests and GitHub observations supply engineering evidence, not
a guarantee of technical immunity.

## Git context and current state

- Existing branch: `feature/unattended-production-deployment`, PR #106.
- Re-fetched clean head before edits: `cd4cd6dac1c694a8f1d582f48b3b8c14c8dfd99a`.
- IMPLEMENTED/TESTED locally; final exact SHA, CI run and Agent 2 verdict are recorded
  in the PR update for this change. No merge or deployment is authorized.
- No application, workflow, secret, provider or live environment setting changed.

## Completed work and evidence

Authenticated GETs of effective main rules and ruleset #25297647 show active branch
rules scoped to `refs/heads/main`, no exclusions, no bypass actors, deletion and
non-fast-forward protection, and17 strict named checks bound to GitHub Actions
App 15368. No human-review rule is installed. The older #19085283 deletion/history
rules remain effective. The checked-in
[fixture](../../../tools/deployment/fixtures/installed-main-governance.json) retains
these non-secret response fields; tests consume both effective rules and concrete
ruleset detail. It is a dated observation, not persistent independent authority.

Reproduction using the baseline controller and actual readbacks rejects installed
governance with `MAIN_PR_AND_CI_GOVERNANCE_REQUIRED`. The revised validator accepts
that governance. It verifies the source, active state, main scope, empty bypass
actors, deletion/history protections and all 17 strict App-bound checks in both
responses. Missing or unreadable bypass evidence fails closed. The controller
checks merged-PR source/head/base/author/time and repeats the source observation;
it no longer requests approvals or collaborator-review permissions. Exact CI
commit/run/current attempt, source bytes, requalification before provider mutations
and partial-rollout/retry controls remain in place.

Focused local controller suite: 190 passed,0 failed,0 skipped. Full local mandatory
Node classifier: 232 executed once and passed,0 skips. This is isolated component
and synthetic-provider evidence against the edited tree, not live release evidence.
The inventory explicitly revises AQUA-SEC-0126–0135 from the earlier candidate-review
predicates to merged-PR provenance predicates,0136 to provenance drift, and0140 to
bypassable-check rejection, under the owner's changed admission contract. These
names/assertions are not claimed unchanged. New cases 0210–0232 cover installed
policy admission and fail-closed governance/scope checks; source pins also include
the installed fixture. All other prior 209-case identities remain unchanged.

The disposable adversarial probe retains policy/verifier outside its synthetic
candidate. It rejects the exact Render-failure case with 170 passes/one skip despite
process exit0; deletion, rename, workflow/verifier removal or replacement, forged
or missing report, wrong commit/attempt and forged signature also fail. This proves
local classification and a simulated custody boundary, not independent GitHub
custody. Its171-case mutation fixture is a selected subset, not the full 232-case run.

Preliminary fresh Agent 2 review independently ran 187 controller tests before three
additional branch-scope negatives were added, with no material findings. Exact
final-commit review is requested separately; see the PR update for its final verdict.

## Prepared environment action — unapplied

Authoritative GET shows `production-release.can_admins_bypass=true`, only the
branch-policy protection rule, custom branch policies enabled and protected-branch
mode disabled. Branch-policy GET has exactly `main`, type `branch`, ID63358875.
No reviewers, wait timer or custom protection rule exists.

[Exact supported UI change and readback procedure](../../operations/safe-automatic-deployment.md#owner-controlled-github-governance-and-outstanding-setting):
deselect **Allow administrators to bypass configured protection rules** and save,
leaving the existing main policy and all other controls intact. Expected readback:
`can_admins_bypass=false`, same sole main branch policy, no approval or wait rules.
Removing the override does not add human deployment approval; administrator
membership is a separate control. No documented REST/GraphQL update field for this
toggle was found. No invented API field or undocumented mutation was used.

Actual infrastructure validation still rejects the true setting. With only that
field substituted to false in memory, the administrator-readback infrastructure
fixture passes. This is not a claim that the live setting changed or the normal
runner can supply administrator-only ruleset evidence.

## Remaining boundaries and next action

**BLOCKED:** GitHub documents that `bypass_actors` is returned only with ruleset
write access. An unauthenticated public GET200 of25297647 omits the property,
confirming the visibility distinction; a normal release-token run was not induced.
The current read-only workflow has no qualified way to obtain that proof and
fails `MAIN_RULESET_CUSTODY_UNVERIFIABLE`. Do not add an administration credential
to the runner. A separately controlled observation/enforcement route remains a
custodian dependency; it was not designed or provisioned by this narrow change.
Disabling environment admin bypass alone therefore cannot make CD operational.

The same candidate can edit the controller, inventory and workflows. Shared GitHub
Actions App binding cannot establish independent workflow identity. Independently
protected verification and production credential custody remain unresolved; this
source correction does not resolve either. Production secrets, provider readiness,
PostgreSQL authority and runtime isolation retain their prior separate blockers.
No production configuration, credential, data or deployment was changed.

Next owner/custodian action: authorize/apply the single prepared UI setting if
wanted, and resolve the disclosed bypass-evidence custody dependency before
claiming unattended deployment operational. No recurring release approval or
activation switch is proposed.
