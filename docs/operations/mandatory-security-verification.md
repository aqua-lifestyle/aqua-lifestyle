# Mandatory security verification and independent custody

**Acceptance: BLOCKED.** This branch implements local classification and a custody
fixture. It does not establish independently enforced GitHub qualification or a
production credential boundary. No production or GitHub configuration was changed.
The intended operating workflow remains merge → CI passes → automatic deployment,
without per-release human approval. Provisioning the trust boundary is a separate,
once-only, authorized control-plane change.

## Root cause and correction within the candidate

At starting commit `1b662546de119edd4dd78514bd655a639444fdc5`, the command
`node --test tools/deployment/*.test.mjs` passes all 171 cases. In a throwaway archive,
changing only `test` to `test.skip` for **Render failure never starts Vercel or
automatic rollback** produces 170 passed, one skipped, zero failed and exit 0.
Node considers an explicit skip compatible with process success.

BRANCH-EXPOSED ROOT CAUSE: `ci.yml` used process exit status as the entire security
qualification. `release-control.mjs:validateRun` checks completed successful job
names on the exact SHA/run/attempt; it has no independent per-case inventory or
result authentication. The `candidate` release job calls that qualification;
the `production` job then requests the `production-release` environment and injects
Render/Vercel secrets before executing further candidate-controlled checks. The
release workflow comes from main after merge, so a merged ordinary PR can change
both tests and enforcement. A main checkout is not independently controlled.

`mandatory-security-tests.json` now explicitly enumerates 193 cases with stable
`AQUA-SEC-0001` through `AQUA-SEC-0193` IDs and exact file/name pairs: the original
171 cases and 22 classifier/workflow checks. Two pre-existing duplicated dynamic
display names now include their distinct parameter values; assertions are unchanged.
Names, IDs and expected cardinality are not rediscovered during normal qualification.
The source hashes pin test assertions, verifier, reporter, release controller and
workflow bytes; edits require an explicit policy revision. The manifest's own digest
must be pinned outside itself. Neither a self-hash nor an artifact name is authority.
This inventory covers the Node release-security suite. It does not replace the
existing PostgreSQL suites or claim an independently protected inventory of all
backend security tests.

`run-security-tests.mjs` compares pinned source bytes, executes the explicit test
file set with a machine-readable reporter, and classifies every case. Only one
successful execution of every exact identity is accepted. Missing, renamed,
duplicated, skipped, todo, cancelled, failed, neutral or inconclusive results,
missing/inconsistent counters and incomplete execution fail. Evidence carries the
actual checkout SHA and the CI repository/run ID/attempt; checkout mismatch fails.
Local execution uses an explicit `local` kind with no invented CI run. Local SHA
metadata identifies HEAD; it does not assert that an uncommitted application tree
is identical to HEAD. CI retains an upload-artifact v6 immutable artifact named
with SHA/run/attempt. Upload immutability prevents replacement of that artifact;
it cannot establish that candidate-provided contents were honestly produced.

The release controller still consumes CI job status, and no independently protected
verification job has been added to its production credential path. This is a stated
unmet requirement, not a hidden same-repository substitute. Removing or rewriting
the local command/manifest in an ordinary PR remains possible under current settings.
The existing governance/bootstrap checks block current rollout, but those checks are
also candidate controlled and cannot establish the requested protection.

## Required external trust boundary (PROPOSED, not provisioned)

| Custody | Required enforcement |
| --- | --- |
| Security/control repository | Separate security custodians own a SHA-pinned verifier, mandatory inventory, exact test/assertion sources and control-path digests. Application contributors cannot write it, select an alternate revision, change pins/keys or approve their own control updates. No ordinary app PR can redefine its own approved base. |
| GitHub admission | Organization required-workflow enforcement or a distinct GitHub App check, tied to its authenticated producer, is required without contributor bypass. Deleting/renaming the candidate's caller/job cannot remove the externally required verification. A check name from the shared GitHub Actions App alone cannot identify trusted workflow source. |
| Trusted execution | The external supervisor authenticates repository identity, immutable source SHA, CI run and current attempt from GitHub. It checks control changes against its own policy before execution. Approved control changes use a separately governed policy update; ordinary changes cannot edit the inventory/gate. Test workers use a fresh read-only source snapshot, a fixed runtime, clean environment and no candidate Actions, hooks, caches, reporters, privileged credentials or signer access. A trusted parent verifies execution and exact identities. |
| Evidence issuer | After actual trusted execution, a separate issuer signs exact raw report and inventory digests plus repository/SHA/run/attempt. Candidate uploads, claimed booleans, NDJSON and unsigned/self-signed JSON are untrusted. Do not sign a candidate's report just because it parses. A runner executing untrusted application code cannot hold an issuance key. Isolate signing and validate worker evidence through a trusted channel. |
| Credential boundary | Keep production keys out of repository/organization secrets accessible to ordinary application workflows. A separately controlled deployment workflow or broker alone can obtain scoped credentials, after authenticating the external verification for the exact source and CI attempt and rechecking current CI. Enforce trusted reusable workflow identity/revision at the broker, not an editable `needs` expression. No candidate code executes with those credentials. A machine deployment protection rule is an alternative only if its policy cannot be bypassed/removed by application contributors and it checks the same identities. |
| Retention and rechecks | Store authenticated receipts in independent immutable custody with explicit retention, issuer/key rotation and replay policy. A changed CI attempt, missing report, unknown status or absent required producer blocks. Revalidate near credential issuance and provider mutation, preserving the existing single orchestrator and retry receipts. |

