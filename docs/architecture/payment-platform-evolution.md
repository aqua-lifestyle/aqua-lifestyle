# Payment Platform Evolution Roadmap

## Objective

This document captures the architectural direction for the AquaLifestyle payment platform after the initial Yoco integration has reached production readiness. It is intended as long-term guidance, not an immediate work backlog.

The current architecture satisfies the approved business requirements for AQGreen and Onyx programme participation. The recommendations below describe how the platform can evolve as the business scales, while preserving the design principles that make the current implementation correct, maintainable, and secure.

Nothing in this document should be interpreted as a defect in the current codebase. Every item under "Future Evolution" is a candidate for reconsideration at a later stage of product maturity.

---

## 1. Current Architecture

### Overview

The payment platform is built around a **shared hosted-checkout lifecycle** with programme-specific activation logic. The current implementation is Yoco-only and uses a single R1,200 payment for AQGreen joining, replacing the historical R600 + R600 split-payment flow.

### Hosted Yoco Checkout

Server-side checkout creation is the only entry point for taking a payment. The backend:

1. Creates a programme-specific checkout intent (`AQGreenJoiningCheckout` or `DirectOnyxCheckoutIntent`).
2. Calls `IYocoCheckoutGateway.CreateAsync`, passing a stable idempotency key derived from the checkout aggregate ID.
3. Persists the returned `ProviderCheckoutId` and `CheckoutUrl`.
4. Returns the `CheckoutUrl` to the frontend for redirection.

The frontend never handles payment secrets, never constructs provider requests, and never determines whether a payment is "confirmed". It is a browser that redirects the customer and later displays server-provided state.

### Verified Webhooks

Yoco delivers `payment.succeeded` events to `YocoPaymentsController.WebhookAsync`. Before any business logic executes:

- The raw body is read without buffering limits that would break signature verification.
- `YocoWebhookSignatureVerifier` validates the HMAC-SHA256 signature using the server-side `Yoco:WebhookSecret`.
- The timestamp is checked against a maximum skew of 3 minutes.
- The event `Mode` is compared against the deployment's configured `Yoco:Mode`.

Only after these checks does `YocoPaymentNotificationProcessor` resolve the
persisted programme checkout from Yoco's documented `metadata.checkoutId` and
dispatch to the programme-specific processor. Application entity IDs submitted
as custom metadata are not trusted for routing.

### Idempotent Payment Processing

`ProgrammePaymentConfirmationProcessor` is responsible for reconciling a verified webhook with persisted checkout state. It:

- Looks up the checkout by ID across tenants (no tenant filter).
- Validates `ProviderCheckoutId`, amount, and currency against the persisted checkout.
- Searches `MemberPayments` by `(Provider, ExternalReference)` uniqueness.
- Reuses an existing confirmed payment if the same provider reference arrives again.
- Returns `WasAlreadyProcessed = true` on repeat delivery without re-applying activation.

### Atomic Activation

Activation is performed inside a single ABP `[UnitOfWork]`:

- The confirmed `MemberPayment` is inserted or reused.
- The programme participation (`EntryParticipation` or `OnyxParticipation`) is updated.
- The checkout aggregate is marked `Completed`.
- `ActiveProgrammeParticipantRoleSynchronizer` promotes the user from Guest to Member if needed.

There is no intermediate "pending activation" state that can be observed without a confirmed payment, and once a verified webhook is processed the activation is atomic and local. A successful provider payment can still remain temporarily inactive if the webhook is delayed, fails, or is lost; in that case the participation stays inactive until webhook retry, polling, manual reconciliation, or future automated reconciliation runs.

### Programme-Specific Payment Handlers

- **AQGreen**: `AQGreenJoiningCheckout` → `ProcessAQGreenJoiningCheckoutAsync` → `EntryParticipation.ApplyConfirmedJoiningPayment`.
- **Onyx**: `DirectOnyxCheckoutIntent` → `ProcessDirectOnyxCheckoutAsync` → `OnyxParticipation.ApplyConfirmedDirectEntryPayment`.

