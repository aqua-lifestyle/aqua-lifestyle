# Yoco payment production-readiness review

Status: reviewed against the implementation on 2026-07-26, including direct
Onyx and AQGreen joining payments.

## Executive summary

The implementation has a sound core trust boundary: the browser cannot activate
a programme, provider secrets remain server-side, a signed Yoco webhook is the
only production confirmation path, and confirmation validates the stored
checkout ID, exact amount, currency, mode, customer, purpose, and programme
before changing programme state. Database uniqueness and domain idempotency make
normal webhook retries safe. Direct Onyx creates no participation before the
confirmed R6,120 payment; AQGreen records placement first and activates only
after one confirmed R1,200 joining payment.

It is not yet a complete mature payment-operations platform. The largest gaps
are a durable webhook inbox/event audit, reconciliation for a successful payment
whose webhook never completes, explicit failed/expired/refunded/disputed states,
concurrency stress coverage, and operational metrics and alerts. These gaps do
not justify a speculative rules engine or a rewrite, but they should be addressed
before payment volume or financial exposure becomes material.

## Scores

| Area | Score | Assessment |
| --- | ---: | --- |
| Overall production readiness | 7.0/10 | Safe core checkout and confirmation path; operational recovery remains incomplete |
| Security | 8.5/10 | Strong signature, secret, mode, checkout, and server-authority controls |
| Financial correctness | 8.0/10 | Exact decimal/cents validation and traceable immutable references; no refund/dispute reconciliation yet |
| Reliability | 6.5/10 | Idempotent success path, but no durable inbox or missing-webhook reconciliation |
| Maintainability | 8.0/10 | Shared hosted-checkout lifecycle with programme-specific activation rules |
| Scalability | 6.5/10 | Stateless API and database invariants scale normally; synchronous webhook processing and limited operations tooling will constrain growth |
| Testability | 7.5/10 | Good domain, gateway, signature, rollback, and UI coverage; controller and concurrency coverage remain |

## Strengths and areas requiring no redesign

- Payment initiation, provider notification, payment confirmation, and programme
  activation are separate responsibilities. No controller performs programme
  activation directly.
- `HostedPaymentCheckout` shares only neutral checkout state. AQGreen and Onyx
  retain separate records and confirmation methods, so one programme's activation
  or recruitment rules cannot silently leak into the other.
- The Yoco API key and webhook secret are read from server configuration. The
  frontend receives only a hosted checkout URL. Production startup rejects
  missing settings and mismatched test/live key prefixes.
- Checkout requests use HTTPS, bearer authentication, stable external/client
  references, and an idempotency key derived from the persisted checkout record.
- Webhook signatures are verified against the raw body with HMAC-SHA256,
  constant-time comparison, a bounded timestamp window, and a 64 KiB body limit.
- Payment success is not trusted from return URLs. Success, cancellation, and
  failure query parameters affect customer messaging only.
- Confirmation matches the webhook's Yoco `checkoutId` to the stored provider
  checkout, then validates mode, exact amount, and currency before mutation.
- Provider/reference uniqueness, one checkout per participation, one payment per
  checkout, and one participation per customer/programme provide persistence-level
  protection. Domain methods also reject duplicate and illegal transitions.
- Payment creation, checkout completion, role promotion, and programme activation
  are saved within the confirmation unit of work. Focused tests prove invalid
  amounts and checkout IDs do not leave partial payment or activation state.
- Reapplying a successfully processed notification returns the existing payment
  and participation rather than duplicating either.

No provider-neutral rules engine, event-sourcing conversion, or broad payment
rewrite is recommended at the current scale.

## Payment lifecycle review

The implemented checkout lifecycle is `Preparing → AwaitingPayment → Completed`.
Those transitions are guarded and cannot skip directly to completion without a
confirmed payment. This is sufficient for the current one-time hosted checkout
success path, but it does not represent provider-side cancellation, expiry,
failure, refund, or dispute. The return URL must remain informational because it
is not authoritative.

High priority: extend the lifecycle only when the associated provider events and
operational behaviour are implemented. A mature system commonly keeps a payment
attempt separate from the resulting financial transaction and records terminal
and post-settlement states without deleting the original success history.

## Webhooks, retries, and concurrency

Normal duplicate success delivery is safe through the stored provider reference,
database unique indexes, completed-checkout verification, and idempotent domain
methods. Unknown event types are acknowledged without mutation. Invalid signed
payloads or mismatched payment facts fail before programme changes.

The following gaps remain:

- Webhook event ID, receipt time, payload hash, processing result, attempt count,
  and failure reason are not persisted in a durable inbox.
- Concurrent first deliveries are protected by database uniqueness constraints
  on `(Provider, ExternalReference)` payments and `(TenantId, CustomerId)`
  participations, plus duplicate-handling recovery in the confirmation processor.
  A losing request now deterministically loads the winner's completed result
  instead of leaving the caller with an unhandled error.
- There is no queue/outbox boundary. A process crash during synchronous handling
  depends on Yoco retrying the request.
- There is no scheduled reconciliation against Yoco for checkouts left awaiting
  payment, and no audited administrator reconciliation workflow.
- There are no explicit tests for concurrent deliveries, out-of-order future
  events, or a crash after provider success.

Mature systems commonly authenticate and persist a webhook quickly, acknowledge
receipt, and process the durable inbox idempotently. A reconciliation job then
compares locally pending attempts to the provider so a lost webhook is recoverable.

## Financial integrity and auditability

