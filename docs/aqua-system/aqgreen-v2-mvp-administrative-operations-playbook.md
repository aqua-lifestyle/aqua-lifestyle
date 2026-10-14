# AQGREEN V2 — MVP ADMINISTRATIVE OPERATIONS PLAYBOOK

**12 October 2026 · Administrator desk guide · Agent 2**

> **Acceptance update:** Yoco online joining is the priority; cash exists but its
> processing implementation is deferred. Latest scoped-admin review code has
> replaced the old host-only guard; section 7 below records the earlier inspection.
> Use the [independent acceptance checklist](aqgreen-v2-independent-operational-mvp-acceptance-checklist.md)
> for current candidate evidence, administrator scenarios and readiness limits.

**Owner decisions:** Aqua is an existing business. All authenticated administrators
are eligible to review sales; there is no host-only reviewer rule. Record access
and final-decision scope remain bounded by Tenant and authorized Area access.
Products: Aqua Lifestyle Club 1L drinking water, 5L drinking water and THZ Spray
125 ml. No retail checkout, inventory or automated sales verification is required.

Use synthetic records/money in the isolated MVP. Procedures marked **recommended**
are proposed operating controls, not newly confirmed business policy. Never share
admin logins, type commission amounts, waive 5/5/5 or overwrite finalized history.

## Before starting a shift

- Sign in with your own administrator account; confirm the correct workspace and
  accessible member/Area. Eligibility to review is not permission to open every record.
- Check pending joining approvals, held sales reviews and unresolved exceptions.
- Identify the on-duty register writer and the separately authorized commission
  operator. Confirm that the common allocation register is available and current.
- If access, evidence or record identity is uncertain, hold/escalate that case;
  do not switch to a stronger account to bypass denial.

## 1. Joining-payment verification: step by step

1. **Find the member:** match Club Member/customer identity, Tenant, AQGreen
   participation, current Area and sponsor/invitation. Check existing joining
   records first. Do not make an existing member pay again to supply missing history.
2. **Read the selected schedule:** either one **R1,200** payment or two distinct
   **R600** joining payments. The **R600 monthly obligation is separate**. Do not
   invent a due date or change a schedule locked by confirmed payment.
3. **Obtain the authoritative confirmation:** for the hosted path, inspect the
   application's confirmed payment and linked provider/checkout evidence. Browser
   return or member-supplied proof alone is not provider confirmation. For any
   actual bank/cash/manual channel, use only the owner-approved source and verifier;
   those channels and authority still need confirmation. Do not fabricate a receipt.
4. **Match each receipt:** member, participation, Tenant, AQGreen joining purpose,
   amount, ZAR currency, schedule/stage and payment reference must agree. Record
   source reference, recorded confirmation time, checking admin/time and discrepancy
   if any. Distinguish provider occurrence from recording time and bank settlement.
5. **Check duplicates/conflicts:** reuse of the same payment is not a second R600
   instalment. A matching retry reuses the record; differing amount, member, purpose
   or reference is an exception. Do not create another transaction or allocate by guess.
6. **Check the resulting state:** first R600 leaves another R600 outstanding and
   no approval yet. Full R1,200, or the second distinct R600, completes joining and
   leaves **PaymentConfirmedAwaitingApproval**, not Active.
7. **Approve separately:** the responsible administrator needs existing approval
   permission and active Area authority inside Tenant. Recheck payment and sponsor
   attribution; approve only the pending participation. The system assigns permanent
   placement and Active together. Reject only with a supported recorded reason.
8. **Read back:** verify decision, actor/time and placement outcome. If approval
   fails, leave it pending and investigate; do not place manually. Following Guest
   promotion, the member may need a fresh sign-in. Internal funeral inclusion is
   not proof of external insurer commencement. No release/payout occurs in MVP.

**Acceptance:** every completed joining has authentic matched payment evidence;
two-instalment joining has two distinct R600 payment IDs; payment alone never
activates; unauthorized approval and conflicting retries leave state unchanged.

## 2. Weekly sales review: step by step

1. **Select member and week:** check Tenant, participation ID, Club Member number,
   accessible Area and displayed boundaries. Use **Friday 00:00 Johannesburg to
   the next Friday 00:00, end exclusive**. Do not put an old sale into the current
   week because its evidence arrived late.