Each programme aggregate decides what a confirmed payment activates. The shared checkout base class (`HostedPaymentCheckout`) knows nothing about activation rules.

### AQGreen Payment Lifecycle

1. Customer is already registered with an `EntryParticipation` in `AwaitingJoiningPayment` status (recruiter placement recorded).
2. Customer requests a checkout. The app service creates an `AQGreenJoiningCheckout` for R1,200.
3. Existing incomplete checkout is reused if present (resume-payment support).
4. The returned Yoco checkout ID is recorded as the stable provider reference.
5. Webhook confirms payment → atomic activation → participation becomes `Active`.

Historical split-payment participations (R600 + R600) are preserved in the database and are explicitly excluded from the new checkout flow. The migration updates only wholly unpaid rows to the new terms.

### AQGreen Migration Rollback Behavior and Tests

Migration `20260726162000_AddAQGreenSingleJoiningPayment` introduces
`JoiningPaymentAmount`, `JoiningPaymentId`, `AQGreenJoiningCheckouts`, and
`AQGreenMigrationBackup`. Its `Down()` path is deliberately blocked by
PostgreSQL `DO $$ ... RAISE EXCEPTION` guards when:

- Any `EntryParticipation` has a non-null `JoiningPaymentId`, **or**
- Any `AQGreenJoiningCheckout` row exists.

This prevents partial downgrade from falsifying financial history. When
protected records exist, the preferred remediation is a data-preserving
forward path: archive the protected `JoiningPaymentId` references and
`AQGreenJoiningCheckout` rows to an external ledger, nullify the foreign
keys, remove the checkout rows, then downgrade. This preserves post-upgrade
payment and checkout history outside the database.

Pre-upgrade snapshot restoration remains available only as an incident
procedure. It returns the database to an exact pre-upgrade state, but it
discards all post-upgrade payments and checkout records, so it requires
financial reconciliation against provider settlement data before the
restored database is trusted.

The behavior is validated by `AQGreenMigrationRollbackPostgreSqlTests` in
`test/AqualLifeStyle.Tests/EntityFrameworkCore/AQGreenMigrationRollbackTests.cs`.

These tests execute the **real** EF Core PostgreSQL migrator against a
disposable `postgres:16-alpine` container on a random host port. They do
not use an in-memory provider because the rollback logic depends on
PostgreSQL `DO $$` blocks, `RAISE EXCEPTION`, transactional DDL, foreign
keys, indexes, migration history, and provider-generated SQL.

Each fact begins with `ResetDatabaseAsync()`, which:

1. Terminates all backends connected to the test database.
2. Drops the database if it exists.
3. Recreates it with the correct owner.
4. Calls `NpgsqlConnection.ClearAllPools()` to eliminate stale pooled
   connections.

This guarantees order independence: a failed downgrade or leftover schema
in one test cannot affect the next.

**Scenario 1 — confirmed joining payment blocks downgrade**

- Migrate to the latest schema.
- Create an `EntryParticipation` with single-joining-payment terms and
  persist a confirmed `MemberPayment` whose `Status` is `Confirmed`.
- Set `JoiningPaymentId` on the participation and save.
- Verify via a separate DbContext that the payment row is `Confirmed` with
  the expected `ConfirmedAt`, and that the participation row carries the
  same `JoiningPaymentId`.
- Execute the real downgrade to
  `20260726145201_AddDirectOnyxCheckoutIntents`.
- Assert `Npgsql.PostgresException` with `SqlState == "P0001"` and
  `MessageText` containing `"Cannot downgrade the AQGreen single-joining-payment migration"`.
- Assert the migration remains applied and the schema is intact.

**Scenario 2 — checkout records block downgrade**

- Migrate to the latest schema.
- Create an `AQGreenJoiningCheckout` row.
- Execute the real downgrade.
- Assert the same `PostgresException` guard.
- Assert the migration remains applied and the checkout row still exists.

**Scenario 3 — successful downgrade when no protected data exists**

- Migrate to the latest schema.
- Create an AQGreen participation with no `JoiningPaymentId` and no
  checkout records.
