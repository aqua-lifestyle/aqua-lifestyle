# Release Report — Programme Engine Gap Closure (`feat/programme-engine-gap-closure`)

Release role: Release Engineer — final validation pass
Branch: `feat/programme-engine-gap-closure` (verification update based on `ab7187c` plus the current weekly-engine changes)
Base: merge-base `e371f85`; current work extends the post-integration programme engine
Date: 2026-08-08
Companion docs: `docs/verification/business-rule-matrix.md`, `docs/verification/verification-report.md`

---

## 1. Purpose reconstructed

### Problem
Three confirmed branch-purpose gaps in the AQGreen/Onyx programme engine had no production implementation:
- **M-15**: automatic recurring R600 monthly obligation scheduling existed only as domain + tests, with no production caller. The prior oldest-open payment allocation heuristic was not authoritative.
- **M-46**: the R30,000 funeral-cover benefit had no requirement/implementation; only product-decision notes.
- **M-28 / M-44 / M-45**: no member-facing commission ledger, payout explanation, or programme education content existed.

### Business outcome
A member can see a truthful, read-only explanation of their AQGreen position (network level, weekly earnings with payout status, monthly subscription, funeral-cover inclusion, and how the programme works), while the engine reliably schedules monthly obligations and records funeral-cover inclusion once and only once per participation after the verified R1,200 joining obligation is complete.

### Confirmed scope (this branch)
- M-15 recurring obligation engine: scheduler, due-date policy, advisory-lock, config-gated worker.
- M-46 funeral-cover inclusion: entitlement aggregate, inclusion processor, payment-confirmation wiring, migration.
- M-28/M-44/M-45 member progress: `GetMyProgressAsync` app service + DTOs, frontend page/hook, navbar link, tests.
- Automatic Friday–Thursday calculation for both AQGreen and Onyx, with travel eligibility and financial settlement kept independent.

### Explicit exclusions
- PD-06 (funeral-cover activation/enrolment timing and effective date) — unresolved business decision, **not** encoded as active.
- Initial monthly due-policy version/day/effective month — intentionally not supplied; the confirmed durable policy model fails closed while empty.
- PD-02, PD-05 — pre-existing unresolved decisions, out of branch scope.
- Webhook rate limiting (M-35/AD-06), parallel-duplicate webhook test (AD-05) — pre-existing accepted debt.

---

## 2. Branch state

- **Integrated baseline:** merge commit `43d968d` (parents `724c9fc` branch, `32f02a4` origin/main), followed by the programme-engine verification and weekly-engine commits/changes documented in §17.
- No upstream configured (`git push --dry-run`/PR not performed; not requested).
- Pre-merge commits (`origin/main..HEAD`, 8): `724c9fc` docs: finalize release readiness documentation; `4e15a28` feat(member): expose commission progress and payout explanations; `9fb1baf` feat(aqgreen): implement funeral-cover inclusion; `e17f3c6` feat(aqgreen): implement recurring obligation model; `e6b6302` docs: record programme-engine gap closure baseline; `34e38d6` docs: add programme-engine business rule matrix and verification report; `4abcdc1` test(frontend): de-flake awaiting-approval admin test; `655b7f9` test(programmes): add deterministic 3,906-participant Onyx simulation.
- Commits integrated from `origin/main` (9): CI workflow additions (PostgreSQL transactional gate, Render deploy gate, manual dispatch), and `b4f0de7` frontend admin-approval async-race fix.
- Pre-merge diff vs merge-base: 40 files, +9024/−4. Merge changed only: `.github/workflows/ci.yml`, `AdminProgrammeParticipations.test.tsx` (conflict resolved, see §4a), `InternalAccountInvitationEndToEndTests.cs`. **No production Programme Engine file was altered by integration.**

---

## 3. Acceptance criteria and results

| # | Criterion | Status | Evidence strength |
|---|-----------|--------|-------------------|
| AC-1 | M-15 scheduler creates one obligation per active participation per period; repeated runs idempotent | **Met** | Automated component test (`ActiveParticipant_GetsOneObligationPerPeriod_RepeatedRunsAreIdempotent`), unique index `IX_EntryMonthlyObligations_EntryParticipationId_PeriodYear_PeriodMonth` |
| AC-2 | M-15 confirmed monthly payment settles only its server-linked obligation | **Not implemented; safe state** | The oldest-open heuristic was removed. No recurring checkout currently persists authoritative `ObligationId` and period evidence, so unlinked confirmed payments require authorised financial reconciliation. |
| AC-3 | M-15 overdue assessment respects due + grace period, marks overdue, holds own payout | **Met** | Automated test `UnpaidObligations_AreAssessed_IntoOverdue`; `AssessStatus` transitions |
| AC-4 | M-15 due-date policy never invents due dates; worker gated by config, defaults disabled | **Met** | Durable empty-by-default policy table + fail-closed resolver tests; `App:EntryMonthlyObligations:Enabled=false` and no configured due-day fallback |
| AC-5 | M-46 funeral cover recorded once per participation after joining satisfied (full or two-installment) | **Met** | Automated tests (`EnsureIncludedAsync_IsIdempotent_AndGrantsOnce`, two-installment test), unique index on `EntryParticipationId` |
| AC-6 | M-46 never encodes insurance activation or waiting period (PD-06) | **Met** | Inspection: single `Included` enum value; domain doc comments |
| AC-7 | M-28/M-44/M-45 member can read level, direct-recruit progress, weekly earning components/status/hold reason, monthly obligation status, funeral-cover inclusion, education | **Met** | Automated tests (`ClubMemberProgrammeProgressAppServiceTests` 4, frontend `member-programme-progress.test.tsx` 3, `use-my-programme-progress.test.ts`) |
| AC-8 | Read-only service; no member mutation path | **Met** | Inspection: service exposes only `GetMyProgressAsync`; no state-changing method |
| AC-9 | Authorization: `Aqua.ProgrammeParticipations.ViewSelf` on backend method and frontend nav/page | **Met** | Inspection + frontend permission test |
| AC-10 | Tenant isolation preserved (customer resolved via tenant + session user) | **Met** | Inspection + existing tenant-filter architecture |
| AC-11 | No N+1 in progress endpoint | **Met** | Inspection: single `LoadPeriodsAsync` dictionary lookup; bounded queries |
| AC-12 | Migration Up/Down safe; snapshot synchronized; no pending model changes | **Met** | `dotnet-ef has-pending-model-changes` → "No changes"; migration Up/Down inspected |

