# AQGREEN V2 — AGENT-EXECUTED OPERATIONAL MVP WALKTHROUGH RESULTS

**12 October 2026 · A. ALL 17 CASES PASSED — READY FOR OWNER ACCEPTANCE REVIEW**

This is newly executed agent-operated browser/API/PostgreSQL evidence against
Agent 2's [17-case checklist](aqgreen-v2-independent-operational-mvp-acceptance-checklist.md).
It is not human usability, market validation, independently verified commerce,
owner acceptance or permission for real-money/production operation. Original
failures and successful retests are retained below and in the execution journal.

## Candidate, environment and actors

Branch `feature/admin-ui-remediation`; HEAD
`e9bd8e96d55d03466492ffd40dd657802ae36d4b` plus preserved local changes and the
four focused frontend corrections/tests listed below. Exact source manifest
SHA-256: `72219b17d8cd5a55f9c0b542190caa969bb5193131fa2924fdefcdff54d1e26c`.
The [candidate record](../verification/aqgreen-v2-agent-walkthrough-20261012/candidate.json)
and [source manifest](../verification/aqgreen-v2-agent-walkthrough-20261012/source-manifest.sha256)
identify source and published .NET artifacts; HEAD alone does not identify this
uncommitted candidate.

Current .NET Release artifacts were published locally and the schema migrated
into disposable PostgreSQL 16, database `aqua_aqgreen_v2_demo`. API environment
was exactly `AQGreenV2Demo`, explicitly enabled, with provider keys/mode empty,
Bird/financial workers disabled and the guarded synthetic adapter. API,
PostgreSQL and frontend shared a Docker internal network namespace with no
default route. Browser execution from the full successful joining run onward
used `bwrap --unshare-net`; local Unix-socket bridges exposed only these app
ports. No real Yoco probe/request, production connection or provider credential
was used. Browser contexts additionally denied external origins. No screenshots,
traces, HAR, audio or video were recorded.

Frontend was the current source copy using Next 16.3.6 dev and the existing host
Node 26.6.0 binary mounted into the cached Debian runtime. **This is not Node
24 qualification or a production frontend build.** Cached Node 24 Alpine could
not load the workspace's GNU SWC binary. The task copy alone allowed the exact
loopback dev origin `127.0.0.1`; repository config and dependencies were unchanged.
No inherited frontend `.env` files were copied; synthetic session secrets were
generated privately. [Runtime evidence](../verification/aqgreen-v2-agent-walkthrough-20261012/runtime-evidence.json)
records health, network routes, week, fixture and ledger identities.

Separate synthetic browser contexts acted as:

- Sponsor/member: `aqgreen.demo.member`, then the newly signed-up
  `aqgreen.operational.walk.sponsor2@example.test` and six `bchild1`–`bchild6`
  accounts; each new account used browser signup and email verification.
- Assigned Area administrator: Tenant 1 `admin`, user #4, active Johannesburg
  assignment and existing administrator review/approval grants.
- Central reviewer: `walk.central`, Host user #43, administrator identity role
  plus explicit sales-review and `AllTenants` grants; no financial calculation
  grant. `walk.central.limited` lacked the broader grant.
- Other-Area reviewer: `walk.outarea`, user #40, assigned only to its separate
  Tenant 1 Area; legitimately rejected its own evidence and was denied the
  joining branch/Area outside its assignment.
- Negative actors: missing review permission, revoked assignment, anonymous,
  ordinary member and Tenant 1 actor targeting Tenant 2.
- Financial operator: separate Host `admin` context, user #1, existing explicit
  calculation/release/record-payment permissions. Demo guards still denied payout.
- Owner-review observer: `walk.owner`, user #45, separate self-view account;
  owner identity conferred no administrative access. No human observer existed.

Reporting week: **2026-10-01 22:00 UTC through 2026-10-08 21:59:59.999999 UTC**,
Friday–Thursday Johannesburg. Seven browser-approved members retained their
actual 12 October timestamps and were excluded from this closed ledger/evidence.
Financial cases used the default pre-cutoff 31-node synthetic tree and a
**separately prepared, controlled historical six-node fixture**, constructed with
existing domain/test conventions, not observed member joining outcomes. Its
explicit synthetic chronology and overdue fact are in the runtime record. No
browser-created placement, payment or review was backdated or rewritten.

## All 17 executed cases

