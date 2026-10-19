> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

# OIDC production readiness

Owner authorization: minimal OIDC readiness authentication; Bird/Yoco remain
mandatory and startup validation remains intact. No deployment, merge, workflow
dispatch, provider mutation or credential provisioning is authorized.

Baseline: application main a6371a495792c313c6985eb4582e87c7849aeb13;
executor main bf163772451c8a164199798de069bf29820e9d60.

Implement a separate read-only GitHub OIDC scheme and closed, revision-bound
readiness response. Ordinary application JWT authentication stays unchanged.
GitHub administrator readback confirmed repository ID 1423239668, owner ID
307087979, and immutable subject prefix repo:aqua-lifestyle@307087979/aqua-deployment@1423239668.

The executor grants OIDC only to its production job. Bootstrap keeps existing
baseline revision/health, history, migration, rollback and non-secret restricted
configuration checks. It requires detailed readiness after Render and before
Vercel; a subsequent verified predecessor also requires it before Render.
There is no 404 fallback and a qualification-only receipt is never a predecessor.

ConfigurationHeld reports existing flag checks, not database grants, worker IAM
or provider delivery proof. Effective production DB authority, original frontend
source/rollback evidence and provider credentials remain external prerequisites.
Focused validation: 57 application health/startup/OIDC tests and 70 executor
admission/readiness tests pass with zero failures/skips. HTTP authorization uses
local Kestrel and disposable RSA keys; provider operations use substitutes.
These are component evidence, not real GitHub token issuance, provider or
production qualification. No mandatory inventory or startup validator changed.
Separate protected PRs will carry the exact commit/CI identities. Owner review and
merge authorization, then fresh main CI/admission and production prerequisites,
remain the next actions. Nothing was merged, dispatched, provisioned or deployed.