---

## 4. Implementation review (M-15 / M-46 / M-28 / M-44 / M-45)

### M-15 — recurring obligation engine
- `EntryMonthlyObligationScheduler` (`Application/EntryMonthlyObligations/EntryMonthlyObligationScheduler.cs`): two idempotent operations — `EnsureObligationsForPeriodAsync` and `AssessObligationsAsync`. Each disables the tenant filter and works across tenants because `EntryMonthlyObligation` carries `TenantId`/`CustomerId`. `SaveChanges` occurs only when work was done. **No duplicate period obligation** is backed by the DB unique index. Payment application is intentionally absent until checkout persists one authoritative `ObligationId`; the scheduler never guesses a target month.
- `PersistedEntryMonthlyObligationDueDatePolicy`: selects exactly one latest applicable host policy version. Missing, ambiguous, or invalid evidence returns an explicit failure result, logs `aqgreen_monthly_due_policy_unresolved`, and creates no obligation. Due dates are day 1–28 at 00:00 `Africa/Johannesburg`, converted to UTC.
- `EntryMonthlyObligationWorker` (`Web.Host/ProgrammeEngine/`): config-gated (`Enabled=false` default), 60-min interval, acquires the scheduling lock inside the UoW, then creates and assesses obligations, and logs structured `ProgrammeEngineAlert` events. Errors are rethrown for operational visibility. It never guesses payment allocation.
- `EntryMonthlyObligationSchedulingLock`: `pg_advisory_xact_lock` on PostgreSQL / `sp_getapplock` on SQL Server; transaction-scoped, released on commit/rollback. Prevents cross-instance double scheduling.
- Assessment: `AssessStatus(asOf)` is deterministic; prevents reassessment at an earlier time; sets `MarkedOverdueAt` once.
- `EntryMonthlyObligation.ApplyConfirmedPayment` validates confirmed status, tenant/customer/currency/amount, idempotent on same `PaymentId`.
- Domain check `IsQualifiedForNetwork` (Active) guards obligation creation.

### M-46 — funeral-cover inclusion
- `AQGreenFuneralCoverEntitlement` (`Core/Domain/Onyx`): `FullAuditedAggregateRoot<Guid>, IMustHaveTenant`; **private setters**; factory `GrantForJoiningCompletion` guards: participation not null, `IsJoiningObligationSatisfied`, `includedAt` within terms window. Records amount, currency, `TermsVersion`, `IncludedAt`, `Status=Included`. Single status value — no activation/waiting-period encoding (PD-06 respected).
- `AQGreenFuneralCoverInclusionProcessor.EnsureIncludedAsync`: idempotent — returns existing entitlement if present; otherwise validates terms effective date and inserts. Invoked from both AQGreen payment-confirmation paths (full and two-installment stage 2).
- Payment-confirmation wiring (`ProgrammePaymentConfirmationProcessor`): `ApplyFuneralCoverInclusionIfCompletedAsync` runs after `ApplyConfirmedJoiningPayment`, before `checkout.Complete`, inside the same UoW as the confirmed payment — so inclusion can never precede verified payment confirmation.
- Concurrency note (verified final): the DB unique index on `EntryParticipationId` is the authoritative correctness guard. The insertion itself is not wrapped in a `DbUpdateException` recovery path (unlike the payment insert at `ProgrammePaymentConfirmationProcessor.cs:441-452`), but this is a defence-in-depth idempotency improvement, **not** a correctness requirement:
  - The live webhook path (`YocoPaymentNotificationProcessor.ProcessAsync`) acquires the transaction-scoped `HostedPaymentCheckoutLock.AcquireCheckoutAsync` (pg_advisory_xact_lock keyed on the checkout ID) at `YocoPaymentNotificationProcessor.cs:129` **before** the receipt idempotency check (`:133-150`) and before the confirmation processor runs. Two concurrent duplicate deliveries of the same checkout therefore serialise on the advisory lock; the second re-resolves the checkout, finds the existing receipt, verifies matching facts, and returns without reaching the inclusion insert.
  - The only other call path (`ProgrammePaymentConfirmationProcessor.ProcessAsync(ConfirmedProgrammePayment)` at `:482`, reaching `ApplyFuneralCoverInclusionIfCompletedAsync` at `:569`) has **no callers in `src`** — it is a reconciliation-oriented API with zero production invocations today.
  - Even in an un-serialised race the unique index admits exactly one entitlement row; the loser rolls back its whole UoW and returns 500, after which Yoco's retry succeeds idempotently. **No data-integrity defect** — state converges correctly. A `DbUpdateException` recovery mirror (AF-02) would only spare a single wasted 500+retry cycle; explicit parallel-replay test remains AD-05 follow-up (AF-03).
