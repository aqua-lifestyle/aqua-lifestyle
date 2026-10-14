# AQGREEN V2 — EXISTING BUSINESS OPERATIONS AND MVP ALIGNMENT

**Agent 2 | 12 October 2026 | Existing-business operational comparison**

> **Later owner decision:** all authenticated administrators are eligible to review
> sales. The host-only code described below is an implementation discrepancy, not
> the current reviewer policy. Record access and final-decision scope remain
> Tenant/Area-bounded and require the remaining clarification. Use the
> [MVP administrative operations playbook](aqgreen-v2-mvp-administrative-operations-playbook.md)
> for current practical procedures, duplicate allocation and authority matrix.

**Conclusion: the membership/network MVP can use authorized administrative sales
confirmation. Retail e-commerce is not a prerequisite.** Preserve the approved
sponsor, joining, Area approval, deterministic spillover, qualification, 5/5/5
review and versioned commission boundaries. Resolve the reviewer authority and
evidence-handling questions below without adding product-sales infrastructure.

## Scope correction and evidence basis

**OWNER STATEMENT — current clarification:** Aqua Lifestyle Club is an existing
business with customers, members and ongoing operations. Its immediate MVP is
membership/network administration, not creation of a new retail business.
Administrators confirm weekly sales from available evidence. The business products
are 1-litre packaged drinking water, 5-litre packaged drinking water and 125 ml THZ
Spray. The stated spray uses concern the face, eyes, teeth, ears and painful areas;
that description is not evidence of safety or efficacy.

This latest clarification governs the present scope. It supersedes the earlier
Agent 2 recommendation to make market discovery the immediate MVP's success
definition. It does not erase the earlier [commercial/legal research](aqgreen-v2-scripture-governed-mvp-business-validation.md),
authorize cutover, establish historical records, or resolve financial/legal policy.
Missing supplied customer records mean **missing evidence for this review**, not
absence of customers, demand or business operations. Actual margins, receipts and
funding are also not verified simply because the business exists.

Evidence distinctions:

- **OWNER STATEMENT:** explicit current business context/scope above. This report
  attributes it to the owner brief; it does not manufacture a formal confirmation
  record or additional decisions.
- **APPROVED RULE / REPOSITORY EVIDENCE:** current rules and inspected source.
  Code establishes modeled behavior, not what today's staff actually do.
- **REPORTED ENGINEERING EVIDENCE:** Agent 1's local qualification results; not
  independently rerun here and not observed business operations.
- **HISTORICAL DOCUMENT:** earlier business materials, not restored current policy.
- **UNKNOWN / INFERENCE:** absent operating evidence or a stated consequence.

Inspection baseline: HEAD `e9bd8e96d55d03466492ffd40dd657802ae36d4b`, branch
`feature/admin-ui-remediation`, with concurrent uncommitted Agent 1 work. No code,
tests, shared configuration, production data or accounts were changed by Agent 2.

## 1. Confirmed processes and the limits of current operating evidence

| Process | What is established and source | What has not been independently established |
| --- | --- | --- |
| Existing business | **OWNER STATEMENT:** current customers, members and operations exist. | Current population, volumes, staff practice, margins and provider settlement. No production investigation was authorized. |
| Joining and sponsorship | **APPROVED RULE:** invited members receive same-Tenant/same-programme sponsor attribution; normal prospective V2 joining has confirmed attribution before payment/approval and permanent placement at approval. [R1,R2] | The precise present-day paper/WhatsApp/office sponsor-recording method and how existing sponsorship disputes are handled. |
| Joining payment and approval | **APPROVED RULE:** R1,200 once or two distinct R600 joining payments; full verified completion awaits responsible Area approval. Payment alone does not activate. [R1,R3] | Which payment channels staff currently use, who checks non-provider receipts, and the current receipt-to-bank reconciliation procedure. |
| Administrative sales confirmation | **OWNER STATEMENT:** authorized admins assess available evidence for MVP. **APPROVED RULE:** separate weekly 5/5/5 categories, manual evidence review and distinct outcomes. [R2 §24] | Existing submission channel, accepted documents, line allocation, reviewer delegation, review deadline, evidence custody and retention. |
| Spillovers and commission | **APPROVED RULE:** sponsor-local five-wide parent-major breadth-first placement; structural 5/25/125 completion; independent weekly sales gate and versioned calculation. [R1,R2] | Whether current staff/manual records already follow that allocator, and which historical payouts/levels were calculated under which rules. |
| Existing-member history | **APPROVED BOUNDARY:** legitimate members may predate modern tables; absence of a checkout/payment row does not prove nonpayment or invalid membership. [R4 §3] | Member-by-member legacy sponsor/payment/approval/placement/cover facts and any accepted V2 migration manifest. |