Evidence numbers below refer to `sequence` in the retained
[execution journal](../verification/aqgreen-v2-agent-walkthrough-20261012/events.jsonl).
They denote actions/readbacks performed now, not carried-forward suites.

| Case / final result | Actor and action actually executed | Expected → observed; evidence |
| --- | --- | --- |
| **J1 PASS** | Sponsor opened browser invitations. New accounts opened invitation, created account, verified email via guarded synthetic outbox helper, signed in and returned. Direct negative/retry requests followed. | Correct sponsor/Tenant; verified accounts had no placement. One immutable attribution and member confirmation after joining. Invalid/self/wrong-programme invitation and conflicting sponsor denied; exact acceptance retry preserved attribution. Member mailbox access 403. #17,20–22,35,55,65,77,86,99,114–115,119. |
| **J2 PASS** | New sponsor and five children completed browser R1,200 checkout, then member status. Sixth completed-member checkout request tested separately. | Exact matched synthetic payment → awaiting Area approval, no placement/approval before activation. Completed member could not create another joining charge. #26,56,70,79,87,100,117,124. |
| **J3 PASS** | `bchild1` chose two instalments; confirmed first R600, saw remaining R600, opened second checkout, resumed it and confirmed through browser. | First remained non-Active; second had different checkout/payment IDs. Two confirmed R600 records total R1,200 → pending approval. Exact second retry reused payment. #36,52,54,124. |
| **J4 PASS** | Member submitted wrong R1,199 confirmation, correct browser confirmation and exact retry; other member attempted that checkout. Final PostgreSQL cardinalities checked. | Wrong amount 400 and unchanged participation; repeat `alreadyConfirmed`; other member 404. Each full schedule has one R1,200 payment; instalment schedule exactly two R600. #24,27,52,124. |
| **J5 PASS** | Area admin attempted first-R600 approval. Ordinary member, out-of-Area admin and cross-Tenant Area admin directly invoked approval API. | Early operation denied (existing HTTP 500 business-error envelope); unauthorized member/Area 403 and cross-Tenant denied. Participation/decision/placement state unchanged. #30,36,109–110. |
| **J6 PASS** | Area admin inspected matching browser queue/payment state and clicked Approve. Repeated API approval; member signed in afresh. Read back all seven activations. | Active and placement together; retry created nothing. Each of seven has exactly one approval, placement, attribution and confirmation. Actor #4/times persisted. #32,54,62,74,82,89,101,119. |
| **P1 PASS** | Newly browser-approved sponsor with empty branch invited six fresh signup/join/payment members; Area admin approved all six through browser. | Sponsor path `111`; first five `1111`–`1115` at its slots 1–5; sixth `11111`, first child's slot 1. All retain sponsor `0b73ae7d-875b-487a-9b56-f4e6d65121ec`, including sixth whose parent differs. #33,54,62,74,82,89,101,116. |
| **P2 PASS** | Fresh sponsor and pre-cutoff demo member opened programme/progress browser pages; direct progress and closed-ledger evidence checked. | Fresh sponsor: six recruitment credits, Level 1 complete, Level 2 **1/25**, not Level 2 complete. Pre-cutoff tree remains Level 2/R400. None of seven newly placed participants occurs in earlier ledger or evidence nodes. #90,102,116. |
| **S1 PASS** | Area reviewer opened browser queue/targets; central reviewer confirmed Tenant 2 case in browser. Other actors invoked direct reads/writes; Host opened review UI. | Own Area allowed and list filters exclude other Areas/Tenants; explicit Tenant 2 list request 403. Cross-Area/Tenant targets, writes/details denied; missing permission, revoked assignment, anonymous/member and missing central scope denied without mutation. Central required explicit broader grant and calculation remained 403. Host compatible. Initial target/fixture failures corrected; #46–53,104–105,118,126. |
| **S2 PASS** | Area reviewer entered references and 5/5/5, confirmed in browser, refreshed, repeated direct request and supplied forged money/threshold/actor/release fields. Independent review found an unrecorded reservation step; a fresh case reserved unique supported lines before its browser confirmation, refresh and retry. | Confirmed + Met, quantities/version, actor #4/time/decision persisted; manual-review references retained. Exact repeat same decision/time; forged fields changed nothing. Final full-procedure case consumes reserved 5/5/5 once. Earlier root reservation is not retrospectively claimed. #63–64,111–112,127–128. |
| **S3 PASS** | Area reviewer edited draft 5/5/5 to 5/5/4, confirmed, then tried conflicting final quantities. Existing evaluator executed against 15/0/0 in fixture helper. | Confirmed + NotMet; exact category tallies, immutable final decision. 15/0/0 evaluates NotMet, not interchangeable total units. Structural placement unchanged. #67–69; runtime `evaluator1500=2`. |
| **S4 PASS** | Other-Area reviewer entered unacceptable synthetic reference/reason and clicked Reject for its authorized case; direct conflicting confirm followed. | Rejected with reviewer #40/time/reason; quantities and threshold null. Conflicting confirmation denied; final row unchanged. #71–72. |
| **S5 PASS** | Area reviewer started Held case, logged missing evidence manually; financial operator attempted calculation. Direct API began earlier-week held case. | Held has null final quantities/reviewer/result. Candidate unresolved evidence blocked batch and ledger stayed empty. Older case retained its original week. Reason/custody/late handling remained companion-log controls; no invented deadline or banking. #58–61. |
| **S6 PASS** | Area reviewer/register writer reserved synthetic invoice sale lines, confirmed shortfall, finalized allocation. Central reviewer submitted renamed receipt claim to register, entered browser draft, retained it held. Older-week duplicate also registered held. | Supported/allocated units 5/5/4; second allocations 0/0/0 across member/week aliases. Confirmed NotMet still consumes first allocation. App persists held decisions; **global deduplication/reservation serialization was manual**, not software. #66,68,106–107,122–125; retained manual register. |
| **C1 PASS** | Financial operator selected **Tenant 1** in browser and prepared latest closed earnings after candidate evidence finalized; direct calculation retry and member browser followed. | Fixed V2 terms `aqgreen-v2-demo-commission`, sales V1, decision V1. Level 2 R150+R250 = R400; separate Level 1 R150. Retry already calculated, zero new records, identical ledger. Member saw **Test calculation — no payout** for unreleased Earned result. #78,80–85,88,90–91. |
| **C2 PASS** | Operator read calculated positive held ledger and original cutoff obligation; controlled member opened browser programme page. | R150 retained, PostgreSQL `PayoutStatus=2` (Held), overdue monthly reason, Level 1/Met evidence, null ReleasedAt/PaidAt. Browser **Weekly test commission / On hold / reason**. This differs from R400 Earned/unreleased. Original wrong DTO/status-label assertions retained; #120–121 and runtime record. |
| **C3 PASS** | Permissioned Host financial operator checked absent UI release action and directly invoked Release/RecordPayment for both positive ledgers; PostgreSQL readback. | Both actions denied by backend demo boundary despite financial permissions; both rows unchanged. No released/paid AQGreen ledger, Onyx participation or loan. #93–98 and runtime record. |

