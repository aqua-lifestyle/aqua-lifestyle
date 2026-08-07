# Aqua Programme Engine — End-to-End Verification Report

Verification mission: "Verify the Aqua Programme Engine End-to-End"
Branch: `verify/programme-engine` (worktree, tracks `origin/main`, clean baseline HEAD `e371f85`); gap closure on `feat/programme-engine-gap-closure`
Date: 2026-08-07

Companion deliverable: `docs/verification/business-rule-matrix.md` (46 rules classified Confirmed/Provisional/Contradictory/Undefined with `file:line` evidence and Product Decisions). Three confirmed Branch-Purpose Gaps (M-15, M-46, M-28/M-44/M-45) are implemented on `feat/programme-engine-gap-closure`.

---

## 1. Purpose reconstructed

The business outcome is a production-grade AQGreen + Onyx programme engine in which money movement, network qualification, commissions, administrator approval, and monthly obligations are correct, idempotent, concurrency-safe, and auditable, consistent with the confirmed business rules. The engine must never activate a programme, settle an obligation, release an entitlement, or make a commission available before a verified payment confirmation; and programme active must never be confused with payment confirmed.

Confirmed scope (from mission brief): AQGreen joining (R1,200 single payment, with the historical two-installment path preserved), funeral cover, administrator approval gate, monthly R600 subscription, recruitment tree, qualification boundaries (4 vs 5), commission engine (earned vs payable vs paid, withholding/release), commission ledger transparency, member dashboard/journey/education, admin portal + email, authorization/Area isolation, audit, a large deterministic simulation (3,906 participants), and concurrency.

Explicit exclusions: BusinessPremier deprecation (documented, out of scope), deferred savings scope in the older `MissionPlan.md` (superseded).

---

## 2. Acceptance criteria and results

| # | Criterion | Status | Evidence | Strength |
|---|-----------|--------|----------|----------|
| AC-1 | AQGreen joining = one verified R1,200 payment; full and preserved two-installment paths converge to the same activation gate | Met | `EntryParticipation.cs:155-286`; `ClubMemberProgrammeParticipationAppService.cs:256-266`; `AQGreenCheckout_ActivatesOnlyAfterOneVerifiedTwelveHundredRandPayment` | Integration + AT |
| AC-2 | Funeral cover (R30,000, 6-month waiting, external insurer) explicitly absent, not silently dropped | Met (as documented gap → now implemented on gap-closure) | FR-44 `requirements.md:86` ❌; A-21; roadmap; no production references | Inspection → AT after M-46 |
| AC-3 | Payment confirmed ≠ programme active; only administrator approval activates | Met | Status presenter; `EntryParticipation.cs:288-299`; PR #50; admin queue | Integration + AT |
| AC-4 | Monthly R600 commitment is separate from joining; domain enforced, scheduling deferred | Met (domain); gap (scheduling → now implemented on gap-closure) | `EntryMonthlyObligation.cs`; `onyx-implementation-plan.md:189-190`; M-15 | AT + Inspection → AT after M-15 |
| AC-5 | Tree 5/25/125/625/3125; 5-per-level boundary; 4 recruits earn nothing | Met | `EntryNetworkQualificationEvaluator`; `OnyxNetworkQualificationEvaluator`; boundary tests | AT |
| AC-6 | Commission amounts match confirmed rates; holds enforced; release/pay idempotent; host-only permissions | Met | `CurrentCommissionTermsProvider.cs`; weekly commission tests; `AdminCommissionAppService` | AT + IT |
| AC-7 | Member commission ledger transparency | Not met — Undefined (→ now implemented on gap-closure as M-28/M-44/M-45) | No member ledger endpoint/component | Inspection → AT after gap-closure |
| AC-8 | Member dashboard/journey; admin portal + email; education | Met except education (Undefined → now implemented on gap-closure) | `member-programmes.tsx`; admin components; email outbox + idempotency | AT + IT |
| AC-9 | Area (tenant) isolation enforced backend and frontend | Met | `AdminAppServiceBase`; cross-tenant tests | Integration |
| AC-10 | Audit records who/what/from/to/when/why, stored append-only | Met (stored); surfaced-to-admin Undefined | Approval decisions, corrections, graduations, receipts | Inspection |
| AC-11 | 3,906-participant deterministic simulation | Met | New `OnyxProgrammeEngineSimulationTests` (2 tests, ~6.7 s) | Automated + repeated |
| AC-12 | Concurrency: unique constraints, idempotency, serializable approval, stale-work recovery | Met | EF configurations; hosted advisory locks; `ProgrammePaymentConfirmationProcessor` recovery paths | IT + Inspection |

