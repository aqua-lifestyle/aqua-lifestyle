# Verification, decision, and risk register

This register is the status and evidence companion to the Aqua system pack. It is truthful at:

```text
Repository: aqua-lifestyle/aqua-lifestyle
Branch baseline: origin/main
Starting commit for the Area/Tenant branch: e8188101e85fbe2cdd42862a3267d2fdedaf2d55
Evidence reviewed: 11 August 2026
```

The production AQGreen reconciliation and programme-network corrections are merged in the starting baseline. Separate member/customer visual work is not treated as authoritative unless merged and verified at its resulting commit.

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
| AQGreen network qualification | `MERGED` | Structural Levels 1–3 are represented and Level 3 is the maximum. Focused tests cover complete/incomplete same-Tenant structures and the Level 3 cap. |
| Programme network tenant boundary | `MERGED` | AQGreen and Onyx graph construction, recruitment placement, host calculation, ledgers, and projections are bounded by Tenant and programme. Mixed-Tenant graph input fails closed. |
| Tenant/Area separation | `IMPLEMENTED — CURRENT BRANCH` | Area aggregate, effective customer assignment, multi-Area administrator assignment, Area-scoped approval, Johannesburg backfill and PostgreSQL constraints/tests. Tenant remains the hard boundary and same-Tenant cross-Area recruitment remains valid. Not merged, deployed, or production verified. |
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

### Carried-forward `origin/main` CI baseline

