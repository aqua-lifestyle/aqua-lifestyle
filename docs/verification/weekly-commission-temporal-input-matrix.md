# Weekly Commission Temporal Input Matrix

Branch: `feat/programme-engine-gap-closure`

Baseline commit: `ab7187c247e14d678d0ccc9710a0fcef4fdca9cf`

Status: temporal rules are partially confirmed; implementation and verification
are incomplete. Weekly automation remains disabled, and administrator-triggered
calculation is not authoritative while any material cutoff input is unresolved.

## Purpose

This document identifies every material input used by AQGreen and Onyx weekly
commission calculation and classifies whether the repository can establish that
input as of the Friday-to-Thursday cycle cutoff.

The target invariant is:

```text
Commission(C) uses business-effective facts for C,
not mutable facts observed when C is processed later.
```

## Classification

| Class | Meaning |
| --- | --- |
| A | Already period-correct. The current calculation uses an authoritative effective timestamp for the cycle. |
| B | Immutable once created. Existing creation/effective evidence is sufficient under the confirmed rule. |
| C | Historically reconstructable. Current state is mutable, but existing history can reconstruct the cutoff state once its effective-time semantics are confirmed. |
| D | Current-state only. A material value can change and existing evidence cannot reliably reconstruct it. |
| E | Product Decision. The repository does not define which available timestamp or state is financially effective. |

## AQGreen Dependency Graph

```text
Weekly worker or host administrator
  -> LatestClosedCommissionWeekResolver
  -> WeeklyCommissionCalculator.CalculateEntryAsync
     -> target-Area cutoff-state resolution
     -> current commission terms provider (not cutoff-effective)
     -> existing EntryCommissionPeriod lookup
     -> EntryParticipation rows across Areas
        -> current Status
        -> ActivatedAt
        -> current RecruiterCustomerId
     -> EntryMonthlyObligation rows
        -> current Status
     -> OnyxLoanAgreement rows for AQGreen participation
        -> current agreement Status
        -> current weekly requirement Status
     -> EntryNetworkQualificationEvaluator
        -> complete levels 1-3 using exactly five branches
     -> EntryWeeklyCommissionCalculator
        -> qualified level
        -> obligation hold
        -> loan hold
     -> EntryCommissionPeriod
     -> EntryWeeklyCommission and level components
     -> explicit release and external-payment recording
```

Material reads are in `WeeklyCommissionCalculator.cs:80-169`,
`EntryNetworkQualificationEvaluator.cs:20-74`, and
`EntryWeeklyCommissionCalculator.cs:18-69`.

## Onyx Dependency Graph

```text
Weekly worker or host administrator
  -> LatestClosedCommissionWeekResolver
  -> WeeklyCommissionCalculator.CalculateOnyxAsync
     -> target-Area cutoff-state resolution
     -> current commission terms provider (not cutoff-effective)
     -> existing OnyxCommissionPeriod lookup
     -> OnyxParticipation rows across Areas
        -> current Status
        -> ActivatedAt
        -> current RecruiterCustomerId
     -> OnyxNetworkQualificationEvaluator
        -> complete levels 1-5 using exactly five branches
     -> OnyxWeeklyCommissionCalculator
     -> OnyxCommissionPeriod
     -> OnyxWeeklyCommission and level components
     -> explicit release and external-payment recording
```

Material reads are in `WeeklyCommissionCalculator.cs:172-247`,
`OnyxNetworkQualificationEvaluator.cs:39-125`, and
`OnyxWeeklyCommissionCalculator.cs:17-38`.

Onyx has no current hold rule. A positive calculated amount is `Earned`; level
zero is `NotEarned`.

## Temporal Input Matrix