## 3. Evidence baseline (final, re-run after changes)

- Backend Release build: **0 errors**. Full backend suite (final re-run): **664 (Application) + 40 (Web.Tests) = 704 passed, 0 failed, 0 skipped** (TRX `backend-tests.trx`, `webtests.trx`). Build warnings: 2035 total, all pre-existing categories (CS1591 dominant, plus NU1902 AngleSharp in Web.Tests).
- EF model check with compatible `dotnet-ef` **8.0.8**: "No changes have been made to the model since the last migration."
- Frontend (final re-run): **ESLint clean, `tsc --noEmit` clean, 377 Vitest tests passed (107 files)**. One intermittent pre-existing flake in `member-programmes.test.tsx` (untouched by the branch; see release report §9, AF-01).
- NuGet vulnerabilities: pre-existing ABP transitives only (unchanged from baseline).

## 4. Engineering defects found and corrected

| # | Defect | Root cause | Fix | Regression evidence |
|---|--------|-----------|-----|---------------------|
| D-01 | Flaky frontend test `lists confirmed payments awaiting Area approval and approves one` | The stat card renders the static text "Awaiting Area approval" immediately, so `findByText` resolved before the `setTimeout(0)`-scheduled `loadParticipations` finished; `getByRole("button", {name:"Approve"})` then ran against a still-loading table | Test now waits on the Approve button itself (`findByRole`) before asserting the status text and enabled state | Reproduced 5×, fixed, then **10/10 consecutive deterministic passes** and full suite green |

No production-code defect was reproduced in any phase. No business-rule change was made. The fix is test-only (smallest complete change).

## 5. Findings and classification

### Blocking
None.

### Accepted debt / required confidence (do not delay the engine, but require decisions)

| # | Finding | Class | Owner |
|---|---------|-------|-------|
| AD-01 | Automatic R600 obligation scheduling and payment allocation had no production caller (only domain + tests), deferred per `onyx-implementation-plan.md:189-190`. **Closed on `feat/programme-engine-gap-closure` (M-15).** | Branch-Purpose Gap (closed) | AqualLifeStyle |
| AD-02 | Funeral cover R30,000 had no requirement/implementation. **Closed on `feat/programme-engine-gap-closure` (M-46): cover recorded as included/eligible after R1,200 joining completion; activation/enrolment timing remains PD-06.** | Branch-Purpose Gap (closed) | Business (PD-06) |
| AD-03 | No member-facing commission ledger or programme education content. **Closed on `feat/programme-engine-gap-closure` (M-28/M-44/M-45).** | Branch-Purpose Gap (closed) | AqualLifeStyle |
| AD-04 | Audit history is stored append-only but not surfaced to administrators. | Unresolved decision (PD-05) | Product |
| AD-05 | No explicit parallel-duplicate webhook delivery test (protection exists via `pg_advisory_xact_lock` + unique EventId index + `DbUpdateException` recovery). | Accepted debt — test coverage improvement | Engineering |
| AD-06 | Webhook endpoint has no rate limiting. | Accepted debt — hardening candidate (P-01) | Engineering |

### Improvements (out of branch scope)
- Consider exposing audit-history read endpoints once PD-05 is decided.

## 6. Conflation guard compliance

