# Gap Analysis: aQua Lifestyle Club Platform

This report compares the business model described in the provided business documents
(*National Club Aqgreen*, *Area Space / Area Leader*, *Membership*) against the current
codebase (`AqualLifeStyle/9.4.2` — ASP.NET Boilerplate backend + Next.js frontend).

> **Current authority (2026-07-26):** The confirmed Onyx decisions recorded in
> [`BusinessDocs/onyx-implementation-plan.md`](BusinessDocs/onyx-implementation-plan.md)
> supersede the older membership assumptions in this report wherever they conflict.

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

`Membership` is a shared plan-catalogue record. Its current `ActivationDate` and
`LastObligationMetDate` fields are consequently not safe for member-specific Onyx
activation or payment compliance. `Customer.MembershipId` is also a single plan link
and must not be used to convert an Entry participation into an Onyx participation.

Frontend (`aqua-frontend`, Next.js): pages exist for customers, enquiries, memberships, order-intents, products.

## 3. Capability Gap Matrix

| Business Capability (from docs) | Status | Notes |
|--------------------------------|--------|-------|
| Membership plan catalogue | ✅ Implemented | Reuse `Membership`; do not create a duplicate catalogue aggregate |
| BusinessPremier replacement by Onyx | ⚠️ Migration required | Enum value `3`, seed data, products, tests, and historical database rows may still reference it; deprecate before an explicit data migration and remove only after production verification |
| Member-specific activation & monthly obligation tracking | 🔧 Foundation implemented | Entry participation and monthly obligations are separate persisted records with versioned amounts, due/grace/overdue/paid states, preserved debt, and payment linkage; obligation scheduling and member/admin workflows remain |
| Entry feeder participation | 🔧 Workflow in progress | Secured self-service joining and status, independent or verified optional-recruiter placement, two confirmed R600 activation payments, permanent history, and read-only administrator reconciliation are implemented; provider checkout/callback integration remains |
| Direct Onyx participation | 🔧 Workflow in progress | Secured self-service joining and status are implemented. A confirmed R6,120 payment activates the separate Onyx record, grants Club Member access without downgrading higher roles, and does not create Entry history; provider checkout/callback integration remains |
| Entry-to-Onyx graduation | ❌ Missing | Creates a separate Onyx participation while preserving Entry history |
| AQGreen/Onyx five-person networks | 🔧 Workflow in progress | Independent customers form network roots; optional recruiters must actively participate in the same programme. Secure participation-based invitation links, recruiter preview, explicit confirmation, immutable Club Member numbers, and audited administrator corrections are implemented. AQGreen evaluates Levels 1–3 and Onyx structural qualification evaluates Levels 1–5 |
| Weekly member commission ledger | 🔧 Foundation implemented | AQGreen and Onyx use separate persisted periods and ledgers. AQGreen retains its complete Levels 1–3 components and member-only compliance holds. Onyx records the confirmed complete-level components through Level 5, including the exact R12.62 Level 3 per-person rate. Secured administrator-triggered calculation and payout workflows remain |
| Member payment and Yoco reconciliation ledger | 🔧 Foundation implemented | Payments and participation are persisted; provider/reference uniqueness and idempotent confirmed-payment processing are enforced. A verified Yoco webhook adapter remains pending until its signing specification and credentials are supplied |
| Onyx loan agreement and repayments | 🔧 Foundation implemented | Level 2 eligibility, versioned R6,120 + 30% terms, member acceptance, admin approval/effective date, four explicit weekly minimums, additional repayments, three-month deadline, idempotent payment allocation, persistence, and member-only commission holds are implemented; secured workflows and automated orchestration remain |
| Product catalog + member eligibility | ✅ Implemented | `ProductEligibilityManager` |
| Product combos (Combo 2/3/4/5) with member vs Jasper pricing | ❌ Missing | Products exist, but no combo/bundle concept or dual pricing |
| Order intents (draft/reserve/cancel/complete) | ✅ Implemented | `OrderIntent` |
| Order window enforcement (opening/cut-off/delivery date cycles: 1st–5th–10th, 6th–10th–15th, 11th–16th–25th) | ⚠️ Partial | `TierBenefits` models order windows; no scheduler/enforcement on `OrderIntent` |
| Club Member savings accounts | 🔧 Foundation implemented | Persisted Area/Customer accounts mature 12 months after opening; confirmed contributions require at least R100 and retain payment-linked, immutable history. Secured Club Member/admin workflows remain |
| Savings window and withdrawal rules | 🔧 Foundation implemented | Contributions are accepted from the 1st–15th and withdrawals remain unavailable until the exact 12-month maturity date |
| 20% savings maturity interest | 🔧 Foundation implemented | Every contribution records its full 20% interest and maturity snapshots principal, interest, and payout separately. Maturity payout and pooled-fund accounting remain |
| Refund rule (savings below threshold within 3 months → refund minus admin/branding) | ⚠️ Policy boundary | The existing three-month threshold check is preserved, but account-specific thresholds, deductions, approval, and refund payment workflow remain unresolved |
| Registration workflows (WhatsApp/online/office/presentation channels, proof of payment, SMS confirmation) | ❌ Missing | Enquiry→Customer conversion exists but no payment-verified registration pipeline |
| Onyx levels 1–5 with rental, product, and travel incentives | 🔧 Domain foundation | Structural qualification and confirmed weekly commission components are implemented through Level 5 independently from AQGreen. A persisted Level 3 travel entitlement tracks eligibility, its three-month wait, activation, and the 10% Club Member contribution. Rental workflows and configurable product rewards remain |
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
| Enquiry→Customer conversion (was fake: only flipped `IsConverted`, never created Customer) | 🔧 In progress | **Fixed in Phase 4** — `ConvertToCustomerAsync` now assigns/activates the membership tier on the referenced Customer; raises `EnquiryConvertedEvent` |
| Enquiry `ReferredByFacilitatorId` + referral attribution | 🔧 In progress | **Phase 4** — `ReferralAttributionService` creates direct (facilitator) + indirect (area leader) referrals on conversion |
| Area Leader licensing, application (20+ interested, 42h review, 4 presentations, 20 startup orders) | 🔧 In progress | **Phase 3** — `AreaLeader` + `AreaSpace` aggregates with Fail-Fast approval guards |
| Area Leader ranks (Ruby → Ambassador) with order targets | 🔧 In progress | **Phase 3** — `RankProgressionPolicy` over `AreaLeaderRankConfiguration` |
| Area Space approval & lifecycle | 🔧 In progress | **Phase 3** — `AreaSpace` workflow (Applied→UnderReview→Approved/Suspended) |
| Facilitator registration, ranking (Bronze → Premier T/60), referral awards | 🔧 In progress | **Phase 2** — `Facilitator` + `Referral` aggregates; `RankProgressionPolicy` over `FacilitatorRankConfiguration` |
| Sales referral / award tracking | 🔧 In progress | Existing `Referral` and `CommissionCalculator` apply to Facilitator/Area Leader enquiry attribution, not the Entry/Onyx member network |
| Real Admin (JWT auth + RBAC on business services) | ❌ Missing → planned | **Phase 5** — `TokenAuth` login + `[AbpAuthorize]` + Admin role grants |
| Admin network dashboard | ❌ Missing → planned | **Phase 6** — `GetNetworkOverviewAsync` read model |
| Enquiry lifecycle (respond/close/reopen, follow-ups, conversion) | ✅ Implemented | base lifecycle; conversion semantics fixed in Phase 4 |
| Multi-tenancy | ✅ Implemented | ABP built-in; new aggregates `IMustHaveTenant` (ADR-001) |

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

Existing platform work remains documented in the Mission Plan. The current Onyx
feature is sequenced separately in
[`BusinessDocs/onyx-implementation-plan.md`](BusinessDocs/onyx-implementation-plan.md):

1. Establish separate Entry and Onyx participation aggregates with versioned terms.
2. Persist confirmed payments and member obligations with idempotent external references.
3. Calculate complete network levels and immutable weekly Entry commissions.
4. Add agreement approval, repayment compliance, and held-payout release.
5. Operate the confirmed complete-level Onyx commission rules through Level 5
   while preserving separate immutable ledger components.
6. Add rental, product-combo, travel, and savings workflows.
7. Expose secured member/admin use cases and professional frontend workflows.

See `docs/BusinessDocs/future-roadmap.md` for the phased plan and `docs/Assumptions.md` for assumptions needing business validation. ADRs for the new work live in `docs/adr/`.