## Defects, operator failures and retests

1. **Pre-hydration login credential submission — corrected.** First browser
   submit before React attached its handler performed native GET and put
   synthetic credentials into the local URL. Reproduction: load login without
   completed hydration, fill and submit → URL query, rather than authenticated
   POST. Root cause was a form with no POST method and an enabled submit before
   hydration. Existing source was unchanged at baseline: **PRE-EXISTING,
   walkthrough-exposed**. Submit is now disabled until hydrated and native method
   is POST. Synthetic diagnostic values were redacted and passwords rotated.
   Three no-JavaScript browser checks passed; later actual sign-ins/signup
   completed. No production credential was involved. Journal #1 retains failure.
2. **Direct weekly-sales target cancelled by Strict Mode — corrected.** Open
   `/admin/weekly-sales-reviews?tenantId=…&participantId=…` → no selected case.
   Effect marked a ref loaded before scheduling; cleanup cancelled the callback,
   and the repeated effect refused to run. Removing that redundant latch
   preserves cancellation and allows the effect to complete. Focused Strict Mode
   regression passes; Area, central, held/confirmed/rejected targets retested in
   actual browser. Original failures #34,38–45 and retests remain retained.
3. **Setup/operator errors, not product defects:** Alpine SWC mismatch; local dev
   origin blocked hydration; overly long synthetic passwords; missing session
   secret; wrong SQL column; central fixture missing ABP identity-role mapping;
   premature second-checkout read before navigation; assertion expecting a DTO
   enum field it does not expose; assertion expecting the unreleased label on a
   genuinely Held ledger. These were corrected within task-only setup/operator
   code. Central explicit grants and production rules were unchanged. The final
   hold check asserts the persisted enum, reason, amount, cutoff fact and UI
   **On hold** state, rather than treating unreleased as Held.