- Migration `20260807065821_AddAQGreenFuneralCoverEntitlements`: creates the table with FKs to `Customers` and `EntryParticipations` (Restrict), unique index on `EntryParticipationId`, supporting indexes; Down drops cleanly. Snapshot synchronized.

### M-28 / M-44 / M-45 — member progress + education
- `ClubMemberProgrammeProgressAppService.GetMyProgressAsync` (`Application/ProgrammeParticipations/`): `[Audited]`, class `[AbpAuthorize]`, method `[AbpAuthorize(Aqua.ProgrammeParticipations.ViewSelf)]`. Resolves tenant from session, customer by `TenantId + UserId`, requires active customer. Loads participation, active participations (for qualification), commissions with components, commission periods (single query → dictionary), obligations, funeral cover. Level evaluated via `EntryNetworkQualificationEvaluator` (5/25/125). Read-only.
- DTOs (`ProgrammeProgressDtos.cs`): `MyProgrammeProgressDto` with `RecentEarnings` (up to 12, `MaxRecentEarnings`), status labels via `CommissionPayoutStatusPresenter`, `NextAction` guidance, `Education` items (4 static items).
- Frontend: `app/member/programme-progress/page.tsx` (metadata + component), `member-programme-progress.tsx` (permission gate, skeleton, level card + progress bar, earnings table, monthly subscription card, funeral-cover card, education), `use-my-programme-progress.ts` hook, `programme-progress.ts` domain types, `endpoints.ts` `getMyProgress`, navbar link "AQGreen progress" gated by `Aqua.ProgrammeParticipations.ViewSelf`.

---

## 4a. Post-integration: `origin/main` merge and conflict resolution

### Merge performed
- Fetched `origin/main` (`git fetch --prune origin`) and merged into the branch (**not** rebased; no commits rewritten).
- Merge commit `43d968d` with parents `724c9fc` (branch) and `32f02a4` (origin/main).
- Merge brought in 9 `origin/main` commits: CI workflow additions (PostgreSQL transactional gate `98e2c31`, Render deploy gate `d599627`/`0c2e0e4`, manual dispatch `61eeb6f`, PR merges `32f02a4`/`dadc1fa`) and the frontend admin-approval async-race fix `b4f0de7`.

### Conflict
- Single content conflict in `aqua-frontend/src/components/admin/AdminProgrammeParticipations.test.tsx`.
- Both sides had independently de-flaked the same test (`lists confirmed payments awaiting Area approval and approves one`):
  - Branch `4abcdc1`: `findByRole("button", { name: "Approve" })` + `getAllByText("Awaiting Area approval").length > 0`.
  - Main `b4f0de7`: `findByRole("button", { name: "Approve" })` + `getByText("Awaiting Area approval", { selector: "span" })`.

### Resolution rationale
Adopted main's version (the merged tree is byte-identical to `origin/main` for this file):
- **Synchronization**: both fixes await the `Approve` button via `findByRole` — the element that only appears after the async participation table renders. This removes the original race where the always-rendered stat-card heading satisfied the wait early.
- **Status assertion**: main's `getByText("Awaiting Area approval", { selector: "span" })` is strictly stronger than the branch's `getAllByText(...).length > 0`. The always-rendered heading is a `<p>` (AdminProgrammeParticipations.tsx:598); the table status badge is a `<span>` (badge.tsx:30). `selector: "span"` therefore targets only the rendered row badge, so the assertion cannot be satisfied by the always-rendered heading.
- **Enabled assertion**: `expect(approveButton).toBeEnabled()` retained on both sides.
- **No sleeps/timeouts, no weakened assertions**, no production code changed to resolve the conflict.

### Verification of the resolution
- Focused test `AdminProgrammeParticipations.test.tsx` ran **10/10 consecutive times, 8/8 tests passed each run**.
- Full frontend suite passed (107 files, 377 tests) with the resolution in place.

---

## 5. Security review

- **Backend authoritative; frontend not trusted**: the progress page never renders anything not returned by `GetMyProgressAsync`; permission checks happen server-side via `[AbpAuthorize]`.
- **Authorization**: `ViewSelf` is granted only to Club Members with the permission; no elevation path; admin permission not reused.
- **Tenant isolation**: customer + participation data scoped by tenant via session; `GetRequiredTenantId` requires a tenant context; cross-tenant access not possible from a tenant session.
- **Payments**: funeral-cover inclusion runs only after a confirmed (provider-verified) joining payment inside the same UoW. No amount/currency leniency (`EnsureExactAmount`).
- **Idempotency/replay**: payment uniqueness `(Provider, ExternalReference)`; entitlement uniqueness per participation; webhook replay rejection via existing Yoco machinery.
- **Secrets/logging**: no secrets, tokens, raw webhook bodies, customer data, or payment details logged in the new code; `ProgrammeEngineAlert` logs are structured and non-sensitive.
- **No new attack surface**: the only new HTTP-visible endpoint is `GetMyProgress` (authenticated, authorized, read-only).
- **No race/rollback failure found** that can corrupt state: unique indexes + idempotent domain methods converge on retry.

