# Aqua Programme Engine — Business Rule Matrix

Verification mission: "Verify the Aqua Programme Engine End-to-End"
Branch: `verify/programme-engine` (worktree, clean tracking of `origin/main`, HEAD `e371f85`)
Classification: **Confirmed** · **Provisional** · **Contradictory** · **Undefined**
Undefined rules are recorded as **Product Decisions** and are never invented in code.

Evidence keys: **E2E** end-to-end · **IT** integration test · **AT** automated component test · **I** inspection · **A** assumption · **U** unknown.

---

## 1. Participation lifecycle and payment confirmation

| ID | Rule | Status | Evidence | Source |
|----|------|--------|----------|--------|
| M-01 | AQGreen joining requires exactly one verified R1,200 payment; a separate R600 monthly commitment is not a joining instalment | **Confirmed** | AT | `docs/BusinessDocs/requirements.md` BR-16 line 136; `EntryParticipation.cs:155-187` (`ApplyConfirmedJoiningPayment` requires `JoiningPaymentAmount`); test `AQGreenCheckout_ActivatesOnlyAfterOneVerifiedTwelveHundredRandPayment` |
| M-02 | New participants must select the `Full` R1,200 schedule; the `TwoInstallments` schedule is only available to a historical participant with one verified joining instalment preserved | **Confirmed** | AT | `ClubMemberProgrammeParticipationAppService.cs:256-266` (`isCompletingVerifiedInstallments` guard; message "AQGreen joining requires one full R1,200 payment"); `EntryParticipation.SelectJoiningPaymentSchedule` `EntryParticipation.cs:189-214` |
| M-03 | Full payment and the preserved two-installment path must converge to the same activation state | **Confirmed** | AT | `EntryParticipation.ApplyConfirmedJoiningPayment(payment)` `EntryParticipation.cs:155-187` and `(payment, stage)` `:246-286` both set `PaymentConfirmedAwaitingApproval`; both require `GetConfirmedJoiningAmount() == JoiningPaymentAmount` (`:283`) |
| M-04 | Webhook confirmation promotes participation only to `PaymentConfirmedAwaitingApproval`; it never sets `Active` | **Confirmed** | AT/IT | `ProgrammePaymentConfirmationProcessor`; `EntryParticipation.cs:186,268,285`; status strings in `ProgrammeParticipationStatusPresenter.cs:34` |
| M-05 | Only `ApproveByAdministrator` sets `Active` + `ActivatedAt` | **Confirmed** | AT/IT | `EntryParticipation.cs:288-299`; `OnyxParticipation` equivalent; `AdminProgrammeParticipationAppService.ApproveProgrammeParticipationAsync`; PR #50 |
| M-06 | Direct Onyx entry requires one confirmed R6,120 payment before activation | **Confirmed** | AT | `OnyxParticipation.cs`; `OnyxParticipationTests.cs:51` (`DirectOnyxParticipation_RequiresAConfirmedSixThousandOneHundredTwentyRandPayment`); `CurrentProgrammeTermsProvider` version "2026-07" R6,120 |
| M-07 | AQGreen-to-Onyx graduation is an explicit administrator decision after revalidated Level 2 eligibility + accepted/approved R6,120 loan; creates a separate independent Onyx participation, never rewrites AQGreen | **Confirmed** | AT | BR-15 `requirements.md:135`; `OnyxGraduationDecision.cs`; `OnyxParticipationTests.cs:177,201,217`; `onyx-implementation-plan.md:22-38` |
| M-08 | AQGreen and Onyx recruitment require an active participation in the same programme; missing recruiter = independent network root | **Confirmed** | AT | `onyx-implementation-plan.md:40-45`; `EntryParticipation.EnsureEligibleRecruiter`; `OnyxParticipation.cs:113` test |

## 2. Administrator approval lifecycle