The historical `docs/BusinessDocs/National Club Aqgreen.pdf`, pp.25–30, describes
online/WhatsApp, office and presentation registration, proof submission, and card,
online and cash channels. It establishes historical process descriptions only.
Its mixed programme/pricing language cannot govern current V2 joining. It does not
establish today's weekly-sales review SOP.

**Minimum operational evidence still needed:** owner-supplied current process
description, blank receipt/sales-evidence forms, role/delegation matrix, reporting
calendar and exception-handling instructions. If separately authorized, use
minimized/redacted examples and a staff walkthrough. No customer-data extraction
or outreach is performed or authorized by this report.

## 2. Current process versus approved V2 behavior

| Stage / actor | Approved prospective V2 behavior | Inspected implementation and alignment |
| --- | --- | --- |
| Member accepts sponsorship | Resolve an eligible sponsor participation and invitation inside Tenant/programme; retain invitation/source, sponsor identity and participant confirmation. Registration is not placement. | `StartEntryAsync` plus invitation resolver/attribution recorder retain attribution and acceptance in the joining transaction. Conflicting retries fail; existing sponsor placement scope is required. Demo V2 rejects raw-recruiter-only and ordinary independent admission. [C1] |
| Member pays joining | Server selects schedule/stage and exact ZAR amount; one payment cannot fill both R600 slots. Payment matches the customer/participation/purpose and leaves full completion pending approval. | Existing checkout/payment processor supports this linkage and duplicate/conflicting receipt handling. Demo confirms synthetic joining money through the existing processor; no independent provider occurrence or bank settlement follows. [R3,C2] |
| Area admin decides | Dedicated permission and current active Area assignment inside Tenant; approve only after payment and attribution checks. Commit placement, approval and Active together. Rejection records reason and does not create placement. | Approval locks/rechecks authority/payment/scope, calls allocator and activates in the same transaction. Failure does not leave ordinary Active-but-unplaced state. Role promotion requires a fresh member session. Host-wide permission is a separate path. [C3,R3,R6] |
| Allocator places | Sponsor's first five occupy immediate slots; later placements search only that sponsor's subtree, breadth-first, parent-major canonical order, slots 1–5. Sponsor credit never becomes placement-parent credit. | Allocator requires an active PostgreSQL transaction, tree lock, authoritative facts and canonically ordered subtree; first vacancy is chosen in parent/slot order. No client-selected position. Source inspection aligns with D02. [C4] |
| Member views qualification | Relative depth and qualifying positions produce complete Levels 1–3. Spillover can contribute to ancestors without transferring sponsorship. Occupancy is not completion or weekly entitlement. | Prospective V2 evaluator/progress path is selected in isolated demo; ordinary selector remains V1. Agent 1 reports browser progress evidence. No live historical topology equivalence is established. [R2 §§15–16,C5,Q] |
| Sales reviewer assesses evidence | Identify participant/week/version; confirm reviewed quantities or reject unacceptable evidence, otherwise hold. System evaluates each threshold independently. | Manual-review service, domain and UI retain these separate facts. Finalization cannot precede week close. Permission/scope and production gate are enforced. **Reviewer path is currently host-only**, not Area-admin review. [C6–C8] |
| Commission operator calculates | Closed week; cutoff-effective structure, exact finalized sales review, immutable rates and holds. Admin cannot choose amounts. | Calculation input selects programme/Tenant; server resolves latest closed week and terms. V2 reader/evaluator/ledger evidence determine paid-as level and components. Missing/held candidate reviews fail before final calculation. No payment service is invoked. [C9] |
| Operator records outcome | Retain immutable calculation evidence; release and payment are separately authorized actions, not effects of sales review. | Demo blocks release and record-payment on the backend as well as hiding controls. Outside demo, those remain distinct permissioned operations; their existence is not approval to use them in MVP. [C10,Q] |