GitHub Actions run [31455701182](https://github.com/aqua-lifestyle/aqua-lifestyle/actions/runs/31455701182) completed successfully at exact commit `6ba776b7` on 11 August 2026. It predates the current `e8188101` branch baseline and is retained only as exact historical evidence; the Area/Tenant PR CI is the current merge gate.

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
| Recruitment structure | Five complete branches per level. AQGreen ends at Level 3 (5, 25, 125); Onyx continues through Level 5 (5, 25, 125, 625, 3,125). |
| AQGreen commission rates | Per-person rates are confirmed at R30 for Level 1 and R10 for Levels 2–3. Level 3 is final, with a cumulative weekly amount of ZAR 1,650. |
| Weekly cycle | Friday–Thursday, Johannesburg time. |
| Initial automated commission terms boundary | 14 August 2026 00:00 Johannesburg. This is the engine's first effective-dated boundary, not necessarily the business model's creation date. |
| Automatic history | No automatic commission backfill before the initial cycle. |
| History | Current state and deployment time cannot substitute for business-effective evidence. |

The owner-confirmed AQGreen Level 1–3 commission facts may be taken from the reviewed historical communication. Other payment, activation, funeral-cover, or loan wording in that same historical source remains subject to the current rules and contradiction boundaries in this register; it is not restored by association.

## G. Unresolved business decisions

| Decision | Impact and current safe state | Owner |
| --- | --- | --- |
| AQGreen `DueDayOfMonth` and first authorised due policy | Blocks monthly worker enablement; no date is invented. | Aqua business owner (#61) |
| Upline effect of an overdue AQGreen member | Current implementation holds only that member's payout and preserves placement. | Aqua programme/commission owner |
| External funeral-cover process | Aqua inclusion exists; insurer enrolment, six-month waiting meaning, cover dates and claims facts remain unclaimed. | Aqua + insurer/compliance |
| Refund/dispute/chargeback consequences | No automatic reversal of participation, inclusion, network or historical earnings. | Aqua Finance/Product/Legal |
| Legacy-member import evidence and authority | No fake modern payments; future audited import required. | Aqua Operations/Business/Compliance |
| Authorised reconciliation outcomes | Discovery is read-only until accepted evidence, decisions and audit requirements are defined. | Aqua Finance/Ops/Product (#66) |
| Administrative visibility of full stored decision/correction history | Audit exists in storage; who needs which read view is not fully decided. | Product/Compliance |

## H. Current operational blockers

These are not the same as business questions.

1. **Weekly enablement gates** — initial rows/activation evidence in production, production preflight, E2E, recovery, owned alert delivery and controlled arming remain open (#55–#60). The task branch adds real-PostgreSQL application-path rollback/retry evidence, but this is not production evidence.
2. **Monthly enablement gates** — business due day, due-policy, obligation completeness, E2E, reconciliation resolution and Yoco occurrence/finality remain open (#61–#66).
3. **Provider acceptance** — real Yoco and Bird external outcomes remain unverified.
4. **Automated full AQGreen E2E** — historical manual browser evidence exists, but recurring CI E2E is open (#67).

The user-supplied operational state on 18 August 2026 says production is healthy. The P0001 deployment report is therefore no longer treated as the current weekly enablement blocker. Current production rows and migrations were not queried in this readiness task.

## I. Open GitHub work

Current issue state was inspected on 11 August 2026.

| Issue | Current purpose |
| --- | --- |
| [#55 Weekly commission production enablement](https://github.com/aqua-lifestyle/aqua-lifestyle/issues/55) | Controlled terms/Area baseline and worker arming. |
| [#56 Weekly commission production E2E acceptance](https://github.com/aqua-lifestyle/aqua-lifestyle/issues/56) | Full PostgreSQL business-path acceptance. |
| [#57 PostgreSQL application-path commission idempotency](https://github.com/aqua-lifestyle/aqua-lifestyle/issues/57) | Task-branch evidence now injects a PostgreSQL ledger failure, proves rollback, retries successfully, and proves a second execution reuses one stable period. Merge/CI status remains separate. |
| [#58 Missed weekly cycle recovery/runbook](https://github.com/aqua-lifestyle/aqua-lifestyle/issues/58) | Safe authorised recovery without arbitrary automatic backfill. |
| [#59 Weekly enablement preflight](https://github.com/aqua-lifestyle/aqua-lifestyle/issues/59) | Task branch expands the current read-only preflight with explicit blocker codes, cycle/startup checks, exact rates, host topology, deleted evidence, operational assertions and fail-closed readiness. Production output remains required. |
| [#60 Commission diagnostics/observability](https://github.com/aqua-lifestyle/aqua-lifestyle/issues/60) | Task branch adds structured per-programme and whole-run evidence. Owned production alert delivery and broader historical diagnostics remain open. |
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
- Broad diagnostics WIP at archive commit `df70e6b` remains reference only. The task branch selectively implements blocker codes and structured run evidence against current Tenant/Area rules.
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

- **Superseded interpretation:** An earlier owner interpretation extended AQGreen structure through Levels 4–5 while rates stopped at Level 3.
- **Current authoritative evidence:** The client subsequently confirmed that AQGreen ends at Level 3, with structural populations of 5, 25, and 125 and per-person rates of R30, R10, and R10. Onyx alone continues through Level 5.
- **Status:** `CLIENT CONFIRMED / IMPLEMENTED` for AQGreen Levels 1–3.
- **Impact:** Retaining the superseded extension would expose nonexistent milestones and qualification states.
- **Required action:** Keep AQGreen qualification and commission outputs capped at Level 3. Preserve Onyx Levels 1–5.

## N. Superseded terminology and rules

| Superseded item | Replacement / boundary |
| --- | --- |
| `Entry` as a customer programme name | AQGreen; `Entry*` remains technical compatibility naming. |
| Business Premier as Onyx | Onyx is the current programme; Business Premier remains separate legacy catalogue/demo material. |
| New AQGreen joining is R1,200 only | R1,200 once or two distinct R600 joining instalments. |
| R1,200 is monthly | R1,200 is joining; R600 is the separate monthly obligation. |
| Payment success activates participation | Payment complete awaits Area Admin approval. |
| AQGreen structurally continues through Levels 4–5 | AQGreen ends at Level 3; Onyx remains the five-level programme. |
| Monday–Sunday or caller-selected weekly periods | Friday–Thursday Johannesburg closed cycles. |
| Recalculate old cycles from current state/terms | Reconstruct cutoff-effective facts and effective-dated terms; fail closed otherwise. |
| Enabled means production ready | Enabled and production verified are separate statuses. |

## O. Final evidence conclusion at baseline

### Implementation

The programme engine implements the intended modern joining, payment/approval, inclusion, monthly-obligation, and AQGreen commission boundaries. AQGreen structural qualification and commissioned depth both end at Level 3; Onyx remains separate through Level 5.

### Workflow

The customer/Area Admin approval and rejection workflow has historical browser evidence and current automated coverage. Current production migration/payment acceptance is under investigation; automated recurring E2E remains open.

### Evidence

Focused repository tests cover AQGreen structural Levels 1–3, incomplete branches, the confirmed Level 1–3 amounts, the Level 3 cap for larger networks, and tenant isolation. Authoritative CI remains the broad regression gate. Production provider, deployment, worker enablement, external delivery, and historical reconciliation resolution must not be inferred from repository tests.

## P. Business-decision authority convention

This convention defines who may establish business-policy authority without
retroactively reclassifying this register's historical entries.

| Status | Meaning |
| --- | --- |
| `UNRESOLVED` | No confirmed policy currently governs the stated question. A proposal may exist, but the question remains unresolved until owner confirmation is durably recorded. |
| `PROPOSED` | A candidate rule drafted for owner consideration. It is not authority and must not govern material behavior as though accepted. |
| `CONFIRMED` | The business owner explicitly authorized the rule and its stated scope. Agents cannot create this status from inference, code, tests, precedent, or silence. |
| `SUPERSEDED` | A newer `CONFIRMED` decision explicitly replaces the older rule for a stated scope or effective boundary. The older record remains traceable. |

Agents may investigate ambiguity, compare evidence, recommend an outcome, and draft
a `PROPOSED` record. They must not independently mark or change a material decision
to `CONFIRMED`. A worklog, agent report, PR description, implementation state, or
agent-authored chat summary is not business authority. A direct owner authorization
is authority only for the scope actually authorized. A material `CONFIRMED` record
must contain durable evidence for all of the following:

- decision ID;
- status;
- scope;
- decision text;
- authorizing owner identity or role sufficient for repository traceability;
- confirmation date;
- durable source or evidence locator;
- effective boundary when relevant; and
- superseded decision ID when applicable.

If any required confirmation evidence is unavailable, do not invent it or
retroactively upgrade the record. Keep the question `UNRESOLVED`, or the candidate
`PROPOSED`, until explicit owner authorization and its durable evidence exist.

Record a new material decision in the narrowest current authority: document 02 for
cross-system business rules, this register for decision/status boundaries, the
Placement V2 specification for V2 design decisions, or an accepted ADR for an
engineering architecture decision. Do not duplicate the rule across all of them.
Each new record must preserve the fields above. A newer confirmed rule must mark the
older one `SUPERSEDED` and link both decision IDs; never silently rewrite history.

These decision statuses are separate from `IMPLEMENTED`, `TESTED`, `INTEGRATED`,
`MERGE READY`, `MERGED`, `DEPLOYED`, `ENABLED`, and `PRODUCTION VERIFIED`. A confirmed
policy may be unimplemented, and merged code may lack business authority.
