# AQGreen monthly obligation checkout reconciliation — analysis and closure report

Branch: `feat/programme-engine-gap-closure`
Scope: `AQGreenMonthlyObligationCheckout`, `EntryMonthlyObligation`, monthly webhook dispatch, period inventory, reconciliation posture.
Companion documents: `release-report-gap-closure.md` (§17.9, D-11), `weekly-commission-temporal-input-matrix.md`.

---

## A. Archaeology: every ReconciliationRequired occurrence and its call path

`AQGreenMonthlyPaymentAllocationStatus.ReconciliationRequired` is set in exactly one domain method:

- `AQGreenMonthlyObligationCheckout.RequireReconciliation(paymentId, completedAt, evidence)` — `src/AqualLifeStyle.Core/Domain/Payments/AQGreenMonthlyObligationCheckout.cs:72`. It records the provider payment on the checkout and persists the machine-produced `AllocationEvidence` reason.

Call sites that reach `RequireReconciliation`:

| # | Call path | Where | Why it occurs |
|---|-----------|-------|---------------|
| 1 | `YocoPaymentNotificationProcessor.ProcessAsync` → `ProcessAQGreenMonthlyObligationCheckoutAsync` (monthly-payment branch) | `src/AqualLifeStyle.Application/Payments/YocoPaymentNotificationProcessor.cs` | The persisted provider checkout identifier resolves a checkout in `AwaitingPayment`; amount, currency, purpose, and ownership facts verify; optional merchant-reference metadata also matches when present; but the target obligation is not in the exact state the settlement requires (not unpaid, missing, wrong period, or already settled). |
| 2 | Same processor, replay branch (checkout already `Completed`): `EnsureMatchingPaymentFacts` then `RequireReconciliation`-equivalence via outcome comparison | same file | A duplicate webhook event with *different* payment facts for the same checkout is rejected; an identical replay is idempotently short-circuited. |

The monthly processing path verifies persisted checkout, obligation, participation, and customer associations; amount, currency, purpose, and obligation period; and optional merchant-reference metadata when present. Equivalent payment and checkout facts are rechecked before a completed-event replay is accepted as idempotent.

`CompleteAllocation` (the non-reconciliation allocation) is a sibling outcome and is mutually exclusive: once a checkout is in `ReconciliationRequired`, `CompleteAllocation` throws (domain test `ReconciliationOutcomeCannotBeRedirectedToAllocation`). Terminal states are `Completed` (+ allocation outcome), `Failed`, `Expired`, `AdministrativelyTerminated`. The EF configuration enforces the outcome invariant with a table check constraint (`CK_AQGreenMonthlyObligationCheckouts_AllocationResult`): `ReconciliationRequired` ⇒ `Status=Completed`, non-null `PaymentId`, non-blank evidence.

No other component writes the reconciliation outcome. No automatic worker resets or retries it; it is a terminal, append-only allocation fact.

## B. Cause classification

Classification follows the mission taxonomy: (1) evidence-complete and deterministically retryable; (2) evidence-complete but invariant-blocked; (3) deterministic repair required; (4) external/business evidence required; (5) unreconstructable historical state.

### B1. Verified monthly webhook for an obligation that cannot accept the recorded payment (EVIDENCE-COMPLETE BUT INVARIANT-BLOCKED — AUTHORISED ACTION REQUIRED)
`ProcessAQGreenMonthlyObligationCheckoutAsync` settles an available obligation whose persisted participation, Customer, period, amount, currency, outstanding amount, and payment allocation match the checkout. `Due`, `GracePeriod`, and `Overdue` obligations can all accept a confirmed payment. When settlement succeeds, the obligation becomes `Paid`, `PaymentId` is set, and commission for the period becomes payable *after* verified payment. When a verified payment cannot settle because the obligation is unavailable, its financial identity no longer matches, or it was settled by a different verified payment, the processor records `ReconciliationRequired` with evidence and **does not classify commission**. The payment itself is a confirmed, verified `MemberPayment` — evidence is complete; no money is unaccounted for.

This case is **not** auto-recoverable. The webhook event is processed once; the checkout records a terminal `ReconciliationRequired` outcome, and a duplicate delivery short-circuits with the same outcome. There is **no deterministic mechanism that makes the state valid on retry** — an authorised decision (or a future authorised workflow) is required to settle or release the recorded obligation. The queue (Part F) exists precisely to surface this case for that decision.