- Execute the real downgrade.
- Assert the AQGreen migration is removed from `__EFMigrationsHistory`.
- Assert `JoiningPaymentId` / `JoiningPaymentAmount` columns no longer
  exist on `EntryParticipations`.
- Assert `AQGreenJoiningCheckouts` and `AQGreenMigrationBackup` tables no
  longer exist.

The success-path assertions use a small `CountAsync` helper that opens a
raw `NpgsqlConnection` and calls `ExecuteScalarAsync()` against
`information_schema.columns` and `information_schema.tables` with
`table_schema = 'public'`. This is necessary because
`Database.ExecuteSqlRawAsync()` returns rows affected, not the scalar
result of a `SELECT COUNT(*)`.

The test infrastructure and these scenarios together prove that the
migration’s financial-history guards are executed by PostgreSQL, that a
failed downgrade rolls back atomically, and that a safe downgrade
completely removes the AQGreen payment schema.

### Onyx Payment Lifecycle

1. Customer requests a direct Onyx checkout. No participation exists yet.
2. `DirectOnyxCheckoutIntent` is created with recruiter/invite placement.
3. Yoco checkout is recorded.
4. Webhook confirms payment → `OnyxParticipation` is created and activated atomically.

### Shared Payment Infrastructure

- `HostedPaymentCheckout` base class: state machine, checkout recording, completion guard.
- `MemberPayment`: provider-neutral confirmed payment record with `(TenantId, CustomerId, Purpose, Amount, Currency, Provider, ExternalReference)`.
- `YocoCheckoutGateway`: server-side credentials, idempotency, mode validation.
- `YocoWebhookSignatureVerifier`: timestamped HMAC verification.
- `ActiveProgrammeParticipantRoleSynchronizer`: role promotion after activation.

### Current Strengths

- The frontend has no authority to confirm payments.
- Provider secrets never leave server configuration.
- Financial amounts are validated exactly (no floating-point rounding surprises).
- Checkout and payment IDs are validated and unique.
- Idempotency is achieved through stable provider references and persisted checkout state.
- Activation is atomic and never observable in a half-completed state.
- Historical data is preserved and not silently migrated.
- The architecture cleanly separates provider plumbing from programme business rules.

---

## 2. Current Design Principles

These principles are already encoded in the implementation and must remain unchanged as the platform evolves.

### Server-Side Trust

All payment authority resides on the server. The frontend can request a checkout and redirect the customer, but it cannot confirm, cancel, or modify a payment. This prevents client-side tampering with financial state.

### Webhook Authority

The only source of truth for payment confirmation is a verified webhook from the provider. No client-side callback, no timer, and no polling can substitute for provider confirmation. This is the foundation of financial integrity.

### Exact Amount Validation

Payments are validated against the exact expected amount in the exact expected currency. Rounding, approximation, or currency conversion is not permitted in the confirmation path. `EntryParticipation.ApplyConfirmedJoiningPayment` and `OnyxParticipation.ApplyConfirmedDirectEntryPayment` enforce this at the domain level.

### Currency Validation

Currency is normalized to a three-letter uppercase code and validated at multiple layers: `HostedPaymentCheckout.Initialize`, `MemberPayment`, and the programme-specific participation aggregates.

### Checkout Validation

Before a webhook can activate a participation, the processor validates that the checkout is in `AwaitingPayment` status, that the `ProviderCheckoutId` matches, and that the payment amount and currency match the persisted checkout. This prevents cross-checkout contamination.

### Provider Reference Validation

`MemberPayments` has a unique index on `(Provider, ExternalReference)`. This guarantees that the same provider payment reference cannot create duplicate confirmed payment records. Successfully processed Yoco event IDs are also recorded in `YocoWebhookReceipts`, whose unique event-ID index deduplicates provider retries independently of the payment reference.

### Transactional Consistency

Activation happens inside a single database transaction. If any step fails, the participation remains in its previous state and the checkout remains incomplete. Once a verified webhook is processed, there is no window in which the local database shows a customer as active without a confirmed payment, or paid without active status. A successful provider payment can still remain temporarily inactive if the webhook is delayed, failed, or lost, until recovery runs.

