# Validation Plan: aQua Lifestyle Club Platform

How to validate the assumptions recorded in `docs/Assumptions.md` before building on them.

## V-01: Pricing Confirmation Workshop
**Validates:** A-01, A-02, A-03, A-04, A-05, A-07

- Compile a single pricing sheet (registrations, minimum savings, subscription fees, combo prices — member vs Jasper vs retail) from all three PDFs, highlighting every conflict found.
- Review with the business owner in one session; record the authoritative figure and effective date for each item.
- Outcome: a signed-off `pricing.md` reference used to seed configuration/seed data (avoid hard-coding prices in code).

## V-02: Savings Rules Specification Session
**Validates:** A-06, A-08, A-09

- Walk through a calendar month with the business owner using 3 concrete member scenarios (on-time saver, late saver, under-threshold saver).
- Confirm: exact open/locked/blocked days, how the 3-month refund threshold is evaluated, how the 20%/17% pool is calculated and paid, and what "no guarantee" means operationally.
- Outcome: acceptance criteria for the savings domain (Phase 2), expressed as unit-testable rules.

## V-03: Business Rule Confirmation with Leadership
**Validates:** A-10, A-11, A-12, A-13, A-14

- Explicitly resolve every "Dave opinion" item: first-year 12-month lock, 6-month interest forfeiture, Area Leader offer pricing.
- Confirm Facilitator stage arithmetic (direct vs indirect, cumulative vs per-stage) with a worked example per rank.
- Confirm the period basis of the Area Leader income table.
- Outcome: signed-off rules appendix; anything unresolved stays out of scope.

## V-04: Technical Discovery Spikes
**Validates:** A-15, A-16, A-18, A-20

- Spike 1: prototype persisting `SavingsAccount` with a deposit-window check to confirm the domain object composes with ABP repositories/unit-of-work.
- Spike 2: evaluate SMS/WhatsApp gateways available in South Africa (Clickatell, Twilio, WhatsApp Business API) — cost per message, template approval lead time.
- Spike 3: decide tenancy mapping (single tenant vs tenant-per-division) and document the decision as an ADR.
- Outcome: ADRs committed under `docs/adr/`.

## V-05: Payment & Insurance Process Review
**Validates:** A-17, A-21, A-22

- Shadow the current manual registration/payment process with ALC admin for one order cycle; document actual steps, timings, and failure points.
- Confirm with the business owner whether card payments are in scope or EFT + proof-of-payment suffices for launch.
- Identify the funeral-cover insurance partner and confirm the platform only tracks eligibility/linkage.
- Outcome: payment workflow spec for Phase 3.

## V-06: POPIA Compliance Checklist
**Validates:** A-19

- Inventory all PII collected at registration (ID number + copy, bank confirmation letter, contact details).
- Define consent capture, retention periods, access controls, and encryption-at-rest requirements before building document upload.
- Outcome: compliance checklist gating the registration pipeline (Phase 3); consider legal review.

## Validation Tracking

| Validation | Assumptions Covered | Owner | Status |
|------------|--------------------|-------|--------|
| V-01 Pricing workshop | A-01–A-05, A-07 | Business owner + dev | ⏳ Pending |
| V-02 Savings rules session | A-06, A-08, A-09 | Business owner + dev | ⏳ Pending |
| V-03 Business rule confirmation | A-10–A-14 | Leadership ("Dave") + dev | ⏳ Pending |
| V-04 Technical spikes | A-15, A-16, A-18, A-20 | Dev team | ⏳ Pending |
| V-05 Payment/insurance review | A-17, A-21, A-22 | ALC admin + dev | ⏳ Pending |
| V-06 POPIA checklist | A-19 | Dev + legal | ⏳ Pending |
