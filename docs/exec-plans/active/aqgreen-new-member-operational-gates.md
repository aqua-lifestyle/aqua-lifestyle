# AQGreen new-member operational gates

> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

Version 2. Base: `2488682e7f36828c380b2f962f29b9f9b4cb6384`.
Current instruction: NO REIMPLEMENTATION; correct only proven operational gates.

## Bounded scope and owner-confirmed intent

One minimal application PR for truthful authorized Normal capabilities and
verification-only restrictions in the existing Bird/outbox. Registration,
verification/token generation, V1 Yoco, joining, funeral inclusion, Area approval
and activation already exist. Their business implementations are unchanged.

The initially drafted pre-commissioning registration exception was premature:
eligible genuine root-customer existence has not been established through an
authorized operator. That exception, its configuration/scope, account/invitation
edits and its dedicated tests were removed before commit. Registration access
remains unchanged pending that evidence and is not part of this patch.

The earlier scope recorded no genuine V2 customer/payment data yet; that is not
an eligible-root readback. Technical, historical and captured records must not
be inferred empty. Joining remains R1,200 once or
two distinct R600 payments; existing R30,000 inclusion follows full verification.
The physical funeral contract suffices. Existing business monthly payments are
untouched; V2 monthly automation, commissions and payouts remain disabled.

## Plan

1. Use existing Normal phase/business lease and release authority to report
   financial capabilities; preserve durable cutover and fail closed on unknowns.
2. Reuse the existing email worker/processor/gateway with verification-only
   Tenant1 selection, delivery cutoff, atomic claims and existing idempotency.
3. Test held/authorized/unavailable state and verification delivery boundaries;
   run required checks before preparing the single minimal PR.

## Authorization boundary

All new enablement defaults off. No production configuration, customer, payment,
phase, replay, commissioning or financial activation is authorized here. Private
readback requires the existing authorized operator; no credentials or access are
created. Genuine activation remains conditional on separate owner authorization.

## Evidence / handoff

IMPLEMENTED and focused TESTED; no deployment or enablement.

- Release restore/build passed. The final reduced-patch build passed with zero
  errors and existing documentation/compiler/advisory warnings.
- `reduced-operational-gates.trx`: 112 passed, zero failed/skipped. Includes existing
  hold/direct-execution regressions, restricted delivery, reload shutdown, health
  Held/Available/Unavailable semantics, and two actual PostgreSQL email tests. Eight
  concurrent claims acquired the eligible message once without touching other mail.
- `reduced-health-final.trx`: 14 passed, zero failed/skipped after final review
  distinguished explicit configuration holds from a failed durable Normal probe.
  These overlap the earlier 112; they are not 14 additional independent cases.
- `reduced-verification-gateway.trx`: 17 passed, zero failed/skipped; controlled HTTP
  handler, not Bird delivery evidence.
- Full frontend suite: 564 passed in 133 files, including the 37 focused contract,
  member and invitation tests. Frontend production build passed; lint/type-check
  passed (one existing navigation warning; full suite also emits existing fixture
  act/local HTTP warnings).
  Local Node was 26.6.0 rather than supported 24.x; hosted CI uses Node24.
- Release safety classifier passed all 240 local cases at base HEAD with edits.
- Production npm audit: zero vulnerabilities. NuGet: pre-existing transitive
  advisories; no package/dependency edits. These are not a clean backend audit.
- Earlier broader frontend/backend commands were interrupted by the clarification;
  they are not full-suite evidence. Removed-registration test results are not used
  to qualify this reduced patch. Required hosted full suites/containers are pending.

Inspection confirms no changes to account registration, verification scheduling,
invitation services, application-service/HTTP access fences, Yoco payment/webhook
processing, joining, funeral inclusion, approval or activation business code.
Current deployed revision/release are carried forward, not customer-journey
acceptance. Eligible-root readback and genuine provider/customer outcomes remain
unverified; production enablement is a separate authorization.
