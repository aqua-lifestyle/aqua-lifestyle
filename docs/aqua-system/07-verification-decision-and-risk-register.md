# Verification, decision, and risk register

This register is the status and evidence companion to the Aqua system pack. It is truthful at:

```text
Repository: aqua-lifestyle/aqua-lifestyle
Branch baseline: origin/main
Commit: 6ba776b7ef2f117aeaf78be7b3da06231a4f72e5
Evidence reviewed: 11 August 2026
```

Agent 1's production AQGreen/Render/P0001 reconciliation and Agent 2's member/customer programme visual work are active, unmerged workstreams. Neither is authoritative until merged into `main` and verified at that resulting commit.

## A. Status ladder

| Status | Meaning |
| --- | --- |
| `IMPLEMENTED` | Code exists in a branch or repository state. |
| `TESTED` | Relevant automated or controlled manual evidence exists. |
| `INTEGRATED` | The capability is composed with its required application layers/dependencies. |
| `MERGE READY` | Review gates are satisfied for a proposed change. |
| `MERGED` | Code is in the authoritative branch. |
| `DEPLOYED` | The exact build/schema reached an environment. |
| `ENABLED` | Operational configuration allows the capability to run. |
| `PRODUCTION VERIFIED` | The real production workflow was observed successfully with authoritative external/state evidence. |

No status implies the status to its right.

## B. Capability status

“Highest verified state” follows the ladder above. Notes expose partial capability boundaries.

| Capability | Highest directly verified state | Evidence and boundary |
| --- | --- | --- |
| AQGreen R1,200 / R600+R600 joining | `MERGED` | Domain, checkout, webhook application, migrations, UI and tests are on current `main`; current CI passed. Production provider acceptance is not verified. |
| Payment-confirmed awaiting approval | `MERGED` | Payment processor/domain tests and current CI; historical browser evidence carried forward. No payment-only activation path is authorised. |
| Area approval queue and badge | `MERGED` | Backend/UI/tests on `main`; historical browser discovery, persistence and badge evidence carried forward. |
| Approval/rejection and rejection reason | `MERGED` | Unique decision model, Area enforcement, customer projection/UI and tests on `main`; historical browser approval/rejection evidence carried forward. |
| AQGreen funeral-cover inclusion | `MERGED` | Runtime and deterministic migration/backfill on `main`; current CI passed. The production migration is under active reconciliation and external insurer activation is not modelled. |
| AQGreen network qualification | `IMPLEMENTED — PR #72` | Structural Levels 1–5 are represented independently from the currently authorised Level 1–3 commission depth. Focused tests cover complete and incomplete same-Tenant structures. |
| Programme network tenant boundary | `IMPLEMENTED — PR #72` | AQGreen and Onyx graph construction, recruitment placement, host calculation, ledgers, and projections are bounded by Tenant and programme. Mixed-Tenant graph input fails closed. Area remains a planned subdivision inside Tenant. |
| Onyx qualification | `MERGED` | Levels 1–5 and 3,906-person deterministic simulation are on `main`. |
| Effective-dated weekly commission engine | `MERGED` | Friday–Thursday resolver, terms registry/resolver, cutoff facts, locks, ledgers and tests on `main`. |
| Weekly automatic calculation | `MERGED` | Worker code is merged; repository default is disabled. Not `ENABLED` or `PRODUCTION VERIFIED`. |
| AQGreen monthly obligations | `MERGED` | Model, policy, scheduler, obligation-linked checkout/payment path and triage are merged. Due day unresolved; worker disabled. |
| Yoco integration | `INTEGRATED` | Hosted checkout, signature validation, receipt/payment idempotency and alerts are composed and tested. Real provider finality/settlement is not production verified. |
| Payment compatibility guard | `MERGED` | Matching API/frontend contract and tests are on `main`; mixed deployment refuses payment actions. |
| Transactional email/outbox | `MERGED` | Durable intent, encryption, retries and Bird adapter are merged/tested. Actual Bird recipient delivery is not verified here. |
| Member programme/progress visibility | `MERGED` | Current pages expose participation, rejection reason, AQGreen progress, earnings, obligation and inclusion. Agent 2's active visual work is not yet authoritative. |
| Historical reconciliation detection | `MERGED` | Migration guards and read-only joining/monthly/period inventories exist. |
| Historical reconciliation resolution | `IMPLEMENTED`: **no** | No general authorised audited mutation workflow exists; issue #66 is open. |