### Idempotency

Repeat webhook delivery is expected and handled. A successfully processed event
receipt is committed in the same transaction as payment confirmation and
activation. Repeated delivery of the same authenticated event returns without
side effects; payment-reference idempotency remains a second line of defence.

### Auditability

`MemberPayment` records carry `InitiatedAt`, `ConfirmedAt`, `CreatorUserId`, and `LastModificationTime`. `HostedPaymentCheckout` records carry `CreatedAt`, `CheckoutCreatedAt`, `CompletedAt`, and the linkage to the confirmed payment. Together these allow full reconstruction of the payment timeline from the database.

### Separation of Concerns

Provider logic (Yoco HTTP calls, webhook signature verification, idempotency key generation) lives in the `Application/Payments/Yoco` folder. Domain logic (what a confirmed payment means for a programme) lives in the domain aggregates (`EntryParticipation`, `OnyxParticipation`) and the programme-specific checkout aggregates. The shared hosted-checkout lifecycle knows nothing about activation rules.

### Why These Principles Must Persist

These principles are not temporary conveniences. They are the minimal guarantees required for a financial system that handles real money, real customer expectations, and real regulatory exposure. Any future evolution should extend these principles, not replace them.

---

## 3. Future Evolution

### Capability 1: Payment Aggregate

#### Description

Introduce a first-class `Payment` aggregate that encapsulates the complete lifecycle of a payment: created, authorized, captured, confirmed, refunded, disputed, reversed. Instead of payment orchestration living primarily in `ProgrammePaymentConfirmationProcessor`, the aggregate would own its own state transitions and emit domain events.

#### Business Value

- Refunds become a natural state transition rather than an afterthought bolted onto `MemberPayment`.
- Disputes and chargebacks can be tracked without denormalizing payment state across multiple tables.
- Finance can ask "what is the current state of every payment?" without reconstructing it from `MemberPayment.Status`, checkout status, and participation state.
- Reconciliation becomes a query over a single aggregate rather than a join across checkouts, payments, and participations.

#### Architectural Value

- Encapsulates payment invariants in one place.
- Makes provider switching feasible because the aggregate defines the universal payment state machine.
- Decouples programme activation from payment mechanics.

#### Problems It Solves

- Current `MemberPayment` has only `Pending` and `Confirmed`. There is no representation for refunded, partially refunded, disputed, or reversed payments.
- Refund logic today would require ad-hoc changes to `MemberPayment` and participation aggregates, risking inconsistency.
- Audit and reporting must reconstruct payment lifecycles from multiple sources.

#### Complexity Introduced

- A new aggregate with its own repository, configuration, and state machine.
- Migration of existing `MemberPayment` rows into the new aggregate (or a compatibility layer).
- Updated processors to work with the new aggregate while preserving backward compatibility.

#### When It Should Be Considered

When the business requires refunds, dispute handling, or multi-provider reconciliation. This is not needed for the current R1,200 AQGreen and R6,120 Onyx flows where every payment is a confirmed, final activation.

#### Currently Recommended?

**No.** The current `MemberPayment` is sufficient for the approved requirements. A `Payment` aggregate is a natural next step when refund or dispute workflows are required.

---

### Capability 2: Domain Events

#### Description

Introduce an internal domain-event bus so that side effects downstream of payment confirmation are decoupled from the confirmation processor. Example flow:

```
PaymentConfirmed
    ↓
ProgrammeActivated
    ↓
CommissionEligibilityUpdated
    ↓
NotificationQueued
    ↓
AnalyticsRecorded
```

Today these concerns are either inline in the processor or triggered separately by callers. An event-driven approach would let each concern subscribe independently.

#### Business Value

- New downstream effects (e.g., welcome email, welcome pack dispatch, CRM sync) can be added without modifying the payment processor.
- Reduces the risk that a non-payment concern (e.g., a notification outage) blocks payment confirmation.

#### Architectural Value

