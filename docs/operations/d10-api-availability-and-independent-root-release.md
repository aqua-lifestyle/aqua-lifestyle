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

## Migration-57 schema binding

The accepted independent-root schema fingerprint is the SHA-256 of the public
`information_schema.columns` snapshot used by
`AQGreenReconciliationEvidenceSnapshot.CaptureAsync`: UTF-8 bytes of
`table_name:column_name:data_type:is_nullable`, joined with newlines in
`table_name, ordinal_position` order. Protected CI measured
`27c55cd3dfbb93319aeeb17b463912fc7c366367a056aee7d0fc7aa629ad3664`; the
owner's read-only production query using the same calculation returned the same
value. The previously committed `48be3ea9…` value was incorrect and is not
supported by a retained query result. Production reported database
`aqualifestyle`, database identity SHA-256
`1be505af7b4ba8efd991448f8ab301c771deb4c962c5eb02c459b14bd6f0c6fe`, 56
applied migrations, latest `20261115113914_AddAQGreenIndependentRootRequests`,
and the independent-root request table present.

The production defect was a unique index that included soft-deleted
`EntryParticipations`. Migration 57 changes only its predicate to
`"IsDeleted" = false`, preserving retired membership and payment rows while
allowing one current participation per customer. Because this index-only
change does not alter public columns, the accepted public-column fingerprint
remains `27c55cd3dfbb93319aeeb17b463912fc7c366367a056aee7d0fc7aa629ad3664`.

The accepted migration history must contain exactly the backend's 57
migrations, ending with migration 56
`20261115113914_AddAQGreenIndependentRootRequests` and migration 57
`20261125202325_UseLiveEntryParticipationUniqueIndex`. A pending, missing, or
unexpected migration or any different public-column fingerprint holds D10.

## Health probe

Render's existing `aqualifestyle-api` health-check path is `/api/health`. It
returns HTTP 200 when the API's database, Data Protection key store, and
configured Redis dependency are available. D10/payment holds are reported in
the response and do not make the whole API unhealthy. D10-dependent API actions
and background work retain their existing fail-closed checks; the health route
does not grant those capabilities.

## Production completion sequence

1. Merge this application correction only after its PostgreSQL regression and
   existing protected CI checks pass.
2. With Render auto-deploy disabled, an owner explicitly deploys the merged
   backend commit. The general API remains available while D10 readiness holds
   on migration 56. The application does not migrate or seed during startup.
3. Apply migration 57 through the existing owner-authorized migration-only
   mechanism. It replaces one index and does not update, delete, or seed rows.
4. Confirm Render stays live and `/api/health` returns HTTP 200. Require
   `AQGreenOperationalReadiness=Ready`, `PaymentAvailability=Available`, and the
   `aqgreen-flexible-joining-v1` plus `programme-approval-queue-v1` capabilities.
   `Ready` means D10 authority and integrity checks pass; it does not override a
   held payment/joining contract. If any condition is missing, stop before the
   frontend release and inspect the actual hold without changing financial or
   commissioning configuration.
5. The matching frontend is already deployed from
   `6c8404af96931417369f6656d6dd415ad00ddd77`. An owner verifies the real member
   independent-root request and administrative
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
