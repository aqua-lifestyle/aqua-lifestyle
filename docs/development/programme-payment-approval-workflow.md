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

## Test Mode and isolated qualification

The execution map above describes business payment confirmation. In Yoco Test
Mode, a valid signed notification returns successfully **before** receipt,
`MemberPayment`, participation, or administrator-notification persistence.
`BusinessPaymentApplied=false` therefore does not exercise steps 5–10. A missing
administrator alert from that test is expected; production Test Mode must not be
changed to create business payments for qualification.

`PaymentAreaApprovalQualificationTests` reuses the PostgreSQL web-test host and
original frontend to qualify those subsequent steps with disposable records.
It first verifies the signed Test Mode no-write boundary, then explicitly invokes
the existing confirmation processor from the test assembly. This confirmation is
synthetic test authority, not evidence of provider settlement. There is no product
endpoint or runtime flag that bypasses Test Mode.

Prerequisites: repository .NET 8 toolchain, Node 24, lockfile-installed frontend
dependencies, local Chrome (`npx playwright install chrome` if unavailable), and
a newly created empty disposable PostgreSQL database. Schema is created from the
EF model; this qualification does not test migrations. Reserve loopback ports
3310 (frontend) and 3320 (test-host bridge). Never reuse an earlier fixture or its
`.browser-complete` marker. Use a new restricted output directory for each run.

Run this opt-in qualification only against that disposable database:

- `PAYMENT_APPROVAL_QUALIFICATION=true`, `REPRO_PG=true`,
  `REPRO_PG_SCHEMA_FROM_MODEL=true`, and `REPRO_TRANSACTIONAL=true`.
- `REPRO_PG_CONNECTION` must use `Host=127.0.0.1` and a database name starting
  `aqgreen_approval_qualification_`. The host rejects other connections before
  database initialization. Never supply a production connection.
- `PAYMENT_APPROVAL_FIXTURE_OUTPUT` is an absolute path inside a restricted,
  non-Git evidence directory. The fixture contains synthetic sign-in credentials
  with a randomly generated per-process password and is written with
  owner-only permissions. Create the containing directory with mode 0700 and use
  `umask 077` before starting either process. Do not publish fixtures, browser
  artifacts, raw logs, provider receipts or database connection settings.
- Start the original Next.js app on `127.0.0.1:3310` with its server-side
  `NEXT_PUBLIC_ABP_API_URL=http://127.0.0.1:3320`, default workspace `Default`, and
  an isolated `NEXTAUTH_SECRET`. Verify the actual server process configuration;
  browser request interception alone cannot constrain the server-side proxy.
  Use `npm run dev -- --hostname 127.0.0.1 --port 3310`; append `--webpack` when
  local dependencies are symlinked outside the worktree (Turbopack rejects that
  layout). No frontend build from a different API origin may be reused.
- From the backend directory, run the following in one terminal (with the above
  environment supplied securely). The test exposes the real test server through a
  loopback-only bridge, writes the fixture, and waits for the browser result.

```bash
dotnet test test/AqualLifeStyle.Web.Tests/AqualLifeStyle.Web.Tests.csproj \
  --configuration Release \
  --filter FullyQualifiedName~PaymentAreaApprovalQualificationTests
```

After the fixture exists, run the browser in a second terminal from the frontend
directory, with the same `PAYMENT_APPROVAL_FIXTURE_OUTPUT` value:

```bash
npx --no-install playwright test --config playwright.payment-approval.config.ts
```

It uses real authentication, application responses and PostgreSQL, and exercises
the existing dashboard, queue, badge, payment details, approval, rejection, replay,
cross-Area/Tenant denial, and host read-only permissions. Both processes must pass;
a skipped opt-in test or test discovery is not qualification evidence. The browser
fixture exercises Onyx; existing AQGreen service regressions are separate evidence.