| Input | Programme | Result affected | Mutable? | Historical evidence exists? | Effective timestamp available? | Post-cutoff change can alter closed cycle now? | Class | Evidence / decision |
| --- | --- | --- | ---: | ---: | ---: | ---: | --- | --- |
| Cycle boundary and timezone | Both | Period identity | No for this branch | Persisted period boundary | Yes | No | A | Resolver uses Friday 00:00 to Thursday end in `Africa/Johannesburg`. |
| Target Area eligibility | Both | Whether the target Area receives a ledger | Yes | Forward-only append-only activation records | Yes after provisioning, an explicit observed baseline, or a later change | No after the first record; earlier cutoffs remain unknown | C/D | Implemented prospectively. New Areas record initial state; existing Areas expose a host-authorised baseline action; activation changes append new evidence. The worker enumerates all Areas and resolves cutoff state. New worker and administrator ledger writes fail before insertion when state is inactive or unknown. No legacy row is seeded or inferred. |
| Source Area activity | Both | Whether a cross-Area node remains in another Area's network | Yes | Current `Tenant.IsActive` only | Not required by the confirmed rule | No, because source-Area activity is not consulted | B/out of calculation | An active participation remains a network node even when its source Area is inactive. Area state governs only the target Area's ledger eligibility. |
| Participation identity | Both | Network node and ledger owner | No sanctioned identity mutation | Participation row | `StartedAt` | No | B | IDs and programme ownership are stable. |
| Participation activation | AQGreen | Network inclusion | One-way before Active | `ActivatedAt` and approval decision | Yes | No for normal Friday approval | A | Calculator already requires `ActivatedAt <= cycle.End`. |
| Participation activation | Onyx | Network inclusion | One-way before Active | `ActivatedAt` and approval/graduation decision | Yes | No for normal Friday approval | A | Calculator already requires `ActivatedAt <= cycle.End`. |
| Participation rejection | Both | Prevents activation | Terminal before Active | Approval decision | `DecidedAt` | No after a valid activation | B | No Active-to-Rejected path exists. |
| Participation deactivation/termination | Both | Network inclusion | Not modelled | None | None | Not through a supported path | E | No inactive/suspended/terminated participation state exists. |
| Participation soft deletion | Both | Network inclusion | Yes through persistence semantics | `DeletionTime` | Processing timestamp exists | Yes | D | Fail closed unless cutoff-effective participation state is independently proven. Do not interpret administrative deletion as financial termination. |
| Recruiter placement | AQGreen | Network edges, levels, amount | Yes | Previous/new correction rows | `CorrectedAt` exists | Yes | C | A correction is prospectively effective at `CorrectedAt`; replay the valid chain as of cutoff. |
| Recruiter placement | Onyx | Network edges, levels, amount | Yes | Previous/new correction rows | `CorrectedAt` exists | Yes | C | Same prospective effective-time rule as AQGreen. |
| Recruiter correction ordering | Both | Deterministic placement replay | Append-only through domain, but no DB sequence | Correction chain and timestamps | No independent sequence/recorded time | Yes when timestamps are equal or history is discontinuous | D | Fail closed on ambiguous chains or add minimal ordering evidence; do not invent legacy order. |
| Active network set | Both | Qualified level | Derived from activation and placement | Activation plus correction history | Conditional | Yes | C | Reconstruct from cutoff-effective participation and placement facts. |
| At-least-five branch rule | Both | Qualified level | Code-deploy mutable | Existing ledgers preserve output only | No calculation algorithm effective date | Yes across a deployment | D | Qualification remains valid with more than five children. Select the earliest five by effective placement/activation time, then stable participation ID. |
| AQGreen maximum level and component rules | AQGreen | Level and amount | Code-deploy mutable | Ledger components and rules version only after calculation | Current hard-coded terms expose one `EffectiveFrom` | Yes before first calculation | D/E | Current implementation has levels 1-3 and R150/R250/R1,250 components, but no retained as-of registry and no authorised Friday start for the existing version. |
| Onyx rates and level rules | Onyx | Amount and components | Code-deploy mutable | Ledger components and rules version only after calculation | Current hard-coded terms expose one `EffectiveFrom` | Yes before first calculation | D/E | Confirmed rates remain R50/R20/R12.62/R5/R4, but no retained as-of registry or authorised Friday start exists. |
| Commission terms version | Both | Rates, currency, rules version | Deploy mutable | Current in-code version and persisted output `RulesVersion` | Not for an uncalculated historical cycle | Yes | D/E | Terms may switch only Friday 00:00 Johannesburg. Introduce an immutable as-of capability that starts empty; do not register the current version until its authoritative Friday boundary is supplied. |
| Existing period identity and immutable facts | Both | Replay/idempotency | Intended immutable | Period row and uniqueness constraint | `CalculatedAt` records processing, not source-fact time | No ordinary recalculation after a visible period | B | The visible row and unique target/start/end key are authoritative duplicate evidence. They do not prove source facts. |
| Period or commission soft deletion | Both | Visibility, idempotency, and ledger completeness | Yes | Soft-delete audit projection | Deletion time is administrative recording evidence only | Yes; default queries hide rows while uniqueness remains | D/E | Fail closed and reconcile. Deletion is not an authorised financial reversal, termination, or recalculation mechanism. |
| Existing calculated components | Both | Persisted level and amount | Immutable through supported workflow | Ledger and components | `CalculatedAt` | No ordinary recalculation | B | Existing component facts explain the stored result but do not prove that every source input was cutoff-correct. |
| Monthly obligation policy coverage | AQGreen | Which obligation months must exist | Append-only capability, currently empty | Policy version, due day, and effective month once supplied | Durable resolver exists | Yes while no version covers the cycle | D/E | First required month is after activation. No initial version, due day, launch boundary, or effective month is authorised. Calculation must fail closed outside proven policy coverage. |
| Expected monthly obligation completeness | AQGreen | Whether compliance can hold payout | Rows are scheduled later and soft-deletable | Year/month, due/grace, policy version, audit fields | Conditional on proven policy coverage | Yes; missing rows currently mean no hold | D | Require exactly one visible, policy-consistent row for every expected month through cutoff. Missing, deleted, duplicate, or inconsistent evidence is unresolved, not compliant. |
| Monthly due and grace boundary | AQGreen | Overdue state | Immutable on row | `DueAt`, `GracePeriodEndsAt` | Yes | Current status can change Friday | C | Policy is versioned/effective-dated; day 1-28 at 00:00 Johannesburg. Derive overdue as of cutoff from persisted boundaries, not current status. |
| Monthly payment-to-obligation association | AQGreen | Which debt is settled | Mutable projection; checkout flow absent | Obligation `PaymentId` when applied | No server-persisted monthly checkout intent exists | Yes | D | A fresh hosted checkout must identify exactly one server-authoritative `ObligationId` and its immutable period context before provider creation. Unlinked or conflicting evidence requires reconciliation. Persistence must prevent one payment settling multiple obligations. |
| Provider payment occurrence | AQGreen | Whether payment removed a hold by cutoff | External fact | Signed event status and provider payment object | No verified successful-payment occurrence field | Yes | D/E | Signature-verified `payment.succeeded` proves final status. Official evidence reviewed so far describes `payload.createdDate` as payment-object creation, not success occurrence. Do not map it to financial effectiveness without a provider contract. |
| Provider event receipt and processing | AQGreen | Operational retry/reconciliation only | Yes | Local notification processing state | `ProcessedAt` exists; distinct HTTP receipt time and completeness watermark do not | Can conceal late or missing evidence | D | Keep provider occurrence, HTTP receipt, and completed processing distinct. No delivery-finality horizon or reconciliation watermark currently proves that all pre-cutoff events were received. |
| Obligation current status | AQGreen | Earned versus Held | Yes | Due/grace/payment timestamps | Conditional | Yes | C | Current enum should be replaced by an as-of derivation once semantics are confirmed. |
| Loan agreement effective state | AQGreen | Earned versus Held | Yes | `EffectiveAt`, deadline, settlement and allocation rows | Multiple timestamps exist | Yes | C/D | Derive from immutable facts as of cutoff. Legacy allocations remain unprovable until authorised reconciliation supplies allocation evidence. |
| Weekly loan requirement | AQGreen | Earned versus Held | Yes | Due time, overdue observation, satisfaction and allocations | Due and payment receipt times exist | Yes | C/D | Cure is effective at the allocation decision time; current rows lack that evidence. |
| Loan repayment allocation | AQGreen | Requirement satisfaction and balance | Yes | Append-only allocation with `ReceivedAt` | No separate `AllocatedAt` | Yes | D | Add immutable `AllocatedAt`; do not backfill legacy values from provider receipt time. |
| Customer active state | Both | Currently no calculation effect | Yes | Current projection only | No history used | No, because calculator ignores it | B/out of calculation | Do not add a new eligibility rule without a business decision. |
| Refund, dispute, or chargeback | Both | Participation, network, compliance, and payout state | External and operational | No complete programme policy or transition history | No confirmed business-effective rule | Unknown | E | Do not reverse participation, qualification, or existing ledgers without an authorised policy and auditable adjustment workflow. |
| Calculation lock | Both | Concurrency/idempotency | Operational | Database transaction | Processing time | Does not solve cutoff facts | B | Preserve shared PostgreSQL/SQL Server lock. |
| Period/commission/component uniqueness | Both | Duplicate prevention | Schema | Unique indexes | Insert time | Does not solve cutoff facts | B | Preserve all existing constraints. |
| Release eligibility decision | Both | `Earned` or `Held` to `Released` | Yes | Current ledger status, release reason, and server recording time | No separate business-effective release decision evidence | Yes | D/E | Current application releases only `Earned`; the domain held-release capability has no production caller. Persist an append-only authorised decision with actor, justification, source eligibility evidence, effective time, and recording time before claiming held funds were validly released. |
| External payout completion | Both | `Released` to `Paid` | External fact recorded by administrator | Free-form reference and server `PaidAt` | No authoritative transfer occurrence time or verified transfer identity | Yes | D/E | Current action asserts an externally completed payment. It does not verify provider, beneficiary, amount, currency, occurrence time, or globally unique transfer identity. `PaidAt = Clock.Now` is recording time only. |