- Follows the Open/Closed Principle: the payment processor is closed for modification but open for extension.
- Makes the system easier to reason about because each listener has a single responsibility.
- Aligns with the existing ABP event-bus infrastructure already available in the project.

#### Problems It Solves

- Currently, `ProgrammePaymentConfirmationProcessor` directly calls `_participantRoleSynchronizer.PromoteGuestToMemberAsync`. If we later need to send a welcome email or create a commission eligibility record, that code would need to be added here or orchestrated by the caller.
- Tight coupling makes it harder to test the payment processor in isolation from its side effects.

#### Complexity Introduced

- Event schema versioning and backward compatibility.
- Ordering guarantees if downstream events have dependencies.
- Handling of failures in event handlers without breaking the payment transaction.

#### When It Should Be Considered

When the number of downstream side effects grows beyond 2-3, or when those side effects need to be developed, deployed, or monitored independently.

#### Currently Recommended?

**No.** The current explicit orchestration is simpler, easier to debug, and has fewer failure modes. Domain events become valuable when the coupling starts to hurt.

---

### Capability 3: Payment Timeline / Reconciliation Ledger

#### Description

Introduce a complete operational payment timeline that records every significant event in the payment journey:

- `CheckoutCreated`
- `CustomerRedirected`
- `WebhookReceived`
- `SignatureVerified`
- `PaymentConfirmed`
- `ParticipationActivated`
- `CommissionEligibilityCreated`

This could be implemented as an append-only ledger table or as a series of domain events projected into a read model.

#### Business Value

- Customer support can answer "what happened to my payment?" inseconds instead of requiring a developer to reconstruct state from multiple tables.
- Finance can reconcile provider payouts against internal records without guesswork.
- Auditors can see a chronological, tamper-evident payment history.

#### Architectural Value

- Makes the system observable.
- Provides a single source of truth for operational questions.
- Enables future automation of settlement and reconciliation.

#### Problems It Solves

- Today, reconstructing the payment journey requires joining `AQGreenJoiningCheckouts` or `DirectOnyxCheckoutIntents`, `MemberPayments`, and `EntryParticipations`/`OnyxParticipations`, and the timing of each transition is implicit.
- Successful Yoco events now have a durable receipt, but rejected and failed
  delivery attempts are not yet retained as a complete operational timeline.

#### Complexity Introduced

- A new table or event store with append-only semantics.
- Projection logic to keep the timeline current.
- Retention and archiving policy.

#### When It Should Be Considered

Once transaction volume reaches a level where operational questions about payment state become a regular burden on the engineering team. This is a "growing business" enhancement.

#### Currently Recommended?

**No.** This is an operational enhancement, not a production blocker. The current database schema already contains the source data needed; a timeline projection can be added later without schema changes to the core aggregates.

---

### Capability 4: Versioned Programme Pricing

#### Description

Formalize the concept of a programme pricing version that is immutable once assigned to a participation. The existing `EntryProgrammeTerms` already carries `Version` and `EffectiveFrom`, which is a good foundation. Future evolution would:

- Make `TermsVersion` a first-class domain concept with its own aggregate or lookup table.
- Ensure that price changes are deployed as new terms versions, never as mutations of historical versions.
- Guarantee that a participation always references the exact terms under which it was created.

#### Business Value

- Prevents accidental price changes from affecting historical participations.
- Enables A/B pricing experiments or seasonal pricing changes without data migration.
- Makes finance confident that historical revenue calculations are tied to immutable pricing.

#### Architectural Value

- Eliminates the risk of " schema drift " where a column like `JoiningPaymentAmount` is updated retroactively.
- Aligns with regulatory requirements for immutable pricing in some jurisdictions.

#### Problems It Solves

- The current migration `20260726162000_AddAQGreenSingleJoiningPayment` updates `JoiningPaymentAmount` and `TermsVersion` on existing unpaid rows. This is safe today because it is a one-time migration, but the pattern of mutating historical rows would be dangerous if repeated.
- If pricing changes again, there is no formal mechanism to prevent accidental updates to existing participations.