2. **Receive evidence:** create a review-case entry in the common register. Record
   submission time, source references and what is missing. Keep original documents
   in the approved restricted location; put only opaque references in the app.
3. **Assess before accepting:** use the approved evidence standard to establish
   the member's completed sales, relevant date, product and units. Check known
   cancellations/refunds. Buying stock or transferring inventory is not automatically
   a qualifying customer sale. Record uncertainty instead of assuming a sale.
4. **Tally separately:** record supported units of **THZ Spray 125 ml**, **1L** and
   **5L**. Do not convert litres to a different product category. Ask for clarification
   on mixed/ambiguous evidence; do not guess quantities.
5. **Clear duplicate allocation:** follow section 3 before finalizing. Record
   included/excluded sale-line keys and the supported quantities for this case.
6. **Choose the evidence decision:**

   | Action | When to use it | Record / system result |
   | --- | --- | --- |
   | **Hold** | Evidence missing, conflicting, disputed or still being assessed. | Begin/retain `HeldForEvidence`; record reason, admin, time and requested next evidence in the case log. No authoritative quantities or threshold. |
   | **Confirm** | Evidence supports authoritative quantities, whether sufficient or insufficient. | Save quantities and references. Record confirmation basis in case log. System computes **Met** only if all three are at least five; otherwise **NotMet**. |
   | **Reject** | Examined evidence is unacceptable and cannot support an authoritative quantity decision. | Save evidence references and rejection reason; system records final reviewer/time, without quantities or threshold. Absence alone normally remains held. |

7. **Finalize only after week close.** Check member/week/references/quantities again,
   submit once, then read back the decision ID, reviewer/time, rule version and
   threshold. **No substitution, carry-forward, borrowing or banking of sales.**
8. **Close the register entry:** link app decision ID and mark the allocation outcome.
   Final decisions are immutable. A timeout is not proof of failure: read back before
   retrying or releasing any reservation. Record reasons for every outcome in the log.
9. **Hand off calculation:** the separate authorized operator checks candidate
   reviews and calculates the same closed week. The system derives structure,
   paid-as level, versioned amount and holds. Reviewer eligibility does not grant
   calculation, release or payment authority.

Call the result **“administratively confirmed from the referenced evidence.”**
Do not call it independently verified commerce unless that verification occurred.
Do not describe approved spray uses as proven safe or medically effective.

**Acceptance:** 5/5/4 is Confirmed + NotMet; 15/0/0 is NotMet; missing evidence
remains held. None of those states permits an admin amount/threshold override.

## 3. Smallest duplicate-evidence control: one manual allocation register

**Recommended:** one access-controlled shared spreadsheet or bound register per
Tenant, with a review-case log and sale-line allocation entries. No new platform.
Use **one active writer at a time** to serialize search/reservation/update, with a
logged handover. The writer may be any eligible administrator with the required
record scope; this coordination task does not create a host-only reviewer class.
All admins may assess cases, but must obtain a recorded reservation before confirmation.

### Blank record layouts

| Review-case log fields |
| --- |
| Case ID; Tenant; participation ID; week start/end; evidence references/location; submission time; status; reason/basis; acting admin/time; next action; app decision ID and final rule/result when available |

| Sale-line allocation fields |
| --- |
| Stable source issuer + sale/transaction reference + line/item ID; document aliases; supported sale date; product; supported total units; allocated units; case/member/week; reservation/final outcome; writer/time; linked app decision; append-only exception/correction entries |

### Allocation steps

1. Identify the **underlying sale line**, not just its PDF filename or scan. Invoice,
   receipt and resubmitted photograph for the same sale map to the same key. If no
   stable source identity can be established, hold the claim and request evidence.
2. Search reservations **and** final allocations across members and all retained
   weeks in that Tenant. A case-local duplicate check is insufficient.
3. Normally allocate a supported line once. If evidence genuinely supports a
   quantity split, record each portion; outstanding reservations + final allocated
   units must not exceed supported units, and each unit has one member/week. A
   finalized reservation is not counted twice. Never invent a split.
4. Writer records a reservation against the case before the reviewer confirms.
   A second request for those units is held pending resolution, not double-counted.
5. After app readback, append the final decision link. **Confirmed + NotMet still
   consumes the counted sales allocation**; those sales cannot be reused next week.
6. If submission fails or outcome is unknown, keep the reservation until the app
   result is checked. Release unused reservations only with a logged reason and
   verified absence of a confirmed allocation; never erase a finalized allocation.