| ID | Rule | Status | Evidence | Source |
|----|------|--------|----------|--------|
| M-09 | Payment confirmed is not programme active; approval is a separate mandatory gate | **Confirmed** | IT/E2E | `PaymentConfirmedAwaitingApproval` status; frontend admin screen "Awaiting Area approval" queue; `AdminProgrammeParticipationAppService.Approve/RejectProgrammeParticipationAsync` |
| M-10 | Approve/Reject use `[AbpAuthorize]` permission `Aqua.Admin.ProgrammeParticipations.Approve`, disabled UoW, serializable isolation, host-side tenant filter disable | **Confirmed** | I | `AdminProgrammeParticipationAppService.cs`; `AdminAppServiceBase.DisableAllTenantDataFiltersForHost()` |
| M-11 | Approval decision emails are idempotent per participation + decision | **Confirmed** | I | Decision email idempotency key `$"{programme}:{participationId}:{approved\|declined}"`; `NotificationType = "ParticipationDecision"` |
| M-12 | Rejection requires a reason; decision is recorded append-only | **Confirmed** | AT/I | `EntryParticipationApprovalDecision`; frontend decline dialog requires reason; `AdminProgrammeParticipations.tsx:315-345` |
| M-13 | A rejected/declined participation keeps the confirmed payment received but does not activate | **Confirmed** | I/AT | `AdminProgrammeParticipations.tsx:801-805`; domain `ApproveByAdministrator` only path to `Active` |

## 3. Monthly obligation (R600 monthly commitment)

| ID | Rule | Status | Evidence | Source |
|----|------|--------|----------|--------|
| M-14 | AQGreen monthly commitment is R600, grace period 7 days; overdue state holds the member's own payout while preserving placement and debt | **Confirmed** (domain) | AT | `EntryMonthlyObligation.cs` (enum Due/GracePeriod/Overdue/Paid; `IsOwnPayoutEligible`); `CurrentProgrammeTermsProvider` version "2026-08-single-1200" |
| M-15 | Automatic obligation scheduling and payment allocation into obligations | **Undefined** | I | Nothing in production calls `EntryMonthlyObligation.Create`/assessment/payment; only tests. `onyx-implementation-plan.md:189-190` "Automatic obligation scheduling and payment allocation are deferred to the secured application workflow phase" |
| M-16 | Effect of an overdue AQGreen member's obligation on upline structural qualification / commissions | **Undefined** | I | `onyx-implementation-plan.md:95-100` "The effect on uplines is unresolved"; `:186-188` "no upline contribution effect is inferred" |

## 4. Recruitment tree and qualification boundaries

| ID | Rule | Status | Evidence | Source |
|----|------|--------|----------|--------|
| M-17 | AQGreen tree: 5 / 25 / 125 (three levels); Onyx tree: 5 / 25 / 125 / 625 / 3,125 (five structural levels) | **Confirmed** | AT | `EntryNetworkQualificationEvaluator` BranchSize=5, MaximumLevel=3; `OnyxNetworkQualificationEvaluator` BranchSize=5, HighestConfirmedStructuralLevel=5; `onyx-implementation-plan.md:73-76` |
| M-18 | Qualification requires exactly 5 qualified direct recruits per level; 4 direct recruits record no partial level | **Confirmed** | AT | `EntryNetworkQualificationEvaluatorTests.cs:29-58` (`Level1_RequiresFiveQualifiedDirectRecruits`, `Level2_...`); `OnyxWeeklyCommissionTests.cs:29-63` (`FourActiveDirectRecruits_RecordNoPartialCommission`, `FiveActiveDirectRecruits_RecordOneTwoHundredFiftyRandCommission`) |
| M-19 | Every branch must complete; incomplete levels record no partial component | **Confirmed** | AT | `OnyxWeeklyCommissionTests.cs:110-132` (`IncompleteLevelTwo_DoesNotEarnAPartialLevelTwoComponent`); `EntryNetworkQualificationEvaluatorTests` |
| M-20 | Inactive direct recruit does not complete a level | **Confirmed** | AT | `OnyxWeeklyCommissionTests.cs:66-85` |
| M-21 | Active cross-Area recruit contributes to network qualification | **Confirmed** | AT | `OnyxWeeklyCommissionTests.cs:88-107`; `onyx-implementation-plan.md:212` "active AQGreen networks are evaluated across Areas" |
| M-22 | Full Level-5 Onyx tree contains exactly 3,906 participants (1 + 5 + 25 + 125 + 625 + 3,125) | **Confirmed** | AT | `OnyxNetworkTestBuilder.BuildCompleteNetwork` (`OnyxNetworkTestBuilder.cs:29-68`); `OnyxWeeklyCommissionTests.CompleteLevelFive_...` |

## 5. Commission engine

