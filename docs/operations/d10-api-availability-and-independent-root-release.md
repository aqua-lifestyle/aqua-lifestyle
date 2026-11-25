# D10 API availability and independent-root release

This note records the application behavior for the migration-56 independent-root
release. It does not authorize production deployment, financial execution, or a
customer transition.

## Runtime boundaries

- Production API startup requires the EF migration history to match the deployed
  backend exactly. The API never migrates, seeds hosts, or seeds tenants.
- D10 authority, source database identity, the reviewed schema fingerprint,
  complete EF migration history, CLEAN_START baseline, tenant/member/area/root
  evidence, and financial-ledger invariants remain checked by the D10 runtime
  before AQGreen operations and financial work.
- A failed D10 check holds the affected capability. It does not terminate the
  general web host. The application health route reports HTTP/API dependencies
  separately from `AQGreenOperationalReadiness` and `PaymentAvailability`.
- Deployment commit, protected CI, and release approval are deployment-time
  provenance. Runtime no longer repeats executor-run identifiers or a mutable
  base64 evidence envelope. A schema successor is accepted only for an exact
  reviewed schema fingerprint and complete migration history. A future schema
  transition requires a reviewed application change with a separately verified
  fingerprint; this code does not admit arbitrary schema drift.

## Migration-56 schema binding

The accepted independent-root schema fingerprint is the SHA-256 of the public
`information_schema.columns` snapshot captured from the already-authorized
Render PostgreSQL connection on 2026-11-25. The query is read-only and covers
column name, type, nullability, and deterministic table/column order. It returns
no customer or financial rows. Production reported database `aqualifestyle`, 56
applied migrations, latest `20261115113914_AddAQGreenIndependentRootRequests`,
and the independent-root request table present.

The accepted migration history must contain exactly the backend's 56 migrations,
ending with migration 55
`20261021051549_AddAQGreenSalesEvidenceInvalidation` and migration 56
`20261115113914_AddAQGreenIndependentRootRequests`. A pending, missing, or
unexpected migration or any different public-column fingerprint holds D10.

## Health probe

Render's existing `aqualifestyle-api` health-check path is `/api/health`. It
returns HTTP 200 when the API's database, Data Protection key store, and
configured Redis dependency are available. D10/payment holds are reported in
the response and do not make the whole API unhealthy. D10-dependent API actions
and background work retain their existing fail-closed checks; the health route
does not grant those capabilities.

## Production completion sequence

1. Merge this application correction only after its focused and existing CI
   checks pass.
2. With Render auto-deploy disabled, an owner explicitly deploys the merged
   backend commit. No migration, seed, or environment change is required. Render
   restarts the single existing API instance as part of deployment.
3. Confirm Render stays live and `/api/health` returns HTTP 200. Require
   `AQGreenOperationalReadiness=Ready`, `PaymentAvailability=Available`, and the
   `aqgreen-flexible-joining-v1` plus `programme-approval-queue-v1` capabilities.
   `Ready` means D10 authority and integrity checks pass; it does not override a
   held payment/joining contract. If any condition is missing, stop before the
   frontend release and inspect the actual hold without changing financial or
   commissioning configuration.
4. Once backend readiness is verified, an owner deploys the already-approved
   frontend source `58b18b2f5a76769a660c2ae834bd6e0c02263ebf` to Vercel Production.
   Confirm deployment `READY` and that the production alias serves that revision.
5. An owner verifies the real member independent-root request and administrative
   approval journey, including resulting root/network placement, using an
   authorized customer account. Do not create synthetic payment transactions or
   payouts. Stop on any authorization, tenant/area, root-provenance, placement,
   or ledger inconsistency.

The existing weekly AQGreen worker is enabled and runs on process start. If D10
readiness passes, it can calculate eligible commission records under the existing
authority and idempotency controls; it does not make payouts. The production
deploy therefore requires explicit owner authorization with that existing
worker effect understood. If D10 readiness fails, its readiness gate prevents
calculation.
