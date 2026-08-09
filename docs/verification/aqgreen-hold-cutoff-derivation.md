# AQGreen commission holds — cutoff-effective obligation and loan evaluation

Mission report for the correction of the weekly commission hold gap on
`feat/programme-engine-gap-closure`.

## 1. Purpose reconstructed

### Problem
`EntryWeeklyCommissionCalculator` evaluated the AQGreen payout holds against the
obligation's and loan's **current** state at calculation time, not the state that
existed at the cycle cutoff (`PeriodEnd`). Two confirmed defects:

1. An obligation assessed overdue after the cutoff (e.g., an August obligation
   assessed in August) held commission for a July cycle that closed weeks before
   the obligation was even due.
2. A payment or loan repayment confirmed after the cutoff could remove the hold
   from an already-closed cycle on replay, because the current status had
   changed.

Both violate the closed-cycle financial invariant: a closed cycle's result must
be deterministic regardless of post-cutoff mutations.

### Confirmed rule
The temporal matrix row 121/125/126/127 semantics (and regression cases 3, 4 in
the matrix) are the confirmed business decisions: evaluate obligation overdue
and loan payout-hold **as of the cycle cutoff** from persisted boundaries, with
a boundary-inclusive cutoff (a fact occurring exactly at `PeriodEnd` counts for
the cycle).

### Scope
- `EntryWeeklyCommissionCalculator`: replace the two current-state reads with
  cutoff derivations.
- Domain: add the as-of derivations to `EntryMonthlyObligation` and
  `OnyxLoanAgreement`.
- Tests: focused boundary and replay tests; correct one application test whose
  scenario encoded the old current-state defect.
- Docs: temporal matrix, gap-closure release report, reconciliation report.

Explicit exclusions (owned by other missions): due-policy launch evidence,
provider occurrence/finality, checkout flow, legacy loan allocation evidence,
terms selection, Area baselines.

## 2. Implementation

### Domain derivations
- `EntryMonthlyObligation.WasOverdueAt(DateTime cutoffUtc)` — overdue iff the
  cutoff is after the persisted `GracePeriodEndsAt` and no payment confirms
  settled the obligation at or before the cutoff. Exact-cutoff facts are
  inclusive (`>=`/`<=`), matching the calculator's `ActivatedAt <= PeriodEnd`
  convention. Never falls back to the current `Status` enum, which remains a
  mutable projection for display and the monthly workflow.
- `OnyxLoanAgreement.WasRequiringPayoutHoldAt(DateTime cutoffUtc)` — true iff a
  weekly requirement was due at or before the cutoff without a satisfaction
  confirmed at or before the cutoff, or the repayment deadline passed the
  cutoff with outstanding balance. Requires the agreement to be effective at
  the cutoff (`EffectiveAt <= cutoff`). Uses persisted
  `DueAt`/`SatisfiedAt`/`PaymentConfirmedAt` boundaries, never current
  requirement/agreement status.

### Calculator
`EntryWeeklyCommissionCalculator` (domain) now evaluates
`obligation.WasOverdueAt(period.PeriodEnd)` and
`agreement.WasRequiringPayoutHoldAt(period.PeriodEnd)`; hold reasons and
messages are unchanged. No other call sites consumed the changed paths; the
`Status`/`RequiresPayoutHold` current-state properties remain for the monthly
and admin workflows.

### Changed files
- `src/AqualLifeStyle.Core/Domain/Onyx/EntryMonthlyObligation.cs`
- `src/AqualLifeStyle.Core/Domain/Onyx/OnyxLoanAgreement.cs`
- `src/AqualLifeStyle.Core/Domain/Onyx/EntryWeeklyCommissionCalculator.cs`
- `test/AqualLifeStyle.Tests/Domain/EntryWeeklyCommissionTests.cs` (10 new
  tests)
- `test/AqualLifeStyle.Tests/Application/ClubMemberProgrammeProgressAppServiceTests.cs`
  (scenario correction)
- `docs/verification/weekly-commission-temporal-input-matrix.md`
- `docs/verification/release-report-gap-closure.md`
- `docs/verification/monthly-obligation-checkout-reconciliation.md`

No migration, no configuration, no API contract change.

## 3. Acceptance criteria and results

