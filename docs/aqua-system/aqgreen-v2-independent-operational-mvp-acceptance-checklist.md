# AQGREEN V2 — INDEPENDENT OPERATIONAL MVP ACCEPTANCE CHECKLIST

**Agent 2 · 12 October 2026 · Verdict: READY FOR OWNER WALKTHROUGH**

> **Current session preparation:** use the [owner walkthrough execution package](aqgreen-v2-owner-walkthrough-execution-ready-package.md).
> The owner authorizes an isolated synthetic-money walkthrough, but participants
> are not formally designated. No recordings of any kind are permitted. Obtain
> informed consent before de-identified written observations. All human outcomes
> below remain NOT RUN; prior engineering evidence is not human acceptance.
> Newer records report a 17/17 **agent-operated** execution and separate Node 24
> derivative qualification. The recorded original technical acceptance does not
> supply human observations, participant designation or consent. See the execution
> package for the exact selected candidate identity; do not mark these human rows
> PASS from the agent results.

**Bounded decision:** ready for the guided, isolated owner/administrator review
below, not accepted live operation or production. The initial scoped-review
evidence blocker closed during this inspection: Agent 1 now reports 14 passing
JWT/direct-HTTP PostgreSQL checks, 69 passing backend regressions and 19 UI checks,
and has corrected its host-only acceptance statement. Agent 2 independently
inspected the corresponding code/test definitions, but did not rerun those results.
Known clarity corrections below remain tracked. No actual Aqua administrator was
contacted or observed; no provider, production or customer data was accessed.

Owner scope: existing Aqua operations; every authenticated administrator is
eligible to review sales **within authorized records**; Yoco online joining is
priority; R1,200 once or two distinct R600; manual 5/5/5 review; system-calculated
commissions; no release/payment. Cash processing, retail/inventory, Onyx, B6 and
B7 are deferred. They are not isolated-MVP acceptance prerequisites.

## 1. Set up one owner-authorized walkthrough

**Facilitator:** reuse Agent 1's guarded local `AQGreenV2Demo` stack and disposable
database setup with prepared task-owned fixtures. Completed test infrastructure
was cleaned up; do not assume a stack is still running. Complete the package's
pre-start checks and consent fields. Confirm the isolated adapter is selected, outbound provider access is
denied as in the existing local setup, and the banner says **TEST ENVIRONMENT —
SIMULATED MONEY — NO REAL PAYMENT OR EARNINGS**. Do not use Yoco test/live keys
or send a provider request. Record the candidate/build, fixture identity and
displayed closed week. Do not use production members.

**Actors:** synthetic sponsor; new member accounts; assigned-Area administrator
reviewer/approver; out-of-scope administrator; ordinary member/anonymous session;
separately permissioned calculation operator. One human may hold several permitted
roles, but use separate browser sessions so the acting identity is clear.

**Fixtures:** default demo already has a complete 31-member tree. Use a newly
approved sponsor with an empty immediate branch and no competing placements in
its subtree for first-five/sixth placement.
Use separate fresh scenarios for mutually exclusive final sales outcomes. A
positive financial-hold case needs the existing controlled overdue-obligation
fixture, not merely a no-payout banner. Ask Agent 1 to provide the prepared fixture
or bounded existing evidence; if unavailable, mark the case NOT RUN.

**Time boundary:** members placed today cannot qualify a week that closed earlier.
Use the existing pre-cutoff synthetic tree for closed-week calculation; never
backdate the new members, reviews or payments to join these scenarios together.

For each row record **PASS / FAIL / NOT RUN**, case ID, acting account/role,
member/period, before/after state, expected versus observed result and evidence
reference. Record assistance and incorrect explanations. Never retain passwords,
JWTs, raw payment payloads or one-time email-verification links.

## 2. Administrator walkthrough and expected outcomes

UI routes/actions below exist in the candidate. **F** means a technical facilitator
uses the existing isolated API or read-only fixture evidence under the stated
actor's session; it is not a fabricated admin screen. Do not repeat automated
suites during this walkthrough.

### Joining, approval, placement and progress

