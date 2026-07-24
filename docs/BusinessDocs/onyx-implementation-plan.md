# Confirmed Onyx Model and Implementation Plan

Status: approved business direction as of 2026-07-23.

This document is the source of truth for the current Onyx feature phase. Older
membership documents remain useful historical material but do not override the
rules below.

## Canonical participation model

- Entry is a feeder programme into Onyx, not an Onyx subtype or a parallel
  `MembershipType`.
- Direct entrants create an Onyx participation only after a confirmed R6,120
  payment.
- Entry entrants create a separate Entry participation and qualify only after two
  confirmed R600 payments.
- Entry Level 2 makes the loan agreement available. Member acceptance followed
  by administrator approval makes the agreement effective. Creation of the
  separate Onyx participation remains deferred until the recruiter-placement rule
  for a graduating member is confirmed.
- Graduation never converts, deletes, or overwrites Entry participation, network
  placement, payments, or commissions.
- Neither lifecycle is represented by replacing `Customer.MembershipId`.
- Entry and Onyx calculations remain independent so Entry commissions can continue
  after graduation.

Customers may join Entry or Onyx independently without a recruiter. When a
recruiter exists, the recruiter is another customer with active participation in
the same programme. A recorded recruiter is therefore optional but verified:
active Entry participation is required to recruit into Entry, and active Onyx
participation is required to recruit into Onyx. Missing recruiter information is
valid and identifies the customer as the starting point of their own network.

Recruiting may cross Area boundaries. Programme placement is independent of the
Facilitator/Area Leader sales-referral model. A recorded placement becomes
permanent when the recruit qualifies, except for an explicit, justified, audited
administrator correction.

## Versioned financial terms

The current Entry terms are:

- registration payment: R600;
- activation payment: R600;
- monthly commitment: R600;
- grace period: seven days;
- complete Level 1 component: R150;
- complete Level 2 component: R250;
- complete Level 3 component: R1,250.

The current direct Onyx entry amount is R6,120. Every participation, obligation,
commission period, and agreement records the terms/rule version and the monetary
amounts used so later changes do not rewrite history.

The repayable R6,120 principal plus 30% charge is described as an Onyx loan rather
than generic funding so members are not led to believe it is a grant. The 30%
term and complete agreement workflow require South African credit-law review
before production launch; the domain model records the approved business proposal
but does not itself establish regulatory compliance.

## Complete-level network rules

Both programmes use five-person branches. Entry has three confirmed levels
(5, 25, 125). Onyx has five structural levels (5, 25, 125, 625, 3,125).
An incomplete level earns no partial component.

Only the Onyx Level 1 weekly cumulative total of R250 is currently confirmed by
the available source material. The repository does not confirm cumulative totals
for Levels 2–5.

If the proposed cumulative totals R250, R750, R2,327.50, R5,452.50, and
R17,952.50 are later approved, their mathematical decomposition is:

| Level | Population | Component | Implied per-person rate | Cumulative |
|---|---:|---:|---:|---:|
| 1 | 5 | R250.00 | R50.00 | R250.00 |
| 2 | 25 | R500.00 | R20.00 | R750.00 |
| 3 | 125 | R1,577.50 | R12.62 | R2,327.50 |
| 4 | 625 | R3,125.00 | R5.00 | R5,452.50 |
| 5 | 3,125 | R12,500.00 | R4.00 | R17,952.50 |

The Level 3 division is exact to one cent: R1,577.50 / 125 = R12.62. These
Levels 2–5 values remain provisional and must not enter executable commission
rules until approved.

## Compliance boundaries

An overdue Entry member keeps their placement and debt, while their own payout is
held. The effect on uplines is unresolved. Structural network qualification must
therefore remain separate from the future commission-contribution policy; no
assumed upline effect may be embedded in placement or qualification.

Commission periods initially use Africa/Johannesburg, Monday 00:00 through Sunday
23:59:59 local time. Period boundaries are configurable application settings and
every run records its exact period, time zone, calculation time, and rule version.
Administrator-triggered calculations precede automated scheduling.

## BusinessPremier deprecation

`MembershipType.BusinessPremier` is present as numeric enum value `3` and is used
by seed data, tier benefits, product eligibility, frontend labels, and tests.
Production databases may contain memberships, customers, and products linked to
those records.

Safe migration sequence:

1. Mark BusinessPremier as legacy in code and prevent new assignments.
2. Inventory production `Memberships`, `Customers`, and `Products` rows using the
   legacy membership.
3. Agree whether each legacy record maps to Onyx or another programme.
4. Apply an explicit, auditable data migration while retaining original IDs where
   practical.