## Onyx Travel Temporal Matrix

| Input | Result affected | Mutable? | Historical evidence exists? | Effective timestamp available? | Post-cutoff impact | Class | Decision |
| --- | --- | ---: | ---: | ---: | ---: | --- | --- |
| Participation activation | Qualification input | One-way | `ActivatedAt` | Yes | Post-cutoff activation already excluded | A | Reuse commission cutoff semantics. |
| Recruiter placement | Level 3 qualification | Yes | Correction history | Yes for valid chains | No after the current correction | C | Reuses the authoritative cutoff network. |
| Existing entitlement immutable facts | Grant idempotency and activation | Waiting to Active | Immutable except supported activation | Stored eligible/wait/activation times | Yes | Existing row bypasses requalification | B | Eligibility vests at the first qualifying closed cutoff; later topology or Area changes do not cancel or reset it. Soft deletion remains unresolved administrative state, not cancellation evidence. |
| Supplied-cycle `EligibleAt` | Three-month wait start | No after grant | Entitlement stores the cycle cutoff | Yes | No for the supplied cycle | A | Synchronization now records the qualifying closed cycle's `PeriodEndUtc`, not processing time. |
| First-ever qualifying-cycle discovery | Correct vested eligibility time | Operationally mutable through missed runs | Existing entitlement only after discovery | No missed-cycle scan | Yes after outage or late rollout | D/E | The worker processes only the latest closed cycle. Earlier first qualification cannot be proven until Area and terms history plus missed-cycle reconciliation exist. |
| Travel terms | Level, wait, contribution | Deploy mutable | Current version and entitlement snapshot only after grant | No historical as-of selector | Yes | D/E | Introduce an immutable as-of capability that starts empty; select by first qualifying cycle start and retain the snapshot. Do not invent the existing version's Friday boundary. |
| Activation time after waiting | Benefit availability evidence | One-way | Stored waiting end | Yes | No | A | Delayed processing now records activation at the contractual `WaitingPeriodEndsAt`; processing audit time remains separate. |
| Entitlement soft deletion | Visibility and benefit availability | Yes | Soft-delete audit projection | Administrative deletion time only | Yes | D/E | Fail closed and reconcile; deletion is not confirmed cancellation or forfeiture evidence. |

