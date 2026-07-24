# Business Requirements: aQua Lifestyle Club Platform

## 1. Project Overview

- **Project Name**: aQua Lifestyle Club (AqualLifeStyle)
- **Description**: Membership, savings-club, and product-subscription platform for aQua Lifestyle Club — a wellness club selling aQuathz water products through membership tiers, a national savings club (AQGreen), and a distributed network of Area Leaders and Facilitators.
- **Target Users**: Club members (Jasper, Onyx, AQGreen, Business Premier), Area Leaders, Facilitators, and aQua administration staff.
- **Business Goal**: "Live in health, inspire to wealth" — recurring product subscriptions, structured monthly savings with interest/profit share, and a licensed area-based distribution network.

## 2. Functional Requirements

Status legend: ✅ Implemented in codebase · 🔧 Domain/persistence foundation implemented; application layer pending · ⚠️ Partially implemented / assumed · ❌ Missing

### 2.1 Membership Management

| ID | Requirement | Priority | Status |
|----|-------------|----------|--------|
| FR-01 | Support membership tiers: Jasper, Onyx, AQGreen, Business Premier | Critical | ✅ Implemented |
| FR-02 | Track membership activation date | Critical | ✅ Implemented |
| FR-03 | Track tier-specific monthly obligations and mark them met | Critical | ✅ Implemented |
| FR-04 | Jasper activation plans: Standard R950, Premium R1200 (limited offer) with combo allocations | High | ❌ Missing |
| FR-05 | Onyx IBA subscription levels 1–5 with level fees (R850–R1200) and product incentives | High | ❌ Missing |
| FR-06 | Members given 4 months to fully commit from sign-up (membership, tools, branding, cards) | Medium | ❌ Missing |
| FR-07 | Membership cards (virtual card with unique QR code) | Medium | ❌ Missing |
| FR-08 | Status A → Status B upgrade at Level 3 (Onyx affiliate) | Medium | ❌ Missing |

### 2.2 AQGreen National Club (Savings)

| ID | Requirement | Priority | Status |
|----|-------------|----------|--------|
| FR-09 | Club Member savings account: minimum R100 per contribution, larger voluntary contributions allowed | Critical | 🔧 Domain and persistence foundation |
| FR-10 | Savings contributions accepted 1st–15th of each month | Critical | 🔧 Domain foundation |
| FR-11 | Savings locked 17th–24th (administration verification period) | Critical | ❌ Missing |
| FR-12 | Every contribution receives the full 20% interest when the account matures 12 months after opening | High | 🔧 Domain and persistence foundation |
| FR-13 | Refund rule: below minimum threshold within 3 months → refund minus admin & branding costs | High | ❌ Missing |
| FR-14 | Withdrawals blocked until account maturity 12 months after opening | High | 🔧 Domain foundation |
| FR-15 | Registration payment must complete within 14 business days with proof submitted | High | ❌ Missing |
| FR-16 | Track savings principal, per-contribution interest, and maturity amount per Club Member | Critical | 🔧 Persistence foundation; application service pending |

### 2.3 Product Catalog & Orders

| ID | Requirement | Priority | Status |
|----|-------------|----------|--------|
| FR-17 | Product catalog (aQuathz 1L, 5L, 125ml/250ml spraythz, health sets) with retail prices | Critical | ✅ Implemented (products CRUD) |
| FR-18 | Membership-based product visibility/eligibility | Critical | ✅ Implemented |
| FR-19 | Product combos (Combo 2 R258/282, Combo 3 R410/425, Combo 4 R378/417, Combo 5 R598/637) with member vs Jasper pricing | Critical | ❌ Missing |
| FR-20 | Order intents with lifecycle Draft → Reserved → Completed/Cancelled | Critical | ✅ Implemented |
| FR-21 | Order cycle enforcement: opening/cut-off/delivery windows (1st→5th→10th; 6th→10th→15th; 11th→16th→25th) | High | ❌ Missing |
| FR-22 | Monthly buying obligation — a member must not skip a month without buying | High | ⚠️ Partial (obligation tracking exists, not tied to orders) |
| FR-23 | Level-based monthly subscription orders (Level 0: Combo 4; Levels 1–3: Combo 4 ×4; Levels 4–5: Combo 4 ×8) | High | ❌ Missing |
| FR-24 | Proof-of-payment submission and admin confirmation before order release | High | ❌ Missing |
| FR-25 | Collection via Area Space/outlet with 42-hour area change notice | Medium | ❌ Missing |

### 2.4 Registration & Enquiries

| ID | Requirement | Priority | Status |
|----|-------------|----------|--------|
| FR-26 | Capture enquiries (name, contact details) and lifecycle Pending → Responded → Closed with reopen | High | ✅ Implemented |
| FR-27 | Enquiry follow-ups with outcomes and conversion probability | High | ✅ Implemented |
| FR-28 | Convert enquiry to customer; assign enquiries to members | High | ✅ Implemented |
| FR-29 | Registration channels: online (WhatsApp/ALC admin, AQG admin), office (reception), presentation (outdoor market) | Medium | ❌ Missing |
| FR-30 | Registration requirements: ID number + copy, contact info, WhatsApp, bank confirmation letter | Medium | ❌ Missing |
| FR-31 | SMS/WhatsApp confirmations for payment, welcome, and collection dates | Medium | ❌ Missing |

### 2.5 Area Leaders, Area Spaces & Facilitators