**Spillover verdict:** approved D02 represents the commissioned V2 process; source
inspection aligns with it. This is not proof that existing manual/V1 topology
already follows V2. Deterministic parent-major order deliberately concentrates
upstream spillover: fill A's five slots before B's five. Do not substitute
round-robin, shortest-leg, admin discretion or apparent fairness balancing.

**Reported engineering evidence:** Agent 1's [interactive qualification](aqgreen-v2-interactive-test-money-qualification.md)
reports local browser/PostgreSQL joining and manual-review/commission flows,
including a synthetic R400 outcome and release/payment denial. It used a fixture
Guest account: first-time participation, not proof of new account registration.
The full R1,200 route ran; two-R600 browser E2E did not separately run in that
phase. Actual staff/member observation and real provider settlement remain
unobserved. Agent 2 inspected source and selected existing test definitions;
no automated tests were executed by Agent 2.

## 3. MVP-critical discrepancies and minimum acceptance criteria

These findings distinguish actual source behavior from unknown operating practice.
They do not authorize changes to Agent 1's work or a wider programme.

| ID / finding | Minimum acceptance criterion and responsibility |
| --- | --- |
| **OP-01 — reviewer authority not yet mapped to actual staff.** Backend rejects tenant/Area sales reviewers and requires host session, dedicated review permission, `Admin.AllTenants`, allowed target scope and enabled review gate. [C6] | Owner identifies the intended reviewer role and resource scope. For an isolated host-review pilot, explicitly document that actor. If actual Area admins must review, Agent 1 needs an owner-authorized Area-scoped contract before adding access. Do not grant broad host authority just to make an Area workflow pass. |
| **OP-02 — references do not establish an operating evidence standard or detect repeated sales across decisions.** Decision uniqueness and within-decision reference deduplication exist, not a global sale-line reconciliation. [C7,C8] | Owner approves a minimal manual SOP linking each counted product quantity to member, sale line/date, week and reviewed source. Reviewer checks cancellation/refund and prior allocation. A shared register may legitimately cover many members/weeks; prevent repeated counting of the same sale line, not all reuse of a document reference. No automated retail system is required. |
| **OP-03 — held evidence and latest-week calculation require sequencing.** A missing/held review for any structural candidate blocks this Tenant/week transaction; the public action calculates only the latest closed week. [C9] | For the pilot's chosen week, identify candidates, begin missing reviews, finalize supported confirmations/rejections, then calculate. Leave genuinely unknown evidence held; do not invent a rejection, shortfall or zero to unblock a batch. Owner specifies handling of delayed evidence before rollover. Historical backfill/correction stays deferred. |
| **OP-04 — misleading prepayment placement wording.** `CreateAQGreenJoiningCheckoutAsync` says “Your network placement must be recorded before checkout” when participation is absent, although V2 placement occurs at approval. [C1] | Agent 1 should describe joining/attribution as the prerequisite, not permanent placement. Preserve payment-before-approval and approval-plus-placement semantics. This is a bounded wording finding, not a changed placement rule. |
| **OP-05 — evidence terminology can imply stronger verification.** Sales UI uses “verified quantities”; storage provenance is `ManualReview`, without independent commerce integration. [C8] | Explain that quantities were reviewed/confirmed administratively from referenced evidence. Do not imply independent payer/payment/delivery verification unless it occurred. No need to rename valid domain facts or add retail verification. |
| **OP-06 — earlier product/scope statements are stale.** Earlier reports called product identity unresolved and discovery once said cleaning spray. The current script has removed cleaning wording; the owner now names the products. | Treat product identities above as owner-supplied context; remove the SKU-card prerequisite from generic membership workflow acceptance. Keep composition/safety/efficacy and additional product claims unknown. Agent 1 owns its document/screen consistency updates. |
| **OP-07 — wider joining coverage is reported, not complete current browser evidence.** R600+R600 and signup were not separately exercised in the interactive qualification. [Q] | State the exact tested boundary. Agent 1 supplies relevant evidence if those paths are included in the pilot acceptance; Agent 2 neither duplicates tests nor claims them passed. |

