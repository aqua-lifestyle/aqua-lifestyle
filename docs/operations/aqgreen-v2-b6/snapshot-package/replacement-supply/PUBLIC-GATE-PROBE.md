> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

# Bounded public environment approval experiment — record version 2

**TEST ONLY. NOT BASELINE ACCEPTANCE OR QUALIFICATION AUTHORITY.**

Owner instruction: “AGENT 2 — TEST THE PUBLIC GITHUB APPROVAL OPTION”, 2026-10-21.
This authorizes harmless public environment testing and assessing a minimal
cross-repository approval handoff. The existing B6 qualification contract and
private workflow remain unchanged pending an independently accepted amendment.

The disposable `test/b6-public-approval-gate-20261021` branch overrides the already
registered `.github/workflows/ci.yml` with a manual-only, permissions-empty job
that prints synthetic public metadata after native environment approval. It has
no checkout, dependency acquisition, restricted input, credential reference,
artifact upload or qualification command. It must not be merged as application CI.

Environment `b6-public-approval-probe` is isolated from deployment/production
environments. Required reviewers, prevent-self-review and no-admin-bypass are
requested with an exact test-branch restriction. Xolani-M is a negative-control
reviewer for the self-review experiment. The existing second organization admin
account may be used solely as a negative-control reviewer for the non-reviewer
admin experiment. Neither account assignment designates an independent baseline
acceptor, and distinct accounts alone do not prove distinct independent people.

Xolani-M authored PR113 and cannot independently accept that baseline. No genuinely
independent reviewer has yet been designated. Positive independent approval and
any operational handoff must not be inferred from configuration or rejection tests.

## Proposed minimal handoff, not adopted

Keep the public gate metadata-only and private qualification/evidence private.
The public workflow needs no access credential for the private repository. A
private non-privileged preflight can query GitHub's public REST run/attempt,
environment and approval-history endpoints over HTTPS. Approval is not transferred
by a green badge, matching environment name or copied status string.

Before any privileged operation, require a live GitHub `approved` review for the
exact public environment ID, run ID/attempt, immutable workflow commit/blob hash,
and a reviewer identity designated by the owner and independent of PR113 author,
implementation and execution. Retain the original response privately. Check the
actual gate job/attempt completed successfully and reject bypass, rejection,
cancellation, missing/ambiguous evidence, stale attempts and changed protection.

The review comment must bind the public run/attempt and a SHA256 commitment to a
canonical private permit containing exact B6 source commit, baseline manifest hash,
public/private workflow identities, private repository/run ID/attempt, owner
authorization reference and a fresh nonce. Only the commitment/public metadata
need be public. The private verifier recomputes the commitment from its actual run
and approved inputs. A rerun needs fresh attempt-bound authorization and approval.

Because the private run ID must already exist when independently approved, the
minimal operational change would be a bounded, non-privileged approval-wait/check
in the existing private preflight, before supply consumption or sudo. No public
credential, dispatch authority or private evidence export is required. The existing
private environment check cannot simply be deleted: replacing it with this exact
external approval authority needs an explicitly and independently accepted,
versioned contract amendment. Gate-Q/A, isolation, trust, custody, owner run
authorization and subsequent independent evidence acceptance remain mandatory.

## Actual experiment results

Public repository visibility was read back as public; evidence repository
visibility remained private. The original private qualification workflow and its
contract were not edited. Its actual workflow-run count remained zero.

GitHub accepted public environment creation with HTTP200 and returned:

- environment ID `24924994010`, name `b6-public-approval-probe`;
- required-reviewer protection, `prevent_self_review=true`;
- `can_admins_bypass=false`;
- an exact test-branch policy and zero environment secrets.

Both dispatched harmless tests used workflow commit
`f7af1b7549925c1a4a70d262d46c39c83eb22a0d`, workflow SHA256
`accc58952785efea158059f1cd79adf1bfea2507a3ec004d10d2caa93cd88272`
and synthetic commitment
`72b98455f74ad441f8997636542920096aa3d087798224c77de0c79afa351980`.
The synthetic commitment is not a real source/supply/private-run permit.

1. [Self-review test, run39623600292 attempt1](https://github.com/aqua-lifestyle/aqua-lifestyle/actions/runs/39623600292):
   Xolani-M was both initiator and the configured negative-control reviewer.
   GitHub returned `current_user_can_approve=false`, job `waiting`, no job steps.
   The attempted approval POST was rejected with HTTP422, “No pending deployment
   requests to approve or reject”. The run remained waiting and approval history
   stayed empty. It was then cancelled, not approved or completed successfully.
2. [Non-reviewer admin release test, run39623907871 attempt1](https://github.com/aqua-lifestyle/aqua-lifestyle/actions/runs/39623907871):
   the second existing admin account was the test-only reviewer; the initiating
   Xolani-M account's repository-admin permission was independently read back true.
   GitHub again returned `current_user_can_approve=false`, job `waiting`, no steps;
   the administrator's approval/release POST was rejected with the same HTTP422.
   Approval history stayed empty. It too was cancelled. This tests the documented
   approval API with admin bypass disabled; no browser/UI bypass was exercised.

These are native provider configuration/blocking/rejection results, not a positive
independent approval result. No genuinely independent person has been designated;
no account was impersonated or appointed as the baseline acceptor. Neither run
executed its harmless print step, operational qualification or any private command.

An unauthenticated HTTPS read test returned HTTP200 for the public run record,
approval history and environment record. Thus public approval metadata is readable
without sending a private repository credential to the public repository. The
approval-history endpoint currently returns an honestly empty array for these
rejected tests; reading a real positive approval remains untested.

GitHub's documented approval-history object has reviewer, state, comment and
environment but no explicit attempt field. The operational amendment must therefore
bind attempts in the approval comment/permit, verify the exact run-attempt and job
records, and reject prior-attempt approval reuse. A bare run ID is insufficient.
The public approval repository must still be publicly readable and retain its
required protection when the private verifier consumes the approval; changed
visibility or missing API/protection evidence must fail closed.

After testing, both runs were confirmed cancelled. The second test-only reviewer
assignment was removed, the original self-negative-control assignment restored,
and the test environment's branch policy changed to
`b6-public-probe-closed-not-authorized`. The initial branch-policy update including
an unsupported `type` field was rejected with422; the supported name-only update
succeeded and was read back. No incomplete protection update was credited as a pass.

**Result: PUBLIC GATE WORKS for the tested native protection capabilities.**
Positive independent approval and an actual cross-repository/private-run handoff
are not established. A genuinely independent reviewer and independently accepted
handoff amendment remain necessary before changing or executing qualification.

## Single owner action

Name a genuinely independent human reviewer and their GitHub login, distinct from
the PR113 author and implementation/execution actors. Another account controlled
by the same author is not independence. That person can perform the positive
harmless review and independently assess the proposed handoff and PR113 baseline.