An external reusable workflow is useful only with enforced use and restricted
credential issuance. Adding `uses: other-repo/...@SHA` to an editable caller alone
can be bypassed by removing it or adding another secret-reading job. CODEOWNERS
without enforced independent control review is insufficient. A candidate-editable
public key or inventory pin has the same problem. Normal merge approval is distinct
from per-release approval; the proposed broker/protection check is automatic.

GitHub documents workflow identity claims for this purpose in
[OIDC with reusable workflows](https://docs.github.com/en/enterprise-cloud@latest/actions/how-tos/secure-your-work/security-harden-deployments/oidc-with-reusable-workflows).
Environment job protection and secret availability are described in
[deployments and environments](https://docs.github.com/en/actions/reference/workflows-and-actions/deployments-and-environments).
These mechanisms are options, not evidence of installation or compatibility with
current Render/Vercel credentials. A separately authorized custodian must qualify
actual provider/broker permissions.

`verifyAttestedReport` is an executable Ed25519 consumer primitive. Its public key,
policy bytes and expected context must come from the external authority. Disposable
keys in tests demonstrate byte/context/signature rejection only. No production key,
signer, external repository, GitHub App, broker or OIDC policy is provisioned here.
Its successful return deliberately retains `independentEnforcement=UNVERIFIED`.

## Executable evidence and limits

From repository root on Node 24+:

```bash
node tools/deployment/run-security-tests.mjs --local
node tools/deployment/mandatory-security-probe.mjs
bash tools/deployment/disposable-recovery.sh
```

The probe retains verifier/policy/key in the parent and modifies a separate
synthetic Git candidate under `/tmp`. It executes the original 171-case set
(with disambiguated duplicate names), then skips, deletes and renames the Render
case. All three mutant test processes exit 0, while qualification fails. The skip
case has exactly 170 passes and one skip. It also removes/replaces CI/release/gate
files, changes the candidate inventory, forges/misses report bytes/signature and
substitutes commit/attempt. Every local negative must be rejected. The synthetic
key is discarded. The throwaway policy custody is an experiment, not a live
GitHub authority or proof that ordinary PR bypass is prevented.

Required external acceptance remains unavailable: an ordinary application identity
must try those mutations against the real protected verifier and credential broker
in an isolated non-production target. It must be unable to qualify or obtain a
sentinel deployment capability, including when all candidate jobs/check names
claim success, the verification job is removed, or a second credential-reading
workflow is added. Obtain exact job/workflow/App identity, artifact/receipt digest,
SHA/run/attempt and denial evidence. No real production deployment is necessary.

Read-only GitHub observations on 2026-10-16: repository ruleset 19085283 contains
only deletion and non-fast-forward rules, classic main protection returns 404,
and `production-release` has no protection rules/branch policy. The account has
repository admin permission; organization ruleset visibility needs `admin:org` and
was unavailable. These facts do not supply separate custody or authorize modifying
settings. Independent enforcement is BLOCKED under the current configured boundary,
not established by repository admin permission. No permission expansion was requested.

| Claim | Evidence limit |
| --- | --- |
| Local classifier correctness | Executed positive/negative unit cases and disposable source mutation probes only. |
| Enforced GitHub CI behavior | Final-head CI can prove the new job runs/classifies; candidate-controlled job success cannot prove external enforcement. |
| Independent verifier custody | BLOCKED: no designated separately protected authority or required external producer. |
| Production runtime privilege isolation | UNKNOWN; synthetic PostgreSQL role denials do not establish production roles/IAM. |
| End-to-end production deployment | Not run; forbidden in this task. No production acceptance claimed. |