#### When It Should Be Considered

When the business introduces its second or third pricing change. The first change is always a migration; subsequent changes reveal whether the architecture handles versioning gracefully.

#### Currently Recommended?

**No.** The existing `TermsVersion` and `TermsEffectiveFrom` fields are sufficient for the current single-pricing-version scenario. Formal versioning should be introduced before the second major pricing change.

---

### Capability 5: Refunds

#### Description

Support for returning money to a customer after a confirmed payment. This includes:

- Full refunds: participation returns to its pre-payment state or is cancelled.
- Partial refunds: not applicable to the current R1,200 / R6,120 activation model, but may become relevant for future multi-component payments.
- Administrative refunds: initiated by support staff, not by the payment provider.
- Refund idempotency: a second request with the same reference must not create a second refund.

#### Business Value

- Enables customer service to resolve disputes without engineering intervention.
- Builds trust with customers who know they can get their money back if something goes wrong.
- Required for compliance with consumer protection regulations in some markets.

#### Architectural Value

- Extends the `Payment` aggregate (see Capability 1) into a full financial record.
- Makes the system suitable for e-commerce beyond pure membership activation.

#### Problems It Solves

- Currently, there is no way to represent a refund. A refund would require ad-hoc changes to `MemberPayment` and participation state, risking data corruption.
- Without a refund record, finance cannot reconcile provider payouts against internal records when money flows back.

#### Complexity Introduced

- State machine for payments (confirmed → partially_refunded → refunded).
- Accounting implications (when is revenue recognized vs. deferred?).
- Provider-specific refund APIs (Yoco refunds, settlement timelines, fees).
- Potential need to reverse participation activation or downgrade user roles.

#### When It Should Be Considered

When the business has a concrete refund policy and a measurable rate of refund requests. This is a "scale" stage capability.

#### Currently Recommended?

**No.** The current implementation correctly treats confirmed payments as final. Refunds should be designed deliberately when the business need is proven.

---

### Capability 6: Chargebacks / Disputes

#### Description

Handle payment reversals initiated by the cardholder through their bank. This is distinct from refunds because:

- The business does not initiate the reversal voluntarily.
- The payment provider notifies the platform after the fact (or through a separate dispute API).
- The money may be temporarily reversed while the dispute is investigated.

Future architecture would include:

- A `Dispute` aggregate linked to the original payment.
- Automatic or manual participation review when a disputed payment is reversed.
- Commission clawback evaluation if the disputed payment funded recruitment commissions.
- Audit trail for financial and legal review.

#### Business Value

- Protects revenue by giving operations a structured way to respond to disputes.
- Enables commission clawback when an activation payment is reversed.
- Provides legal and finance with the evidence trail needed for dispute resolution.

#### Architectural Value

- Separates dispute handling from the core payment lifecycle.
- Makes the platform compliant with card-network requirements for dispute management.

#### Problems It Solves

- Today, a chargeback would appear as an unexplained reversal in the provider dashboard with no internal record linking it to a participation or commission.
- There is no mechanism to freeze or review a participation while a dispute is pending.

#### Complexity Introduced

- Integration with provider dispute APIs.
- Business rules for participation state during a dispute.
- Commission reversal logic.
- Fraud detection integration possibilities.

#### When It Should Be Considerered

Once transaction volume makes disputes a regular operational event, or when compliance requirements demand formal dispute handling.

#### Currently Recommended?

**No.** Dispute handling is a mature-product concern. The current "confirmed = final" model is appropriate and reduces operational burden.

---

### Capability 7: Multi-Provider Support

#### Description

Abstract the provider-specific details (checkout creation, webhook verification, refund APIs, settlement formats) behind interfaces so that another provider (Ozow, PayFast, Stripe, Adyen) can be introduced without changing programme business logic.

#### Business Value

- Payment continuity if the primary provider has an outage.
- Access to better rates or features from competing providers.
- Geographic expansion into markets where Yoco is not available.

#### Architectural Value

- Provider isolation at the boundary, consistent with the existing separation of concerns.
- Ability to A/B test providers or route by programme/region.