External checkout transport alone is substituted; it makes no provider request.
Email delivery is optional and must be authorized separately: supply
`PAYMENT_APPROVAL_BIRD_SEND=true`, the approved
`PAYMENT_APPROVAL_EMAIL_RECIPIENT`, and secured `PAYMENT_APPROVAL_BIRD_KEY`,
`PAYMENT_APPROVAL_BIRD_FROM`, `PAYMENT_APPROVAL_BIRD_NAME`, and
`PAYMENT_APPROVAL_BIRD_REPLY` as applicable. The existing outbox processor and
Bird adapter deliver only the first explicitly identified administrator alert.
The qualification gateway denies every other reference, recipient, and repeated
send. Without those settings, assert outbox generation but report actual delivery
as unverified. Provider acceptance alone is not proof of delivery; retain a
restricted provider delivery receipt independently.

After execution, retain sanitized results outside Git and stop the owned frontend
and disposable database. Passing isolated qualification does not authorize
production payments, approvals, B6 reconciliation or D10 commissioning.

## Owner-assisted production dashboard check

Use the owner's already authenticated session at the existing production site.
Successful administrator login is already owner-confirmed. The agent environment
has no connection to that session. No credentials, session cookies, exported
browser state or member/payment data are required. Return only PASS, blocked or
unavailable, aggregate counts, and non-sensitive permission/navigation labels.

| Existing page | Read-only observations needed |
| --- | --- |
| `/admin/dashboard` | Confirm the original Administration overview renders. Record visible/enabled sidebar destinations and any recovery/unavailable notice. Record the Programme participation approval badge if present; absence alone does not establish zero pending approvals. |
| `/admin/members` | Confirm access with `Aqua.Admin.Members.View`, whether existing Tenant 1 records load, and any load/permission error. This page loads at most 100 records; its displayed count is not a full inventory. The Area column renders `tenantId`, so “Area 1” does not establish business-Area ownership or Area isolation. |
| `/admin/programme-participations` | On both AQGreen and Onyx tabs, inspect All records and Awaiting approval. Record whether existing records load, displayed Area labels match the operator's known scope, and status/payment evidence is present. Compare each tab's approval summary with the queue and sidebar; reload once and return to the tab. Do not copy identities, amounts, references or timestamps. |
| Same participation page | Observe whether Approve/Reject controls or the read-only explanation appear. This observes presentation only: `Aqua.Admin.ProgrammeParticipations.View` and `.Approve` are distinct permissions, and the backend remains authoritative. Do not open/submit a decision or attempt an unauthorized write. |
| `/admin/users` and `/admin/access-levels`, only if already authorized | Inspect the signed-in operator's existing role/permission presentation (`Aqua.Admin.Users.View` / `Pages.Roles`). Confirm Tenant 1 and assigned business Areas using existing displayed evidence or the owner's established assignment records. Do not edit grants or export a user list. If the current screen cannot establish assignments, record them as unavailable. |

For Tenant/Area visibility, the owner should compare a known in-scope record and,
only where the operator is already authorized to perform the comparison, a known
out-of-scope record. Report only the scoped outcome. Do not enumerate other
Tenants, switch to unapproved identities, or infer isolation from an empty list.
If no suitable existing records exist, record that check as unavailable.

The existing notification presentation is the Programme participation sidebar
badge, per-programme approval summary, awaiting queue and status details. The
queue is durable authority; Bird email only draws attention to it. A blank badge
or `—` summary can reflect unavailable data. No separate notification center is
established by this qualification.

**REPOSITORY EVIDENCE:** `AdminMember.GetAll` currently enforces Tenant filtering
without an Area-assignment predicate. Together with the Tenant-as-Area display,
this leaves general member Area visibility unqualified. This predates the test
branch; the isolated participation authorization PASS must not be extended to it.
Report the gap for scoped commissioning review; this tests/documentation change
does not repair it or assert actual production exposure.

The smallest next step is this read-only walkthrough and a scope decision on that
gap. Actual production provider-to-administrator approval remains UNVERIFIED and
requires a separately authorized acceptance exercise with genuine provider
confirmation. Separate isolated/Bird and production Test Mode results cannot be
combined into that claim. No payment replay, production record/configuration
mutation, financial operation, commission/monthly automation, D10 change or
deployment is authorized by this checklist.

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
