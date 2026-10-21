# Bounded public environment approval experiment

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

Actual experiment results and public run links will be recorded after execution.
