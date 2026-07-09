# Gap Analysis: aQua Lifestyle Club Platform

This report compares the business model described in the provided business documents
(*National Club Aqgreen*, *Area Space / Area Leader*, *Membership*) against the current
codebase (`AqualLifeStyle/9.4.2` — ASP.NET Boilerplate backend + Next.js frontend).

## 1. Business Sources Analyzed

| Source | Content |
|--------|---------|
| Membership.pdf | Jasper (Standard R950 / Premium R1200) and Onyx subscription structures, IBA levels 1–5, combo pricing, order/collection dates, therapy plans, trainings |
| National Club Aqgreen.pdf | AQGreen National Club divisions (Standard R560, Club Millionaire R1200, Business Premier R790, Investment Projects R2500), savings rules, refund rules, product pricing, registration channels |
| Area Space / Area Leader.pdf | Area Leader licensing, ranks (Ruby → Ambassador), Area Space approval, Facilitator roles and ranking (Bronze → Premier T/60), income tables, trainings |

## 2. Current Implementation Snapshot

Backend (`aspnet-core`), verified in code:

- **Domain entities**: `Customer`, `Product`, `Membership` (+ `TierBenefits`, `MembershipBenefit`), `Enquiry` (+ `EnquiryFollowUp`), `OrderIntent`, `SavingsAccount` (domain object only, no repository/app service)
- **Enums**: `MembershipType` (Jasper, Onyx, AQGreen, BusinessPremier), `OrderIntentStatus` (Draft, Reserved, Cancelled, Completed), `EnquiryStatus` (Pending, Responded, Closed)
- **App services**: Products, Customers, Enquiries, Memberships, Orders (+ ABP built-ins: Users, Roles, Tenants, Sessions)
- **Cross-cutting**: centralized exception hierarchy, `AqualLifeStyleValidator`, `ProductEligibilityManager` (async)

Frontend (`aqua-frontend`, Next.js): pages exist for customers, enquiries, memberships, order-intents, products.

## 3. Capability Gap Matrix

| Business Capability (from docs) | Status | Notes |
|--------------------------------|--------|-------|
| Membership tiers Jasper/Onyx/AQGreen/BusinessPremier | ✅ Implemented | `MembershipType` + `TierBenefits` |
| Membership activation & monthly obligation tracking | ✅ Implemented | `MembershipAppService` |
| Product catalog + member eligibility | ✅ Implemented | `ProductEligibilityManager` |
| Product combos (Combo 2/3/4/5) with member vs Jasper pricing | ❌ Missing | Products exist, but no combo/bundle concept or dual pricing |
| Order intents (draft/reserve/cancel/complete) | ✅ Implemented | `OrderIntent` |
| Order window enforcement (opening/cut-off/delivery date cycles: 1st–5th–10th, 6th–10th–15th, 11th–16th–25th) | ⚠️ Partial | `TierBenefits` models order windows; no scheduler/enforcement on `OrderIntent` |
| Savings accounts with monthly deposits (min R100–R1500 by tier) | ⚠️ Partial | `SavingsAccount` domain object exists; not wired to repository or app service |
| Savings window rules (deposits 1st–15th; locked 17th–24th admin period) | ❌ Missing | Documented in TierBenefits only; no enforcement |
| 20% (17% Business Premier) interest / profit-share pool (60/40 split) | ❌ Missing | No interest accrual or distribution logic |
| Refund rule (savings below threshold within 3 months → refund minus admin/branding) | ❌ Missing | |
| Registration workflows (WhatsApp/online/office/presentation channels, proof of payment, SMS confirmation) | ❌ Missing | Enquiry→Customer conversion exists but no payment-verified registration pipeline |
| IBA subscription levels 1–5 with level fees and product incentives | ❌ Missing | |
| Area Leader licensing, application (20+ interested members, 42h review, 4 presentations) | ❌ Missing | No Area Leader/Area Space domain at all |
| Area Leader ranks (Ruby → Ambassador) with order targets & income tables | ❌ Missing | |
| Area Space approval & lifecycle | ❌ Missing | |
| Facilitator registration, ranking (Bronze → Premier T/60), referral awards | ❌ Missing | |
| Referral / commission / merge-payment tracking | ❌ Missing | |
| Investment projects (R2500 activation, R5000 security savings, project catalog) | ❌ Missing | |
| Business Premier clubbing plans (R6k/12k/20k/50k circles, 6-month waiting) | ❌ Missing | |
| Funeral plan auto-link (6-month waiting, R30,000 plan) | ❌ Missing | |
| Therapy plan bookings (3-in-1 / 2-in-1 packages) | ❌ Missing | |
| Training & events scheduling (weekly/monthly training calendar) | ❌ Missing | |
| Enquiry lifecycle (respond/close/reopen, follow-ups, conversion) | ✅ Implemented | |
| Multi-tenancy | ✅ Implemented | ABP built-in |

## 4. Document Status Matrix

| Document | Status | Action Taken |
|----------|--------|--------------|
| requirements.md | ❌ Was missing | Created — `docs/BusinessDocs/requirements.md` |
| user-stories.md | ❌ Was missing | Created — `docs/BusinessDocs/user-stories.md` |
| domain-model.md | ❌ Was missing | Created — `docs/BusinessDocs/domain-model.md` |
| workflows.md | ❌ Was missing | Created — `docs/BusinessDocs/workflows.md` |
| future-roadmap.md | ❌ Was missing | Created — `docs/BusinessDocs/future-roadmap.md` |
| Assumptions.md | ❌ Was missing | Created — `docs/Assumptions.md` |
| ValidationPlan.md | ❌ Was missing | Created — `docs/ValidationPlan.md` |
| ARCHITECTURE_GAP_REPORT.md | ✅ Existing | Technical/architecture view; this report adds the business-document view |
| README.md (root) | ⚠️ Minimal | Contains only the repo name; recommend expanding with project overview and doc links |

## 5. Priority Recommendations

1. **Savings domain (HIGH)** — wire `SavingsAccount` to persistence + app service; enforce deposit windows, minimums per tier, refund rule, interest accrual.
2. **Order cycle enforcement (HIGH)** — apply opening/cut-off/delivery date rules and monthly-buying obligation to `OrderIntent`.
3. **Combos & dual pricing (HIGH)** — model product combos with member/Jasper/retail price tiers.
4. **Registration pipeline (MEDIUM)** — payment-verified registration with proof-of-payment upload and confirmation notifications.
5. **Area Leader / Area Space / Facilitator contexts (MEDIUM)** — new aggregates for licensing, ranks, referrals, and incentives.
6. **Investment projects & profit share (LOW/LATER)** — depends on savings + membership maturity.

See `docs/BusinessDocs/future-roadmap.md` for the phased plan and `docs/Assumptions.md` for assumptions needing business validation.
