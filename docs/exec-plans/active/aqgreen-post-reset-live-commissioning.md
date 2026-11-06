> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

# Post-reset D10 commissioning and complete Normal release

## Authority and baseline

The current owner instruction authorizes one cohesive correction, focused full-flow
tests, protected publication/deployment and only necessary executor source bindings.
Baseline is deployed `fcbd930d0f1b0cd1f8fea8510ca2f91758aa43dd`. The accepted reset,
22/22 conservation acceptance, migration55 and captured native schema/database
evidence remain authoritative for their actual scope. No historical time is changed.

## Bounded contract review — before implementation

| Predicate | Finding / correction or retained prerequisite |
|---|---|
| Historical reset boundary versus LIVE effective time | Currently aliased. Introduce explicit versioned Decision reset boundary; retain original CleanStartEvidence bytes/facts and use the old boundary for historical state reads. |
| Owner authorization versus independent authority acceptance | V1 requires acceptance before Decision authorization. New explicit CLEAN_START V2 requires actual acceptance on/after owner authorization, valid expiry, and no future-dated grant becoming usable. Legacy contracts retain their encoding/semantics. |
| Reset-only boundary authority versus financial authority | Currently forced equal. New authority explicitly binds the original reset authority separately from genuine current financial authority; reset-only authority cannot be relabelled LIVE permission. |
| Population between reset and commissioning | No adoption, including soft-deleted prospective rows in that interval. Root/history classification uses the immutable reset boundary; real prospective admission uses the new financial effective instant. |
| Runtime/source/configuration bindings after new deployment | Existing readiness exports none of the required native hashes. Extend the existing protected hash-only readiness response and executor audit, not SSH/credentials or local-build substitution. New-source bindings are captured from actual loaded assemblies/configuration. Prior hashes remain predecessor evidence. |
| D10 input custody/selection | Retain all six exact pins and all existing file privacy/symlink checks. Add an explicit fixed-key private-process JSON transport for the new contract through existing protected Render configuration; this removes SSH/cold-start file staging dependency without relaxing file custody or creating credentials/infrastructure. Exact size/hash/duplicate/selection checks still apply. Install accepted configuration coherently; no partial enablement. |
| Authority and configuration | Actual Tenant/Area scope, owner/independent acceptance references, DB/schema identity, actual source/runtime and intended LIVE configuration remain mandatory. No fabricated acceptance or defaults. |
| Yoco Normal | Replay source state, migration55, full artifact, pinned Host/Pages.Tenants, no unfinished events, exact complete event-set hash, actual outstanding-reference reconciliation, <=24h accepted manifest and effective D10 write gate remain. Empty inbox is not merchant reconciliation. |
| Customer terminal lifecycle | Normal -> genuine registration/verification -> exclusive Host root -> full payment/cover inclusion -> separate approval/placement. Existing deferred worker/payout holds remain. |

## Implementation plan

1. Version only post-reset CLEAN_START Decision/VerifiedAuthority; preserve legacy
   V1/B6 and original retirement evidence formats/receipts.
2. Fix every dependent historical-boundary read/classification and authorization
   ordering together. Keep reset-to-commissioning participation gap closed.
3. Export actual three runtime bindings plus canonical intended configuration from
   the existing production-readiness authorization boundary; persist them in the
   protected executor audit for exact-source consumption.
4. Extend the existing PostgreSQL commissioning/Normal/root/payment/cover/approval
   lifecycle using a genuinely later synthetic authorization, preserving original
   receipt/retired payment evidence and testing negative chronology/drift/gap cases.
5. Publish one application correction and its necessary exact-source executor
   update. Verify all new-source checks; reuse completed unchanged evidence.
6. Complete genuine operator file custody/acceptance and merchant-reference review,
   then coordinated selection/financial enablement and Replay -> Normal. GO only
   on actual production readbacks; do not fabricate operator/provider acceptance.

## Current state

Branch `fix/aqgreen-post-reset-commissioning`, clean baseline before edits. No new
production database capture, reset, migration, credential or infrastructure action.
Retained metadata review confirms original journal contains archive count and
financial fingerprints; reduced status receipts do not themselves publish counts.
Actual immutable retained receipt facts can be reused without repeating retirement.

## Evidence / next action

Implemented explicit V2 chronology, separate old reset/current financial authority,
immutable historical comparisons and deleted-gap denial. The existing protected
readiness boundary now exports native hash-only bindings; the existing protected
process configuration can transport exact pinned new-contract JSON without SSH or
weaker private-file permissions. Component tests134 passed, zero skipped. The
expanded PostgreSQL lifecycle passed once with actual private-input parsing,
future-effective and missing-merchant-review Normal denial, configuration-drift
denial, late real synthetic authority, ordinary registration/root/payment/cover/
approval/placement and preserved reset/payment history. All authority/provider
inputs in tests are explicitly synthetic. Two compile-only issues (legacy host
language syntax and a test tuple member) were corrected before that final run.

Next: finish review/full required CI and exact-source executor binding, including
strict version2 readiness validation and native-binding audit custody.
Actual operational authority/custody and merchant acceptance remain human evidence,
not statuses implied by tests, code or a generated hash.
