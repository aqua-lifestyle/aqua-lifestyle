# Exact protected-readiness GET through the Yoco cutover fence

> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

Version: 1. Application base: `1b748dfdaf3b63fb9dcc42f781ab79d5c39ca503`.
Branch: `fix/yoco-cutover-protected-readiness`.

## Goal and authorization

The owner authorized only the exact readiness GET to reach its existing OIDC
authorization layer, preservation tests, required application CI and one review
PR. Executor changes, deployment, diagnostics, migrations and financial activation
are outside this task. Existing source and unrelated worktrees are preserved.

## Scope

One operational-route condition in `ApplicationRecoveryMiddleware`. Tests exercise
the real middleware/authentication/authorization/controller order, normal and
cutover profiles, invalid OIDC identities, signing-key failure, exact GET scope,
and financial/business-write fences. No authorization or readiness contract changes.

## Evidence

Run `41822854444/1` stopped at protected readiness. The previously authorized
credential-free observation returned HTTP 503 with
`YOCO_CUTOVER_BUSINESS_WRITES_FENCED` and receiver `FullV2`. This task performs no
additional production observation. Local/CI tests are not production evidence.

## Plan

1. Reproduce the middleware denial in a focused test.
2. Add the exact GET route and verify OIDC/business restrictions remain enforced.
3. Run focused Release validation and required application CI.
4. Publish one protected application PR with the new-source implications below.

## Release implications — assessment only

- The PR head and eventual main merge SHA are new application identities. Neither
  can use old main-push CI `41601901625/2` as its own qualification; a successful
  exact new main-push run and its artifacts are required after any authorized merge.
- Existing Render deployment `dep-dblemb8u01pc73a0ouh0` remains the historical
  `1b748df...` binary. It cannot serve this code change without a separately
  authorized backend build/deployment. Do not relabel or redeploy it in this task.
- The current executor pins `1b748df...`, the old CI identity, and the old middleware
  digest in its exact partial-release reconciliation and completed-m55 candidate
  carry-forward. Its recovery readback is also candidate-bound and expires
  `2026-11-05T05:54:23.080Z`. These records do not automatically authorize a new SHA.
  The failed readiness continuation remains historical evidence, not a completed
  full-release predecessor. Executor/release authority must be explicitly aligned
  before recommending deployment; no such change is implemented here.
- Frontend source files are unchanged, but the coupled release's source/archive
  proof and Vercel metadata must bind the eventual new application SHA, not relabel
  the old frontend proof. The new middleware digest is
  `a7ab7d5f2c2a9c68fb4eadccf133c2535f28259d3132ac6f21081875f7666073`;
  the executor currently accepts
  `e39cacea3bec6fe765d794d053cb9d21019a8164302cf3a9a987bc3253e8b00f`.
- Completed migration facts are retained. This HTTP-only correction supplies no
  migration execution or financial authority.
- The existing automatic trigger listens for successful **main-push** CI. A PR
  does not meet that condition; any later merge must be coordinated with explicit
  release authorization rather than assuming the main-CI completion is inert.

## Validation and handoff

- Initial restore/build reached test execution but the combined command exceeded
  its 120-second tool timeout. The subsequent no-build regression run executed
  and failed as expected (`reached` was false before the production change).
- Release build after the fix: passed, zero errors (existing warnings remain).
- Focused middleware, real HTTP/OIDC, CLEAN_START and recovery tests:
  **149 passed, zero failed/skipped**.
- Local mandatory release-safety classifier: **240 cases passed**; explicitly
  local evidence, not new main-push CI qualification or independent enforcement.
- NuGet advisory review completed from the test-project directory. It reports
  existing vulnerable transitive packages; project/package references are unchanged.
  No dependency remediation or clean-audit claim is included in this route fix.
- `git diff --check`: passed. Full application CI (17 required jobs, including
  frontend, backend partitions, PostgreSQL and container builds) is recorded on
  the single PR at its exact head after an observed result.

Implementation is ready for protected review; deployment is **NO-GO under the
current executor bindings** until the new exact main SHA/CI/source proof and
historical/recovery disposition are separately authorized. This task performs no
executor modification, additional production check or provider operation.
