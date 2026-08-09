# Confirmed Onyx Model and Implementation Plan

Status: approved business direction, including the AQGreen naming clarification
and confirmed joining/graduation rules, as of 2026-08-01.

This document is the source of truth for the current Onyx feature phase. Older
membership documents remain useful historical material but do not override the
rules below.

## Canonical participation model

- AQGreen is a feeder programme into Onyx, not an Onyx subtype or a parallel
  `MembershipType`.
- AQGreen is the business name that replaces the earlier working name “Entry”.
  Existing `Entry*` code identifiers, API routes, database tables, and immutable
  records are retained for backward compatibility; they represent AQGreen and
  must not be treated as a separate programme.
- Direct entrants create an Onyx participation only after a confirmed R6,120
  payment.
- AQGreen entrants create a separate AQGreen participation and activate only after
  one verified R1,200 joining payment. The R600 monthly commitment is separate
  from the joining payment.
- AQGreen Level 2 makes the loan agreement available. Member acceptance followed
  by administrator approval makes the agreement effective. Creation of the
  separate Onyx participation uses independent placement: the graduate has no
  Onyx recruiter and becomes the root of a new Onyx network.
- Graduation never converts, deletes, or overwrites AQGreen participation, network
  placement, payments, or commissions.
- Neither lifecycle is represented by replacing `Customer.MembershipId`.
- AQGreen and Onyx calculations remain independent so AQGreen commissions can continue
  after graduation.

The customer-facing Onyx joining action has one path: direct Onyx entry. AQGreen
graduation is a separate, explicit administrator decision after the current
eligibility and the active, member-accepted, administrator-approved R6,120 loan
are revalidated; it is not a second choice in the Onyx joining form and is never
triggered merely because a loan becomes active. Graduation creates a separate
Onyx participation while preserving the complete active AQGreen record.

Customers may join AQGreen or Onyx independently without a recruiter. When a
recruiter exists, the recruiter is another customer with active participation in
the same programme. A recorded recruiter is therefore optional but verified:
active AQGreen participation is required to recruit into AQGreen, and active Onyx
participation is required to recruit into Onyx. Missing recruiter information is
valid and identifies the customer as the starting point of their own network.

Recruiting may cross Area boundaries. Programme placement is independent of the
Facilitator/Area Leader sales-referral model. A recorded placement becomes
permanent when the recruit qualifies, except for an explicit, justified, audited
administrator correction.

## Versioned financial terms

The current AQGreen terms are:

- joining obligation: one R1,200 payment;
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

Both programmes use five-person branches. AQGreen has three confirmed levels
(5, 25, 125). Onyx has five structural levels (5, 25, 125, 625, 3,125).
An incomplete level earns no partial component.

The business owner has confirmed the Onyx per-person weekly commission rates
and cumulative totals through Level 5:

| Level | Population | Component | Confirmed per-person rate | Cumulative |
|---|---:|---:|---:|---:|
| 1 | 5 | R250.00 | R50.00 | R250.00 |
| 2 | 25 | R500.00 | R20.00 | R750.00 |
| 3 | 125 | R1,577.50 | R12.62 | R2,327.50 |
| 4 | 625 | R3,125.00 | R5.00 | R5,452.50 |
| 5 | 3,125 | R12,500.00 | R4.00 | R17,952.50 |

The Level 3 rate is exactly R12.62 per qualifying participant and is retained as
decimal currency without substitution or further rounding. Each complete level
adds its own immutable ledger component; incomplete levels earn no partial
component.

## Compliance boundaries

An overdue AQGreen member keeps their placement and debt, while their own payout is
held. The effect on uplines is unresolved. Structural network qualification must
therefore remain separate from the future commission-contribution policy; no
assumed upline effect may be embedded in placement or qualification.

Commission periods use Africa/Johannesburg, Friday 00:00 through Thursday
23:59:59 local time. Every run records its exact period, time zone, calculation
time, and rule version. Automatic AQGreen and Onyx orchestration is implemented
behind a disabled production gate, but must not be armed until calculation uses
business state effective at Thursday close. Administrator-triggered calculation
must not be used as historical recovery: older missing cycles require authorised
manual financial reconciliation.

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

1. **Domain foundations:** versioned terms, separate AQGreen and Onyx participation,
   confirmed-payment activation transitions, optional verified recruiter
   placement, independent network roots, and complete 5/25/125 AQGreen
   qualification.
2. **Persistence and payments:** EF mappings/migration, payment-provider boundary,
   unique external references, and controlled confirmed-payment processing.
3. **Obligations and AQGreen commissions:** monthly debt/grace records, configurable
   weeks, immutable components, and earned/held/released/paid transitions.
4. **Loan agreement:** member acceptance, admin approval/effective date, four
   weekly R200 requirements, additional repayments, deadline, and compliance
   restoration.
5. **Onyx network and earnings:** calculate confirmed complete-level components
   through Level 5 and retain every component in the immutable weekly ledger.
