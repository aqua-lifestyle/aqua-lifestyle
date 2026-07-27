# Future Roadmap: aQua Lifestyle Club Platform

> The active Onyx delivery sequence is maintained in
> [`onyx-implementation-plan.md`](onyx-implementation-plan.md). Its confirmed
> decisions supersede the older membership assumptions below wherever they
> conflict.

## Phase 1: Foundation (Current — Implemented)
- ✅ ABP-based backend with multi-tenancy, users, roles, JWT auth
- ✅ Membership plan catalogue with the legacy Jasper, Onyx, AQGreen, and Business Premier values
- ⚠️ Shared-plan activation dates and obligation dates exist but are not valid member-specific participation records
- ✅ Product catalog with membership-based eligibility (`ProductEligibilityManager`)
- ✅ Order intents (Draft → Reserved → Completed/Cancelled)
- ✅ Enquiry lifecycle with follow-ups, conversion probability, and enquiry→customer conversion
- ✅ Centralized exception hierarchy and validation framework
- ✅ Next.js admin frontend (customers, products, memberships, enquiries, order-intents)

## Phase 2: Savings & Order Cycle (Next Priority)
- [x] Persist Club Member `SavingsAccount` and payment-linked contribution ledger
- [x] Add secured Club Member savings history and administrator reconciliation views
- [ ] Add account-opening authority and provider-confirmed contribution workflows
- [ ] Club account types with registration fees and tier minimums
- [x] Savings contribution window enforcement from the 1st–15th
- [ ] Refund-rule flagging (3-month threshold per tier) (BR-02–04)
- [x] Full 20% interest per contribution with 12-month maturity and early-withdrawal lock
- [ ] Maturity payout processing and pooled-fund/AQGreen-loan accounting
- [ ] Order calendar enforcement (opening/cut-off/delivery cycles) on `OrderIntent`
- [ ] Product combos with member vs Jasper pricing (FR-19)
- [ ] Monthly buying obligation tied to actual orders (BR-05)

## Phase 3: AQGreen and Onyx Participation
- [x] Confirmed-payment records with idempotent external references
- [x] Separate AQGreen and Onyx participation records; never convert one into the other
- [x] Direct Onyx activation after a confirmed R6,120 payment
- [x] AQGreen activation after one confirmed R1,200 joining payment, with independent joining or an optional verified AQGreen recruiter
- [x] Secured customer joining/status screens and read-only administrator reconciliation
- [x] Guest-to-Club-Member access promotion after verified final activation payment
- [x] Secured AQGreen monthly-commitment history and administrator reconciliation views
- [ ] Automatic AQGreen commitment scheduling and confirmed-payment allocation
- [x] Yoco hosted checkout and signed webhook activation for direct Onyx and AQGreen joining payments
- [ ] Yoco checkout workflows for other confirmed payment purposes
- [ ] AQGreen-to-Onyx graduation that preserves AQGreen history
- [x] Independent Onyx network placement confirmed for AQGreen graduates
- [ ] Registration channels (online/office/presentation) with required documents (ID, bank letter)
- [ ] SMS/WhatsApp notifications (payment confirmation, welcome, collection dates)
- [x] Domain and persistence foundations for complete-level AQGreen and Onyx commissions
- [x] Onyx structural qualification and confirmed commission components through Level 5
- [x] Level 3 travel eligibility, three-month waiting period, automatic activation tracking, and Club Member visibility
- [x] Secured administrator calculation and review for the latest completed Johannesburg week
- [ ] Confirm whether future earning periods should close on Friday instead of Sunday
- [x] Host-administrator release and external-payment recording workflows
- [x] Onyx Levels 2–5 commission rules using the confirmed per-person rates and cumulative totals
- [ ] Jasper activation plans (Standard R950 / Premium R1200) with combo allocation
- [ ] Virtual membership cards with QR codes

## Phase 4: Area Network (Area Leaders, Area Spaces, Facilitators)
- [ ] Area Leader aggregate: licensing, application workflow, rank progression (Ruby → Ambassador)
- [ ] Area Space aggregate: approval workflow (42h review, 4 presentations, 20 startup orders)
- [ ] Area subscriptions and capacity/target tracking per rank
- [ ] Facilitator registration, referral tracking, and ranking (Bronze → Premier T/60)
- [ ] Referral awards and incentive issuance
- [ ] Order collection routing through Area Spaces/outlets

## Phase 5: Onyx Loan and Legacy Business Premier Migration
- [x] Member-accepted and administrator-approved Onyx loan agreement domain and persistence foundation
- [x] Four explicit weekly minimum repayments and three-month settlement deadline foundation
- [x] Member-only loan compliance holds and idempotent release domain transitions
- [x] Secured Club Member loan history and administrator reconciliation views
- [ ] Loan offer, member acceptance, Club approval, and provider-confirmed repayment workflows
- [ ] Inventory and explicitly migrate legacy `BusinessPremier` membership data
- [ ] Investment project catalog and participation (60/40 profit share, bi-quarterly distribution)
- [ ] Funeral plan auto-link for Club Millionaire (R30,000, 6-month waiting)

## Phase 6: Engagement & Operations
- [ ] Life Therapy booking (3-in-1 / 2-in-1 packages) via Area Leader or admin
- [ ] Training & events calendar with attendance tracking
- [ ] Reporting: Area Space weekly reports, member volume targets (Onyx 75% / Jasper 25% / AQG 50%)
- [ ] Audit logging for all financial actions; POPIA compliance review for member PII
- [ ] Public member-facing portal (currently the frontend is admin-oriented)