The latest-cycle synchronizer now applies these rules to the supplied closed
cycle. It does not yet scan missed historical cycles, because Area state and
cycle-effective travel terms cannot be proven for those cutoffs; therefore the
system cannot yet guarantee discovery of the first qualifying cutoff after an
extended outage or late rollout.

## Reconstructable Inputs

### Already sufficient

- Cycle boundaries and timezone.
- Participation activation for both programmes.
- Stable participation identity and programme ownership.
- Existing calculated periods, commission rows, component amounts, rules version,
  and calculation time, subject to unresolved soft-deletion semantics.
- Database uniqueness and shared calculation locking.

### Conditionally reconstructable from existing evidence

- Recruiter placement, by replaying valid previous/new correction history as of
  the cutoff. Ambiguous or discontinuous chains fail closed.
- Existing monthly-obligation overdue state, only where policy coverage,
  expected-row completeness, obligation association, provider occurrence, and
  provider delivery completeness can all be proven.
- Future loan compliance, using effective date, requirement due dates, deadline,
  and immutable allocation decision times.
- Future commission and travel terms after authoritative Friday boundaries are
  registered in an immutable as-of capability.

### Current-state only or missing evidence

- Deterministic ordering for ambiguous/equal recruiter correction timestamps.
- Business termination/deactivation of active programme participation.
- Obligation periods before the first authoritative due-policy version and
  operational launch boundary.
