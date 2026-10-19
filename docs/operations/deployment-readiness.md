# Protected production readiness

The owner authorized GitHub Actions OIDC for this read-only endpoint and confirmed
that Bird and Yoco production startup configuration remains mandatory. No key is
created, shared with Actions or returned by this endpoint. The startup validator
is unchanged.

GET /api/deployment-readiness?expectedRevision=<40-character-commit> accepts only
the separate GitHubDeploymentReadiness scheme. Ordinary application JWTs continue
using their original scheme and do not authorize this endpoint. GitHub discovery
and rotating signing keys come from https://token.actions.githubusercontent.com
over HTTPS. Signature, RS256, exact issuer/audience, expiry, short issuance lifetime,
repository 1423239668 / owner 307087979, immutable subject, main branch,
deploy.yml workflow, workflow/source revision equality, workflow_dispatch event,
github-hosted runner, production-release environment and positive run/attempt claims
are required. Duplicate identity claims, unavailable keys and unexpected subject
configuration deny access. The audience is the exact readiness endpoint URL.

The closed response reports only contract/version, Ready/Unavailable, build revision,
Bird/Yoco StartupValidated, normal-integrated profile and ConfigurationHeld.
Missing restrictions, invalid configuration, configuration reload or revision
mismatch return 503. Anonymous/other identities receive 401/403. The response is
not public, ABP-wrapped or cacheable. No exception/configuration values are returned.
Only the runtime invokes the existing pure startup validator; no provider operation
or database mutation is involved.

ConfigurationHeld means the existing explicit background/financial/weekly/monthly,
commissioning, demo, pilot and seed switches are false. It does not prove database
role isolation, provider connectivity, successful delivery/payment or business
commissioning. Those remain separate evidence requirements.

Bootstrap does not call this endpoint on the legacy API. The executor retains
baseline health/revision, individually read non-secret Render settings, exact
rollback pair, schema and history checks, then requires new readiness after
Render and before Vercel. A verified production predecessor requires it before
subsequent Render mutations too. Missing readiness never triggers a legacy fallback.
Merges, credentials, initial deployment and automatic triggering remain separately
unauthorized. Live API OIDC access is unqualified until a separately authorized run.