5. Verify production data and API compatibility.
6. Remove the enum value only in a later breaking-change release.

No historical record is silently reinterpreted in the current phase.

## Delivery phases

1. **Domain foundations:** versioned terms, separate Entry and Onyx participation,
   confirmed-payment activation transitions, optional verified recruiter
   placement, independent network roots, and complete 5/25/125 Entry
   qualification.
2. **Persistence and payments:** EF mappings/migration, payment-provider boundary,
   unique external references, and controlled confirmed-payment processing.
3. **Obligations and Entry commissions:** monthly debt/grace records, configurable
   weeks, immutable components, and earned/held/released/paid transitions.
4. **Loan agreement:** member acceptance, admin approval/effective date, four
   weekly R200 requirements, additional repayments, deadline, and compliance
   restoration.
5. **Onyx network and earnings:** implement Level 1 first; add Levels 2–5 only
   after cumulative totals are approved.
6. **Benefits and separate accounts:** rental obligations, configurable product
   combos, travel entitlement, and persisted savings without mixing balances.
7. **Application and UI:** secured member/admin services, audited corrections,
   provider callbacks, reconciliation views, and business-language workflows.

Every phase must build and pass its focused tests before the next phase begins.

### Phase 2 implementation status

The persistence and provider-neutral confirmation foundation is complete:

- Entry participation, Onyx participation, recruiter-correction history, and
  member payments have explicit EF Core mappings and a PostgreSQL migration.
- A provider/reference pair is unique across the payment ledger.
- A verified confirmation is reconciled idempotently and invokes the domain
  transition for Entry registration, Entry activation, or direct Onyx entry.
- The confirmation processor is an internal application component, not a remote
  application service. Customers and administrators cannot call it as an API to
  mark a payment successful.
- Provider-specific callback verification remains intentionally unimplemented
  until Yoco credentials, signing rules, and webhook specifications are supplied.

### Phase 3 obligation status

The Entry monthly-obligation foundation is complete:

- each obligation records its Entry participation, customer, year/month identity,
  applicable terms version, amount, due time, and seven-day grace boundary;
- the lifecycle distinguishes due, grace period, overdue, and paid;
- overdue debt and the permanent Entry network position are preserved;
- overdue status blocks only the customer's own payout eligibility;
- confirmed late payment settles the debt and restores that eligibility without
  deleting the fact that the obligation became overdue;
- no upline contribution effect is inferred from the obligation state because
  that business rule remains unresolved.

Automatic obligation scheduling and payment allocation are deferred to the
secured application workflow phase.

The Entry weekly commission-ledger foundation is also complete:

- every closed period records its exact start, end, time zone, calculation time,
  and rules version;
- incomplete network levels record no partial component;
- completed levels retain separate R150, R250, and R1,250 components, producing
  the explainable cumulative total;
- each Entry participation can have only one ledger record per commission period;
- payout state distinguishes not earned, earned, held, released, and paid;
- releasing held funds and recording payment use explicit, idempotent domain
  transitions without rewriting the calculated components;
- an overdue customer's own record is held, while structural network
  qualification remains unchanged and no unconfirmed upline effect is applied.

The secured administrator-triggered calculation and review workflow is complete:

- only a host administrator with both the dedicated calculation permission and
  all-Areas access can prepare earnings;
- the calculation derives the latest fully completed Monday-to-Sunday week in
  `Africa/Johannesburg` time rather than accepting administrator-entered dates;
- active Entry networks are evaluated across Areas while ledger records are
  created only for the selected Area;
- repeating the calculation returns the existing period without duplicating
  ledger records;
- Area-scoped administrators can review only their own Area, while unscoped host
  review requires all-Areas permission;
- the administrator interface describes the records as weekly earnings and
  states that calculation does not release or pay funds.

Discrepancy reporting, release, payment, and automatic scheduling remain
application-layer work.

### Phase 4 loan-agreement status

The provider-neutral Onyx loan lifecycle foundation is complete:

- only an active Entry participant who has completed Level 2 may be offered the
  current versioned loan terms;
- the current R6,120 principal and 30% interest produce an explicit R7,956 total;
- member acceptance is recorded before administrator approval, and the
  administrator approval time is the effective date that starts the three-month
  repayment period;
- the first four R200 weekly requirements are separate records with separate due
  dates, so one late R800 payment cannot silently satisfy all four requirements;
- confirmed payments require explicit weekly allocation when used as catch-up,
  while additional unallocated repayments reduce the overall balance;
- payment application is idempotent and prevents overpayment;
- missed weekly requirements and an outstanding balance after the three-month
  deadline expose a payout-hold decision without changing network placement;