## C. Current-tree verification evidence

### Current `origin/main`

GitHub Actions run [31455701182](https://github.com/aqua-lifestyle/aqua-lifestyle/actions/runs/31455701182) completed successfully at exact commit `6ba776b7` on 11 August 2026.

| Job/check | Current evidence |
| --- | --- |
| Backend build and tests | Passed. Downloaded TRX: **809 total / 809 passed / 0 failed / 0 skipped**. |
| PostgreSQL transactional invitation regressions | Passed. TRX: **2 / 2**, plus the marker proving the PostgreSQL transactional body ran. This job is invitation-specific, not general proof of all programme PostgreSQL paths. |
| Frontend dependency audit | Passed. Exact vulnerability count was not reconstructed from retained job output. |
| Frontend ESLint | Passed. |
| Frontend type check | Passed. |
| Frontend tests | Passed. Exact current count was not available in the retained job metadata inspected for this pack. |
| Frontend production build | Passed. |
| API and web container builds | Passed. |

Evidence type: current CI execution. Confidence: high for what each job actually ran; it is not production workflow evidence.

### Documentation-branch checks

This pack changes Markdown only. Final link, Mermaid inspection, secret/PII scan, and `git diff --check` results are recorded in the handoff report; no application suite is required solely for documentation changes.

## D. Carried-forward evidence

Carried-forward evidence is context, not a current rerun.

| Evidence | Classification |
| --- | --- |
| AQGreen browser journey—registration, real application join, payment confirmation to waiting state, natural Area Admin discovery, queue/badge, payment evidence, approval, fresh Member authentication, Active state, rejection reason, Area isolation, funeral inclusion, reload/restart/re-authentication durability | `CARRIED FORWARD — VERIFIED AT 6440849` and recorded by issue #67. Not rerun on `6ba776b`. The Yoco step used a correctly signed simulated webhook after the available real test credential failed; it did not prove Yoco platform delivery. |
| Focused funeral migration matrix (11/11), runtime/approval independence (4/4), EF model clean and Release build | `CARRIED FORWARD` from the payment/approval branch review before PR #54; implementation subsequently merged. Current main CI is newer broad automated evidence, but not a replacement for PostgreSQL-specific meaning where a test used that provider. |
| Cutoff-effective AQGreen hold regression and full backend 747/747 | `CARRIED FORWARD — VERIFIED ON THE PROGRAMME-ENGINE INTEGRATION WORK`, documented in `aqgreen-hold-cutoff-derivation.md`; current main CI is newer. |
| Previous frontend counts (377/381/383 across branch stages) | `HISTORICAL`; not stated as the current test count. Current CI reports the job passed without a count recovered for this review. |

## E. External and unavailable evidence

| Dependency | Status |
| --- | --- |
| Real Yoco test/live checkout and actual provider webhook delivery | `NOT VERIFIED` at this baseline. Application-side signed-webhook processing is evidenced. |
| Yoco settlement into Aqua's bank account | `NOT REPRESENTED / NOT VERIFIED`. |
| Refund, dispute and chargeback behaviour | `UNRESOLVED`; no authorised programme/ledger policy. |
| Actual Bird recipient delivery, bounce or rejection | `NOT VERIFIED`; outbox intent and worker mechanics are tested. |
| External funeral insurer enrolment, policy, waiting-period completion or cover commencement | `NOT REPRESENTED / NOT VERIFIED`. |
| Current production deployment and enablement of the merged programme schema | `STATUS: UNDER ACTIVE INVESTIGATION` because of the P0001 migration incident. |
| Weekly worker production cycle | `NOT ENABLED / NOT VERIFIED`. |
| Monthly worker production cycle | `NOT ENABLED / NOT VERIFIED`. |

## F. Confirmed business decisions and boundaries

| Decision | Authoritative boundary |
| --- | --- |
| AQGreen joining | ZAR 1,200 once or two distinct ZAR 600 joining payments. |
| AQGreen monthly | Separate ZAR 600 recurring obligation, seven-day grace; due day unresolved. |
| Direct Onyx joining | Full ZAR 6,120 confirmed payment; no direct-entry instalments. |
| Payment and approval | Payment completion awaits the responsible Area Admin; only approval activates. |
| Funeral inclusion | Earned on final qualifying AQGreen joining payment; `IncludedAt` is final `MemberPayment.ConfirmedAt`; approval/rejection does not earn/remove it. |
| Programme separation | AQGreen and Onyx participation, recruitment, payments and ledgers remain separate. |
| Recruitment structure | Five complete branches per level; both AQGreen and Onyx structurally continue through Levels 1–5: 5, 25, 125, 625, and 3,125 people. |
| AQGreen commission rates | Per-person rates are confirmed at R30 for Level 1 and R10 for Levels 2–3. Level 4/5 rates are unresolved; current authorised components end at Level 3. |
| Weekly cycle | Friday–Thursday, Johannesburg time. |
| Initial automated commission terms boundary | 14 August 2026 00:00 Johannesburg. This is the engine's first effective-dated boundary, not necessarily the business model's creation date. |
| Automatic history | No automatic commission backfill before the initial cycle. |
| History | Current state and deployment time cannot substitute for business-effective evidence. |

The owner-confirmed AQGreen Level 1–3 commission facts may be taken from the reviewed historical communication. Other payment, activation, funeral-cover, or loan wording in that same historical source remains subject to the current rules and contradiction boundaries in this register; it is not restored by association.

## G. Unresolved business decisions

| Decision | Impact and current safe state | Owner |
| --- | --- | --- |
| AQGreen Level 4 and Level 5 per-person commission rates | Structural requirements are confirmed at 625 and 3,125 people. No Level 4/5 rate, zero amount, or extrapolation is authorised; future rates must be effective-dated. | Aqua programme/commission owner |
| AQGreen `DueDayOfMonth` and first authorised due policy | Blocks monthly worker enablement; no date is invented. | Aqua business owner (#61) |
| Upline effect of an overdue AQGreen member | Current implementation holds only that member's payout and preserves placement. | Aqua programme/commission owner |
| External funeral-cover process | Aqua inclusion exists; insurer enrolment, six-month waiting meaning, cover dates and claims facts remain unclaimed. | Aqua + insurer/compliance |
| Refund/dispute/chargeback consequences | No automatic reversal of participation, inclusion, network or historical earnings. | Aqua Finance/Product/Legal |
| Legacy-member import evidence and authority | No fake modern payments; future audited import required. | Aqua Operations/Business/Compliance |
| Authorised reconciliation outcomes | Discovery is read-only until accepted evidence, decisions and audit requirements are defined. | Aqua Finance/Ops/Product (#66) |
| Administrative visibility of full stored decision/correction history | Audit exists in storage; who needs which read view is not fully decided. | Product/Compliance |

## H. Current operational blockers

These are not the same as business questions.

1. **P0001 production migration incident** — production row classification/remediation is not merged or production verified. Agent 1 owns the active workstream.
2. **Weekly enablement gates** — initial rows/baselines in production, topology, PostgreSQL application-path idempotency, E2E, recovery, observability and controlled arming remain open (#55–#60).
3. **Monthly enablement gates** — business due day, due-policy, E2E, reconciliation resolution and Yoco acceptance remain open (#61–#66).
4. **Provider acceptance** — real Yoco and Bird external outcomes remain unverified.
5. **Automated full AQGreen E2E** — historical manual browser evidence exists, but recurring CI E2E is open (#67).

## I. Open GitHub work

Current issue state was inspected on 11 August 2026.

| Issue | Current purpose |
| --- | --- |
| [#55 Weekly commission production enablement](https://github.com/aqua-lifestyle/aqua-lifestyle/issues/55) | Controlled terms/Area baseline and worker arming. |
| [#56 Weekly commission production E2E acceptance](https://github.com/aqua-lifestyle/aqua-lifestyle/issues/56) | Full PostgreSQL business-path acceptance. |
| [#57 PostgreSQL application-path commission idempotency](https://github.com/aqua-lifestyle/aqua-lifestyle/issues/57) | Invoke real application path twice and prove persisted idempotency. |
| [#58 Missed weekly cycle recovery/runbook](https://github.com/aqua-lifestyle/aqua-lifestyle/issues/58) | Safe authorised recovery without arbitrary automatic backfill. |
| [#59 Weekly enablement preflight](https://github.com/aqua-lifestyle/aqua-lifestyle/issues/59) | Full read-only enablement gate. Its WIP file references are archived; current main has a smaller bootstrap/preflight service. |
| [#60 Commission diagnostics/observability](https://github.com/aqua-lifestyle/aqua-lifestyle/issues/60) | Explain outcomes safely; implementation referenced by issue remains archived WIP. |
| [#61 AQGreen monthly due-day decision](https://github.com/aqua-lifestyle/aqua-lifestyle/issues/61) | Business authority for the due day. |
| [#62 Monthly obligation production enablement](https://github.com/aqua-lifestyle/aqua-lifestyle/issues/62) | Controlled policy/worker rollout. |
| [#63 Monthly obligation E2E](https://github.com/aqua-lifestyle/aqua-lifestyle/issues/63) | Generation-to-provider-allocation lifecycle. |
| [#64 Yoco production finality/webhook acceptance](https://github.com/aqua-lifestyle/aqua-lifestyle/issues/64) | Real provider semantics and acceptance. |
| [#65 Programme-engine readiness documentation](https://github.com/aqua-lifestyle/aqua-lifestyle/issues/65) | Stale existing docs; this pack centralises the current baseline but does not silently rewrite those historical files. |
| [#66 Manual financial reconciliation workflow](https://github.com/aqua-lifestyle/aqua-lifestyle/issues/66) | Authorised audited resolution of detected contradictions. |
| [#67 Automated AQGreen joining/payment/approval E2E](https://github.com/aqua-lifestyle/aqua-lifestyle/issues/67) | Durable CI regression for the browser-proven workflow. |

## J. Engineering risks

| Risk | Impact | Evidence | Status / owner | Required follow-up |
| --- | --- | --- | --- | --- |
| P0001 modern AQGreen contradiction blocks funeral migration | API deployment/schema may not advance; production facts may need classification | Merged fail-closed migration; active inventory branch, no merged production result | Blocking deployment incident; Agent 1 + Finance/Ops | Complete read-only inventory, classify from evidence, obtain authorised remediation, preserve audit. |
| AQGreen structural depth exceeds currently authorised commission depth | Future rate changes could rewrite history or invent components if the two concepts are conflated | Five-level evaluator tests, separate qualified/commissioned projections, and Level 4/5 calculator regressions | Mitigated in the current implementation; Engineering | Keep Level 4/5 components absent until owner-authorised effective-dated rates exist. |
| Weekly workers enabled before inputs are authoritative | False or duplicate financial ledgers | Workers default off; open issues #55–#60 | Blocking enablement; Ops/Engineering/Business | Complete prerequisites in document 06. |
| Monthly worker enabled without due policy | Invented member obligation dates | Empty append-only policy and issue #61 | Blocking enablement; Business/Ops | Authorise due day and first month, then E2E and controlled rollout. |
| Manual reconciliation resolution absent | Detected financial contradictions become operational dead ends | Read-only queries, no resolution API; #66 | Blocking monthly enablement; Product/Finance/Engineering | Define outcomes/evidence and implement immutable audit workflow. |
| Provider finality/timestamps incomplete | Historical hold or payment meaning may be wrong | #64 and temporal matrix | Required confidence; Yoco/Engineering/Ops | Real provider contract and controlled acceptance. |
| Destructive funeral migration Down | Rollback after use loses entitlements | Migration inspection and tests | Operational risk; DBA/Ops | Backup, forward remediation or controlled restoration. |
| External email outcome absent | Operators may mistake outbox/Bird acceptance for delivery | Deployment/outbox design | Non-blocking for durable queue; Ops | Add provider delivery evidence if release requires it. |
| Demo/legacy terminology and numeric-ID fixture contamination | Misleading UI/tests and unreliable acceptance actors | Demo seed still contains Business Premier; historical clean-actor rerun passed | Safe follow-up; Product/QA | Use clean actors; retire stale demo material separately. |
| Active member-journey visual work not merged | Pack can fossilise proposed UI | Separate Agent 2 worktree | Non-blocking; Agent 2/Product | Update pack after authoritative merge; do not claim current. |

## K. Accepted or deferred technical debt

- Full approval/correction audit history is stored but not comprehensively surfaced.
- Webhook rate limiting and a dedicated parallel-delivery regression remain hardening candidates.
- Successful provider events do not yet form a complete durable inbox with every failed attempt.
- Release/payment recording lacks provider-verified transfer occurrence and globally unique payout identity.
- Refund, dispute, chargeback and financial adjustment ledgers are not defined.
- Weekly diagnostics and broad preflight WIP at archive commit `df70e6b` remain reference only.
- Legacy `Entry*` technical names remain for database/code compatibility.

## L. Existing documentation audit

| Source | Classification | Material note |
| --- | --- | --- |
| [`programme-payment-approval-workflow.md`](../development/programme-payment-approval-workflow.md) | `CURRENT` | Strong current execution map and historical funeral rule. |
| [`payment-testing.md`](../development/payment-testing.md) | `CURRENT` with operational caveats | Correct compatibility/testing boundary; production acceptance still outstanding. |
| [`aqgreen-hold-cutoff-derivation.md`](../verification/aqgreen-hold-cutoff-derivation.md) | `CURRENT IMPLEMENTATION / HISTORICAL EVIDENCE` | Fix is merged; branch test totals are carried forward. |
| [`monthly-obligation-checkout-reconciliation.md`](../verification/monthly-obligation-checkout-reconciliation.md) | `CURRENT` | Current fail-closed allocation/triage boundary. |
| [`release-report-gap-closure.md`](../verification/release-report-gap-closure.md) | `PARTIALLY CURRENT / HISTORICAL` | Valuable design/evidence; some status and counts predate current main. |
| [`weekly-commission-temporal-input-matrix.md`](../verification/weekly-commission-temporal-input-matrix.md) | `PARTIALLY CURRENT` | Issue #65 tracks stale classification/status wording. |
| [`business-rule-matrix.md`](../verification/business-rule-matrix.md) | `PARTIALLY CURRENT` | Several core rules are useful; single-payment-only rows are superseded by flexible joining. |
| [`verification-report.md`](../verification/verification-report.md) | `HISTORICAL` | Explicitly superseded by later integration and current CI. |
| [`requirements.md`](../BusinessDocs/requirements.md) | `PARTIALLY CURRENT` | BR-16 is current; FR-44 still marks funeral cover missing and mixes legacy product concepts. |
| [`onyx-implementation-plan.md`](../BusinessDocs/onyx-implementation-plan.md) | `PARTIALLY CURRENT` | Rich confirmed boundaries, but its three-level AQGreen wording and some deferred/cutoff status wording are superseded. |
| [`future-roadmap.md`](../BusinessDocs/future-roadmap.md) | `PARTIALLY CURRENT` | Issue #65 tracks readiness corrections. |
| [`workflows.md`](../BusinessDocs/workflows.md) | `HISTORICAL / TARGET` | Explicit target flows include legacy Business Premier and savings assumptions, not current programme authority. |
| [`yoco-production-readiness-review.md`](../BusinessDocs/yoco-production-readiness-review.md) | `PARTIALLY CURRENT / HISTORICAL` | Security architecture is useful; single-only AQGreen and payment-activation wording is superseded. |
| [`Assumptions.md`](../Assumptions.md) | `HISTORICAL ASSUMPTIONS` | A15 admin-only frontend and A17 manual-EFT-only assumptions are superseded; A21 remains a boundary, not proof of insurer state. |
| Business PDFs under `docs/BusinessDocs` | `HISTORICAL BUSINESS SOURCE` | Preserve original language and benefits, but resolve contradictions through confirmed decisions. |
| Archive commit `df70e6b` | `WIP / REFERENCE` | Unreviewed diagnostics/preflight and doc corrections; never treated as merged implementation. |

## M. Documentation contradictions discovered

### 1. AQGreen joining schedule

- **Conflicting sources:** Current BR-16/code permits R1,200 once or R600+R600; older business-rule matrix and Yoco review wording describes R1,200 once for new customers and instalments as historical only.
- **Current authoritative evidence:** Confirmed business decision and merged implementation at `6ba776b7` support both schedules.
- **Status:** `SUPERSEDED` (older single-only wording).
- **Impact:** Following the old text could deny a valid joining choice or produce an incompatible checkout.
- **Required action:** Use document 02 and current code; retain old sources as historical evidence only.

### 2. Payment versus activation

- **Conflicting sources:** `docs/deployment.md` and older Yoco text say a valid webhook activates AQGreen/Onyx; the merged lifecycle requires an Area Admin decision after confirmed payment.
- **Current authoritative evidence:** Domain, approval workflow, tests, and carried-forward browser evidence at the commits recorded in sections C and D.
- **Status:** `SUPERSEDED` (payment-activation wording).
- **Impact:** Treating payment as approval would bypass an audited authorization boundary.
- **Required action:** Document and operate `payment confirmed -> awaiting approval -> Area Admin decision` only.

### 3. Funeral-cover inclusion and external cover

- **Conflicting sources:** `requirements.md` marks FR-44 missing and a business PDF describes a six-month external plan; current code records Aqua's R30,000 inclusion at joining-payment completion without modelling insurer activation.
- **Current authoritative evidence:** Confirmed Aqua inclusion rule and merged implementation at `6ba776b7`; no authoritative insurer integration evidence exists.
- **Status:** `CURRENT RULE CONFIRMED` for Aqua inclusion; `UNRESOLVED` for insurer enrolment, waiting period, and external cover.
- **Impact:** Conflation could either hide an earned Aqua inclusion or falsely claim external insurance facts.
- **Required action:** Show Aqua inclusion only; obtain insurer/compliance authority before documenting external cover semantics.

### 4. Commission readiness

- **Conflicting sources:** Older gap/plan documents call cutoff holds or terms selection incomplete; current `main` includes cutoff derivation, immutable terms, and a limited bootstrap/preflight.
- **Current authoritative evidence:** Merged code and CI at `6ba776b7`; production baseline rows, enablement, and production-cycle evidence remain absent.
- **Status:** `CURRENT RULE CONFIRMED` for implementation; enablement remains open.
- **Impact:** Old text understates implementation, while an unqualified completion claim would overstate operational readiness.
- **Required action:** Use documents 03 and 06; complete issues #55–#60 before enablement.

### 5. AQGreen monthly workflow

- **Conflicting sources:** Older plans call scheduling/payment allocation deferred; current `main` implements scheduling and obligation-linked checkout/allocation.
- **Current authoritative evidence:** Merged code and CI at `6ba776b7`; due day, full E2E, resolution workflow, and enablement are not established.
- **Status:** `CURRENT RULE CONFIRMED` for implementation; `UNRESOLVED` business due day and operational gates.
- **Impact:** Either description alone could cause operators to miss available controls or enable an unauthorised obligation date.
- **Required action:** Keep the worker disabled until document 06 prerequisites and issues #61–#66 are satisfied.

### 6. Frontend audience and scope

- **Conflicting sources:** `Assumptions.md` describes an internal-admin-only frontend; current `main` includes signed-in customer/member programmes, progress, savings, obligations, loans, and invitations.
- **Current authoritative evidence:** Merged frontend and CI at `6ba776b7`. Agent 2's proposed visual journey is unmerged and not current evidence.
- **Status:** `SUPERSEDED` (admin-only assumption); `ACTIVE WORKSTREAM` for Agent 2 visuals.
- **Impact:** Readers could omit existing customer journeys or mistake proposed visual changes for delivered behaviour.
- **Required action:** Document current-main views only and reassess after Agent 2 merges and is verified.

### 7. Payment channel

- **Conflicting sources:** `Assumptions.md` and target workflows assume manual EFT/proof only; current `main` integrates hosted Yoco checkout.
- **Current authoritative evidence:** Merged server-side checkout/webhook integration and CI at `6ba776b7`; real provider delivery remains unverified.
- **Status:** `SUPERSEDED` (manual-only assumption).
- **Impact:** Old text misstates the current contract; overstatement could falsely claim Yoco production acceptance.
- **Required action:** Describe Yoco as integrated but not production verified; keep manual material historical.

### 8. Programme names

- **Conflicting sources:** Business Premier appears in older documents, demo seed, and compatibility code; current programme rules use Onyx.
- **Current authoritative evidence:** Confirmed programme boundary and merged Onyx domain at `6ba776b7`.
- **Status:** `CURRENT RULE CONFIRMED`; legacy terminology remains as historical/compatibility debt.
- **Impact:** Treating Business Premier as an Onyx alias could apply the wrong benefits, payments, or network rules.
- **Required action:** Use Onyx for the current network programme and retire stale demo terminology separately.

### 9. Issue #59 preflight location and scope

- **Conflicting sources:** Issue #59 references archived standalone WIP files; current `main` has a smaller preflight in `AdminCommissionBootstrapAppService`.
- **Current authoritative evidence:** Repository inspection at `6ba776b7` and archive commit `df70e6b`; issue acceptance has not been established.
- **Status:** `UNRESOLVED` open issue; archived WIP is not merged implementation.
- **Impact:** A stale file checklist could produce a false completion claim or prompt duplicate work.
- **Required action:** Reconcile issue #59 against current behaviour and remaining acceptance criteria before closing it.

### 10. AQGreen structural depth and commission depth

- **Conflicting sources:** Earlier programme documents and the pre-correction code stopped AQGreen at Level 3; owner-confirmed business intent defines AQGreen structural Levels 1–5 while authorising commission rates only through Level 3.
- **Current authoritative evidence:** Owner clarification confirms structural populations of 5, 25, 125, 625, and 3,125 and per-person rates of R30, R10, and R10 for Levels 1–3. The current evaluator and enum represent Levels 1–5; weekly ledger and API projections record structural qualification separately from commissioned depth; focused Level 4/5 regressions retain only the Level 1–3 components.
- **Status:** `IMPLEMENTED` for five structural levels; `UNRESOLVED` for Level 4/5 rates.
- **Impact:** Conflating structural depth with authorised commission depth can hide valid network progress or invent unauthorised money.
- **Required action:** Add no Level 4/5 commission amount until Aqua authorises effective-dated rates; introduce any future rate only through reviewed effective-dated terms.

## N. Superseded terminology and rules

| Superseded item | Replacement / boundary |
| --- | --- |
| `Entry` as a customer programme name | AQGreen; `Entry*` remains technical compatibility naming. |
| Business Premier as Onyx | Onyx is the current programme; Business Premier remains separate legacy catalogue/demo material. |
| New AQGreen joining is R1,200 only | R1,200 once or two distinct R600 joining instalments. |
| R1,200 is monthly | R1,200 is joining; R600 is the separate monthly obligation. |
| Payment success activates participation | Payment complete awaits Area Admin approval. |
| Monday–Sunday or caller-selected weekly periods | Friday–Thursday Johannesburg closed cycles. |
| Recalculate old cycles from current state/terms | Reconstruct cutoff-effective facts and effective-dated terms; fail closed otherwise. |
| Enabled means production ready | Enabled and production verified are separate statuses. |

## O. Final evidence conclusion at baseline

### Implementation

The programme engine implements the intended modern joining, payment/approval, inclusion, monthly-obligation, and AQGreen commission boundaries. AQGreen structural qualification now continues through Level 5 while currently authorised commission components remain limited to Levels 1–3.

### Workflow

The customer/Area Admin approval and rejection workflow has historical browser evidence and current automated coverage. Current production migration/payment acceptance is under investigation; automated recurring E2E remains open.

### Evidence

Focused repository tests cover AQGreen structural Levels 1–5, incomplete branches, the unchanged Level 1–3 amounts, Level 4/5 structural-versus-commissioned projections, and absence of Level 4/5 components. Authoritative CI remains the broad regression gate. Production provider, deployment, worker enablement, external delivery, and historical reconciliation resolution must not be inferred from repository tests.