Verified that the codebase keeps distinct: Referral (placement), Qualification (structural level), Commission Earned, Commission Released/Payable, Commission Paid, Programme Active, Payment Confirmed, Administrator Approved. No code path conflates them (status presenter, payout status enum, separate approval gate).

## 7. Security review summary

- Yoco webhook: anonymous by design; HMAC verified before any state change; exact amount/currency; provider+reference uniqueness; idempotent replay rejection; no secrets or raw bodies logged; processing transaction-safe. No rate limiting (AD-06).
- Payments applied only after authoritative provider confirmation; activation only via administrator approval.
- Area isolation enforced at tenant level; host callers need `AllTenants` permission.
- No bypass, privilege escalation, stale endpoint, or information-leakage defect found.

## 8. Remaining risks

1. Recurring R600 obligation creation is implemented on `feat/programme-engine-gap-closure` (M-15); ongoing assessment of due/grace/overdue status and allocation of confirmed payments into obligations is production-supported but relies on the worker being deployed and enabled (config-gated). Payment allocation for `EntryMonthlyCommitment` payments follows the established confirmed-payment path.
2. Funeral cover is now recorded as included/eligible after R1,200 joining completion (M-46). The commercial risk shifts to PD-06: activation/enrolment timing and effective date are still unresolved and must not be presented as "active".
3. Commission holds currently keyed to overdue own obligations / loans; the upline effect of an overdue member is unresolved (business decision).
4. Webhook rate limiting and an explicit parallel-replay test would strengthen hardening (accepted debt).

## 9. Final verdict

### Implementation
**Meets purpose** — for the confirmed scope. All implemented business rules behave correctly. The three confirmed Branch-Purpose Gaps (M-15 monthly obligation creation, M-46 funeral-cover inclusion, M-28/M-44/M-45 member commission transparency/education) are implemented on `feat/programme-engine-gap-closure` with regression tests. The remaining open items are explicit product decisions (PD-02, PD-05, PD-06), not implemented bugs.

### Evidence
**Sufficient for merge** — for the engine's implemented surface, including the gap-closure features, with the accepted debts above owned and tracked. (Product Decisions PD-02/PD-05/PD-06 must be scheduled; they do not invalidate the implemented engine.)

### Operational state
**CI green** (backend suite + gap-closure regression tests, frontend suite, EF model clean, lint + type-check clean). Final release validation and verdict are in `docs/verification/release-report-gap-closure.md`.

---

## 10. Deliverables produced

- `docs/verification/business-rule-matrix.md` — 46 rules classified with evidence; Product Decisions updated (PD-06 added; PD-01/03/04 promoted to Branch-Purpose Gaps).
- `docs/verification/verification-report.md` — this report.
- `aspnet-core/test/AqualLifeStyle.Tests/Domain/OnyxProgrammeEngineSimulationTests.cs` — deterministic 3,906-participant simulation (qualification by depth, commission bounded, aggregate R353,402.50).
- Fixed flaky frontend test `AdminProgrammeParticipations.test.tsx`.
- `feat/programme-engine-gap-closure`: M-15 monthly obligation creation, M-46 funeral-cover inclusion/eligibility, M-28/M-44/M-45 member commission progress + education + payout explanations, each with regression tests.

## 11. Commits

Two atomic commits, each independently validated:
1. `test(programme-engine): add deterministic 3,906-participant Onyx simulation`
2. `test(frontend): de-flake awaiting-approval admin test`
Plus the two documentation deliverables (can be a third commit: `docs: add programme-engine business rule matrix and verification report`).

Gap closure on `feat/programme-engine-gap-closure`:
1. `docs: record programme-engine gap closure baseline` (reclassifications + PD-06)
2. `feat(aqgreen): implement recurring obligation model`
3. `feat(aqgreen): implement funeral-cover inclusion`
4. `feat(member): expose commission progress and payout explanations`
5. Final verification re-run and verdict update.