6. **Benefits and separate accounts:** rental obligations, configurable product
   combos, travel entitlement, and persisted savings without mixing balances.
7. **Application and UI:** secured member/admin services, audited corrections,
   provider callbacks, reconciliation views, and business-language workflows.

Every phase must build and pass its focused tests before the next phase begins.

### Phase 2 implementation status

The persistence and provider-neutral confirmation foundation is complete:

- AQGreen participation, Onyx participation, recruiter-correction history, and
  member payments have explicit EF Core mappings and a PostgreSQL migration.
- A provider/reference pair is unique across the payment ledger.
- A verified confirmation is reconciled idempotently and invokes the domain
  transition for an AQGreen joining payment, legacy AQGreen split payments, or direct Onyx entry.
- The confirmation processor is an internal application component, not a remote
  application service. Customers and administrators cannot call it as an API to
  mark a payment successful.
- Direct Onyx and AQGreen joining now use persisted checkout records, Yoco's
  hosted checkout, and signature-verified payment webhooks. The adapter checks
  timestamp freshness, deployment mode, exact amount/currency, invitation and
  recruiter eligibility, and provider-reference idempotency before atomically
  creating or activating programme state. AQGreen placement exists before payment,
  but it becomes active only after the verified R1,200 joining total. An active
  hosted checkout locks the full-payment schedule. A historical participant with
  one verified R600 joining instalment may complete that preserved obligation;
  the verified payment history is not rewritten.

### Phase 3 obligation status

The AQGreen monthly-obligation foundation is complete:

- each obligation records its AQGreen participation, customer, year/month identity,
  applicable terms version, amount, due time, and seven-day grace boundary;
- the lifecycle distinguishes due, grace period, overdue, and paid;
- overdue debt and the permanent AQGreen network position are preserved;
- overdue status blocks only the customer's own payout eligibility;
- confirmed late payment settles the debt and restores that eligibility without
  deleting the fact that the obligation became overdue;
- Club Members can securely review persisted commitment months, due dates,
  grace periods, outstanding balances, and payment status;
- administrators can reconcile commitments within their Area, while host-wide
  review separately requires all-Areas permission;
- no upline contribution effect is inferred from the obligation state because
  that business rule remains unresolved.

Automatic obligation scheduling and payment allocation are deferred to the
secured application workflow phase.

The AQGreen weekly commission-ledger foundation is also complete:

- every closed period records its exact start, end, time zone, calculation time,
  and rules version;
- incomplete network levels record no partial component;
- completed levels retain separate R150, R250, and R1,250 components, producing
  the explainable cumulative total;
- each AQGreen participation can have only one ledger record per commission period;
- payout state distinguishes not earned, earned, held, released, and paid;
- releasing held funds and recording payment use explicit, idempotent domain
  transitions without rewriting the calculated components;
- an overdue customer's own record is held, while structural network
  qualification remains unchanged and no unconfirmed upline effect is applied.

The shared automatic and administrator-triggered calculation foundation is complete:

- only a host administrator with both the dedicated calculation permission and
  all-Areas access can prepare earnings;
- the calculation derives the latest fully completed Friday-to-Thursday cycle in
  `Africa/Johannesburg` time rather than accepting administrator-entered dates;
- active AQGreen networks are evaluated across Areas while ledger records are
  created only for the selected Area;
- repeating the calculation returns the existing period without duplicating
  ledger records;
- Area-scoped administrators can review only their own Area, while unscoped host
  review requires all-Areas permission;
- the administrator interface describes the records as weekly earnings and
  states that calculation does not release or pay funds.

Network qualification now reconstructs AQGreen and Onyx placement at the period
cutoff from `ActivatedAt` and valid recruiter-correction history. Ambiguous,
discontinuous, dangling, cyclic, or soft-deleted network evidence fails closed;
branches with more than five children use the earliest five by effective
placement/activation time and participation ID. Onyx travel consumes the same
cutoff network, vests eligibility at the first qualifying closed-cycle cutoff,
and records delayed activation at the contractual waiting-period end.

Production calculation is still not fully cutoff-correct. AQGreen continues to
read mutable obligation and loan compliance state, existing Areas still require
explicit prospective activation baselines, cycle-effective terms boundaries are
not configured, and the Yoco occurrence timestamp/finality contract is
unverified. The append-only Area-history capability now makes future cutoffs
deterministic and blocks new ledgers when target-Area state is unknown or inactive;
it does not invent historical state. Consequently
`App:WeeklyCommissions:Enabled` must remain `false`, and the latest-week
administrator action must not be treated as authoritative production recovery.

The host-only period inventory reports canonical Friday-to-Thursday periods,
legacy Monday-to-Sunday or malformed periods, soft-deleted rows, exact boundary
duplicates, non-overlapping boundaries, and missing canonical cycles. It is
read-only. Every missing cycle is classified as requiring manual financial
reconciliation; no API calculates an arbitrary historical cycle from current
state or current terms.