Evidence type: inspection + integration tests (existing webhook/payment tests) + authorization attribute review. Confidence: **High** for authorization, tenant isolation, and the funeral-cover inclusion race (the live webhook path is serialised by the checkout advisory lock plus receipt dedupe before the insert, and the DB unique index is the authoritative guard; see §4 M-46 concurrency note).

---

## 6. Regression review

Every change reviewed for breakage of existing behaviour:
- **Auth**: no permission/role changes; new permission usage is additive (`ViewSelf` already existed).
- **Authorization**: `AquaPermissions` unchanged; service added, not modified.
- **Persistence/migrations**: new table only; `Restrict` FKs; `has-pending-model-changes` clean; snapshot aligned.
- **API contracts**: `GetMyProgress` is a new endpoint; existing ABP services untouched except constructor injection added to `ProgrammePaymentConfirmationProcessor` (wired via ABP IoC; existing tests still pass).
- **Frontend contracts**: new nav link + new page; existing member pages unchanged. Navbar test still passes.
- **Concurrency/background jobs**: new worker is disabled by default; only armed via explicit config. Scheduling lock prevents duplicate execution.
- **Messaging/email**: untouched.
- **Caching**: untouched.
- **Deployment**: appsettings addition is additive; worker requires `Enabled=true` to run.
- **Full suites re-run (post-merge, on the integrated tree)**: backend 704 passed (Application 664 + Web.Tests 40), frontend 377 passed (including the previously flaky `member-programmes.test.tsx` in this run). No regression introduced.

---

## 7. Time and state review

- All timestamps are UTC-typed (`DateTimeKind.Utc` validated in scheduler/policy; domain `AssessStatus` rejects earlier reassessment).
- `IncludedAt`/`paidAt`/`dueAt` are explicit, never invented. Monthly due-policy resolution returns a typed missing/ambiguous/invalid result rather than guessing.
- No global normalizers or compatibility switches introduced.
- `MarkedOverdueAt ??= asOf` keeps first-overdue time.

---

## 8. Performance review

`GetMyProgressAsync` performs a bounded, fixed number of queries:
1. customer by tenant+user
2. participation by customer
3. active participations (tenant-scoped, for qualification)
4. commissions with components (ordered desc)
5. commission periods (single `IN` query → `Dictionary<Guid,EntryCommissionPeriod>`)
6. obligations (ordered desc)
7. funeral cover (single row)

The `RecentEarnings` mapping uses `periods[commission.CommissionPeriodId]` — a dictionary lookup, **not** a query → no N+1. `CommissionPeriodId` is a non-nullable `Guid` set at creation, so no missing-key risk. Worst-case tenant-wide active-participation load for qualification is proportional to active participants; acceptable at current scale, noted as a scale follow-up if the tree grows large. Scheduler/worker are batch-oriented and save only on change.

---

## 9. Frontend suite: flaky test investigation

### Observation
A full-suite run reported `1 failed / 376 passed`. The failing test was `src/components/members/member-programmes.test.tsx > MemberProgrammes > blocks payment actions when the API payment contract is incompatible` — `Unable to find an accessible element with the role "button" and name "Join AQGreen"`.

### Investigation
- The test file, the `member-programmes.tsx` component, and the `use-my-programme-participations.ts` hook are all **untouched by this branch** (verified via `git diff origin/main...HEAD`).
- Root cause is a timing race inside the test: `await screen.findByText(/cannot verify a compatible payment API/i)` resolves on the first render because the health-incompatibility message renders synchronously, but the "Join AQGreen" button only renders after the `setTimeout(0)`-scheduled `reload()` plus the async `httpClient.get` completes. Under parallel suite load the macrotask can be delayed, so the subsequent synchronous `getByRole("button", { name: "Join AQGreen" })` runs against a still-loading view.
- Reproduction: across this session the failure was observed twice in full-suite runs (once captured at 13:43, once in the prior run), with 5 clean full-suite runs interleaved, and failed 1/8 when the file was run in isolation. It is a genuine intermittent flake, not a deterministic regression.
- The branch introduced the same class of race in its own new test `member-programmes.test.tsx`? **No** — the branch's own tests (`member-programme-progress.test.tsx`, `use-my-programme-progress.test.ts`) use `findByRole`/`findByText` on loaded data and passed 5× consecutively when re-run.

### Classification
- **Pre-existing flaky test**, not branch-introduced, not branch-exposed. The branch did not modify the test, the component, or the hook.
- Per AGENTS.md change policy (only modify branch for verified branch-owned defects), the test is **not** corrected on this branch.