No receipt intake channel, staff roster or existing sales review deadline is
invented to close these gaps. A queue showing started reviews does not necessarily
inventory every candidate needing a review; the pilot operator must reconcile
candidate membership with the review list before calculation.

## 4. Minimum administrative sales-confirmation requirements

**Manual confirmation is sufficient as the chosen MVP mechanism**, subject to the
following accountable controls. The approved rule remains
`AQGreenWeeklySalesEligibilityV1`.[R2 §24]

1. **Identity:** an AQGreen participation in an explicit Tenant, not just a name,
   user or membership label. Display Club Member number and period for the reviewer;
   backend verifies participant/Tenant and authorized scope independently.
2. **Week:** Friday 00:00 Africa/Johannesburg inclusive to the following Friday
   exclusive. Store canonical UTC boundaries; classify sales by their supported
   transaction dates, not submission dates. Finalize only after the week closes.
3. **Product quantities:** independently count THZ Spray, 1L and 5L units; each
   must reach five. No substitution, banking or carry-forward. Owner's product
   direction identifies the 125 ml THZ Spray; do not count other spray sizes or
   products by assumption.
4. **Evidence:** manual references must retrieve what was actually reviewed under
   the owner's approved custody/access procedure. Completed sales must not be
   cancelled/refunded at review; inventory transfers or buying one's stock do not
   automatically establish customer sales. The procedure may use existing paper,
   receipts, invoices, registers or payment evidence—actual accepted types need
   owner confirmation. No customer checkout, stock system or delivery integration.
5. **Distinct decisions:**

   | Review state | Authoritative quantities/result | Commission consequence for a candidate |
   | --- | --- | --- |
   | `HeldForEvidence` / missing review | No finalized quantities, threshold or final reviewer/time. Review creation audit is a separate fact. | Cannot finalize V2 calculation; no inferred zero/NotMet. |
   | `Confirmed + Met` | Reviewed quantities and system-evaluated 5/5/5 pass. | Apply versioned rates to structural level, then existing payout holds. |
   | `Confirmed + NotMet` | Reviewed supported shortfall, e.g. 5/5/4. | No paid-as level/amount; distinct finalized NotEarned evidence. Structure remains. |
   | `Rejected` | Required reason, evidence references, final reviewer/time; no authoritative quantities or threshold. | Distinct finalized NotEarned decision, not a verified quantity shortfall. |

6. **Authority and audit:** separate review permission; recorded final reviewer ID
   and server UTC time. Held creation is creation-audited, not a completed review.
   Evidence references must be opaque; raw buyer/medical/bank information must not
   enter ordinary logs. Define minimal retention and retrieval outside the app.
7. **Duplicates/retries:** one decision per Tenant/participation/week/rules version;
   transaction lock plus database uniqueness; matching final replay reuses the
   decision, conflicting replay fails. Manual line allocation handles duplicate
   sales across members/weeks until an authorized different mechanism exists.
8. **Durability:** final decisions cannot be edited; evidence is append-only.
   PostgreSQL migration guards prevent update/delete/truncate and finalization
   without evidence; Tenant-coherent foreign keys protect ownership. Source
   inspection establishes these guards' presence, not their production installation.
9. **Independent calculation:** reviewer submits quantities, evidence and reason,
   never threshold result, structural level, paid-as level or money amount.
   Calculation reads the finalized week/version and recomputes eligibility.
