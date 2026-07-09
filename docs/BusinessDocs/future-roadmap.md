# Future Roadmap: aQua Lifestyle Club Platform

## Phase 1: Foundation (Current — Implemented)
- ✅ ABP-based backend with multi-tenancy, users, roles, JWT auth
- ✅ Membership tiers (Jasper, Onyx, AQGreen, Business Premier) with `TierBenefits`
- ✅ Membership activation dates and monthly obligation tracking
- ✅ Product catalog with membership-based eligibility (`ProductEligibilityManager`)
- ✅ Order intents (Draft → Reserved → Completed/Cancelled)
- ✅ Enquiry lifecycle with follow-ups, conversion probability, and enquiry→customer conversion
- ✅ Centralized exception hierarchy and validation framework
- ✅ Next.js admin frontend (customers, products, memberships, enquiries, order-intents)

## Phase 2: Savings & Order Cycle (Next Priority)
- [ ] Persist `SavingsAccount` (repository + app service) and add `SavingsDeposit`
- [ ] Club account types with registration fees and tier minimums (FR-09)
- [ ] Savings window enforcement: deposits 1st–15th, locked 17th–24th (BR-01)
- [ ] Refund-rule flagging (3-month threshold per tier) (BR-02–04)
- [ ] Interest/share-pool accrual (20% / 17%) with 12-month first-year lock (BR-12)
- [ ] Order calendar enforcement (opening/cut-off/delivery cycles) on `OrderIntent`
- [ ] Product combos with member vs Jasper pricing (FR-19)
- [ ] Monthly buying obligation tied to actual orders (BR-05)

## Phase 3: Registration & Subscription Levels
- [ ] Payment-verified registration pipeline (proof-of-payment upload, admin confirmation)
- [ ] Registration channels (online/office/presentation) with required documents (ID, bank letter)
- [ ] SMS/WhatsApp notifications (payment confirmation, welcome, collection dates)
- [ ] Onyx IBA subscription levels 0–5 with level fees, order sets, and payment/collection dates
- [ ] Jasper activation plans (Standard R950 / Premium R1200) with combo allocation
- [ ] Virtual membership cards with QR codes

## Phase 4: Area Network (Area Leaders, Area Spaces, Facilitators)
- [ ] Area Leader aggregate: licensing, application workflow, rank progression (Ruby → Ambassador)
- [ ] Area Space aggregate: approval workflow (42h review, 4 presentations, 20 startup orders)
- [ ] Area subscriptions and capacity/target tracking per rank
- [ ] Facilitator registration, referral tracking, and ranking (Bronze → Premier T/60)
- [ ] Referral awards and incentive issuance
- [ ] Order collection routing through Area Spaces/outlets

## Phase 5: Business Premier & Investments
- [ ] Clubbing plans A–D with waiting/circle periods and pooled equipment purchases
- [ ] Borrowing workflow (6-month eligibility, 30% charge, 6–8 month repayment)
- [ ] Investment project catalog and participation (60/40 profit share, bi-quarterly distribution)
- [ ] Funeral plan auto-link for Club Millionaire (R30,000, 6-month waiting)

## Phase 6: Engagement & Operations
- [ ] Life Therapy booking (3-in-1 / 2-in-1 packages) via Area Leader or admin
- [ ] Training & events calendar with attendance tracking
- [ ] Reporting: Area Space weekly reports, member volume targets (Onyx 75% / Jasper 25% / AQG 50%)
- [ ] Audit logging for all financial actions; POPIA compliance review for member PII
- [ ] Public member-facing portal (currently the frontend is admin-oriented)