### Verdict impact
- Does **not** block merge: it is pre-existing, intermittent, and unrelated to branch code. It is recorded as **accepted follow-up debt** (owner: engineering) — the test should use `findByRole` on the "Join AQGreen" button (matching the already-de-flaked `AdminProgrammeParticipations.test.tsx` pattern) so it awaits the loaded state instead of racing the `setTimeout(0)`.

---

## 10. Repository health checks

- `git diff origin/main...HEAD --check`: **clean** (no whitespace errors).
- Secret scan of the full branch diff: no credentials/API keys/private keys. Hits were ABP framework schema columns (`PasswordResetCode`, `UserToken`, `Secret` concurrency token) and the existing documentation mention of HMAC-gated webhooks.
- TODO/FIXME/HACK/XXX scan: none added.
- Debug instrumentation scan (`console.log`, `debugger`, `Debug.WriteLine`, `alert(`): none added.
- Commented-out-code scan: only XML doc comments; no dead code blocks.
- Build warnings: 2035 total, **0 errors**; the overwhelming majority are pre-existing `CS1591` missing-XML-comment (codebase-wide pattern), plus pre-existing `CS8632/CS1570/CS1998/CS0168/CS0618/CS0162/xUnit2020` and the known `NU1902` AngleSharp (Web.Tests, moderate, pre-existing). New branch files add only `CS1591` entries consistent with the existing convention; no new warning categories introduced.

---

## 11. Documentation review

- `docs/verification/business-rule-matrix.md`: M-15/M-28/M-44/M-45/M-46 statuses reflect implementation; PD-06 remains unresolved, while monthly due semantics are confirmed and the initial policy row remains intentionally absent.
- `docs/verification/verification-report.md`: accurate for its baseline; evidence numbers there predate the post-integration re-run, which this report supersedes.
- Appsettings now documents the obligation worker config block with defaults (`Enabled=false`), matching operational reality.
- No stale "not implemented" statements remain about M-15/M-46/M-28/44/45.

---

## 12. PD-06 / PD-07 status verification

- **PD-06 (funeral-cover activation/enrolment timing and effective date)**: still an **unresolved business decision**. Verified not encoded: `AQGreenFuneralCoverStatus` has a single value `Included = 0`; domain XML docs explicitly forbid encoding activation; frontend text says activation/waiting period/claims are handled by the external insurer. No code path presents the benefit as active.
- **Monthly due-date policy**: semantics are confirmed and implemented as append-only, versioned, effective-dated host evidence. The initial version/day/effective month is intentionally absent, so the empty table fails closed and the disabled worker creates no obligations. A future authorised migration/workflow must insert the first version; no mutation API or default exists.
- PD-06 remains owned by Business; the initial due-policy row and launch remain owned by Business/Ops. Both paths stay safe while unset: inclusion is eligibility-only and recurring obligations require exactly one valid persisted policy.

---

## 13. Member-journey usability review (as a member)

Walk-through of M-28/M-44/M-45 from the member's perspective, based on the rendered component and data contract:

1. **Discovery**: navbar → More → "AQGreen progress" (permission `Aqua.ProgrammeParticipations.ViewSelf`). Correctly appears only for members who can already view "My programmes".
2. **Entry**: page shows breadcrumb (My programmes → AQGreen progress), title, and a one-line summary of what the page shows.
3. **No participation**: shows `EmptyProgress` card — "Not yet qualified", 0 of 5 direct recruits, R0 total earned, and a clear explanation. No dead-end.
4. **Has participation**: level card (current label, next goal or "highest level"), direct-recruit progress bar with counts and "N more needed for Level 1/the next level", funeral-cover badge when included.
5. **Weekly earnings**: four aggregates (Total earned / Awaiting release / On hold / Paid to you) plus a per-week table with week dates, levels, amount, status, and hold reason. Reasonable and complete.
6. **Monthly subscription**: status, amount, due date, outstanding, and an explicit "Next action" call-to-action sentence. Consistent with the obligation engine states.
7. **Funeral cover**: shows the R30,000 amount and "included with your completed AQGreen joining", plus the correct caveat that activation/waiting period/claims are external. No false "active" claim (PD-06 compliant).
8. **Education**: four items explain joining R1,200 → R30,000 cover, network levels 5/25/125, weekly components R150/R250/R1,250, and the monthly R600 with grace. Numerically consistent with confirmed terms (M-23, M-29).
9. **Error/empty**: skeleton loading, error status message for load failure, permission-denied message without any API call.

Findings (minor, non-blocking):
- Level-1→2 progression copy ("N more needed for the next level") uses the same direct-recruit counter for the Level 1 threshold; for Level 2/3 the true requirement is 25/125 across the network. The education card clarifies this, so the progress bar stays truthful about *direct recruits*, but the label could say "direct recruits needed" to avoid implying Level 2 needs only 5. **Improvement, not defect.**
- `MonthlyObligationAmount` shows the most-recent obligation's `AmountDue`; since all obligations are the same fixed R600 under current terms this is consistent, but the DTO could be clearer about which period's amount is shown. **Improvement, not defect.**
- The endpoint returns a single `Education` list rendered as static cards; content duplication with other education surfaces is not an issue on this branch.

Overall: the member journey is coherent, truthful, permission-gated, and does not overstate entitlement state.

---

## 14. Causality of observed failures