#### Problems It Solves

- Today, `YocoCheckoutGateway`, `YocoWebhookSignatureVerifier`, and `YocoPaymentNotificationProcessor` are Yoco-specific. Adding a second provider would require duplicating the orchestration layer or creating an incomplete abstraction prematurely.
- If Yoco changes its API or pricing, the impact is spread across multiple classes.

#### Complexity Introduced

- Provider abstraction interfaces that are stable and well-designed.
- Webhook routing by provider (different endpoints, different signature schemes).
- Settlement and reconciliation differences between providers.
- Testing complexity: each provider needs its own fake/test double.

#### When It Should Be Considered

When the business has a concrete need for a second provider (e.g., international expansion, provider outage contingency plan, or significant cost savings from competition).

#### Currently Recommended?

**No.** Yoco-only is the correct current decision. Introducing provider abstractions now would be speculative. The existing code already has clean provider-specific classes that could be duplicated or abstracted later without affecting business logic.

---

### Capability 8: Operational Monitoring

#### Description

Build operational visibility into the payment platform:

- **Dashboard**: payment success rate, failure reasons, average confirmation latency, checkout abandonment rate.
- **Alerts**: webhook delivery failures, signature verification failures, provider API errors, unusual volume spikes.
- **Reconciliation reports**: daily/weekly comparison between internal confirmed payments and provider settlement files.
- **Webhook monitoring**: retry rate, duplicate detection rate, out-of-order delivery.

#### Business Value

- Reduces mean-time-to-detect for payment issues from hours to minutes.
- Gives finance independent visibility into cash flow.
- Builds operational confidence as transaction volume grows.

#### Architectural Value

- Makes the system observable without requiring log scraping.
- Enables proactive rather than reactive operations.

#### Problems It Solves

- Today, if a webhook fails silently or a checkout is abandoned, the only signal is a support ticket or a manual database query.
- There is no automated comparison between internal records and provider settlement data.

#### Complexity Introduced

- Dashboard infrastructure (could be as simple as structured logs + existing monitoring, or a dedicated observability platform).
- Alerting thresholds and on-call runbooks.
- Reconciliation report generation and discrepancy investigation workflow.

#### When It Should Be Considered

Once daily transaction volume justifies dedicated operational attention. Early-stage systems can be monitored manually; scale demands automation.

#### Currently Recommended?

**No.** The current implementation is correct and observable through logs and database queries. Operational dashboards become valuable once the volume of payments makes manual monitoring impractical.

---

## 4. Enterprise Readiness Roadmap

### Stage 1: Startup (Current)

**Focus:** Correctness and financial integrity for a single provider.

**Capabilities:**

- Hosted Yoco checkout lifecycle
- Verified webhooks with exact amount and currency validation
- Atomic programme activation
- AQGreen single R1,200 payment
- Onyx direct R6,120 payment
- Idempotent webhook processing
- Frontend with resume-payment support
- Admin visibility into confirmed payments and recruiter corrections

**Key Metrics:** Zero financial integrity incidents, 100% test coverage of payment paths, successful security review.

---

### Stage 2: Growing Business

**Focus:** Operational excellence and customer experience.

**Capabilities:**

- Payment timeline / reconciliation ledger (Capability 3)
- Operational monitoring dashboards (Capability 8)
- Refund workflows (Capability 5, if business demand exists)
- Enhanced admin tools for payment investigation

**Trigger:** Sustained growth in weekly active participants, or a measurable support burden from payment-related queries.

---

### Stage 3: Scale

**Focus:** Maintainability and extensibility as the codebase and team grow.

**Capabilities:**

- Payment aggregate (Capability 1)
- Internal domain events (Capability 2)
- Versioned programme pricing formalized (Capability 4)
- Advanced reporting and analytics

**Trigger:** Multiple programmes with differing payment structures, or a team size where explicit boundaries between concerns become necessary for parallel development.

---

### Stage 4: Enterprise

**Focus:** Resilience, compliance, and global expansion.

**Capabilities:**