10. **Later-invalid evidence:** preserve the original review and ledger; flag for
    authorized reconciliation. No silent overwrite/recalculation. A rejected final
    review cannot simply be reopened to approve replacement evidence today.

The same properly authorized person may perform review and calculation if the
owner's delegation allows it; actions remain separately authorized/audited.
Different humans or dual approval are not automatically mandated by Scripture or
the MVP specification. A structurally incomplete Level 0 member is not a commission
candidate and does not require a finalized sales review to record that valid
structural result; unavailable topology is not equivalent to Level 0.

## 5. Joining-payment and commission-integrity requirements

### Joining payments

- Retain R1,200 or two distinct R600 **joining** payments and the separate R600
  monthly obligation; never allocate monthly money as joining by aggregate guess.
- In the hosted path, match authoritative receipt/provider, checkout, member,
  participation, Tenant, purpose, amount, currency and stage. Other existing
  channels need authentic owner-approved linkage, not fabricated checkout rows.
  Confirmed status needs actual confirmation evidence; screenshots alone, browser
  returns and membership approval are not substitutes.
- Provider path: signature/mode/freshness checks precede payment processing;
  repeated same facts are idempotent, conflicting facts fail. Preserve checkout,
  receipt/payment link and resulting participation state atomically.[R3,C2]
- If actual business joining uses bank transfer/cash/manual proof, owner must
  identify the current verifier and authoritative receipt source. The inspected
  provider path does not establish a generic manual-confirmation authority.
  Do not add a real-money override or make retail checkout the answer to this gap.
- Full completion leads to `PaymentConfirmedAwaitingApproval`; first R600 leaves
  another R600 outstanding. Area approval and placement commit together; neither
  provider confirmation nor the simulated confirmation endpoint makes Active.
- Record unmatched/contradictory funds for governed reconciliation, not arbitrary
  allocation or a second charge. Existing legitimate members must not be charged
  new joining fees to create missing modern records.
- Keep simulated receipt time, actual provider occurrence and bank settlement
  conceptually separate. Current Yoco `payload.createdDate` occurrence semantics,
  settlement and refund/chargeback treatment remain unresolved in the register;
  do not display them as independently established historical facts.[R3 §§3,11]

### Commissions

- Resolve closed Friday–Thursday week, approved effective financial terms and
  cutoff-effective structural evidence. AQGreen stops at Level 3: components
  R150/R250/R1,250, cumulative R150/R400/R1,650 under the current terms.[R1]
- Keep placement/qualification, sales-policy, financial-terms and decision versions
  distinct. V2 calculation retains immutable structural manifest and review
  identity/audit evidence; normal selection remains LegacyV1 outside the demo.
- Use `Confirmed + Met` only for earning eligibility. Confirmed shortfall/rejection
  are recorded distinctly; held/missing/corrupt/unsupported candidate evidence fails
  closed, with no V1 fallback or finalized Tenant/week residue.[C9]
- Own overdue monthly obligation or applicable existing loan can hold an otherwise
  positive commission at cutoff. A financial payout hold is not a held sales
  review and does not remove placement or invent an upline penalty. No loan feature
  or monthly due policy is enabled by this requirement.
- Preserve one period/ledger decision under transaction lock and database
  uniqueness. A calculation replay must not overwrite history or double liability.
- Admin calculation does not send money. In the isolated MVP, direct release and
  mark-paid API calls must remain blocked as well as hidden. No ability to type
  arbitrary amounts or externally assert payment is needed for MVP.
- A calculated/held/released amount is not provider settlement or available funding.
  Retain the earlier compensation-funding and sustainability questions as present
  business risks; do not resolve them with a successful calculation.

### Scriptural controls and validation boundary