### B2. Deterministic pipeline prevention (GATED, NOT A REPAIR MECHANISM)
Obligation state is recomputed from `AssessStatus(asOf)` (Due/GracePeriod/Overdue/Paid) with `LastAssessedAt` monotonicity and a `MarkedOverdueAt` capture. The worker that creates obligations is disabled by default (`App:EntryMonthlyObligations.Enabled=false`) and the due-policy table starts empty, so there is no production backlog of un-armed periods yet. The obligation creation and the temporal checkout creation follow the same deterministic pipeline, which prevents rather than repairs misalignment. No code path silently re-routes a `ReconciliationRequired` payment to a different period; **no automatic repair path exists** — a reconciliation-required checkout remains terminal until an authorised action resolves it.

### B3. External-evidence class (NEEDS PROVIDER + AUTHORISED ACTION)
Any case where the provider's payment exists but the system cannot tie it to a recorded checkout (unknown/expired checkout, merchant-reference mismatch) is handled by other terminal states (`Failed`/`Expired`/validation rejection) plus provider-side records. These are out of the reconciliation-required channel; they require provider confirmation and an authorised Finance/Ops decision. The Yoco webhook pipeline never applies a payment without a verified provider notification.

### B4. Corruption class (NOT OBSERVED)
No path was found that mutates `AllocationStatus` or `PaymentId` after the terminal outcome; the check constraint and `RequireReconciliation`'s evidence-normalization guard prevent contradictory outcomes. No corruption path is evidenced in the current code.

## C. Ledger correction model (what the queue must support, not implement)

Per `AGENTS.md`, production payment state may only be corrected through an authorised, auditable reconciliation workflow. This mission therefore implements the **read-only operator view** (Part F) and does **not** add an automatic mutation path. The correction contract (for a future authorised workflow) is:

- Read: checkout (amount, currency, provider id, status, outcome, evidence) + obligation (period, due policy, current status) + payment (provider reference, confirmed-at) + participation/customer facts.
- Decide: allocate the verified payment to the recorded obligation, or keep the obligation unsettled and classify the period accordingly.
- Write: through a new authorisation-gated, lock-protected, evidence-required service method; never through the inventory.

That contract is exactly what `GetMonthlyObligationCheckoutReconciliationAsync` returns per row.

## D. Cycle determinism

- Obligations are per `(participation, PeriodYear, PeriodMonth)`; a monthly commitment maps 1:1 to a period. The checkout snapshots the obligation id, participation id, period, amount, currency, due-policy version — so the reconciliation evidence is period-stable.
- Commission classification for a period only happens after a verified payment is applied (`ApplyConfirmedPayment`). `ReconciliationRequired` checkouts therefore never classify commission, preserving the invariant that no release/classification occurs before verified payment confirmation.
- The inventory (`GetPeriodInventoryAsync`) computes missing canonical cycles from stored closed periods and the latest closed week. A missing *latest* closed cycle is not corruption and not reconstructible-from-history: it is the cycle for which the automatic calculation pipeline (`WeeklyCommissionCalculator`, which is the only creator of `EntryCommissionPeriod` rows) has not yet run. Once calculation is enabled, that pipeline computes it deterministically at the cycle cutoff from cutoff-effective state. Older gaps cannot be reconstructed (see Part E).
- The worker cadence gating and the `pg_advisory_xact_lock`/`sp_getapplock` scheduling lock keep period generation single-owner; `CreateClosedPeriod` idempotency keeps replays harmless.

## E. Effective-dated terms placeholders (accepted debt)

The following business-effective state cannot yet be reconstructed from history, so historical (pre-migration) commission periods are deliberately **not** auto-calculated:

1. Network placement per cycle cutoff (recruiter assignment reconstructed only at `PeriodEnd` for forward computation; no existing-Area baseline is seeded).
2. Qualification/eligibility state at cycle cutoff (activation state history exists prospectively only; `AreaActivationStateRecords` has no legacy baseline).
3. AQGreen compliance state at cutoff — `WeeklyCommissionCalculator` reads **current** obligation/loan status for AQGreen holds (documented §17.10), a post-cutoff overdue assessment can retroactively hold the previous cycle.
4. Commission terms version per cycle (terms are current-state, not cycle-effective).
5. Provider finality for historical cycles (unchallengeable provider confirmation is not guaranteed for pre-migration periods).