- Frontend full-suite single failure (pre-merge): **pre-existing flaky test**, root cause = `setTimeout(0)` race between the health-incompatibility message (synchronous render) and the load-triggered "Join AQGreen" button; branch-independent (verified no diff to the test, component, or hook). Classification: pre-existing / unrelated-to-branch; not fixed here (out of change policy scope). Post-merge, the same file passed the full frontend run and 6/6 isolated re-runs — consistent with intermittent flake, not a regression.
- The single merge conflict (admin approval test) was a **test-only** overlap of two independent de-flake fixes; resolved to the strictly stronger assertion (§4a). No production behaviour involved.
- No other failure observed in any suite or build across the validation pass, pre- or post-merge.

---

## 15. Evidence baseline (final — post-integration, on the merged tree)

- Backend Release build: **0 errors**, 2035 warnings (pre-existing categories; no new categories).
- Full backend suite (post-merge, Application + Web.Tests TRX files): **704 passed, 0 failed, 0 skipped** (Application `application-tests.trx`: 664 passed; Web.Tests `webtests.trx`: 40 passed).
- PostgreSQL transactional tests (from `origin/main`): `InternalAccountInvitationEndToEndTests` — both `CreateAndInvite_TransactionalRollback_LeavesNoPartialAdministrator` and `InvitationAcceptance_AllowsTokenAuthentication` **passed** in the 40 Web.Tests.
- Category evidence from Application TRX: EntityFrameworkCore/PG/Migration 24; Authorization 73; Integration 11; Payments 63; Simulation 2. The PostgreSQL migration subset re-ran clean (24/24) against live `postgres:16-alpine` containers.
- EF model check (`dotnet-ef` 8.0.8, `has-pending-model-changes`): **"No changes have been made to the model since the last migration."**
- Migration Up/Down inspected; unique index on `EntryParticipationId` verified.
- Frontend (post-merge): ESLint clean; `tsc --noEmit` clean; production build compiled successfully (61 static pages, incl. `/member/programme-progress`); full Vitest suite 377 passed/107 files; focused admin test 10/10 consecutive clean runs; previously-flaky `member-programmes.test.tsx` 6/6 isolated runs clean.
- `git diff --check` clean; no conflict markers in the tree; secret/TODO/debug/commented-code scans clean (only `origin/main`-introduced CI test-only credentials, clearly named).

---

## 16. Findings and classification

### Blocking
None.

### Required confidence
None outstanding.

### Accepted follow-up debt (does not block merge)
| # | Finding | Owner |
|---|---------|-------|
| AF-01 | Pre-existing flaky frontend test `member-programmes.test.tsx` "blocks payment actions…" races `setTimeout(0)`; fix by awaiting the button with `findByRole`. Not branch-owned; passed the full post-merge run and 6/6 isolated re-runs but remains intermittently racy under load. | Engineering |
| AF-02 | Funeral-cover inclusion insert has no `DbUpdateException` recovery around the unique index (mirror the payment-insert pattern at `ProgrammePaymentConfirmationProcessor.cs:441-452`). Idempotency-only improvement: the live webhook path is already serialised by the checkout advisory lock + receipt dedupe, and the only other call path has no production callers; correctness is guaranteed by the unique index. | Engineering |
| AF-03 | An explicit parallel-duplicate webhook delivery test (AD-05) would strengthen hardening evidence. | Engineering |
| AF-04 | Webhook rate limiting (AD-06 / M-35 / P-01) — pre-existing accepted debt. | Engineering |
| AF-05 | Scale follow-up: progress endpoint loads tenant-wide active participations for qualification; re-evaluate if the network grows large. | Engineering |

### Unresolved business decisions (required, not code defects)
- PD-06 funeral-cover activation/enrolment timing and effective date — **must not** be encoded as active until decided. Presently represented correctly as included/eligible.
- PD-07 monthly due-date semantics are confirmed: the first obligation month is the month after activation; policy versions are effective-dated; the business selects day 1–28; due time is 00:00 `Africa/Johannesburg`. The initial version, day, effective instant, and launch month remain unset, so the worker must remain disabled.
- PD-02 (upline effect of an overdue member), PD-05 (audit surfaced to admins) — out of scope.

### Improvements (out of branch scope)
- Progress-bar label clarifying the Level-2/3 network requirement (§13). Optionally show period for `MonthlyObligationAmount`.

---

## 17. Operational and migration requirements