Eligible earnings can now be released for payment by a host administrator with
separate release and all-Areas permissions. A separately permissioned action
records an externally completed payment and its reference; the platform does not
send money. Both actions require an audit justification and are idempotent.
Held AQGreen earnings cannot be released through this workflow until compliance
restoration can be verified from an approved policy. Discrepancy reporting and
automatic scheduling remain application-layer work.

### Phase 4 loan-agreement status

The provider-neutral Onyx loan lifecycle foundation is complete:

- only an active AQGreen participant who has completed Level 2 may be offered the
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
- the AQGreen commission calculator applies that decision only to the borrowing
  member's own payout, and an already-earned payout can be placed on hold without
  rewriting its calculated components;
- after compliance is restored, the held payout uses the existing explicit,
  idempotent release transition;
- the agreement, requirements, repayment allocations, payment references, and
  audit fields are persisted by the PostgreSQL model.
- Club Members can securely review their persisted loan terms, weekly
  requirements, confirmed repayments, outstanding balance, and any own-payout
  hold;
- administrators can reconcile persisted loan agreements within their Area,
  while a host-wide view separately requires all-Areas permission.

Loan offer creation, secured member acceptance, administrator approval,
provider callbacks, payment allocation workflows, and automated payout
orchestration remain application-workflow work. The domain now provides a
transition for an effective agreement that creates a separate active Onyx
participation with independent placement, linked to the original AQGreen
participation and loan.
The future secured approval workflow must invoke this transition atomically and
idempotently so it cannot create duplicate Onyx participation records.

### Phase 5 Onyx network and earnings status

The independently calculated Onyx network foundation is complete:

- only active Onyx participation contributes to the Onyx network; AQGreen
  participation and sales referrals cannot contribute to this calculation;
- structural qualification evaluates the confirmed five-person branches through
  Levels 1–5;
- an incomplete level records no partial commission for that level;
- confirmed Levels 1–5 each record a separate immutable weekly component;
- commission records preserve both the highest structurally qualified level and
  the highest commissioned level, with cumulative totals derived from only the
  fully completed levels;
- every closed Onyx period records its exact boundaries, time zone, calculation
  time, and rules version;
- each Onyx participation has at most one commission record per period in a
  ledger separate from AQGreen;
- earned commission is released and marked paid through explicit, idempotent
  transitions.

Secured calculation, review, release, and external-payment recording use the same
latest-completed-week, permission, Area-scope, audit, and idempotency controls
documented for AQGreen. Payment recording requires the reference from a transfer
completed outside the platform and does not initiate a transfer. No Onyx hold
rule has been inferred from AQGreen obligations or the unresolved effect of
overdue members on their uplines. These controls do not solve the documented
period-end placement cutoff blocker.

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

The weekly engine synchronizes travel eligibility independently from commission
calculation. A verified complete Level 3 structure in the latest closed Onyx
network grants one entitlement, repeated runs do not duplicate it, and a later
run activates it after the three-month waiting period has elapsed. A travel
failure cannot roll back an Onyx commission period, and a commission failure
cannot suppress travel synchronization. The signed-in Club Member can see the
waiting-period end, availability status, and their 10% trip contribution in
business language. New travel qualification uses the same cutoff-effective Onyx
network as commission and no longer changes because of a post-cutoff recruiter
correction. Existing-Area baselines, cycle-effective travel terms, missed-cycle
discovery, and complete workflow evidence remain blockers, so the automatic gate
must remain disabled.

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

The Club may use pooled savings to support loans for customers entering AQGreen,
but no individual Club Member contribution is assigned to or netted against a
specific borrower. Savings ownership, loan receivables, loan repayments, and
maturity liabilities remain separate ledgers.

Club Members can now review their persisted account, confirmed contribution
history, projected interest, and maturity date. Separately authorised
administrators can reconcile the same ledgers within their Area scope, while
host-wide review requires all-Areas permission. An account whose maturity date
has passed is labelled as requiring maturity processing rather than falsely
claiming that a payout occurred.

Account-opening authority, secured contribution workflows, verified provider
callbacks, maturity payout processing, and pooled-fund accounting remain
application-layer work. The promised 20% return and use of member savings for
lending require appropriate South African financial-services and consumer-credit
review before production launch; the domain model records the confirmed business
terms but does not establish regulatory compliance.

### Phase 7 application and UI status

The first secured programme-participation workflow is complete:

- a signed-in customer can review their own AQGreen and Onyx participation;
- a customer can start AQGreen or direct Onyx independently, or provide an
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
  never downgraded;
- role promotion invalidates the previous authenticated session, so the customer
  is asked to sign in again and then returns to the programme status page with
  current Club Member permissions.

The provider-neutral confirmation processor remains private. Checkout creation,
Yoco callback signature verification, and customer payment instructions cannot
be completed safely until the provider account, credentials, callback
specification, and deployment URLs are supplied.
