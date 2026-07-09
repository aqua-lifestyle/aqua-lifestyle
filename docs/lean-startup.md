# Lean Startup: Area-Network Experiments

Build–Measure–Learn loops for the Area-Network features, framed for the demo and post-demo iteration.

## 1. Riskiest assumptions

| # | Assumption | Experiment | Metric |
|---|-----------|-----------|--------|
| L1 | Facilitators will generate converting leads | Recruitment + conversion funnel | Facilitator recruitment velocity (new facilitators / week) |
| L2 | Referred leads convert better than organic | Compare conversion rate by source | Referral→conversion rate (direct/indirect vs organic) |
| L3 | Area Leaders grow their area to approval | Track Area Space applications to approval | Area-leader growth & approval rate |

## 2. Experiments

### E1 — Facilitator recruitment velocity
- **Build:** facilitator registration + referral attribution.
- **Measure:** `# facilitators registered` and `# referrals attributed` per week from the network dashboard.
- **Learn:** if velocity is low, revisit onboarding friction (training requirement, license fee).

### E2 — Referral → conversion rate
- **Build:** `Enquiry.ReferredByFacilitatorId` + conversion event.
- **Measure:** conversion rate of referred enquiries vs organic; direct vs indirect award payout.
- **Learn:** validate that direct referrals convert materially higher; tune `CommissionCalculator` rates.

### E3 — Area Leader growth
- **Build:** Area Space application workflow with approval guards.
- **Measure:** applications submitted, approvals granted, time-in-review vs 42h SLA, cap utilisation (of 300).
- **Learn:** calibrate the 20-interested / 4-presentation / 20-startup-order gates.

## 3. Build-Measure-Learn cadence

- Cadence: weekly review of dashboard metrics during the demo pilot.
- Pivot trigger: if referral→conversion rate < organic for two consecutive weeks, re-examine the
  incentive structure (award amounts in `CommissionCalculator` are seeded, not hard-coded — see V-03).
- Per-feature instrumentation: every approval/award is audit-logged via `FullAuditedAggregateRoot`.

## 4. Anti-patterns to avoid

- Hard-coding commission/rank figures in domain code — they live in seeded config and are flagged V-03.
- Premature savings/investment features — explicitly deferred.