Under Christ's lordship (Colossians 1:15–20), accountable operations are a derived
obligation, not a claim of automatic success. Proverbs 11:1 concerns honest weights
and measures: exact amounts, quantities and evidence labels follow by application.
Proverbs 18:13,17 requires hearing/testing accounts: compare owner intent, staff
practice and records, including exceptions. Proverbs 21:5 commends diligence over
haste: keep this focused MVP and explicit scope. Proverbs 27:23–27 concerns careful
knowledge of entrusted flocks/resources: reconcile memberships, pending approvals
and outstanding reviews. In 2 Corinthians 8:16–24 Paul safeguards administration
of a collection before God and people: separate money receipt, review and payout
with audit. Philippians 2:1–8 calls for humility and others' interests: prevent
double charges, false earnings/cover claims and concealed adverse decisions.

These are **theological/business applications**; Scripture does not prescribe a
software stack, THZ Spray use or the commission formula. Validate the resulting
controls with actual operational evidence and Agent 1's engineering evidence.
Passing tests is not legal, commercial, medical or production approval.

## 6. Existing-member history is not yet migrated

The pilot uses **new prospective, disposable V2 participation** and explicit
synthetic root/scope evidence. It proves no conversion of existing members.
For current/historical operations, preserve these classifications:[R2 §§26,34;R4]

| Historical question | Evidence required; prohibited assumption |
| --- | --- |
| Existing member legitimacy / old joining payment | Real membership and payment source evidence; no fake R1,200/R600 payments, placeholder identities or repeat joining to fill missing tables. |
| Sponsor of record versus structural parent | Actual attribution/provenance and effective history. Current V1 recruiter ID cannot establish separate original sponsor and V2 parent/slot. |
| Legacy roots / multiple trees | Owner-approved root classification and scope/population mapping (D03B/D09). Null recruiter is only a historical candidate, not automatic V2 root authority. |
| Prior placement and completion | Cutoff evidence under the rule actually used. Do not backdate V2 slots or reinterpret V1 earliest-five selection through spillover. |
| Activation and Area | Actual approval/activation and effective Area history. Current Active/Area/security status and system-introduction baseline cannot prove earlier facts. |
| Weekly sales and commissions | Actual week, reviewed evidence, approved terms and original ledger. Missing old 5/5/5 review does not retroactively invalidate a V1 calculation. |
| Funeral inclusion / external cover | Aqua entitlement evidence and external insurer facts are separate. Missing modern joining history does not prove a legitimate legacy member lacks cover. |
| Refund, terminal sponsor or post-Active event | Preserve facts and topology; D08/D11/D14B and general refund/reconciliation decisions govern affected outcomes. No compression, reassignment, deletion or financial reversal by guess. |

No inventory, migration simulation or production examination was performed here.
The register's incident/status records are historical baseline evidence, not a
fresh finding about today's production condition. B6/firewall remains parked;
B7/D10 cutover remains unauthorized.

## 7. Exact owner decisions and current risks

### Decisions needed for faithful administrative operations

| Decision | Exact question / scope | Safe pilot consequence while unanswered |
| --- | --- | --- |
| **OD-01 staff authority** | Who approves joining; who reviews weekly sales; who calculates/views commissions? Are sales reviewers host/Tenant-wide or Area-assigned, and what resource boundaries/delegations apply? | Use only explicitly scoped synthetic actors under current permissions. Do not assume all admins have all powers. |
| **OD-02 joining evidence channels** | Which present joining channels are used; who verifies each receipt against which authoritative source; how are unmatched, duplicate or disputed amounts escalated? | Simulated processor path remains valid for test money; no assumed cash/bank/provider reconciliation or new manual override. |
| **OD-03 manual sales SOP** | Which existing evidence is acceptable, how does it identify seller/product/date/completed sale, how are quantities and cancellations checked, and who owns custody/retention and duplicate line allocation? | Use conspicuous synthetic evidence for workflow. No claim of a verified outside sale from a test reference. |
| **OD-04 review calendar / lateness** | When must evidence be finalized after close; how are held cases escalated before the latest-week window advances? Which existing records require later authorized correction? | Pick one closed pilot week and leave unknowns held; no automatic earlier-week backfill or history rewrite. |
| **OD-05 pilot entry** | Does acceptance cover an existing-account/new-participation member, completely new account registration, both joining schedules, and an explicitly privileged test root? | Describe current browser proof exactly; obtain additional Agent 1 evidence only for included paths. |