A shared source document may contain several legitimate lines/members/weeks;
do not prohibit document reuse wholesale. If authorized coordination cannot see
enough to clear a cross-Area conflict, escalate/hold—do not obtain unrestricted
access. Owner must define the register custodian and permissible coordination scope.

**Acceptance:** two simultaneous reviewers cannot allocate the same units; renamed
copies map to the original sale; repeated evidence in another week is detected;
register totals equal confirmed app quantities. These manual controls have not
been observed or execution-validated here; they depend on enforced single-writer
discipline, evidence identity and authorized access.

## 4. Exceptions and late evidence

For every exception, record case/member/week, facts versus uncertainty, reason,
actor/time, affected records and next action. Use the existing permissioned path.

| Exception | Recommended administrator action |
| --- | --- |
| Missing evidence | Begin/retain held review; log missing item and request/next action. Do not enter zero, reject arbitrarily or assert threshold failure. |
| Evidence arrives after weekly close, before final review/calculation | Assess against the **original week**; closure does not itself mean evidence is prohibited. No cutoff/deadline is invented. Finalize through the available authorized path if supported. |
| Evidence arrives after a final decision or after latest-week rollover | Preserve the original review/week; open an exception. Current public calculation targets only latest closed week. Do not move sales forward or use ad hoc recalculation. Owner defines late submission/escalation and any governed historical remedy. |
| Suspected duplicate across members/weeks | Reserve nothing further for contested units; hold the affected unresolved claim. Compare underlying sale keys and source facts without exceeding record scope. Do not assume the first submission was correct. |
| Proven repeated sale line | Exclude already allocated units from the new claim; confirm remaining authoritative quantities if evidence supports them, or reject unacceptable evidence with reason. If the earlier final decision was wrong, preserve it and escalate correction authority. |
| Incorrect quantities | Before finalization, correct the draft tally and log basis. After finalization, flag the original decision and ledger; no editing, deleting or replacement decision to rewrite history. |
| Cancelled/refunded sale | Before final review, exclude affected units and reassess remaining quantities. If found later, log/flag affected review and commission for authorized reconciliation. Do not infer reversal or confiscation policy. |
| Disputed/unmatched joining payment | Retain evidence and pending state; ask the designated finance verifier to reconcile actual source facts. Do not force confirmation, allocate to another member or ask for duplicate payment. Refund/chargeback disposition needs authority. |
| Unauthorized approval/review attempt | Leave state unchanged; record the denial/available audit reference and notify the responsible administrator. Do not bypass permissions, share credentials or use a stronger account. |
| Incorrect sponsorship | Stop affected approval; document claimed sponsor versus immutable attribution and source. Resolve through approved correction authority. No silent sponsor change, root creation, rejoining workaround or topology move. |
| Sales review held / commission absent | Resolve evidence; not a financial payout hold. Missing/held candidate review blocks final Tenant/week V2 calculation. Never reject/zero merely to unblock the batch. |
| Positive commission financially held | Read stored cutoff hold reason and underlying obligation. Refer to responsible finance/admin; any permitted cure concerns future eligibility. Do not rewrite a closed cycle, release in MVP or remove placement. |

## 5. Administrative authority matrix

**Eligibility ≠ record access ≠ authority to finalize ≠ authority to pay.**

| Action | Eligible actor | Record scope / remaining boundary |
| --- | --- | --- |
| Receive/assess weekly evidence; begin, hold, confirm or reject sales review | **Every authenticated administrator**, under latest owner decision. | Must be a recognized administrator, not merely a logged-in member. Tenant isolation and authorized Area scope remain. No host-only or `AllTenants` prerequisite for ordinary within-scope review. |
| Inspect raw evidence / update common register | Eligible admin acting as reviewer or on-duty writer/custodian. | Only approved evidence access; ownership/privacy restrictions persist. Wider duplicate coordination requires an expressly authorized scope. |
| Verify joining evidence | Existing payment processor or owner-designated verifier for an approved channel. | Correct Tenant/member/participation and receipt source. Sales-review eligibility does not authorize creating payment facts. |
| Approve/reject participation | Existing approval-permitted administrator with active Area assignment. | Customer's active Area in same Tenant; privileged host access is separately granted. Latest sales decision does not expand joining approval. |
| Allocate parent/slot; evaluate qualification and thresholds; compute amounts | **System**, under approved versioned rules. | Admin cannot select topology, waive thresholds or enter amounts. |
| Trigger calculation / inspect financial outcomes | Existing separately permissioned operator/viewer. | Preserve current financial access checks; owner has not extended calculation authority to every sales reviewer. |
| Release or mark paid | **Unavailable in isolated MVP.** | Backend denial, not just hidden buttons. |
| Correct finalized reviews, sponsorship or historical ledgers; migrate/cut over | Only a separately authorized governed process, when available. | No general-admin inference. B6/firewall parked; B7 unauthorized. |

