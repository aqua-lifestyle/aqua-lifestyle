# Safe payment testing

Use this procedure only with a disposable database and fresh test customers. Do
not use the ambiguous 31 July R600 record, production data, or production
provider credentials.

## Current test-mode limitation

The currently approved configuration intentionally has no Yoco webhook signing
secret:

```text
Yoco__Mode=test
Yoco__SecretKey=sk_test_<obtain-from-Yoco-secret-store>
Yoco__WebhookSecret=<absent>
```

The API starts and can create hosted test checkouts in this configuration. The
webhook endpoint returns `503 Service Unavailable` before reading the request
body, and cannot confirm a payment. Never add a placeholder webhook secret or a
runtime signature bypass.

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

## Test sequence without a webhook secret

1. Create or select a disposable PostgreSQL database.
2. Apply all migrations and confirm migration history through the protected
   diagnostics endpoint.
3. Start the API in test mode with a real Yoco test secret key and no webhook
   secret.
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
10. With fresh AQGreen data, test administrator recovery as described below.
11. Confirm AQGreen and direct-Onyx payment purposes cannot settle one another.

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

### Verified without a webhook secret

- server startup in Yoco test mode without a signing secret;
- checkout request amounts, purposes, and hosted redirect handling;
- payment-contract compatibility gating;
- competing-checkout prevention and local idempotency boundaries;
- browser return pages remaining pending;
- Area-scoped administrator inspection and termination;
- migration, uniqueness, amount-cap, authorization, and purpose-isolation
  behavior covered by repository tests.

### Not yet verified

A real Yoco test webhook secret and a separately authorised test webhook are
required before claiming end-to-end verification of signatures, successful or
failed provider events, provider delivery duplication, payment allocation,
AQGreen or Onyx activation,
commission/entitlement release, or payment-driven progression.

The exact real `payment.failed` payload remains unverified until Yoco supplies a
documented example or an authorised tester captures and sanitises a signed test
delivery. Do not weaken the current event, status, amount, currency, mode,
checkout-reference, and replay checks to accommodate an assumed payload.