**Already supplied by the owner:** existing business status, manual-admin sales
confirmation as MVP mechanism, no retail platform now, and the three product
identities. Those questions need not be reopened. Invitation expiry is a further
product choice only if required in the intended pilot; no present expiry model
or approved expiry rule is evidenced. Existing monthly due/first-liability,
refund/remedy, evidence corrections and terminal sponsor decisions remain open
only for their affected workflows—do not make all of them blanket blockers to
unaffected isolated joining/placement tasks.

### Present-day risks, not merely future concerns

- **THZ Spray:** business-described application to eyes/ears and painful areas is
  consequential product context. Composition, appropriate quality/sterility,
  labeling, contraindications, safety and efficacy evidence were not supplied.
  Do not promote those uses as safe, medical treatment or proven pain relief in
  the pilot. This research cannot determine actual safety or regulatory product
  classification. Owner should obtain competent product/medical-regulatory advice
  for existing claims/use, without adding product-sales functionality or exposing
  anyone to a product-use experiment.
- **Consumer/compensation:** source of funds, ordinary-member outcomes, promises
  and CPA §§37/38/41/43 questions remain applicable to existing operations, not
  only future V2 enablement. No illegality is alleged without the facts. Obtain
  fact-specific South African advice where the existing offer raises these issues.
- **Money and member protection:** unresolved joining rejection refunds,
  chargebacks, overdue obligations and cover representations can affect current
  members. Identify the actual finance/remedy owner; a simulator or immutable
  ledger does not provide the remedy or independently validate cover.
- **Privacy:** a manual reference is useful only if evidence is retrievable lawfully;
  current handling of personal/bank/health information needs accountable custody,
  lawful access and retention. Test money does not exempt real research data.

The earlier report's [legal source table and limitations](aqgreen-v2-scripture-governed-mvp-business-validation.md#8-south-african-law-targeted-pre-commercialization-questions)
remain useful. These are material questions for actual operations; commercial
research, legal review and operational safeguards do not require an online store.

## 8. Explicit deferrals and Agent 1 handoff

**Defer:** customer retail checkout, product ordering/inventory, automated retail
payment verification, delivery integration, sales-document upload platform,
automatic sale feeds, unrestricted amount editing, actual payout/release,
production provider activation, insurer integration, monthly worker enablement,
new loans/savings/investments, Onyx expansion, company-assisted acquisition,
general topology repair, historical backfill/conversion, B6/firewall and B7 cutover.
Existing operations/records are not erased by these software deferrals.

**Agent 1 handoff:** preserve the current manual confirmation and system-derived
commission design. Address OP-04/OP-05's bounded wording distinctions and describe
OP-07's coverage honestly. Use the host-review path only within its explicit pilot
scope; OP-01 requires an owner authority choice if Area staff are intended users.
OP-02/OP-03 first need a minimal operating procedure/decision, not new retail code.
Keep historical and prospective V2 paths separate and payout disabled. Update
Agent 1's report/product language against this owner clarification; its files and
tests have not been edited here.

**Assessment:** inspected implementation supports the isolated prospective
membership → joining payment → Area approval/placement → manual sales review →
versioned commission-recording path, with the gaps above. End-to-end engineering
evidence is **reported local evidence from Agent 1**. Fidelity to current staff
practice is **partially established** until the operating SOP/authority mapping
is supplied; live workflow, legal/commercial approval and migrated history are
not established. Continue focused alignment; do not expand the MVP into retail.

## Evidence locators

### Current rules and reported evidence