1. **Migrations**: run `AddAQGreenFuneralCoverEntitlements`, `AddAQGreenMonthlyObligationDuePolicies`, then `AddAreaActivationStateHistory`. The due-policy migration creates an empty append-only host policy table and does not seed or backfill. The Area migration also creates no baseline; new provisioning and explicit operator observation create only prospective evidence. PostgreSQL rejects update, delete, and truncate of Area evidence. Both Down migrations refuse to discard recorded evidence, so rollback after rollout requires an authorised data-preserving plan.
2. **Worker configuration** (`App:EntryMonthlyObligations`): `Enabled=false` by default. Keep it disabled until Business/Ops authorise and insert the first immutable policy version, day, effective instant, and launch month. The policy table is intentionally empty after migration.
3. **Scheduling lock**: uses `pg_advisory_xact_lock` (PostgreSQL) / `sp_getapplock` (SQL Server); multi-instance safe. Providers without a supported lock are treated as single-node.
4. **Arming timing**: the worker schedules the current `Africa/Johannesburg` month only and never creates activation-month debt. The first obligation month for each participation is the following Johannesburg month, bounded by the first applicable policy version. The engine performs no backfill for months before policy launch or while disabled. Business/Ops must choose an effective launch month and arm before obligations for that month are expected.
5. **Frontend**: no env change needed for the new page; the navbar link appears automatically for members with `ViewSelf`.
6. **Logs**: structured `ProgrammeEngineAlert` events for processed obligations and unresolved persisted-policy outcomes; no secrets/customer/payment data logged.
7. **Product workflow verification** (per AGENTS.md "Product Workflow Verification Standard"): verified payment and Area Administrator approval create separate active AQGreen or Onyx participation. Calculation records Earned/Held/NotEarned only; release and external payout remain separate authorised human actions. Network placement and travel qualification use a shared cutoff-effective projection. Forward-only target-Area history and its operator baseline action are implemented, but no existing-Area baseline is seeded. AQGreen obligation and loan payout holds are now derived as of the cycle cutoff (`WasOverdueAt`, `WasRequiringPayoutHoldAt`) and are replay-stable; the automatic Friday workflow is still **not verified complete** because cycle-effective terms are absent, provider finality is unproven, and pre-baseline Area cutoffs remain unknown. Production automation therefore remains blocked.
8. **Automatic weekly commission engine implementation** (`WeeklyCommissionCalculationWorker`, gated by `App:WeeklyCommissions:Enabled=false`): orchestration, independent transactions, locking, idempotency, and no-automatic-release boundaries are implemented. Those controls do not establish financial cutoff correctness. `App:WeeklyCommissions:Enabled` must remain `false`; the worker is not approved for production arming.
9. **Inventory and recovery posture**: the host-only period inventory is read-only and includes soft-deleted AQGreen and Onyx periods, boundary classification, totals, exact duplicates, non-overlapping boundaries, and missing canonical cycles. Missing cycles are classified by origin: the latest closed cycle is `PendingCalculation` (deterministically generated by the automatic calculation pipeline at the cycle cutoff once calculation is enabled; not reconstructible from current state; commission is only classified after verified payment), while older gaps are `ManualFinancialReconciliationRequired` because period-effective network, qualification, eligibility, and terms cannot be reconstructed reliably. The read-only monthly obligation checkout reconciliation **discovery/triage queue** (host/Area-scoped, `ViewLegacyPaymentReconciliation`-gated) surfaces completed checkouts with allocation outcome, evidence, payment facts, and recorded obligation status for authorised Finance/Ops review; it does not resolve or mutate anything. No arbitrary historical recovery API is exposed, because current network, qualification, eligibility, compliance, and terms cannot prove an older cycle's business-effective state. The existing latest-week calculator is also not authoritative production recovery until its Thursday-cutoff correctness is resolved.
10. **Cutoff correction status**: activation and recruiter placement are reconstructed at `PeriodEnd`; valid correction chains are replayed and uncertain network evidence fails closed. Target-Area observation/change is serialized per Area; provisioning and mutation use database time on supported production providers; unknown/inactive state creates no new ledger. Legacy Area deletion is rejected because deletion has no authorised financial meaning. AQGreen holds are now cutoff-effective: obligation overdue and loan payout-hold are derived from persisted boundaries at `PeriodEnd` (`WasOverdueAt`, `WasRequiringPayoutHoldAt`) instead of current status, so a post-cutoff assessment, cure, or repayment cannot change a closed cycle. Existing-Area baselines, versioned due-policy rollout, immutable loan allocation time, cycle-effective terms, and provider finality remain unresolved evidence/implementation blockers. The first calculated result is retained idempotently, so weekly automation remains disabled.

---

## 18. Deployment checklist

Pre-merge / pre-release verification steps, in order:

| # | Check | Owner | Expected |
|---|-------|-------|----------|
| D-1 | Migration `20260807065821_AddAQGreenFuneralCoverEntitlements` applied (Up) on the target environment. | Ops/DBA | Table `AQGreenFuneralCoverEntitlements` exists with unique index on `EntryParticipationId`; FKs `Restrict`. Down drops the table safely. |
| D-1a | Migration `20260809054416_AddAQGreenMonthlyObligationDuePolicies` applied after D-1. | Ops/DBA | Empty `EntryMonthlyObligationDuePolicies` table exists; legacy `DuePolicyVersion` values remain null; no policy row or obligation is seeded/backfilled. |
| D-1b | Migration `20260809081746_AddAreaActivationStateHistory` applied after D-1a. | Ops/DBA | Empty append-only `AreaActivationStateRecords` table exists; no legacy baseline is seeded. Update/delete/truncate and evidence-losing rollback are rejected. |
| D-2 | EF snapshot aligned; `dotnet-ef has-pending-model-changes` returns "No changes". | Dev | Clean (already verified for this branch). |
| D-3 | Confirm `App:EntryMonthlyObligations.Enabled` is `false` until the first authorised policy row and launch month are approved and deployed. | Ops | Recurring obligations are **not** created while disabled; assessment only runs when the worker is enabled. Payment application requires the separate obligation-linked checkout workflow. |
| D-4 | Do not arm the monthly worker until an authorised migration/workflow inserts the first immutable policy version, effective obligation month, and day 1–28. | Ops + Business + Engineering | Empty/missing/ambiguous/invalid policy evidence logs a warning and creates no obligation; no obsolete configured-day fallback exists. |
| D-4a | Confirm the operational launch month before scheduling. The first obligation month is the month after activation, bounded by policy launch; do not invent debt for earlier disabled periods. | Ops + Business | Expected obligation existence is deterministic and auditable. |
| D-5 | Multi-instance safety confirmed: `pg_advisory_xact_lock` (PostgreSQL) / `sp_getapplock` (SQL Server) is transaction-scoped and released on commit/rollback. | Ops | One scheduler at a time across instances. |
| D-6 | Frontend: rebuild + redeploy static pages; new nav link "AQGreen progress" appears for members with `Aqua.ProgrammeParticipations.ViewSelf`. No env change needed. | Dev/Ops | `/member/programme-progress` reachable, permission-gated, read-only. |
| D-7 | Backend Release build 0 errors; full backend 704 + frontend 377 suites pass. | Dev | Same as §15 evidence baseline. |
| D-8 | Smoke: an AQGreen member with a completed joining obligation sees their funeral-cover inclusion card; a member with no participation sees the "Not yet qualified" empty state; a user without `ViewSelf` is denied. | QA | Matches §13 member journey. |
| D-9 | PD-06 remains unresolved; monthly due-policy semantics are confirmed but the initial policy version/day/effective month remains intentionally unset. | Business | No code path presents funeral cover as active or invents monthly due-policy evidence. |
| D-10 | Keep `App:WeeklyCommissions.Enabled=false`. Network placement and prospective target-Area state are cutoff-aware and AQGreen obligation/loan holds are cutoff-derived, but existing-Area baselines, terms selection, and provider finality remain incomplete. | Ops + Engineering + Business | No automatic AQGreen/Onyx calculation or Onyx travel qualification runs. |
| D-11 | Run the host-only period inventory, resolve legacy overlaps, and assign Finance/Ops ownership for every missing cycle. The inventory does not calculate amounts or mutate ledgers. Latest-closed missing cycles appear as `PendingCalculation` and are generated deterministically by the automatic calculation pipeline once calculation is enabled (workers remain disabled until then); older gaps require an authorised manual reconciliation decision with audit evidence. Use the read-only monthly obligation checkout reconciliation discovery/triage queue to review completed checkouts (allocation outcome, evidence, payment facts, recorded obligation status) per Area and period; it resolves nothing. | Finance + Ops | Every historical gap has an authorised manual reconciliation decision and audit evidence; every `ReconciliationRequired` checkout is reviewed and resolved by an authorised action. |
| D-12 | Before any future arming, implement and verify period-end state correctness for network placement and all applicable eligibility/compliance inputs, including Friday-delay, cross-Area, correction, and retry tests. | Engineering + Business | The same closed cycle produces the same financially correct result regardless of post-cutoff mutations or processing order. |

Any deviation from D-1/D-3/D-4/D-10 is a deployment blocker. D-10 reflects the
remaining verified financial cutoff gaps that require correction before
production arming.

---

## 19. Final verdict

### Implementation
**Partially meets purpose.** Friday-to-Thursday orchestration, isolation, locking, idempotency, read-only period inventory, cutoff-effective recruiter placement, stable earliest-five selection, shared travel network timing, and forward-only target-Area history are implemented. AQGreen obligation and loan payout holds are now derived as of the cycle cutoff from persisted boundaries and are replay-stable. The durable monthly due-policy capability starts empty and disabled, and unauthoritative oldest-open payment allocation was removed. Existing-Area baselines, cycle-effective terms, provider finality, and missed historical travel-cutoff recovery remain incomplete. Historical recovery from current state was rejected and is not exposed.

### Evidence
**Additional verification required.** The final local Release solution build passed with 0 errors; existing XML-documentation, nullable-context, analyzer, obsolete-API, and dependency-advisory warnings remain. The full Release backend suite passed **747/747** (including PostgreSQL migration tests) after the hold-derivation correction; focused obligation, loan, commission, and progress tests cover the cutoff boundaries (paid at cutoff, grace ending at cutoff, post-cutoff assessment, post-cutoff cure/repayment, replay determinism, no current-state fallback). EF reports no pending model changes. Passing tests prove the implemented slices, not the unresolved financial workflow.

### Operational state
**Production arming blocked; remote CI not run.** The disabled configuration is the required safe state.

---

### Release verdict: **NOT READY FOR MERGE OR PRODUCTION ARMING**

Rationale:
- Verified branch-owned financial blockers remain in AQGreen compliance, existing-Area baseline evidence, terms selection, provider finality, and historical travel-cutoff recovery.
- Database uniqueness and idempotency prevent duplicates but retain the first potentially cutoff-incorrect result; they do not repair it.
- Period inventory safely identifies legacy and missing periods without mutation. It does not make automation safe or provide historical commission reconstruction.
- Older gaps require authorised manual financial reconciliation. A future application workflow requires separate design and is not implemented here.
- The branch still has no upstream/PR; no push, PR, merge, or commit was performed in this pass.