- Multi-provider support (Capability 7)
- Chargeback / dispute management (Capability 6)
- Accounting-system integration
- Advanced reconciliation with automated settlement matching
- Compliance reporting (PCI-DSS scope reduction, audit trails)

**Trigger:** International expansion, provider diversification requirements, or regulation that mandates dispute handling and detailed audit trails.

---

## 5. Architecture Principles to Preserve

These principles must never be compromised, regardless of which future capabilities are introduced.

### Webhooks Remain the Payment Authority

No client-side event, no database flag, and no background timer can substitute for a verified provider webhook. Payment confirmation is an external fact, not an internal assumption.

### Frontend Never Confirms Payment

The frontend can request a checkout, redirect the customer, and display server-provided state. It never receives provider secrets, never validates signatures, and never records a payment as confirmed.

### Provider Secrets Remain Server-Side

API keys, webhook secrets, and other provider credentials must never be exposed to the client, logged in plain text, or embedded in frontend configuration.

### Payment Validation Remains Strict

Amounts, currencies, checkout IDs, and provider references must be validated exactly. Leniency in validation creates financial risk and debugging nightmares.

### Idempotency Remains Mandatory

Every webhook handler and every public endpoint that touches payment state must be safe to call multiple times. The system must treat duplicate delivery as a normal condition, not an error.

### Financial History Remains Immutable

Once a payment is confirmed and a participation activated, the historical record must not be altered. Corrections (e.g., recruiter placement) are additive audit events. Refunds and reversals are new records that reference the original, not mutations of it.

### Provider Logic Remains Isolated from Business Rules

The code that talks to Yoco must not contain programme activation logic. The code that activates a programme must not know which provider was used. This boundary allows the provider to change without rewriting business rules.

### Programme Rules Remain Separate from Payment Infrastructure

AQGreen, Onyx, and any future programme must define their own activation and qualification rules on top of the shared payment infrastructure. The payment layer must not contain programme-specific branching.

---

## 6. Recommendations

### Already Production Ready

The current implementation is production-ready for the approved business requirements:

- AQGreen single R1,200 joining payment
- Onyx direct R6,120 entry payment
- Hosted Yoco checkout lifecycle
- Verified webhook processing
- Idempotent activation
- Frontend with resume-payment support
- Admin reconciliation view
- Comprehensive test coverage

No changes are required before production deployment.

---

### Future Improvements

The capabilities described in Section 3 are legitimate architectural evolutions. They represent the natural maturation of a successful payment platform as operational complexity increases. None of them are urgent, and none of them should be started without a concrete business trigger.

Recommended sequencing:

1. **Payment timeline / reconciliation ledger** (growing business) — lowest complexity, highest operational value.
2. **Operational monitoring dashboards** (growing business) — enables proactive support.
3. **Refunds** (scale) — requires deliberate design; implement only when business demand is proven.
4. **Payment aggregate** (scale) — foundational for refunds, disputes, and multi-provider support.
5. **Domain events** (scale) — decouple downstream effects once the number of listeners exceeds 2-3.
6. **Versioned programme pricing formalized** (before second pricing change).
7. **Chargebacks / disputes** (enterprise) — compliance-driven.
8. **Multi-provider support** (enterprise) — only when a concrete second-provider need exists.

---

### Not Recommended Yet

- **Multi-provider abstraction now**: Premature. Yoco-only is simpler, more secure, and easier to debug.
- **Domain events now**: Adds complexity without current benefit. The explicit orchestration in `ProgrammePaymentConfirmationProcessor` is easier to trace and test.
- **Payment aggregate now**: Adds indirection without a current consumer. `MemberPayment` is sufficient for a two-programme, single-provider, no-refund system.
- **Refunds now**: No business requirement. Introducing refund logic before it is needed creates dead code and financial edge cases that must be maintained.

---

## Conclusion

The current payment architecture is sound, correct, and production-ready. The evolution path described in this document is designed to extend that foundation gently, in response to actual business needs rather than speculative architecture. The principles in Section 5 are the non-negotiables that every future capability must respect.