**Owner scope decision, 12 October 2026:** Area administrators review within their
authorized Areas; central administrators require separately granted broader
scope. The dedicated review permission remains required. Active, effective,
unrevoked same-Tenant Area assignments govern Tenant reads and finalization;
the existing host `AllTenants` grant governs central access. This replaces the
earlier scope question and host-only restriction. Cross-Area duplicate
coordination still needs a designated operating custodian; it grants no extra
record access. See the [authority record](07-verification-decision-and-risk-register.md#f-confirmed-business-decisions-and-boundaries).

One person may hold both sales-review and calculation permissions. Keep the
actions distinct; this playbook does not require a second human approval.

## 6. Evidence and faithful stewardship

Keep app facts **and** companion register facts linked; do not claim a log entry
is in-app evidence when it is not. Minimum records are the blank layouts above,
joining payment/checkout links and approval actor/time/reason, plus commission
period, structure/sales/financial/decision versions, components, hold reason and
immutable evidence links. Keep raw personal/bank documents out of logs/reports;
owner sets custody, access and lawful retention. Use synthetic evidence in MVP.

| Scripture in context | Observable operating control |
| --- | --- |
| Proverbs 11:1, honest weights, within 11:1–6 | Match every amount/quantity to evidence; separate categories; reconcile register/app totals. |
| Proverbs 18:13–17, hearing before judgment and testing the first account | Examine documents and contrary claims; hold unresolved conflicts instead of guessing. |
| Proverbs 21:5, diligence rather than haste, within 21:1–8 | Same member/week checklist every time; reserve before confirming; read back after submission. |
| Proverbs 27:23, care for entrusted flocks/resources, within 27:23–27 | Shift review of pending approvals, held evidence, reservations and unresolved exceptions. |
| 1 Corinthians 4:2, faithful stewards of God's mysteries, within 4:1–5 | By application, use personal accounts, preserve records and report omissions faithfully; sincere self-assessment is not proof of correctness. |
| 2 Corinthians 8:20–21, accountable administration of a collection, within 8:16–24 | Trace receipt, verifier, approver, reviewer and calculation as separate actions; no unauthorized payment release. |

These are derived business/engineering applications, not biblical prescriptions
of a database or compensation formula. [Contextual passages](https://www.biblegateway.com/passage/?search=Proverbs+11%3A1-6%3B18%3A13-17%3B21%3A1-8%3B27%3A23-27%3B1+Corinthians+4%3A1-5%3B2+Corinthians+8%3A16-24&version=KJV).
Engineering controls need technical evidence; tests do not confer legal/commercial
approval. Earlier value, funding, consumer/medical/insurance/privacy risks remain
in the prior research; no new product sales or product-use experiment is required.

## 7. Candidate alignment and Agent 1 acceptance criteria

**Read-only inspection at HEAD `e9bd8e96d55d03466492ffd40dd657802ae36d4b` with local
changes, 12 October 2026.** No Agent 1 files changed; no application tests rerun.

| Procedure | Classification | Actionable acceptance criterion / inspected limitation |
| --- | --- | --- |
| All authenticated admins review within authorized records | **IMPLEMENTED and TESTED in isolated infrastructure** | The review permission is available to Host and Tenant administrators. The service scopes list/detail/latest-week/begin/confirm/reject to current Area assignments for Tenant callers and explicit `AllTenants` for central callers. No blanket broader grant was introduced. The [authorization qualification](aqgreen-v2-admin-sales-authorization.md) records 14 direct-HTTP PostgreSQL checks and focused regressions. |
| Joining schedules, pending approval, atomic placement | **Already supports core procedure** | Preserve distinct receipt IDs, wrong-amount/conflict rejection, pending/Active distinction and Area approval. Actual cash/bank channels and manual confirmation authority **require owner decision**. |
| Member/week, quantities, threshold, confirm/reject, immutable final records | **Already supports** | Readback includes matching member/week/version, actor/time and threshold. Confirmed shortfall is not rejection. Existing DTOs contain no commission amount override. |
| Hold reason and confirmation basis | **Supports with operational procedure** | App Begin creates Held but accepts no reason/evidence; Confirm accepts no reason field. Store reason/basis, actor/time and next action durably in linked case log. Add its opaque reference on finalization. If owner requires these notes in-app, Agent 1 makes a narrow correction; do not imply it already exists. |
| Cross-member/week duplicates | **Supports with operational procedure** | App prevents duplicate decisions and reference repetition within a decision, not global sale allocation. Use section 3's single-writer reservations and readback; no retail/inventory implementation. |
| Evidence labels / joining prerequisite wording | **Needs small correction** | UI “verified quantity” must mean administratively reviewed evidence, not independent commerce. Pre-checkout error currently says placement must exist first; refer to joining/attribution, since permanent placement occurs at approval. |
| Calculation and holds | **Already supports, with sequencing procedure** | Missing/held structural-candidate review fails closed; finalize supported reviews before calculation. System—not admin—derives rates, components and hold state. No waiver to process a batch. |
| Late rollover / finalized corrections / actual payout / existing-member conversion | **Requires owner decision / intentionally deferred** | Record exception and preserve original week/history; no ad hoc historical recalculation or actual payment. |

Agent 1's earlier [interactive qualification](aqgreen-v2-interactive-test-money-qualification.md)
reports a host-review browser run; it does not prove new all-admin access. Its
new signup/two-R600 test source is present, but the latest operational worklog
still records execution pending at inspection: no new pass is inferred.

## 8. Exact unresolved owner decisions

1. **Delegation and coordination:** which administrators hold the separately
   granted central scope and who coordinates cross-Area duplicate claims? Area
   review scope is now confirmed; these operating assignments do not block its
   isolated technical validation.
2. **Payment channels:** which joining channels are actually used now; which source
   confirms each; who may record/resolve non-provider confirmations and disputes?
3. **Evidence standard:** accepted documents, supported completed-sale date/identity,
   ambiguous/shared quantities and allowed source aliases; no invented invoice rule.
4. **Register adoption:** custodian, existing approved medium/location, one-writer
   handover and access for cross-Area duplicate coordination; reason log sufficient
   for MVP or reasons required in-app; lawful evidence/record retention.
5. **Late evidence/remedies:** submission/escalation policy (no deadline yet
   invented), older-week access after rollover, finalized-review corrections,
   refunds/chargebacks and sponsorship correction authority. Preserve history
   while unresolved; do not enable a general repair/backfill programme.

### Technical evidence locators (for Agent 1)

Under `AqualLifeStyle/9.4.2/aspnet-core/src/`:

- `AqualLifeStyle.Application/Admin/Commissions/AdminAQGreenWeeklySalesEligibilityAppService.cs:66–181,214–355,423–480`: access, gate, lock, normalized references and retry identity.
- `AqualLifeStyle.Core/Domain/AQGreen/AQGreenWeeklySalesEligibilityDecision.cs:42–164` and `AqualLifeStyle.Application/Admin/Commissions/Dto/AQGreenWeeklySalesEligibilityDtos.cs:10–65`: states, close timing and missing hold/confirm reason fields.
- `AqualLifeStyle.Application/Admin/Commissions/AdminCommissionAppService.cs:89–127,190–345` and `WeeklyCommissionCalculator.cs:175–267` in the same directory: latest-week calculation, independent financial permission, evidence consumption and demo payout denial.
- `AqualLifeStyle.Application/ProgrammeParticipations/ClubMemberProgrammeParticipationAppService.cs:283–319`: joining/pre-checkout wording. Payment/approval and persistence controls remain as traced in the [operations comparison](aqgreen-v2-existing-business-operations-and-mvp-alignment.md#evidence-locators).

Rules: [joining/payment authority](04-payments-approval-and-yoco.md), [V2 §24 sales](aqgreen-network-placement-specification.md#24-commission-dependencies), [Tenant/Area boundaries](08-area-and-tenant-boundaries.md), [history](05-data-history-migrations-and-legacy-members.md).
Technical claims here are source inspection plus explicitly reported prior
evidence; the proposed manual register and actual staff operation are untested.