| ID | Rule | Status | Evidence | Source |
|----|------|--------|----------|--------|
| M-23 | AQGreen commission components: L1 R150, L2 R250, L3 R1,250 (version "2026-07") | **Confirmed** | AT | `CurrentCommissionTermsProvider.cs:19-25` |
| M-24 | Onyx per-person weekly rates: L1 R50, L2 R20, L3 R12.62, L4 R5, L5 R4; cumulative L1 R250, L2 R500, L3 R1,577.50, L4 R3,125, L5 R12,500; total R17,952.50 (version "2026-07-onyx-levels-1-5") | **Confirmed** | AT | `CurrentCommissionTermsProvider.cs:27-35`; `OnyxWeeklyCommissionTests.cs:162-181,206-213` |
| M-25 | Commission ledger distinguishes Earned → Released → Paid (weekly payout status), with per-period uniqueness per participation | **Confirmed** | AT/IT | `OnyxWeeklyCommission` + `WeeklyCommissionPayoutStatus`; `EntryWeeklyCommission`; per-period uniqueness in EF configurations; `EarnedCommission_IsReleasedAndPaidThroughIdempotentTransitions` `OnyxWeeklyCommissionTests.cs:184-203` |
| M-26 | Overdue own obligations and active Onyx loans hold the member's own payout | **Confirmed** | AT/I | `EntryMonthlyObligation.IsOwnPayoutEligible`; loan-hold decision (`onyx-implementation-plan.md:243-246`); `AdminCommissionAppService` holds logic |
| M-27 | Commission Calculate / Release / RecordPayment are host-only permissions, idempotent per period | **Confirmed** | I/AT | `AquaPermissions.Commissions.*` host `CreateChildren`; `AdminCommissionAppServiceTests` |
| M-28 | No member-facing commission ledger is exposed | **Undefined** | I | No `IClubMember*` commission ledger service/endpoint; frontend has no member commission view (Product Decision) |

## 6. Terms versioning and money quantities

| ID | Rule | Status | Evidence | Source |
|----|------|--------|----------|--------|
| M-29 | AQGreen terms version "2026-08-single-1200": joining R1,200, monthly R600, grace 7 days, effective 2026-07-26 | **Confirmed** | AT | `CurrentProgrammeTermsProvider.cs` |
| M-30 | Direct Onyx entry R6,120 version "2026-07", effective 2026-07-01 | **Confirmed** | AT | `CurrentProgrammeTermsProvider.cs` |
| M-31 | Travel benefit requires Onyx Level 3; terms provider rejects other levels | **Confirmed** | AT | `OnyxTravelBenefitTerms` ctor; `OnyxTravelBenefitEntitlementTests`; `OnyxTravelBenefitEligibilityProcessor` |
| M-32 | Currency is ZAR throughout; amounts enforced exactly (no tolerance) | **Confirmed** | AT | `EnsureExactAmount` in `EntryParticipation`/`OnyxParticipation`; `OnyxParticipationTests.cs:89` |

## 7. Yoco webhook security and idempotency

| ID | Rule | Status | Evidence | Source |
|----|------|--------|----------|--------|
| M-33 | Webhook is anonymous by design, HMAC-signature-gated before any state change, amount/currency exact, no secrets or raw bodies logged | **Confirmed** | I/IT | `YocoPaymentsController.cs`; `YocoWebhookSignatureVerifier.cs`; `YocoPaymentNotificationProcessor.cs`; webhook tests under `Tests/Payments` |
| M-34 | Provider+reference uniqueness and idempotent replay rejection | **Confirmed** | I/IT | Yoco receipt uniqueness; `StaleYocoCheckoutDetector`; webhook tests |
| M-35 | No rate limiting on the webhook endpoint | **Confirmed** (gap) | I | No rate-limit middleware; recorded as accepted debt / follow-up |
| M-36 | Webhook ends at `PaymentConfirmedAwaitingApproval` for both AQGreen and direct Onyx | **Confirmed** | I/AT | Processor + domain transitions |

## 8. Authorization and Area (tenant) isolation