- An authoritative payment-success occurrence field or provider contract and a
  delivery-completeness watermark.
- The decision time of historical loan repayment allocation.
- The calculation algorithm version used by older ledger rows.
- Authoritative release decisions for held funds and verified external payout
  occurrence evidence for `Paid`.
- Financial meaning of period, commission, obligation, and entitlement deletion.

## Regression Status

The following deterministic regressions define the temporal correction scope:

1. AQGreen placement changes after cutoff: corrected and covered by domain and
   application tests.
2. Onyx placement changes after cutoff: corrected and covered by domain and
   application tests.
3. AQGreen obligation changes from current to overdue after cutoff and the
   current calculator incorrectly holds the closed cycle.
4. AQGreen obligation is overdue at cutoff, is paid after cutoff, and the current
   calculator incorrectly removes the closed-cycle hold.
5. Onyx travel qualification changes after cutoff: corrected by sharing the
   cutoff-effective network; focused processor/domain coverage passes, while an
   application-level post-correction travel test remains desirable.

The obligation cases remain blocked by the provider and due-policy evidence in
the final section. The correction does not promote partial compliance logic into
an authoritative result.

## Approved Minimum Architecture

1. Keep current participation and compliance columns as current-state
   projections.
2. Build one cutoff-effective network projection for each programme from
   `ActivatedAt` and existing recruiter correction history.
3. Make both commission and Onyx travel qualification consume that same temporal
   network semantics.
4. Derive AQGreen compliance from immutable due/deadline/payment/allocation facts
   rather than current status enums.
5. Resolve commission and travel terms from a small immutable as-of capability by
   the Friday 00:00 Johannesburg cycle start; start empty and do not create a
   generic rules engine or invent the current versions' boundaries.
6. Fail closed when required history is absent, ambiguous, deleted, or cannot be
   proven. Route those periods to manual financial reconciliation.
7. Preserve transaction separation, the shared weekly calculation lock, and all
   unique constraints.
8. Add only the minimum persistence needed to disambiguate future evidence. Do
   not backfill legacy timestamps or order with invented certainty.
9. Add append-only Area activation-state history from an explicitly observed
   baseline. Target-Area state governs ledger eligibility only; an active
   participation in an inactive source Area remains a cross-Area network node.
10. Keep provider occurrence, receipt, processing, and business allocation times
    distinct. Monthly payment effectiveness uses verified provider occurrence;
    loan cure uses immutable allocation decision time.
11. Resolve every material readiness dimension before inserting a period. The
    worker and administrator path must create no period, commission, or component
    when Area state, terms, compliance, provider finality, or required history is
    unresolved.
12. Represent reconciliation as an authorised, auditable workflow before it can
    change financial state. A display label or log entry is not durable evidence.