- **R1:** [Business rules](02-business-rules-and-workflows.md), §§2–3,6–7.
- **R2:** [Placement V2 specification](aqgreen-network-placement-specification.md), §§7–12,15–16,23–24,26,34; resolved prospective authority only.
- **R3:** [Payments/approval](04-payments-approval-and-yoco.md), §§1–7,11.
- **R4:** [History and legacy members](05-data-history-migrations-and-legacy-members.md), §§1–3,8–11.
- **R5:** [Decision/evidence register](07-verification-decision-and-risk-register.md), especially §§E,G,O,P; evidence at its stated baseline, not new production observation.
- **R6:** [Tenant/Area boundaries](08-area-and-tenant-boundaries.md), lines 33–55, particularly administrative assignments and host distinction.
- **Q:** [Interactive qualification](aqgreen-v2-interactive-test-money-qualification.md), §§“Actual tests and results” and “User learning”; reported/carried-forward, not rerun by Agent 2.

### Source inspected (paths relative to `AqualLifeStyle/9.4.2/aspnet-core/src/`)

- **C1:** `AqualLifeStyle.Application/ProgrammeParticipations/ClubMemberProgrammeParticipationAppService.cs:196–280,283–319`, `AqualLifeStyle.Application/ProgrammeParticipations/AQGreenInvitationAttributionRecorder.cs:36–84`, and `AqualLifeStyle.Application/Recruitment/ProgrammeInvitationResolver.cs:106–182`.
- **C2:** `AqualLifeStyle.Application/Payments/ProgrammePaymentConfirmationProcessor.cs:417–553`.
- **C3:** `AqualLifeStyle.Application/Admin/ProgrammeParticipations/AdminProgrammeParticipationAppService.cs:707–934`.
- **C4:** `AqualLifeStyle.EntityFrameworkCore/EntityFrameworkCore/AQGreenPlacementAllocator.cs:44–123,330–355` and `AQGreenPlacementTopologyReader.cs:264–266,326–348` in the same directory; SQL orders relative depth then canonical path with `C` collation, matching V2 D02.
- **C5:** `AqualLifeStyle.Application/Admin/Commissions/LegacyV1AQGreenCommissionStructuralModelSelector.cs`, `AqualLifeStyle.Web.Host/AQGreenV2Demo/AQGreenV2DemoSelectors.cs` and Q for progress workflow evidence.
- **C6:** `AqualLifeStyle.Application/Admin/Commissions/AdminAQGreenWeeklySalesEligibilityAppService.cs:185–355,423–480`; permissions, gate, scope, lock, normalized references and retry comparison.
- **C7:** `AqualLifeStyle.Core/Domain/AQGreen/AQGreenWeeklySalesEligibilityDecision.cs:42–164`, `AQGreenCommissionWeek.cs`, `AQGreenWeeklySalesEligibility.cs`; identity, states, close timing, evaluator.
- **C8:** `AqualLifeStyle.EntityFrameworkCore/EntityFrameworkCore/EntityConfigurations/AQGreenWeeklySalesEligibilityConfiguration.cs:66–87,123–129`, `AqualLifeStyle.EntityFrameworkCore/Migrations/20260830204240_AddAQGreenWeeklySalesEligibility.cs:100–185`; UI at `AqualLifeStyle/9.4.2/aqua-frontend/src/components/admin/AdminWeeklySalesReviews.tsx:379–488`.
- **C9:** `AqualLifeStyle.Application/Admin/Commissions/WeeklyCommissionCalculator.cs:100–281`, `AqualLifeStyle.EntityFrameworkCore/EntityFrameworkCore/AQGreenWeeklySalesEligibilityDecisionReader.cs:23–153`, `AqualLifeStyle.Core/Domain/Onyx/EntryWeeklyCommissionCalculator.cs:88–163`.
- **C10:** `AqualLifeStyle.Application/Admin/Commissions/AdminCommissionAppService.cs:89–127,190–345`; independent calculation permission and demo release/payment denial.

Selected existing authorization test definitions were inspected in
`test/AqualLifeStyle.Tests/Application/AdminAQGreenWeeklySalesEligibilitySafetyTests.cs`.
Their presence is source evidence, not a fresh execution result. Documentation
checks, source/path inspection and Git-state review are Agent 2's verification
scope; automated implementation testing remains Agent 1's responsibility.