| ID | Rule | Status | Evidence | Source |
|----|------|--------|----------|--------|
| M-37 | Area scoping = ABP tenant; cross-tenant access from tenant sessions is rejected; host callers without `Aqua.Admin.AllTenants` are rejected | **Confirmed** | I/IT | `AdminAppServiceBase.ValidateRequestedTenant/ResolveTargetTenant`; admin service tenant checks |
| M-38 | Area admins cannot view or approve participants of another Area | **Confirmed** | I | Tenant-filter + `DisableAllTenantDataFiltersForHost` only for host approval; frontend admin screens are tenant-scoped |
| M-39 | Admin list/approve/reject/terminate require distinct permissions (View / Approve / CorrectRecruiter / ViewPaymentCheckouts / TerminatePaymentCheckouts) | **Confirmed** | AT | `AdminProgrammeParticipations.tsx:83-109`; permission tests |

## 9. Audit trail

| ID | Rule | Status | Evidence | Source |
|----|------|--------|----------|--------|
| M-40 | Approval decisions, recruiter corrections, graduations, and termination decisions are recorded append-only with who/when/why | **Confirmed** (stored) | I | `EntryParticipationApprovalDecision`, `EntryRecruiterCorrection`, `OnyxRecruiterCorrection`, `OnyxGraduationDecision`; `FullAuditedAggregateRoot` |
| M-41 | The stored audit history is surfaced to administrators | **Undefined** | I | No application-service DTO or admin endpoint exposes `ApprovalDecisions`/`RecruiterCorrections`; frontend only shows effects, not history (Product Decision) |
| M-42 | Payment attempts, provider notifications, reconciliation and status transitions are append-only | **Confirmed** | I | Yoco receipt records, payment records, weekly commission ledger |

## 10. Member experience, education, transparency

| ID | Rule | Status | Evidence | Source |
|----|------|--------|----------|--------|
| M-43 | Member dashboard shows programme participation, next payment, activation state | **Confirmed** | AT/I | `member-programmes.tsx`, `use-my-programme-participations.ts`; `member-dashboard.test.tsx` |
| M-44 | Member-visible education component about the programme/tree/commissions | **Undefined** | I | No dedicated education component (Product Decision) |
| M-45 | Member-facing commission ledger / transparency view | **Undefined** | I | No member commission endpoint or component (Product Decision) |

## 11. Funeral cover

| ID | Rule | Status | Evidence | Source |
|----|------|--------|----------|--------|
| M-46 | R30,000 funeral cover plan with 6-month waiting period, external insurer | **Undefined** | I | FR-44 `requirements.md:86` ❌ Missing; A-21 `Assumptions.md` (external insurer); `future-roadmap.md`; no production code references funeral cover (only unrelated `Timer.Period = 30_000` in `TransactionalEmailOutboxWorker.cs`) |

---

## Undefined rules → Product Decisions (not implemented; require business decision)

| # | Product Decision | Rationale |
|----|------------------|-----------|
| PD-01 | Automatic scheduling and payment allocation of the R600 monthly obligation | Domain is complete; production scheduling deferred by `onyx-implementation-plan.md:189-190`. Business must choose a secured scheduler design + scope. |
| PD-02 | Upline effect of an overdue AQGreen member | Explicitly unresolved in `onyx-implementation-plan.md:95-100,186-188`. |
| PD-03 | Member-facing commission ledger and education content | Transparency requested by mission; no requirement doc / no implementation. |
| PD-04 | R30,000 funeral cover product | Missing in requirements (FR-44), assumptions (A-21 external insurer) and roadmap. Requires insurer integration decision before design. |
| PD-05 | Audit history surfaced to administrators | Data stored append-only; surfacing UX/API is an open decision. |

## Contradictory / provisional notes

- C-01 (Contradictory, resolved): Earlier docs and some tests referenced a split R600+R600 joining lifecycle. BR-16 (`requirements.md:136`) and PR #50 supersede this: one R1,200 payment is the joining requirement; the split path survives only as a preserved historical obligation. No production contradiction remains.
- P-01 (Provisional): Webhook rate limiting absent; acceptable for launch volume but a follow-up hardening candidate (M-35).
- P-02 (Provisional): EF model comparison baseline used the compatible `dotnet-ef` 8.0.8 (the globally installed 10.0.9 is incompatible; `docs/development/validation.md:45-59`). Model check clean.

## Conflation guard (mission rule)

The following are distinct and are never treated as synonyms: **Referral** (network placement), **Qualification** (completed structural level), **Commission Earned** (calculable), **Commission Payable/Released** (hold released), **Commission Paid** (paid out), **Programme Active** (administrator-approved), **Payment Confirmed** (provider-verified), **Administrator Approved** (explicit admin decision). The verification report below relies on these distinct statuses.