| Check | Actor / executable action | Correct result and evidence to record | Must never happen |
| --- | --- | --- | --- |
| [ ] **J1 Invitation / signup** | Sponsor: `/member/invitations`, AQGreen invitation. New member: `/i/<code>` → **Create my account**, create synthetic account, verify email, sign in and return to invitation. F obtains the synthetic verification link from existing `POST /api/demo/aqgreen-verification-link`; there is no mailbox UI. | Sponsor shown correctly; programme/Tenant context retained. Record account/verification outcome, then immutable invitation attribution/confirmation IDs after acceptance. Preserve the link only in memory. | Wrong-programme/self/invalid invitation accepted; member reads the admin test mailbox; registration creates permanent placement. |
| [ ] **J2 Full joining** | New member chooses R1,200 and **Accept and open simulated checkout**. Read exact amount; **Confirm simulated payment**; open `/member/programmes`. | One matched synthetic R1,200 joining payment; **Awaiting Area approval**; no Active status or placement. Record checkout/payment/participation IDs and state. | Redirect or payment itself activates; actual Yoco call; monthly payment used as joining. |
| [ ] **J3 Two instalments** | Separate new member chooses **Pay two R600 instalments**. Confirm first; view status; **Complete joining…600**; confirm second. | First: 50%, R600 remaining, non-Active. Second: distinct checkout and payment IDs, two R600 records, pending approval. Record both links/amounts. | First R600 satisfies joining; one payment fills both slots; another joining charge after completion. |
| [ ] **J4 Incorrect/repeated confirmation — F** | Under member session, call existing `POST /api/demo/aqgreen-checkout/confirm` with that checkout ID and incorrect amount, then correct amount; repeat exact correct request. | Wrong amount rejected without mutation; matching repeat returns already-confirmed and reuses one payment. Another member cannot confirm it. Capture sanitized response/state counts, not credentials. | Wrong amount accepted, duplicate payment created, identity/purpose changed. Synthetic endpoint replay does not prove Yoco webhook authenticity. |
| [ ] **J5 Approval too early / unauthorized — F** | On first-R600-only participation, invoke existing `ApproveProgrammeParticipation` action. Try a fully paid participation as ordinary member and as admin outside its assigned Area/Tenant. | Denied; payment/participation/decision/placement counts unchanged. Queue absence alone is not direct-API proof. | Early activation; broad access inferred from admin login or sponsor's Area. |
| [ ] **J6 Authorized approval** | Assigned-Area approver: `/admin/programme-participations`, inspect matching full payment and attribution; **Approve**. F repeats the existing approval API action; no second UI button is assumed. Member signs in afresh. | One approval decision and one permanent placement, Active together; repeat creates nothing. Member can view active progress. Record actor/time, sponsor ID, parent/slot/path from F's read-only placement evidence. | Active-but-unplaced success; ordinary admin chooses parent/slot; repeated approval creates a second position. |
| [ ] **P1 First five / sixth spillover** | From empty-branch sponsor S, repeat genuine synthetic invitation/join/approval for six members. F reads placements. | First five under S at slots 1–5, paths S+`1`…S+`5`; sixth under S's first child at slot 1, path S+`11`. Sixth retains S as sponsor, not its placement parent. Record scope, paths, slots and attribution IDs. | Round-robin or discretionary parent selection; transfer of recruitment credit; cross-tree/Tenant allocation. Do not call an already-full root's next newcomer its sixth. |
| [ ] **P2 Progress / cutoff** | Member: `/member/programmes`; explain current counts and level against F's fixture facts. | Qualifying placement occupants, credited recruitment and completed structural levels remain distinct. Six immediate-branch newcomers do not mean Level 2. Old closed-week progress excludes later placements. | Aggregate descendant count or spillover presented as personal recruitment; structure presented as earned money. |

### Weekly review, commission and no-payout boundary