| Criterion | Status | Evidence |
|-----------|--------|----------|
| Eligible at cutoff, overdue afterward → Earned, replay-stable | **Met** | `EligibleAtCutoff_BecomingOverdueAfterCutoff_KeepsHistoricalCycleEarned` |
| Overdue at cutoff, paid afterward → Held, replay-stable | **Met** | `OverdueAtCutoff_PaidAfterCutoff_HistoricalCycleRemainsHeld` |
| Paid before cutoff → Earned | **Met** | `ObligationPaidBeforeCutoff_HistoricalCycleEligible` |
| Paid exactly at cutoff → Earned (inclusive) | **Met** | `ObligationPaidExactlyAtCutoff_HistoricalCycleEligible_InclusiveBoundary` |
| Grace ending exactly at cutoff → not overdue | **Met** | `GracePeriodEndingExactlyAtCutoff_IsNotOverdue_ForThatCycle` |
| Grace ending before cutoff → overdue | **Met** | `GracePeriodEndingBeforeCutoff_IsOverdue_ForThatCycle` |
| No fallback to current assessment state | **Met** | `ObligationStanding_IsIndependentOfAssessmentState_NoCurrentStateFallback` |
| Loan not effective at cutoff → Earned | **Met** | `LoanNotEffectiveAtCutoff_HoldsNoHistoricalCommission` |
| Loan requirement overdue at cutoff, repaid after → Held, replay-stable | **Met** | `LoanRequirementOverdueAtCutoff_RepaidAfterCutoff_HistoricalCycleRemainsHeld` |
| Loan requirement repaid before cutoff → Earned | **Met** | `LoanRequirementRepaidBeforeCutoff_HistoricalCycleEligible` |
| Agreement-level overdue (requirements satisfied, deadline passed) → Held | **Met** | `LoanAgreementOverdueAtCutoff_HoldsPayout_WhenRequirementsSatisfiedButDeadlinePassed` |
| Existing application test no longer encodes current-state defect | **Met** | `ClubMemberProgrammeProgressAppServiceTests` corrected scenario; 4/4 pass |

## 4. Regression review

- Full Release solution build: 0 errors; only pre-existing warnings.
- Full backend suite (Release): **747 passed / 0 failed** — includes the
  PostgreSQL migration tests (Docker verified).
- The only failing test found by the full run was
  `ClubMemberProgrammeProgressAppServiceTests.HeldCommission_ReportsHoldReasonAndOverdueObligation`.
  Causality: the scenario's commission cycle closed 2026-07-12, but its
  obligation was due 2026-08-01 (grace ends 2026-08-08) — the cycle closed
  before the obligation was due, so it could only be "Held" via the defective
  current-state read. Corrected the scenario to make the obligation genuinely
  overdue at cutoff (due `EffectiveFrom + 1 day`, grace ends before cutoff);
  the asserted progress-report behaviour (hold reason, next action, amounts) is
  unchanged.
- No persistence, API, authorization, frontend, or worker contract changed.

## 5. Security and time/state review

- The calculator is a deterministic domain function of persisted timestamps;
  no clock reads were introduced (cutoff is the period's own `PeriodEnd`).
- Current-status properties remain projections; nothing grants them new
  authority. Derivation never weakens a hold based on missing rows: absent
  obligation evidence means no hold from that input, exactly as before — the
  completeness evidence items remain owned by the due-policy/provider missions.
- No secrets, logging, or external calls involved.

## 6. Validation evidence

| Check | Result |
|-------|--------|
| `dotnet build AqualLifeStyle.sln -c Release` | Succeeded, 0 errors, no new warnings |
| Full suite (Release) | 747/747 passed (incl. PostgreSQL migration tests) |
| Domain-focused run | 234/234 passed |
| `EntryWeeklyCommissionTests` | 20/20 passed (10 new) |
| `git diff --check` | Clean (below) |

## 7. Remaining work

### Accepted debt / out of scope (unchanged, documented owners)
- Due-policy launch evidence and operational launch month (Business/Ops, D-3/D-4).
- Provider payment-success occurrence and delivery-completeness evidence.
- Obligation-linked checkout persistence; unlinked confirmed payments require
  authorised reconciliation.
- Immutable loan allocation decision time for legacy rows.
- Cycle-effective terms registry and authoritative Friday boundaries.
- Existing-Area activation baseline rollout.
- The weekly worker remains disabled (`App:WeeklyCommissions.Enabled=false`);
  release verdict for production arming is unchanged (see §19 of the
  gap-closure report).

### Validation gaps
- The agreement-overdue branch is exercised only through a far-future period
  (deadline is months out); an end-to-end journey for that branch is not
  practical at weekly-cycle scale and is covered by the focused domain test.
- No new integration/E2E test over the full worker pipeline; the calculator is
  exercised through the application tests that invoke it directly.

## 8. Final verdict

### Implementation
**Meets purpose.** Both confirmed defects are removed; holds are derived from
persisted boundaries as of `PeriodEnd`, inclusive, replay-stable, with no
current-state fallback, and the loan branch includes agreement-effective-date
and deadline semantics.

### Evidence
**Sufficient for merge.** Full Release suite 747/747 after the correction,
focused boundary/replay tests for every confirmed rule, clean diff checks.

### Operational state
No CI run (consistent with the branch's local-validation practice documented in
the gap-closure report). Production arming remains blocked by the unchanged
items in section 7.