4. **Manual reservation omission — independent review and fresh retest.** The
   original root 5/5/5 review recorded no contemporaneous sale-line reservation
   search. Agent 2 identified this procedure omission. A different existing
   pre-cutoff member (`aqgreen.demo.network.08`) then received a fresh review:
   unique `SIM-SALE-002` supported units were reserved in the register before
   browser confirmation, finalized once, refreshed and replayed without changes.
   This demonstration occurred after calculation and did **not** reopen/recalculate
   the finalized period; all 37 ledger rows stayed identical (#129). It supplies
   full S2 procedure evidence, not retroactive proof of the earlier root's manual
   control. Post-calculation real-evidence exceptions remain an owner policy.

Exact product files changed in this execution:

- `AqualLifeStyle/9.4.2/aqua-frontend/src/components/auth/login-form.tsx`
- `AqualLifeStyle/9.4.2/aqua-frontend/src/components/auth/login-form.test.tsx`
- `AqualLifeStyle/9.4.2/aqua-frontend/src/components/admin/AdminWeeklySalesReviews.tsx`
- `AqualLifeStyle/9.4.2/aqua-frontend/src/components/admin/AdminWeeklySalesReviews.test.tsx`

No backend joining, placement, role permission, calculation, financial rule or
production guard was changed. Prior local and Agent 2 work was preserved.

## Focused validation and operating limits

Newly executed: local Release publish/schema migration; actual headless Chromium
operator workflows above; PostgreSQL readbacks; direct authenticated/anonymous
API negatives; **21/21** Vitest checks for the two corrected components; three
no-JavaScript browser form checks; TypeScript, focused ESLint and diff whitespace
checks passed. Old test reports are not the evidence for these case results.
The [auxiliary record](../verification/aqgreen-v2-agent-walkthrough-20261012/auxiliary-checks.json)
and [focused regression output](../verification/aqgreen-v2-agent-walkthrough-20261012/focused-regressions.log)
retain command/results; the three no-JavaScript checks were captured in actual
tool stdout rather than contemporaneous journal events.
No broad suite, CI, provider acceptance, production frontend build, Node 24 rerun,
deployment or production verification was executed.

The [manual register](../verification/aqgreen-v2-agent-walkthrough-20261012/manual-register.json)
is a synthetic operator exercise: one writer searched aliases and retained
reservations/final allocations across weeks. Application enforced record access,
held/final state, reviewer audit, fixed thresholds and immutable final decisions.
It does not automatically discover reused sales across documents/members/weeks,
and no synthetic reference establishes external retail commerce. Actual custody,
writer appointments, evidence standards, retention and exceptions remain owner
operating decisions before human/real-money use. Existing invitation model has no
expiry field; no invented expiry policy or expiry PASS is claimed by this checklist.

No remaining demonstrated MVP-critical defect in these 17 cases. Existing
clarity debt remains: sales UI says “verified quantity” for manual administrative
confirmation, and a missing-participation backend message still mentions
placement before checkout. Neither was reinterpreted as external verification or
pre-approval placement. Supported Node 24/production-build qualification is a
separate outstanding runtime limit, not claimed by this Node 26 dev execution.
Commercial/legal questions remain with Agent 2 and competent owner/legal review;
no commercial rules were silently changed.

Christ is Lord (Colossians 1:16–17). Honest measures (Proverbs 11:1), examined
claims (18:17), diligent work (21:5), and accountable financial administration
(2 Corinthians 8:20–21) govern truthful quantities, preserved failures/audits and
no payout. These are scriptural principles and derived engineering duties;
passing software results require the separate executed evidence above.

**Engineering status: TESTED in this isolated runtime.** Owner acceptance remains
not given. No human participants, outreach, real payments, insurer activation,
payouts, production records/access, B6/B7, Git publication or deployment occurred.
Evidence was handed to Agent 2. Its completed
[independent review](aqgreen-v2-agent-executed-walkthrough-independent-review.md)
supports **A** for the bounded recorded results, independently matched all 1,739
source hashes and inspected the scoped permission/payout guards. It identified
the S2 reservation and S1 log omissions closed by fresh execution above. This
was source/record review, not an independent runtime rerun or owner acceptance.

Cleanup completed: the three task-owned containers and PostgreSQL volume,
internal network, bridge processes/sockets and synthetic secret files were
removed. Sanitized evidence remains; no live pilot is left running.
