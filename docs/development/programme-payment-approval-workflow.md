# Programme payment approval workflow

This document records the implemented AQGreen and direct-Onyx payment approval
boundary. Payment confirmation and programme activation are deliberately
separate facts.

## Execution map

1. The member UI requests a checkout through
   `ClubMemberProgrammeParticipationAppService`.
2. The member selects either one R1,200 AQGreen payment or two R600
   instalments. The app service persists `AQGreenJoiningCheckout` (including
   schedule and stage) or `DirectOnyxCheckoutIntent`, then the Yoco gateway
   creates the hosted checkout.
3. `YocoPaymentsController.WebhookAsync` verifies the exact request signature,
   timestamp, mode, event status, and safe payload shape.
4. `YocoPaymentNotificationProcessor` resolves the persisted checkout from the
   provider checkout identifier and records an idempotent `YocoWebhookReceipt`.
5. `ProgrammePaymentConfirmationProcessor`, in the same transactional unit of
   work, validates the checkout, amount, currency, provider reference, and
   payment purpose; persists one confirmed `MemberPayment`; completes the
   checkout; and moves the participation to
   `PaymentConfirmedAwaitingApproval`.
6. A completed AQGreen R1,200 joining obligation records one
   `AQGreenFuneralCoverEntitlement` with status `Included`. This is eligibility
   evidence only and does not represent insurer activation or enrolment.
7. `ProgrammeApprovalNotificationScheduler` resolves active, non-deleted users
   in the payment Area whose business role is `SystemAdmin` and whose effective
   permissions include `Aqua.Admin.ProgrammeParticipations.Approve`. It writes
   one protected transactional-outbox alert per authorised administrator. The
   participation state—not email—is the durable queue source.
8. The customer receives the existing payment-confirmed/awaiting-review email.
   `TransactionalEmailOutboxWorker` delivers both customer and administrator
   messages asynchronously.
9. `AdminProgrammeParticipationAppService` exposes an Area-scoped pending
   summary and server-filtered queue. `AdminSidebar` displays the global count;
   `AdminProgrammeParticipations` displays payment evidence and explicit
   approve/reject actions.
10. Approve/reject acquires a transaction-owned, per-participation database
    lock, verifies tenant and permission scope, records one append-only decision,
    and writes the customer outcome email in the same transaction. Approval
    changes the participation to `Active` and promotes an eligible Guest to
    Member. Rejection records the reason and leaves the customer role unchanged.

## Invariants and recovery

- Only authoritative provider confirmation can create confirmed payment state.
- Provider confirmation never changes a participation directly to `Active`.
- Only `Active` participations enter network qualification, commission, travel,
  or other Active-gated calculations.
- Duplicate webhooks reuse the receipt/payment and outbox idempotency keys.
- A unique decision index plus the transaction-owned lock prevents competing
  administrator outcomes. A repeated matching outcome succeeds without adding
  another audit record or email; a conflicting outcome fails.
- If no active authorised Area Administrator exists, a structured warning is
  logged and the participation remains in the durable queue.
- Email delay or failure does not remove or resolve a queue item.
- The decision-uniqueness migration refuses to proceed if historical duplicate
  decisions exist. Those rows require authorised reconciliation; migration must
  not silently discard audit evidence.

Recurring reminders and insurer-specific activation semantics are intentionally
outside this workflow. The unresolved AQGreen monthly-obligation due-date policy
also remains separate: joining approval neither creates nor settles a monthly
obligation.

## Historical funeral-cover inclusion

The repository can prove a modern AQGreen joining completion from the
participation's payment links and the linked confirmed `MemberPayment` facts.
The R30,000 Aqua inclusion was part of the AQGreen promise before this software
implementation. `AddAQGreenFuneralCoverEntitlements` therefore backfills a
modern in-system participation only when its linked payment records prove one
confirmed R1,200 AQGreen joining payment or two distinct confirmed R600 AQGreen
joining payments. `IncludedAt` is the full payment confirmation time or the
later instalment confirmation time. Current `Active` status, approval time,
audit timestamps, and migration execution time are never used as substitutes.

Incomplete payments are left untouched. Contradictory modern completion data,
including payment/customer/tenant mismatch or reuse of one instalment payment
for both slots, stops the migration for authorised reconciliation.

Payment completion earns the Aqua inclusion before the Area Administrator's
programme decision. Approval is not an entitlement trigger, and a later
programme rejection does not delete the already-earned inclusion.

The configured `2026-07-26` lower bound is inherited from the modern AQGreen
joining terms. It is not an insurer activation date, external cover start date,
funeral-cover promise inception date, or software deployment cutover.

Existing legacy members who predate the application onboarding lifecycle and
do not yet have authoritative participation/payment records are not migration
candidates. A future `LegacyAQGreenMemberImport`-style workflow must record
their identity, participation, entitlement facts, known inclusion date and
source evidence, authorising operator, and import timestamp. It must not create
fake payments to make legacy members resemble modern customers. Designing that
authorised import and any insurer/provider integration is outside this branch.

Rolling `AddAQGreenFuneralCoverEntitlements` back deletes the entitlement table
and every entitlement recorded after deployment. That rollback is destructive
by design; production rollback requires a verified backup/database restoration
or reviewed forward remediation rather than synthetic historical reconstruction.