- the Entry commission calculator applies that decision only to the borrowing
  member's own payout, and an already-earned payout can be placed on hold without
  rewriting its calculated components;
- after compliance is restored, the held payout uses the existing explicit,
  idempotent release transition;
- the agreement, requirements, repayment allocations, payment references, and
  audit fields are persisted by the PostgreSQL model.

Secured member acceptance, administrator approval, provider callback,
reconciliation, and automated payout orchestration remain application-workflow work. A
loan agreement does not yet create an Onyx participation because the placement
of a graduating member in the Onyx network has not been confirmed.

### Phase 5 Onyx network and earnings status

The independently calculated Onyx network foundation is complete:

- only active Onyx participation contributes to the Onyx network; Entry
  participation and sales referrals cannot contribute to this calculation;
- structural qualification evaluates the confirmed five-person branches through
  Levels 1–5;
- incomplete Level 1 records no partial commission;
- the approved Level 1 weekly commission is one immutable R250 component;
- commission records preserve both the highest structurally qualified level and
  the highest commissioned level, so deeper structure does not accidentally
  create Levels 2–5 earnings while those amounts remain unapproved;
- every closed Onyx period records its exact boundaries, time zone, calculation
  time, and rules version;
- each Onyx participation has at most one commission record per period in a
  ledger separate from Entry;
- earned commission is released and marked paid through explicit, idempotent
  transitions.

Secured calculation and review use the same latest-completed-week, permission,
Area-scope, audit, and idempotency controls documented for Entry. Release and
payment workflows remain application-layer work. No Onyx hold rule has been
inferred from Entry obligations or the unresolved effect of overdue members on
their uplines.

### Phase 6 benefits and separate accounts status

The confirmed Level 3 travel-entitlement foundation is complete:

- a Club Member must have active Onyx participation and a complete Level 3
  structure before an entitlement can be granted;
- eligibility and activation are separate states;
- the entitlement records the qualification level, eligibility time,
  three-month waiting-period end, activation time, terms version, and the
  Club Member's confirmed 10% trip contribution;
- activation before the waiting period ends is rejected and repeated activation
  with the same facts is idempotent;
- the entitlement is persisted separately from participation, commissions,
  loans, payments, and future travel bookings.

Trip selection, pricing, booking, fulfilment, and payment are intentionally not
implemented.

#### Savings account status

The Club Member savings domain and persistence foundation is complete:

- an account belongs to an Area and Customer and matures exactly 12 months after
  opening;
- contributions use confirmed `SavingsContribution` payment records and are
  accepted only from the 1st through the 15th;
- every contribution must be at least R100 and may be larger;
- every contribution records the full 20% maturity interest independently,
  including contributions made later in the 12-month term;
- the same confirmed payment cannot be added twice to an account or across
  accounts;
- principal, projected interest, and the projected maturity amount remain
  separate, explainable values;
- withdrawals are unavailable before maturity;
- maturity snapshots principal, interest, and the amount due to the Club Member
  without rewriting contribution history;
- the existing caller-supplied three-month refund-threshold policy boundary is
  preserved until account-specific refund thresholds are confirmed.

The Club may use pooled savings to support loans for customers entering Entry,
but no individual Club Member contribution is assigned to or netted against a
specific borrower. Savings ownership, loan receivables, loan repayments, and
maturity liabilities remain separate ledgers.

Secured Club Member contribution workflows, verified provider callbacks,
maturity payout processing, pooled-fund accounting, and reconciliation remain
application-layer work. The promised 20% return and use of member savings for
lending require appropriate South African financial-services and consumer-credit
review before production launch; the domain model records the confirmed business
terms but does not establish regulatory compliance.

### Phase 7 application and UI status

The first secured programme-participation workflow is complete:

- a signed-in customer can review their own Entry and Onyx participation;
- a customer can start Entry or direct Onyx independently, or provide an
  optional recruiter who must already be active in the same programme;
- duplicate submissions preserve the original network-placement facts rather
  than silently changing the recruiter;
- the Club Member view explains current activation progress and the next amount
  due without claiming that a payment has been taken;
- a separately authorised administrator can reconcile participation status,
  Area, network placement, and provider-confirmed payment references;
- the administrator screen cannot mark payments as confirmed;
- a verified final activation payment promotes a Guest account to Club Member
  access, while Facilitator, Area Leader, and System Administrator roles are
  never downgraded.

The provider-neutral confirmation processor remains private. Checkout creation,
Yoco callback signature verification, and customer payment instructions cannot
be completed safely until the provider account, credentials, callback
specification, and deployment URLs are supplied.