13. Preserve release-decision and external-payout evidence as append-only facts;
    do not treat server recording time or a free-form reference as external
    occurrence proof.

### Implemented forward-only Area slice

- `AreaActivationStateRecord` preserves state, effective/recorded times, actor,
  justification, and whether the fact was provisioned, observed, or changed.
- PostgreSQL and SQL Server serialize observation/change per Area. Supported
  production providers assign provisioning and mutation times from the database;
  mutation time is read after lock acquisition, avoiding cross-instance
  application-clock ordering.
- New Area provisioning records initial state in the host transaction. Existing
  Areas remain unknown until the explicit administrator baseline action or a
  later activation change records prospective evidence.
- Legacy tenant profile updates cannot mutate activation; they must use the
  audited activation action. Legacy Area deletion is rejected because deletion
  has no authorised financial meaning; operators must deactivate instead.
- PostgreSQL and EF reject update/delete, and PostgreSQL also rejects truncate;
  `(TenantId, EffectiveAt)` is unique; rollback refuses to discard evidence.
- The calculator returns existing ledgers idempotently, but rejects every new
  ledger before period insertion when target-Area state is inactive or unknown.
- The worker no longer filters by current `Tenant.IsActive`; it resolves all
  target Areas at the closed-cycle cutoff and does not process inactive or
  unknown targets. Source-Area state remains outside network qualification.

## Alternatives Rejected Before Implementation

| Alternative | Reason rejected |
| --- | --- |
| Event sourcing every aggregate | Existing transition facts already cover most inputs; disproportionate change. |
| Giant weekly state snapshot | A snapshot produced after cutoff can already contain Friday mutations and does not solve event-time finality. |
| Database temporal tables for every entity | Broad infrastructure change is not justified by the actual input gaps. |
| Use transaction isolation alone | A stable Friday database snapshot is still not the Thursday business-effective state. |
| Share all mutation locks with calculation | Prevents some races but cannot reconstruct mutations committed before delayed calculation. |
| Use current state and document the delay | Violates the closed-cycle financial invariant. |
| Automatically recalculate old periods | Existing evidence is incomplete and ledgers are intentionally idempotent/immutable. |

## Remaining Required Evidence

The temporal rules are confirmed where stated. The following operational values,
business decisions, or external evidence are still required before affected
workers can be enabled:

1. AQGreen's initial due-policy version, due day, effective instant, and
   operational launch month. No historical debt may be invented before that
   boundary.
2. Official or real provider-contract evidence for an authoritative
   payment-success occurrence time. Current evidence does not establish
   `payload.createdDate` for that purpose. A reconciliation mechanism must also
   establish delivery completeness around a cutoff.
3. Authorised reconciliation of legacy loan allocations; `AllocatedAt` must not
   be copied from `ReceivedAt` without evidence.
4. The authoritative Friday start for each existing commission and travel terms
   version. The current `2026-07-01 00:00 UTC` value is not a Friday 00:00
   Johannesburg boundary.
5. An authorised observed Area-state rollout baseline for every existing Area.
   The capability and operator action are implemented, but no baseline is seeded;
   cutoffs before an Area's first row remain unknown and require reconciliation.
6. An inventory confirming that all programme Areas share the host database, or
   a separate central programme-network projection. A database-per-Tenant Area
   cannot participate in the current cross-Area query.
7. A durable missed-cycle detection/reconciliation workflow for monthly
   obligations and travel qualification. The current workers process only the
   latest applicable month/cycle and must not invent historical state.
8. An obligation-specific recurring checkout and member-facing payment UI that
   persist authoritative obligation identity before provider checkout. Unlinked
   confirmed payments must never be auto-assigned to another month.
9. An authorised refund/dispute/chargeback policy covering participation,
   qualification, obligations, loans, release, and already-paid ledgers.
10. An authorised held-release workflow and authoritative external payout
    evidence contract, including occurrence time, amount, currency, beneficiary,
    provider, and unique transfer identity.

Production code may implement independently proven forward-only temporal
semantics, but must fail closed where this evidence is absent. Weekly and monthly
automation remain disabled until the complete workflow is verified.