Use `/admin/weekly-sales-reviews`; when no review exists, existing route
`?tenantId=<id>&participantId=<id>` opens the latest closed target and offers
**Start weekly sales review**. The list is not automatically every member needing
a review. Use the [playbook's case/allocation register](aqgreen-v2-mvp-administrative-operations-playbook.md#3-smallest-duplicate-evidence-control-one-manual-allocation-register).

| Check | Actor / executable action | Correct result and evidence to record | Must never happen |
| --- | --- | --- | --- |
| [ ] **S1 Assigned admin access** | Ordinary authenticated assigned-Area admin—not a host-only workaround—opens list/target, begins review and performs supported confirmation/rejection in separate cases. Explicitly authorized central reviewer repeats within broader synthetic scope. F attempts out-of-scope list/detail/target and writes under other actors. | Allowed own records and explicitly granted central records; denied other Tenant/unauthorized Area, missing permission, revoked assignment and anonymous/member cases; no denied mutation. Record current evidence for read **and** write paths. | All-admin eligibility becomes unrestricted visibility; reviewer receives calculation/payout authority automatically. |
| [ ] **S2 Confirmed + Met** | Review synthetic evidence for the exact member/week; clear sale-line reservations; enter supported THZ125/1L/5L quantities 5/5/5 and opaque references; **Confirm sales**. | **Confirmed · Met**, exact quantities, version, final reviewer/time and immutable decision ID. Refresh and repeat exact request via F: same decision. | Reviewer types money or overrides threshold; administrative evidence called external commerce. |
| [ ] **S3 Shortfall / incorrect quantities** | In a fresh case, correct a mistaken draft tally from 5/5/5 to supported 5/5/4 before confirmation. Also inspect existing 15/0/0 evaluator evidence. | **Confirmed · Not met** for supported shortfall; no category substitution; structural level unchanged. Final quantity changes are refused, not quietly saved. Record supported tally and result. | Missing proof called zero; shortfall mislabeled rejected evidence; totals alone satisfy 5/5/5. |
| [ ] **S4 Rejected** | Separate case: provide unacceptable evidence reference, record why, **Reject evidence**. | Rejected with reason/actor/time; no authoritative quantities or threshold. Final conflicting confirmation denied. | Rejection silently reopened to earn money or converted to confirmed shortfall. |
| [ ] **S5 Held / missing / late** | **Start weekly sales review**, leave incomplete evidence held; log missing item/reason/admin/time in companion register. Operator attempts calculation with unresolved candidate evidence. Review an older-week case if available. | Held has no final quantities/result/reviewer. Candidate missing/held evidence prevents final Tenant/week calculation. Late evidence keeps original week; after finalization/rollover it is an exception, not sales banked forward. | Invented zero/rejection to unblock batch, fabricated deadline, silent historical recalculation. No dedicated in-app hold-reason or older-cycle calculation screen is assumed. |
| [ ] **S6 Duplicate sale evidence — manual** | Two admins submit renamed copies/receipt-and-invoice for the same synthetic sale line, for different members/weeks. Register writer serializes reservations before confirmation. | One allocation of each supported unit; second unresolved claim held. Record keys, reservations, final app links and register totals. Confirmed + NotMet sales also remain allocated. | App reference deduplication claimed to detect global sale reuse; two writers race; unauthorized cross-Area document access. |
| [ ] **C1 Calculated / retry** | Calculation-permitted operator: `/admin/weekly-earnings`, correct Tenant target, **Prepare weekly earnings**, then repeat. All required candidate reviews must be finalized. | Closed-cutoff Level 1 example R150; Level 2 example R150+R250=R400 under fixture terms. Record model, period, versions, components, status/reason; repeat creates no duplicate ledger. Member sees **Test calculation — no payout**. | New post-cutoff placements qualify old week; reviewer chooses amount; calculated means transferred/funded. |
| [ ] **C2 Financial hold** | Admin/operator examines existing controlled positive-held fixture; F supplies original overdue-at-cutoff fact and immutable result if not available in UI. | Positive amount retained with actual `PayoutStatus=Held` and financial hold reason; structure and reviewed 5/5/5 retained; ReleasedAt/PaidAt null. Log whether observed in UI or only bounded executed-fixture evidence. A test definition alone is NOT RUN. | “Not paid yet” mistaken for financially Held; held sales review mistaken for payout hold; cure rewrites a closed cycle. |
| [ ] **C3 Release / mark paid — F** | Confirm UI buttons absent in demo; invoke existing `AdminCommission/Release` and `RecordPayment` on synthetic ledger under otherwise authorized operator. | Requests denied; readback status, ReleasedAt and PaidAt unchanged. | Hidden UI used as sole safeguard; actual transfer, release or asserted payment. |

### Per-case written execution fields — not yet observed

Each row below is the execution record for the identically numbered action above;
retain its actor/action/expected state/evidence/prohibited outcome. Permission
aliases are defined in the [package actor manifest](aqgreen-v2-owner-walkthrough-execution-ready-package.md#3-proposed-actors-and-exact-permission-aliases).
Replace placeholders only after participant designation and informed consent.
Use a role-labelled written evidence reference, never a recording or identifying quote.

| Case | Proposed actor / required permission and scope | Human result | De-identified written observation / evidence reference |
| --- | --- | --- | --- |
| J1 | I: Invite + Active same-programme sponsor; M: signup/Join own record; F: authorized synthetic verification helper | NOT RUN | — No observation collected |
| J2 | M: Join, own participation/checkout | NOT RUN | — No observation collected |
| J3 | M: Join, own participation and two joining stages | NOT RUN | — No observation collected |
| J4 | M: Join own checkout; X: other member denied; F facilitates only isolated API | NOT RUN | — No observation collected |
| J5 | A: Approve within assigned Area for early denial; X: unprivileged/out-of-scope actor; F facilitates | NOT RUN | — No observation collected |
| J6 | A: View/payment-view/Approve + active Area assignment; M: ViewSelf | NOT RUN | — No observation collected |
| P1 | I/M/A: Invite/Join/Approve within Tenant and Area; F: synthetic placement readback | NOT RUN | — No observation collected |
| P2 | M/I: ViewSelf; F: synthetic cutoff/placement evidence only | NOT RUN | — No observation collected |
| S1 | R: review grant + assigned Area; CR: review + explicit AllTenants; X: negative scope/permission | NOT RUN | — No observation collected |
| S2 | R or CR: review within authorized records; W: authorized reservation | NOT RUN | — No observation collected |
| S3 | R or CR: review within authorized records; W: supported tally allocation | NOT RUN | — No observation collected |
| S4 | R or CR: review/reject within authorized records | NOT RUN | — No observation collected |
| S5 | R or CR: begin/hold within authorized records; F: separate Calculate permission | NOT RUN | — No observation collected |
| S6 | R/CR/W: authorized manual allocation coordination, single writer; no additional data access | NOT RUN | — No observation collected |
| C1 | F: View + Calculate + explicit central scope; Tenant.View for selector; M: ViewSelf | NOT RUN | — No observation collected |
| C2 | F: permitted financial view; M: ViewSelf if prepared held record is visible; synthetic cutoff evidence | NOT RUN | — No observation collected |
| C3 | F: existing otherwise-entitled financial test profile; demo must deny Release/RecordPayment | NOT RUN | — No observation collected |

Human PASS requires all required human substeps observed after consent. If only
prior tests, a source definition or facilitator execution is available, put it in
the separate engineering/independent category; do not fill a human PASS. A declined
or unprepared case is NOT RUN, with only an agreed minimal written explanation.

## 3. Rule alignment and evidence status

**Independent source inspection** establishes the mechanisms; it does not establish
runtime outcomes. **Agent 1-reported evidence:** operational report records 3
isolated browser flows, 43 PostgreSQL allocator/sales/commission checks, 4 approval
checks and 13 gateway/registration checks. The newer [authorization qualification](aqgreen-v2-admin-sales-authorization.md)
reports 14 direct-HTTP PostgreSQL passes, 69 backend regression passes and 19 UI
passes; final alignment adds 7 weekly-earnings component and 9 SQLite mapping
passes. It carries forward joining/spillover/browser/PostgreSQL authorization evidence. No result was
independently rerun here; a new Area-admin browser run is not claimed.

| Acceptance category | Current finding |
| --- | --- |
| **A — established evidence** | Inspected invitation/attribution, synthetic signup/checkout, payment processor, atomic approval, sponsor-local allocator, progress, scoped admin review, versioned manual review/calculation and demo payout denial align with current rules. Named runtime results are **Agent 1-reported**, not independent executions. Current authorization result and acceptance statement are now supplied. Cash/retail are not needed. |
| **B — bounded Agent 1 follow-up** | Tenant labels and role mapping are now corrected/qualified. Pre-checkout placement wording and manual-review terminology remain unchanged; explain and observe them, or request Agent 1's narrow correction if required. Provide prepared fixture/record evidence, particularly positive financial hold. A current API result is not a human browser observation. |
| **C — owner choices for actual walkthrough** | Authorize the designated people, synthetic accounts/assigned Areas and recording; accept the manual duplicate/reason register and its custodian. Any broader-than-existing record access or cross-Area coordination needs explicit scope. No reconfirmation of all-admin reviewer eligibility is needed. |
| **D — deferred** | Real Yoco delivery/settlement acceptance, cash processing, actual payments, retail/inventory, insurance/loan/Onyx capabilities, monthly automation, historical conversion/correction programme and B6/B7. Unaffected isolated tasks do not wait for these. |

## 4. Existing Yoco contract: acceptance without provider contact

| Requirement | Available evidence / acceptance criterion |
| --- | --- |
| Authentic confirmation | Controller verifies raw-body HMAC/freshness before processor; successful payment type/status and mode are checked. Signature/tamper/stale test definitions exist. **Actual Yoco-issued confirmation/delivery is not established** by the simulator or current report. Accept only explicitly mock/inspection evidence for this walkthrough. |
| Exact ownership/amount/purpose/reference | Persisted checkout resolves Tenant/member/participation/stage; processor requires expected amount, ZAR, checkout ID and purpose. Additional merchant reference, when supplied, must agree. Demonstrate mismatch rejection with existing mock evidence, not a provider request. |
| Replays/duplicates | Event/hash/payment linkage and transaction locks preserve matching repeats and reject conflicting facts; distinct instalment payments required. Simulator repeat proves shared joining behavior, not the entire signed-webhook path. |
| Both schedules and activation | Existing isolated browser evidence reports R1,200 and two separate R600, pending approval then authorized activation. Review matching durable links; browser success redirect alone never confirms or activates. |
| Isolation | Existing report describes no-outbound API/database setup and guarded no-network adapter; real gateway rejects demo before HTTP. Verify those current local conditions before any click. Stop if adapter/banner/environment differs. |

Payment-object `createdDate` is currently mapped to ConfirmedAt; its exact provider
success-occurrence meaning and bank settlement remain unresolved. Preserve that
limitation; it is not a requirement to perform real Yoco acceptance now. Cash is
known to exist but its software processing is explicitly deferred.

## 5. Clarity checks and bounded corrections

Ask the administrator to explain, without coaching: **sponsor vs parent; joining
vs monthly money; synthetic vs verified provider payment; pending vs Active;
structure vs weekly sales eligibility; admin confirmation vs external verification;
calculation vs payment**. Record errors/assistance; do not claim observed competence
until the designated person actually completes the walkthrough.

- **Tenant label correction verified:** `AdminAreaSelectionField.tsx:16,25,55–67`
  supports scopeLabel; `AdminWeeklyEarnings.tsx:146–162,235–237,401–405` now labels
  Tenant and passes `scopeLabel="Tenant"`. Agent 1 reports 7/7 component checks;
  the broader browser suite was not rerun. Calculation remains Tenant-wide,
  not a business-Area filter. Do not keep the old label defect open.
- **Placement timing:** joining service `ClubMemberProgrammeParticipationAppService.cs:305–308`
  still tells a missing participation that placement must precede checkout. Use
  joining/attribution wording; V2 placement is at approval.
- **Sales labels:** `AdminWeeklySalesReviews.tsx:379,430–432` says “verified
  quantities”; explain/label manual evidence review accurately. The app has no
  hold/confirmation reason field; use linked companion log, not an invented screen.
- **Hold evidence distinction:** browser test asserts an unreleased R400 and the
  no-payout label, not a non-null financial hold reason. Separate lean fixture
  asserts positive R150 Held with overdue commitment. Agent 1's report must not
  equate those two proofs or claim a financial-hold browser result without evidence.

No demonstrated critical authorization bypass, incorrect payment acceptance or
financial corruption was established by this read-only inspection. The scoped
authorization evidence blocker and Tenant-label finding are closed by the supplied
qualification/source. Explain the remaining distinctions before calculation and
record understanding only after consent. Do not
turn guided-walkthrough readiness into a claim of unassisted operational acceptance.

## 6. Minimum decision and completion record

For **READY FOR OWNER WALKTHROUGH**, retain the supplied scoped-review
positive/negative qualification and matching local build, then provide the
prepared fixture/accounts/roles, reconfirm isolation and explain Tenant targeting
before execution. The former stale host-only statement has been corrected in
Agent 1's operational report. This checklist is prepared, not an executed sign-off.
Unprepared optional demonstrations are marked NOT RUN, never PASS; include a
bounded financial-hold example/evidence if claiming that behavior established.

Owner has authorized the isolated walkthrough, **not any recording**. The owner
must still designate participants/role assignments and note readers/retention;
obtain informed consent before written observations. Existing assigned-Area and
explicit central scopes apply; broader access is not assumed.
Late-case/correction policy remains an explicit exception boundary, not a prerequisite
for unaffected new-member tasks. Retail, cash-processing, medical/insurance and
production migration questions are not added as isolated-MVP gates.

**Scriptural application:** Proverbs 11:1's honest measures require exact
amounts/quantities and truthful test labels; Proverbs 18:13,17 requires examining
source and contrary cases; Proverbs 21:5 calls for deliberate preparation;
1 Corinthians 4:1–5 concerns faithful entrusted stewardship, applied here through
personal accounts and preserved audit; 2 Corinthians 8:16–24 models accountable
money administration; Philippians 2:1–8 calls for others' interests, applied by
avoiding double charges, false earnings and concealed exceptions. Under Christ's
lordship these are moral obligations; technical results still require evidence.
No Scripture passage establishes the formula, legal approval or successful deployment.

**Walkthrough sign-off:** candidate/fixture/week; authorized observer and actors;
row outcomes and evidence; assistance/misunderstandings; defects and owners; excluded
cases; final owner decision. Owner-review readiness is not business-operational
acceptance, production enablement, payment authorization or migrated history.

### Evidence locators

- [Agent 1 operational acceptance](aqgreen-v2-operational-mvp-acceptance.md), lines 41–117: reported joining/placement/browser results; lines 151–165 now close the former host-only gap.
- [Authorization qualification](aqgreen-v2-admin-sales-authorization.md), lines 33–73, and [completed authorization worklog](../exec-plans/completed/aqgreen-v2-admin-sales-authorization.md), lines 34–52: current reported executions and evidence limits, not independent business authority or Agent 2 reruns.
- Source under `AqualLifeStyle/9.4.2/aspnet-core/src/`: sales app service `AdminAQGreenWeeklySalesEligibilityAppService.cs:72–206,264–308,386–437`; authorization provider `AqualLifeStyleAuthorizationProvider.cs:169–184`; Yoco controller `YocoPaymentsController.cs:42–125`, signature verifier `YocoWebhookSignatureVerifier.cs:63–109`, notification processor `YocoPaymentNotificationProcessor.cs:88–227,363–396`; demo controllers `AQGreenV2DemoCheckoutController.cs:97–156` and `AQGreenV2DemoVerificationController.cs:52–100`.
- Test sources, not fresh results: `aqua-frontend/e2e-real/aqgreen-v2-signup-installments.real.spec.ts:50–158`, `aqgreen-v2-demo.real.spec.ts:48–120`; `aspnet-core/test/AqualLifeStyle.Web.Tests/Controllers/AQGreenSalesReviewAuthorizationPostgreSqlTests.cs`; `Integration/AQGreenV2LeanMvpTests.cs:170–237` for distinct earned/financial-held fixtures.
- [Existing playbook](aqgreen-v2-mvp-administrative-operations-playbook.md), [payment authority](04-payments-approval-and-yoco.md), [V2 specification](aqgreen-network-placement-specification.md), [Tenant/Area boundaries](08-area-and-tenant-boundaries.md). Earlier authority/source research is reused, not repeated.

Inspection baseline: `e9bd8e96d55d03466492ffd40dd657802ae36d4b` plus concurrent local
changes on `feature/admin-ui-remediation`. Only Agent 2 documents were authored;
no code, suites, provider transactions, production access or outreach.