| ID | Requirement | Priority | Status |
|----|-------------|----------|--------|
| FR-32 | Area Leader registration/licensing (entre R750, Area Independent Leader R2500) | High | ❌ Missing |
| FR-33 | License application criteria: 20+ interested members, 42h area review, 4 consecutive presentations, 30-day profile completion | High | ❌ Missing |
| FR-34 | Area Leader ranks: Ruby, Emerald, Premier, Dimond, VIP, Presidential, Chairman's circle, Ambassador — with order targets (20 → 18,000) and income tables | High | ❌ Missing |
| FR-35 | Area subscriptions by rank (R500 – R5,590 monthly) | Medium | ❌ Missing |
| FR-36 | Area Space approval workflow (address, promotional area, profile, weekly meetings, 20 startup orders in 4 weeks) | High | ❌ Missing |
| FR-37 | Facilitator registration under Area Leaders, referral tracking | High | ❌ Missing |
| FR-38 | Facilitator ranking: Bronze (10 direct), Gold (10), Pearl (5), Sapphire (5), Ruby (20), Platinum (10), Premier T/60 — with indirect targets and awards (R50 → R68,750) | High | ❌ Missing |
| FR-39 | Cap constraints: max 300 Area Leaders; target 700 outlet businesses | Low | ❌ Missing |

### 2.6 Business Premier & Investments

| ID | Requirement | Priority | Status |
|----|-------------|----------|--------|
| FR-40 | Clubbing plans A–D (R6,000 / R12,000 / R20,000 / R50,000) with 6-month waiting and 3-month circle periods | Medium | ❌ Missing |
| FR-41 | Borrowing: 6 months saving without skipping before borrowing; 30% total charge repaid in 6–8 months | Medium | ❌ Missing |
| FR-42 | Investment project catalog (therapy centre, stores, bottling company, lodges, green energy, farm, education, funeral/insurance) | Low | ❌ Missing |
| FR-43 | Profit share: company 60% / shareholder pool 40%, paid bi-quarterly, shared equally among pool participants | Medium | ❌ Missing |
| FR-44 | Funeral plan auto-link: 6-month waiting period, R30,000 allocated plan (Club Millionaire) | Medium | ❌ Missing |

### 2.7 Therapy, Training & Events

| ID | Requirement | Priority | Status |
|----|-------------|----------|--------|
| FR-45 | Life Therapy plan bookings (3-in-1 R3,160 / R1,495; 2-in-1 R1,920) via Area Leader or admin | Low | ❌ Missing |
| FR-46 | Training calendar (membership maintenance Sat 9am, presentations Wed 10am, mission Mon 9am, leadership Thu 2×/month, etc.) | Low | ❌ Missing |
| FR-47 | Event management (testimonials bi-monthly, brand demos monthly, Area Leader intro events) | Low | ❌ Missing |

### 2.8 Platform / Administration

| ID | Requirement | Priority | Status |
|----|-------------|----------|--------|
| FR-48 | Multi-tenancy | Critical | ✅ Implemented (ABP) |
| FR-49 | User/role management and authentication (JWT) | Critical | ✅ Implemented (ABP) |
| FR-50 | Admin UI for customers, products, memberships, enquiries, order intents | High | ✅ Implemented (Next.js frontend) |

## 3. Non-Functional Requirements

| ID | Requirement | Target |
|----|-------------|--------|
| NFR-01 | API response time | < 500ms p95 (assumed) |
| NFR-02 | Concurrent users | 100+ (assumed) |
| NFR-03 | HTTPS only in production | Required |
| NFR-04 | JWT-based auth with secure key management | Required (implemented; secrets via env vars) |
| NFR-05 | Tenant data isolation | Required (implemented via ABP) |
| NFR-06 | Audit logging of financial actions (savings, payments, refunds) | Required for savings features |
| NFR-07 | POPIA compliance for member PII (ID numbers, bank letters) | Required — South African market |

## 4. Business Rules

| ID | Rule | Source |
|----|------|--------|
| BR-01 | Savings deposits only between the 1st and 15th; no deposits 17th–24th | National Club / Membership docs |
| BR-02 | Legacy Standard R1,500 three-month refund threshold remains a policy input until its relationship to the current universal Club Member account is confirmed | National Club doc / current clarification |
| BR-03 | Legacy Club Millionaire R2,500 three-month refund threshold remains a policy input until account types are confirmed | National Club doc / current clarification |
| BR-04 | Legacy Business Premier R4,500 three-month refund threshold must not be assigned to current Onyx savings without an explicit migration decision | National Club doc / current clarification |
| BR-05 | A member month must not pass without buying a product | National Club doc |
| BR-06 | Onyx members may only pay subscription for the level they qualify for | Membership doc |
| BR-07 | Area Leaders can only receive and redirect collections; extra orders for themselves only, unless license-qualified to sell | Area Leader doc |
| BR-08 | Area Leaders cannot sell to club members at extra price or charge transport fees | Area Leader doc |
| BR-09 | Orders are paid directly to the company account, not to Area Leaders | Area Leader doc |
| BR-10 | Funeral cover has a 6-month waiting period | National Club doc |
| BR-11 | Borrowing requires 6 months of saving without skipping | National Club doc |
| BR-12 | Withdrawals are blocked until 12 months after account opening; every accepted contribution receives the full 20% interest at maturity | Current confirmed rule |
| BR-13 | Pooled savings may finance Entry customer loans, but Club Member savings and borrower loan ledgers remain separate | Current confirmed rule |

## 5. Assumptions

See `docs/Assumptions.md` for the full assumptions register and `docs/ValidationPlan.md` for how each will be validated.