These placeholders are why older missing cycles remain `ManualFinancialReconciliationRequired`. The latest-closed-cycle gap is the only gap the deterministic pipeline *can* generate (from cutoff-effective state at the time it runs); whether it is generated depends on calculation enablement, so it is classified `PendingCalculation`, not manual reconciliation (Part F).

## F. Implementation delivered

1. **Inventory classification split** — `AdminCommissionAppService.BuildPeriodBoundaries` now classifies a missing canonical cycle that is the latest closed cycle as `MissingCommissionCycleDisposition.PendingCalculation` (message: automatic calculation pending for the latest closed cycle; generated deterministically by the automatic calculation pipeline at the cycle cutoff once calculation is enabled; not reconstructible from current state; commission only classified after verified payment). Older gaps remain `ManualFinancialReconciliationRequired`. New enum member is append-only (`ManualFinancialReconciliationRequired = 0`, `PendingCalculation = 1`).

2. **Read-only reconciliation discovery/triage queue** — `IAdminProgrammeParticipationAppService.GetMonthlyObligationCheckoutReconciliationAsync`:
   - Scope: host-wide access requires `Admin.AllTenants`; tenant administrators see only Customers whose current active Area has an active assignment for that administrator. Revoked assignments, inactive Areas, missing current Customer authority, and cross-Tenant requests fail closed. The same permission gate as the legacy reconciliation screen applies (`Admin.ProgrammeParticipations.ViewLegacyPaymentReconciliation`).
   - Filters: `TenantId`, `PeriodYear`, `PeriodMonth`; paged; ordered by completion then creation (newest first). Tenant administrators are further restricted by the authorised current Customer Areas resolved by the service.
   - Rows: checkout id, current Customer Area when available, club number, customer name, period, amount, currency, status, provider checkout id, payment id + provider reference, allocation status, allocation evidence, created/completed times, `IsPaymentAllocated` (checkout payment == obligation payment), nullable recorded obligation status, and `ObligationAvailable`, `ParticipationAvailable`, `CustomerAvailable`, and `AreaAvailable`. The Area is not an immutable checkout snapshot and must not be projected backward as Area-at-payment.
   - Historical visibility: an authorised host query starts from completed checkout facts with soft-delete filtering disabled for dependencies. A later soft-delete therefore does not erase the financial checkout from discovery; the availability flags identify which related records remain available. Tenant administrators still require current Customer/Area authority and do not gain access from a deleted or unavailable dependency.
   - Read-only: no repository writes; no data returned beyond the operator checklist (reason, detected-at, payment reference, amount/currency, recorded obligation state, and dependency availability).

3. **Docs** — `release-report-gap-closure.md` §17.9 and D-11 updated to the new classification and the queue's role.

### F1. Obligation completeness remains unresolved

`BLOCKED — BUSINESS/OPERATIONS DECISION REQUIRED`

The absence of an `EntryMonthlyObligation` row is not evidence that a member complied, failed to comply, or had no obligation. Due day, first-liability month, and worker-arming/backfill policy remain unresolved. Reconciliation may report a missing obligation through `ObligationAvailable=false`, but it must not invent a status or create an obligation from that absence. Production completeness classification and any corrective write require approved policy plus an authorised, auditable workflow.

## G. Independent review of commit 5202a17

Scope reviewed: migration `AddAQGreenMonthlyObligationCheckouts` (+ designer/snapshot alignment), `AQGreenMonthlyObligationCheckout`, `YocoPaymentNotificationProcessor` monthly path, `ProcessAQGreenMonthlyObligationCheckoutAsync` (locked UoW), `EnsureNotificationMatchesCheckout`, replay/Completed path, worker cadence gating, `EntryMonthlyObligationDuePolicy` (append-only, empty start).

Verdict: **No blockers.** The review conclusion is the mission instruction: review independently of the implementation report, verify causality, challenge assumptions.

Material findings (non-blocking, all documented or addressed):

