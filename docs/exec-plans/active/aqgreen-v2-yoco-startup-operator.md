> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

# Bounded Yoco receive-only startup correction

Version 1. Base: `a4755801ce89868d7791422f3c983363ab76bf30`.
Branch: `fix/yoco-receive-only-startup-operator`.

## Goal and authority

The owner's current instruction permits a minimal application correction and
focused tests, through protected PR/review, allowing safe full-V2 receive-only
startup without a pinned operator. No production deployment, configuration change,
database operation, financial activation or release-policy amendment is authorized.
Keep failed Render deployment `dep-dbkftrcipgvc73asn0lg` and workflow 41527223264/1
recorded as failures; this patch does not reconcile or authorize retrying them.

## Source comparison and invariant

Compared the working pre-cutover production baseline
`1251fb82979e18be53d1d0e7f5c6fab2d0c50a58` (the source of the documented
compatibility receiver) with the accepted application revision. The checkout,
signature-verification and business-confirmation implementations are reused.
The cutover adds durable, signature-verified capture before acknowledgement,
deduplication, a shared transaction/lease fence and separately authorized replay.
It does not replace the provider or credentials. Clean-start compatibility also
retains late payment facts without reviving retired participation or financial
eligibility. Those existing paths remain unchanged here.

The constructor unnecessarily coupled capture/startup to privileged operator
selection. Removing that check alone would permit unsafe zero-ID replay or a
stored Normal phase to bypass intended receive-only behavior. An absent pin must
mean capture/read-only only, regardless of stored phase. Invalid explicit pins
still deny; a latched operator cannot change through reload. Host context and
Pages.Tenants remain mandatory at privileged HTTP boundaries.

## Scoped implementation and validation plan

1. Permit an absent operator pin without changing the enabled cutover fence.
2. Explicitly deny unpinned privileged operations, callback business dispatch and
   HTTP business leases, including stored-Normal cases.
3. Exercise actual startup caller, privileged boundaries and PostgreSQL capture,
   concurrency/commit/deduplication, plus a real full-V2 Production host.
4. Run existing application/security checks and protected PR CI; report skips,
   unavailable checks and exact revision honestly.

## State and evidence

- Source comparison, causal trace and bounded implementation complete.
- Existing signature verifier, provider request format, credentials, receipt
  schema, lock protocol, migration files and financial/commissioning controls are
  not edited.
- No production operation has been performed in this workstream.
- Local restore and Release solution build passed (zero build errors). Existing
  compiler/documentation/analyzer warnings remain; restore reports the unchanged
  Web.Tests-only AngleSharp 1.1.2 moderate advisory. No dependency was changed.
- Focused startup/operator/PostgreSQL tests: **39 passed, 0 failed, 0 skipped**.
  Includes a real full-V2 Production host without an operator, valid signed receipt
  commit/deduplication, pinned-host enforcement and held financial fingerprints.
- Broader Yoco/payment-confirmation regressions: **101 passed, 0 failed, 0 skipped**.
  Includes the unchanged pinned Schema54 compatibility artifact, restart/durable
  capture, signature rejection, conflicting/concurrent duplicates, replay authority
  and rollback tests. These are disposable PostgreSQL/Redis fixtures, not production.
- Mandatory Node security inventory: **240/240 passed**, unchanged inventory.
- Initial unsharded solution test attempt timed out after 20 minutes and is not
  a complete/passing full-suite claim. Its Web.Tests run without the CI environment
  had 26 failures (missing Default-tenant fixtures), 69 passes and 85 skips.
- Rechecking with CI's explicit `ASPNETCORE_ENVIRONMENT=Development`: affected web
  class **13/13 passed**; complete Web.Tests **95 passed, 0 failed, 85 existing
  skips**. No assertions, skips or environment defaults were modified.
- Standalone NuGet vulnerability listing was unavailable: explicit-solution CLI
  failed configuration-path resolution, canonical auto-discovery timed out. The
  restore advisory above is recorded rather than claiming a clean audit.
- Diff review found no blocking branch-owned issue. HTTP host/Pages.Tenants and
  positive-operator checks, invalid/reloaded pin rejection, unknown/stored phases,
  callback commit/duplication and no-provider-side-effect paths were reviewed.
- Required hosted sharded application, frontend, image and security CI is pending
  PR publication. It remains required before any merge/readiness claim.

## Next action

Open the protected PR, collect the required hosted checks and review, then stop
for the owner's next decision. A changed application SHA
needs new reviewed acceptance and fresh authenticated CI; no old qualification or
executor source pin is silently reused.
