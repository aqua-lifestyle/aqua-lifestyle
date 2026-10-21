# B6 public/private approval handoff — proposed amendment 2

**PROPOSED — PENDING INDEPENDENT ACCEPTANCE.** Owner instruction “AGENT 2 — CLOSE
THE REMAINING B6 APPROVAL-GATE BLOCKERS” authorizes this scoped implementation and
harmless tests, not acceptance of this amendment, the supply baseline or privileged
qualification. Agent1's technical BLOCKED review is not independent human approval.

The verified replacement supply and PG18 compatibility evidence are carried forward.
No dependency acquisition, PG18 work or unchanged passing suite is repeated here.
The only application change is the already-approved follow-redirects1.16.1 lockfile
fix from `924263e26d9ef98a40bbde0f7e7633c439059690`.

## Narrow proposed replacement of approval location

Replace only the unsupported private required-reviewer environment check with a
native public required-reviewer gate plus a fail-closed private verifier. The private
qualification workflow, source/supply verification, Gate-Q/A, isolation, freshness,
cleanup, terminal evidence, private custody and subsequent independent acceptance
remain mandatory. No implicit fallback between approval implementations is allowed.

The public gate runs the exact reviewed bytes of
`.github/workflows/b6-public-approval.yml`, carried at the already registered
`.github/workflows/ci.yml` on isolated branch
`test/b6-public-approval-gate-20261021`. Application CI on main and PR113 is unchanged.
The carrier commit, workflow ID314866230, file bytes, environment ID24924994010,
required reviewer, prevent-self-review, no-admin-bypass and exact branch restriction
are all verified. The gate has no token permissions, checkout, private payload,
artifact upload or qualification command. Its sole input is an opaque SHA256.

## Exact private permit and native review

`.github/b6_approval.py` executes from the private workflow's exact Git commit,
then verifies its own bytes and both workflow files against the selected public
source commit. It requires actual public source/private custody visibility and
source PR identity. Each invocation creates a fresh256-bit nonce and a canonical
private permit binding:

- exact source repository/commit and PR;
- unchanged candidate-baseline bytes/SHA256 and this amendment's SHA256;
- private workflow commit/file digest and verifier digest;
- actual private run ID and attempt, and harmless/qualification mode;
- public carrier commit/path/workflow ID/file digest and environment ID;
- immutable reviewer user ID/login, owner authorization issue and nonce.

Canonical encoding is sorted-key ASCII JSON with separators `,` and `:` and no
trailing newline. `permit_sha256` is SHA256 of those exact UTF8 bytes. The permit
and nonce stay in private logs/evidence. Only the commitment goes to the public gate.
The native review comment must be exactly:

```text
B6_HANDOFF_APPROVED {"permit_sha256":"<digest>","public_run_attempt":"1","public_run_id":"<id>"}
```

The verifier queries GitHub's public HTTPS API without sending the private token.
It requires one native approved record, the appointed User's exact ID/login, exact
comment/commitment and successful completion of the exact gate job. It checks both
public initiator and triggering actor, source author and private executor against
reviewer identity. Missing, rejected, cancelled, ambiguous, bypass-enabled,
wrong-source/workflow/nonce/mode and stale-attempt records reject authorization.
Public reruns are not accepted: use a fresh public run at attempt1. Private reruns
get a new attempt and nonce and require new owner authorization/native review.

The public approval API has no native review-attempt field, so explicit comment and
permit binding, run-attempt/job verification and rejection of public reruns are
mandatory. Neither green status nor a copied receipt is approval authority.

## Owner record and independent human boundary

Owner creates an open private issue with this body while approval is outstanding:

```json
{"schema":"B6PublicPrivateAuthorizationPendingV1"}
```

The bounded, non-privileged preflight waits at most20 minutes, without acquiring
supply or invoking sudo. Its private output supplies the actual permit and digest.
Only exact-commit Git contents are cached; mutable authority is always reread.
Private pending-owner records are polled at15 seconds, anonymous public approval
at180 seconds to bound public API use. Provider errors remain failures, not approval.
After dispatching the public gate with that digest, the owner updates the issue to:

```json
{
  "schema": "B6PublicPrivateRunAuthorizationV1",
  "permit": "<replace with the exact private permit OBJECT, not a string>",
  "public_run_id": "<actual public run ID>",
  "public_run_attempt": "1",
  "independence_attestation": "OWNER_ATTESTS_A_DIFFERENT_INDEPENDENT_HUMAN_NOT_AN_AUTHOR_CONTROLLED_ACCOUNT"
}
```

This is not an instruction for Agent2 to attest an unappointed person's independence
or create qualification authority. A genuinely independent human must be designated
by the owner and configured as the public environment's sole reviewer and private
`B6_INDEPENDENT_REVIEWER`. Another author-controlled account is not independence;
GitHub account separation alone cannot establish that human fact. Xolani-M authored
PR113 and is expressly excluded as its independent acceptor.

Qualification mode additionally requires a non-draft exact source PR, a current
APPROVED review by that independent human with both exact lines:

```text
B6_SUPPLY_BASELINE_APPROVED sha256:<candidate-baseline.json SHA256>
B6_PUBLIC_HANDOFF_AMENDMENT_ACCEPTED sha256:<this file SHA256>
```

Only that real review, not generated file hashes or an agent report, can satisfy
independent source/baseline/amendment acceptance. This document stays PROPOSED until
the proper acceptance record exists. Mutable source review, visibility/protection
and native approval are revalidated immediately before capability release.

## Harmless demonstration and execution hold

The workflow defaults to `handoff-test`. Its separate job has read-only permissions
and only the common verifier and a harmless result print; it contains no acquisition,
installation, sudo, bootstrap or release-upload step. A real positive handoff test
still requires native approval by the qualified independent human. Mock/unit
positives are labelled as such and are not promoted into provider/human evidence.

The qualification job retains the existing private environment/branch hold. It also
requires private variable `B6_PRIVILEGED_QUALIFICATION_ENABLED` to equal exactly
`OWNER_AUTHORIZED_AFTER_INDEPENDENT_ACCEPTANCE`, plus all real source/native/owner
checks. Neither the hold nor this variable is enabled by this implementation.
Separately authorizing an exact privileged run remains an owner action after
independent acceptance. No production extraction, merge, deploy or V2 activation
is authorized. Complete approval responses and permits remain private evidence.