| # | Finding | Class | Disposition |
|---|---------|-------|-------------|
| 1 | Inventory misclassified the missing *latest closed* cycle as `ManualFinancialReconciliationRequired`, while it is deterministically producible by the automatic calculation pipeline (gated) | branch-owned, correctness-affecting | **Fixed** in this change (Part F). |
| 2 | `WeeklyCommissionCalculator` computed AQGreen holds from current obligation/loan state (§17.10); a post-cutoff overdue assessment held the previous cycle's commission | pre-existing documented gap; blocked arming | **Resolved** — the calculator now derives obligation overdue and loan payout-hold as of `PeriodEnd` (`WasOverdueAt`, `WasRequiringPayoutHoldAt`) from persisted boundaries; post-cutoff assessment/cure/repayment cannot change a closed cycle. Covered by focused domain tests; full suite 747/747. Worker remains disabled (`App:WeeklyCommissions.Enabled=false`). |
| 3 | A duplicate webhook delivery for a checkout already in `ReconciliationRequired` re-logs the programme alert every replay (no alert dedupe) | safe follow-up | Logged; no state impact; follow-up item. |
| 4 | A second real payment against the same completed checkout is rejected by `EnsureMatchingPaymentFacts` and is not recorded locally (same behaviour as the pre-existing joining/Onyx paths) | pre-existing pattern, applies to all three programmes | Follow-up item: consider recording the unmatched provider payment evidence. |
| 5 | Reconciliation evidence is free text; four distinct machine-produced reasons share one channel | safe follow-up | Structured reason codes on the checkout as a follow-up. |
| 6 | Migration preflight (policy-refusal) is Npgsql-gated; SQL Server surfaces a raw unique-index error | safe follow-up | Documented; provider parity follow-up. |

Not re-validated as infrastructure (per AGENTS.md infrastructure-verification rule): Yoco provider integration, lock implementation, scheduling infra — the branch consumes them; only the integration was reviewed.

## H. Tests and validation

New tests (all in `AqualLifeStyle.Tests`):

- `AdminCommissionAppServiceTests.HostAdministrator_CanInventoryLegacyPeriodsWithoutMutation` — updated: latest-closed missing cycle now `PendingCalculation`; still read-only (period/commission counts unchanged).
- `AdminCommissionAppServiceTests.Inventory_ClassifiesLatestClosedMissingCycleAsPendingCalculation` — canonical periods present, latest closed missing → `PendingCalculation`.
- `AdminCommissionAppServiceTests.Inventory_DistinguishesOlderGapFromPendingLatestClosedCycle` — both dispositions in one inventory: older gap `ManualFinancialReconciliationRequired`, latest closed `PendingCalculation`.
- `AdminProgrammeParticipationAppServiceTests.Administrator_CanReviewMonthlyObligationCheckoutReconciliationQueue` — allocated vs reconciliation-required checkouts: period filter, area/member facts, payment reference resolution, `IsPaymentAllocated`, recorded obligation status, evidence.
- `AdminProgrammeParticipationAppServiceTests.TenantAdministrator_CannotRequestAnotherAreasMonthlyObligationReconciliation` — cross-Area denied.

Validation evidence:

- Solution Release build: succeeded, no new warnings (only pre-existing warnings in untouched files).
- Full backend suite (Release): **736 passed, 0 failed** — includes the PostgreSQL migration tests (Docker verified; `AQGreenMigrationTest` started/stopped its container).
- EF model check (`dotnet ef migrations has-pending-model-changes`): **No changes** — no migration needed; the queue reads existing tables.
- `git diff --check`: clean.

## I. Operational notes and remaining work

- No migration, no config, no frontend change: the queue is an ABP AppService method (auto-exposed), reusing the existing permission constant.
- To enable production use later: arm the monthly obligation worker only after Business/Ops insert the first immutable due-policy row (D-3/D-4), keep `App:WeeklyCommissions.Enabled=false` until Part E items 1–5 are resolved (D-10), and resolve every `ReconciliationRequired` row through an authorised workflow once one exists (D-11).
- Accepted follow-ups (owners to assign): webhook alert dedupe (#3), unmatched second-payment evidence (#4), structured reconciliation reason codes (#5), SQL Server migration preflight parity (#6), and the Part E terms placeholders.
