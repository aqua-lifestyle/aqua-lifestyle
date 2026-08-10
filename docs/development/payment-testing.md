# Safe payment testing

Use this procedure only with a disposable database and fresh test customers. Do
not use the ambiguous 31 July R600 record, production data, or production
provider credentials.

## Test-mode configuration

Use only the separately stored Yoco test credentials and the signing secret
returned by the registered test webhook:

```text
Yoco__Mode=test
Yoco__SecretKey=sk_test_<obtain-from-Yoco-secret-store>
Yoco__WebhookSecret=whsec_<obtain-from-Yoco-secret-store>
```

The API rejects a missing or malformed signing secret and never accepts an
unsigned request. Test mode uses the same signature, payment-fact, checkout,
purpose, and idempotency checks as live mode. Never add a placeholder webhook
secret or a runtime signature bypass.

A hosted checkout redirect proves only that the browser returned. Success,
cancellation, and failure returns leave local payment and participation state
pending. They do not activate AQGreen or Onyx, allocate a payment, advance an
instalment, release commission, or create an entitlement.

## Deployment and compatibility order

When separately authorised to deploy, use this order:

1. Run the migrator against the target disposable or staging database.
2. Start the matching API.
3. Verify `GET /api/health` reports the expected build and payment contract.
4. As a host administrator with `Aqua.Admin.Diagnostics.View`, verify
   `GET /api/admin/operations-diagnostics` reports the intended environment,
   database fingerprint, latest migration, and no pending known migrations.
5. Start the matching frontend only after the API checks pass.

Supply a safe build identifier through `Deployment__BuildId`; deployment
platform commit variables are used as fallbacks. `Deployment__ImageId` and
`Deployment__EnvironmentId` are optional but recommended. Do not put a
connection string, hostname, credential, or customer identifier in these
values. The database fingerprint is one-way and is available only from the
protected diagnostics endpoint.

The member payment and invitation screens compare the API payment contract and
required capabilities before exposing checkout actions. An incompatible API is
shown as an operational error instead of an unusable payment journey.

## Test sequence with the registered webhook

1. Create or select a disposable PostgreSQL database.
2. Apply all migrations and confirm migration history through the protected
   diagnostics endpoint.
3. Start the API in test mode with the stored Yoco test secret key and test
   webhook signing secret.
4. Confirm the public health response identifies the intended API build and
   payment contract.
5. Start the matching frontend.
6. Create fresh test customers and programme records.
7. Verify checkout creation for:
   - one R1,200 AQGreen joining payment;
   - one full R6,120 direct-Onyx joining payment.
8. Verify a repeated request reuses the persisted checkout URL, an in-progress
   request cannot start another provider checkout, and an unsupported AQGreen
   instalment request is rejected without creating payment state.
9. Follow each hosted checkout return route and confirm the UI still says that
   secure confirmation is pending and programme state has not advanced.
10. Complete one Yoco test payment and confirm exactly one signed success event,
    receipt, payment record, checkout completion, and transition to
    `PaymentConfirmedAwaitingApproval`—never directly to `Active`.
11. For AQGreen, confirm the completed R1,200 joining obligation records one
    R30,000 funeral-cover inclusion without asserting insurer activation.
12. Confirm the responsible active Area Administrator with the approval
    permission sees the durable queue item and receives one transactional email.
13. Repeat or redeliver the event and confirm no duplicate payment, funeral-cover
    inclusion, approval requirement, or administrator email is created.
14. Approve or reject from the Area-scoped portal. Confirm one audit decision,
    one customer outcome email, and removal from the pending queue. Only approval
    may transition the participation to `Active`.
15. With fresh AQGreen data, test administrator recovery as described below.
16. Confirm AQGreen and direct-Onyx payment purposes cannot settle one another.

Do not represent locally generated signing fixtures in automated tests as Yoco
integration evidence.

## Administrator checkout recovery

Checkout recovery has separate permissions:

- `Aqua.Admin.ProgrammeParticipations.ViewPaymentCheckouts`
- `Aqua.Admin.ProgrammeParticipations.TerminatePaymentCheckouts`
- `Aqua.Admin.ProgrammeParticipations.ViewLegacyPaymentReconciliation`

These permissions are not automatically assigned to existing roles. An
authorised operator must configure the intended role mapping, and users may
need to sign out and in again to refresh permission claims.

The recovery panel is Area-scoped server-side. It shows pending AQGreen joining
checkout evidence without exposing checkout URLs or raw webhook content.
Termination requires a recorded justification and stores the administrator and
decision timestamp. It is idempotent for identical evidence and cannot
terminate a verified payment. A late provider response cannot reopen an
administratively terminated checkout.

Elapsed time, closing the browser, and browser cancellation are not terminal
provider evidence. [Yoco's currently documented Checkout webhook
contract](https://developer.yoco.com/api-reference/checkout-api/webhook-events/payment-notification)
lists payment and refund notifications but does not document a checkout-expired
event. Therefore abandoned checkouts remain locked until authoritative provider
evidence is supported or an authorised administrator terminates them. Do not
use a timer as proof that a provider URL is no longer payable.

## Legacy records

Legacy reconciliation remains read-only. Do not infer purpose from an R600
amount. Before any production reconciliation, separately obtain authorised
evidence for the deployed frontend/API builds, API database identity, applied
migrations, participation terms and amounts, obligation purposes, checkout and
provider references, and verified confirmation history. Ambiguous records must
not be rewritten or charged automatically.

## Validation boundary

### Verified by repository tests

- fail-closed request handling for missing signing secrets and startup rejection
  of malformed configured secrets;
- exact-body signature verification with valid, invalid, and stale fixtures;
- checkout request amounts, purposes, and hosted redirect handling;
- payment-contract compatibility gating;
- competing-checkout prevention and local idempotency boundaries;
- browser return pages remaining pending;
- Area-scoped administrator inspection and termination;
- Area-scoped approval queues, permission-aware administrator email resolution,
  decision idempotency, and cross-Area denial;
- R1,200-once and R600-plus-R600 funeral-cover inclusion;
- migration, uniqueness, amount-cap, authorization, and purpose-isolation
  behavior covered by repository tests.

### Not yet verified

Registration and local fixtures do not prove end-to-end provider delivery. A
real Yoco test payment against the matching deployed API is required before
claiming successful or failed provider-event delivery, provider duplication,
payment allocation, AQGreen or Onyx activation, commission/entitlement release,
or payment-driven progression.

The exact real `payment.failed` payload and checkout retry semantics remain
unverified until an authorised tester captures and sanitises a signed test
delivery. A valid, matching failure is retained as idempotent provider evidence
but does not terminally close the checkout or block a later authoritative
success. Do not weaken the event, status, amount, currency, mode,
checkout-reference, purpose, or replay checks to accommodate an assumed payload.