Amounts use decimal domain values and are converted to integer cents only after
verifying that no fractional cent is lost. The stored checkout amount/currency
must exactly match the notification. Each successful joining payment can be
traced through customer, programme-specific checkout, Yoco checkout ID, Yoco
payment reference, `MemberPayment`, participation, and activation.

AQGreen's current joining terms are one R1,200 payment. The R600 monthly
commitment is separate. Historical AQGreen records with either legacy R600
payment are deliberately excluded from the new checkout and require support,
preventing an accidental additional R1,200 charge. Fully unpaid historical
participations are migrated to the new terms without rewriting paid history.

Remaining limitations:

- No provider settlement/reconciliation record proves that the amount was paid
  out by Yoco to the club's bank account.
- No refund, partial refund, dispute, or chargeback ledger exists.
- `MemberPayment` retains auditable references, but the model does not yet define
  accounting exports, journal entries, or immutable settlement adjustments.
- Commission eligibility remains downstream from participation activation, but
  there is no policy yet for reversing or holding eligibility after a future
  refund or chargeback.

## Failure recovery and provider availability

Client disconnects and browser retries are safe because the checkout is persisted
and returned again. Yoco checkout creation occurs outside the database transaction,
then the provider result is recorded transactionally. A stable idempotency key
reduces duplicate provider checkout creation.

If Yoco succeeds but the API cannot save the provider checkout response, a retry
should recover through the same idempotency key, but this behaviour needs a real
provider contract/integration test. Typed `HttpClient` is used, but explicit
timeouts, bounded retry/backoff, and circuit-breaking policy are not configured.
Provider errors are intentionally converted to safe customer messages, but no
operations alert is raised.

## Security review

No payment success can be forged by changing frontend state or return query
parameters. Joining endpoints require the `Aqua.ProgrammeParticipations.Join` permission and
derive the customer and Area from the authenticated session. Webhook endpoints
are public by necessity but require a valid signature and fresh timestamp.
Internal entity IDs are not placed in customer-facing URLs; opaque checkout URLs
and provider metadata carry the persisted checkout reference.

Immediate operational action: any secret pasted into chat, an issue, or logs must
be rotated in Yoco and replaced in the deployment secret store. Secret values must
never be committed, returned to the frontend, or logged. The repository contains
configuration names and placeholders only.

## Observability

The implementation logs checkout creation without keys or payment data and the
platform already emits console logs. For production operations it still needs:

- structured fields for checkout ID, provider payment reference, tenant/Area,
  programme, event ID, processing outcome, duration, and correlation ID;
- counters for created, completed, rejected, duplicate, and stuck checkouts;
- alerts for signature failures, repeated processing failures, checkout-creation
  error rate, and payments awaiting reconciliation beyond a threshold;
- an administrator view that exposes safe payment state and reconciliation history
  without exposing secrets or raw sensitive payloads.

## Testing assessment

Current coverage includes signature validation and timestamp rejection, checkout
request construction and idempotency headers, exact amount and checkout matching,
AQGreen R1,200 activation, R600 underpayment rejection, no-partial-mutation checks,
successful retry idempotency, direct-Onyx deferred creation, and customer UI flows.

High-priority missing tests are:

1. controller-level raw-body signature and payload parsing;
2. concurrent duplicate webhook delivery against PostgreSQL;
3. provider timeout followed by checkout retry with the same idempotency key;
4. durable recovery after a simulated processing crash;
5. reconciliation of a provider-success/local-pending mismatch;
6. future refund/dispute behaviour once those states are defined.

## Priorities

### Critical before accepting real payments

- Rotate every exposed or shared Yoco key and webhook secret.
- Configure live mode, live secret, webhook secret, and public webhook URL only in
  the production secret store; verify a signed live-mode webhook end to end.
- Add monitoring for webhook failures and manually reconcile every checkout that
  remains awaiting payment beyond the agreed operational threshold.

### High priority

- Add a durable webhook inbox with unique provider event IDs and processing status.
- Add provider reconciliation and an audited, permission-protected recovery action.
- Make concurrent duplicate delivery deterministically return the completed result.
- Add explicit HTTP timeout and bounded transient-failure policy.
- Add the missing integration and concurrency tests listed above.

### Medium priority

- Model expiration/cancellation only after confirming Yoco's authoritative events
  and retry semantics.
- Add settlement, refund, and dispute records before offering those operations.
- Add structured payment metrics, dashboards, and alerts.
- Define how refunds or chargebacks affect activation, network qualification, and
  commission holds without rewriting historical ledgers.

### Low priority

- Introduce another provider adapter only when a second provider is selected.
- Add invoices, vouchers, promotions, subscriptions, partial payments, or EFT
  reconciliation as separate confirmed business features, not generic flags on
  the current joining checkout.

## Assumptions and technical debt

- Yoco returns `checkoutId` in successful payment webhook metadata and preserves
  the application metadata submitted during checkout creation.
- A successful, signed `payment.succeeded` event is the current authorization and
  capture confirmation; separate authorization/capture is not modelled.
- ZAR one-time joining payments are the only current online payment use cases.
- AQGreen placement may exist while awaiting payment; Onyx placement may not.
- The current three-minute signature window matches Yoco's retry signing behaviour.
- AQGreen's legacy `Entry*` code and database names remain technical debt retained
  for compatibility.
- Checkout records currently remain stable rather than expiring or being revoked.

Before production, the assumptions about webhook metadata, retry signing,
settlement semantics, and idempotency-key retention must be validated against the
configured Yoco account and a test-mode end-to-end webhook run.
