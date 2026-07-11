# Assumptions Register: aQua Lifestyle Club Platform

Assumptions made while interpreting the business documents and mapping them to the codebase.
Each assumption references its validation approach in `docs/ValidationPlan.md`.

## A. Pricing & Financial Figures

| ID | Assumption | Basis | Confidence | Validation |
|----|------------|-------|------------|------------|
| A-01 | Standard Club registration is **R560** (National Club summary) even though a later slide shows R500; the R560 figure is authoritative | Conflicting figures across slides in National Club Aqgreen.pdf | Low | V-01 |
| A-02 | Standard minimum monthly saving is **R310** (summary) though other slides show R210 and R510 | Three different figures appear in the same document | Low | V-01 |
| A-03 | Club Millionaire registration is **R1200** with **R1210** shown elsewhere being a variant/typo | Conflicting figures | Low | V-01 |
| A-04 | Onyx Level 5 subscription fee "R 1" is a typo (likely R1,200+ tier); Status B totals (e.g. R8,880) are authoritative | Membership.pdf level table | Low | V-01 |
| A-05 | All amounts are ZAR (South African Rand) | Contact details are Johannesburg-based; "R" prefix | High | V-01 |
| A-06 | The 20% interest is an annual share-pool benefit administered manually, not a guaranteed bank-style interest ("No guarantee" noted) | National Club doc | Medium | V-02 |
| A-07 | Combo prices differ intentionally between member set price (e.g. Combo 4 = R378) and Jasper plan price (R417) | Both price lists appear across the docs | Medium | V-01 |

## B. Business Rules

| ID | Assumption | Basis | Confidence | Validation |
|----|------------|-------|------------|------------|
| A-08 | Savings deposits are blocked on the 16th and from the 25th to month end (docs only state 1st–15th open, 17th–24th locked) | Gap in the documents | Medium | V-02 |
| A-09 | Refund thresholds (R1500/R2500/R4500 "in less than 3 months") mean cumulative savings after 3 months, evaluated monthly | Ambiguous wording | Medium | V-02 |
| A-10 | The "Dave opinion" annotations (first-year 12-month lock, 6-month interest forfeit) are provisional rules pending owner confirmation | Explicitly marked as opinion in the doc | Low | V-03 |
| A-11 | "A month must not skip without buying" means one qualifying order per calendar month per member | National Club doc | High | V-03 |
| A-12 | Facilitator stage targets are cumulative direct referrals reaching 60 total at Premier T/60 | Area Leader doc ranking table | Medium | V-03 |
| A-13 | Area Leader income table figures (e.g. Ruby: R15,120 order set / R1,380 income) are per monthly cycle | Table lacks explicit period | Low | V-03 |
| A-14 | Orders are always paid directly to the company account; Area Leaders never handle member money | Stated in Area Leader doc | High | V-03 |

## C. Technical & Scope

| ID | Assumption | Basis | Confidence | Validation |
|----|------------|-------|------------|------------|
| A-15 | The current Next.js frontend is an internal admin tool; a separate member-facing portal will be needed | Frontend pages are CRUD/admin oriented | Medium | V-04 |
| A-16 | SMS/WhatsApp notifications will be delivered through a third-party gateway (e.g. Twilio/Clickatell); currently manual | No notification infrastructure in codebase | High | V-04 |
| A-17 | Payments are manual EFT with proof-of-payment upload — no card gateway required initially | Registration flows all reference bank deposit + proof | Medium | V-05 |
| A-18 | Multi-tenancy (ABP) will map to club divisions/regions rather than separate companies | ABP default in codebase; docs don't mention tenancy | Low | V-04 |
| A-19 | POPIA applies: ID numbers and bank letters are personal information requiring consent, retention limits, and secure storage | South African jurisdiction | High | V-06 |
| A-20 | `SavingsAccount` in the domain is intended to grow into the AQGreen club account concept | Naming and gap-report notes | High | V-04 |
| A-21 | Funeral cover (R30,000, 6-month waiting) is administered by an external insurer; the platform only tracks linkage/eligibility | Insurance requires licensing | Medium | V-05 |
| A-22 | Therapy bookings are scheduling-only (no payment processing) in the first iteration | Therapy plans are "top-up" from monthly maintenance | Medium | V-05 |
