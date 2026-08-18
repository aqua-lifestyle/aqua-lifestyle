# Weekly Commission Production Readiness

Assessment baseline: `b88aeb3` plus the focused safeguards on
`feat/weekly-commission-production-readiness`.

This document answers what prevents a controlled production calculation test.
It does not authorise deployment, worker enablement, commission release, payout,
or production mutation.

## Executive verdict

`IMPLEMENTATION`: the first authorised 14-20 August 2026 calculation path is
substantially ready after the terms-boundary correction and real PostgreSQL
retry test.

`ENABLEMENT`: blocked. The target cycle is still open until 21 August 2026
00:00 Johannesburg, and production recovery, terms, activation evidence,
topology, migration/build, observability, and preflight output are not verified
in this repository.

`CONTINUOUS OPERATION`: blocked. The worker processes only the latest closed
cycle, missed older cycles have no approved reconstruction workflow, and
monthly-obligation completeness plus Yoco payment-occurrence semantics must be
resolved before monthly holds can affect a weekly cycle.

Both worker defaults remain false:

```text
App__WeeklyCommissions__Enabled=false/unset
App__EntryMonthlyObligations__Enabled=false/unset
```

## Purpose and boundary

The engine records a deterministic weekly ledger. It does not release a
commission or execute a transfer. Its hard graph boundary is
`TenantId + Programme`. Business Areas inside one Tenant do not partition an
AQGreen or Onyx network. A cross-Tenant relationship fails closed.

The lifecycle is:

```text
authoritative payment confirmation
-> Area Admin approval
-> Active participation and ActivatedAt
-> cutoff-effective qualification
-> cycle-opening terms
-> weekly ledger
-> separately authorised release/payment/reconciliation
```

Payment-confirmed but unapproved participation is not Active, has no
`ActivatedAt`, is excluded from the application query, and cannot receive or
contribute to weekly qualification.

## Qualification and rates

AQGreen has exactly three structural levels and remains Level 3 above 125:

| Level | Complete population | Rate/person | Increment | Cumulative |
| --- | ---: | ---: | ---: | ---: |
| L1 | 5 | R30 | R150 | R150 |
| L2 | 25 | R10 | R250 | R400 |
| L3 | 125 | R10 | R1,250 | R1,650 |

Onyx remains Levels 1-5. Incomplete levels receive no partial component. The
effective network deterministically chooses five children per recruiter and
fails on mixed Tenant data, dangling recruiters, cycles, ambiguous correction
timestamps, discontinuous correction history, deleted participation evidence,
or missing activation evidence.

## Cycle and startup semantics

The canonical cycle is Friday 00:00 through the final tick before the next
Friday in `Africa/Johannesburg`.

```text
First authorised cycle: 14-20 August 2026
Earliest safe boundary: 21 August 2026 00:00 Johannesburg
UTC boundary:           20 August 2026 22:00 UTC
```

`LatestClosedCommissionWeekResolver` always selects the latest fully closed
cycle. Before the safe boundary, it selects the preceding 7-13 August cycle; it
cannot select the open 14-20 August cycle.

The API-hosted ABP timer has `RunOnStart=true`. ABP 9.4.1 invokes it immediately
when the host starts, prevents overlapping callbacks in one process, and starts
the next interval only after the current callback completes. The configured
default interval is 1,440 minutes. An unhandled whole-run failure is swallowed
by the timer after logging and is retried on the next completion-relative wake.
Tenant/programme failures are caught independently, allowing later work to
continue.

Consequences:

- enabling before 21 August cannot calculate the authorised open cycle, but
  preflight blocks that action;
- enabling from 21 August until the next cycle closes targets 14-20 August;
- enabling after another cycle closes targets only that newer cycle and can
  skip 14-20 August;
- startup preflight reports `startup_would_target_different_cycle` in that case;
- restarting safely retries the latest cycle; it does not backfill all gaps.

## Terms resolution

Terms are immutable and selected by the cycle's opening Friday. A terms row
effective on 21 August governs the 21-27 August cycle and can never rewrite the
14-20 August cycle.

The earlier implementation selected at the next Friday after `PeriodEnd`. That
was inconsistent with the authorised opening-boundary rule and could make a
delayed previous-cycle calculation fail after new terms were registered. The
resolver and regression tests now use `PeriodStartUtc` for AQGreen and Onyx.

Members do not retain personal weekly commission rates from joining/payment.
Joining terms govern programme admission obligations; the immutable
cycle-opening commission terms govern that week's ledger for all qualifying
members.

## Payment and approval timestamps

The weekly network uses `ActivatedAt`, set only by Area Admin approval. Joining
payment time alone does not qualify a member.

For AQGreen monthly holds, `EntryMonthlyObligation.WasOverdueAt` compares the
immutable grace boundary and `PaidAt`. `PaidAt` is copied from
`MemberPayment.ConfirmedAt`. The Yoco webhook currently maps Yoco
`payload.createdDate` to `ConfirmedAt`.

`CONFIRMED BLOCKER FOR FUTURE MONTHLY HOLDS`: repository evidence does not prove
that `createdDate` is Yoco's authoritative successful-payment occurrence time.
A payment object created before cutoff and completed after cutoff could be put
on the wrong side of the weekly hold boundary.

`NOT APPLICABLE TO THE FIRST TARGET CYCLE`: monthly automation is authorised to
start no earlier than September 2026 and must remain disabled. Preflight blocks
the first cycle if any obligation at or before August exists, because that would
contradict the expected clean boundary and require reconciliation.

Do not leave weekly automation armed into a cycle where monthly standing can
matter until Yoco occurrence semantics and expected-obligation completeness are
resolved.

## Persistence, idempotency, and concurrency

Each Tenant/programme calculation runs in its own transaction. It acquires the
same PostgreSQL `pg_advisory_xact_lock` used by the administrator path. The lock
is released on commit, rollback, connection loss, or process failure.

The database independently enforces:

- one period per `(TenantId, PeriodStart, PeriodEnd)` per programme;
- one ledger row per `(ParticipationId, CommissionPeriodId)`;
- one component per `(CommissionId, Level)`.

The application first checks for an existing period. Replay returns the same
period, reports `WasAlreadyCalculated=true`, and creates zero rows.

Real PostgreSQL application-path evidence now performs:

```text
transaction + advisory lock
-> stage period and six AQGreen L1 ledgers
-> injected PostgreSQL trigger failure
-> rollback proves 0 periods / 0 ledgers / 0 components
-> remove injected fault
-> retry commits 1 period / 6 ledgers / 1 component
-> second successful execution returns existing period
-> counts remain 1 / 6 / 1 and total remains R150
```

This complements, rather than replaces, existing real-PostgreSQL advisory-lock
serialization and uniqueness tests.

If a connection fails before commit, PostgreSQL rolls back. A failure while the
client awaits commit can leave commit outcome unknown to the client; retry is
safe because persisted period identity and unique indexes are authoritative.

An existing complete period is safely ignored. An imported, manually corrupted,
or partial historical period is not automatically repaired; inventory and
authorised reconciliation are required.

## Observability

The worker emits structured, non-PII events:

| Alert type | Meaning |
| --- | --- |
| `weekly_commission_programme_completed` | Tenant/programme completed or was already processed; includes cycle, rules version, evaluated/created/NotEarned/positive/Held counts. |
| `weekly_commission_calculation_failed` | One Tenant/programme failed; includes cycle and exception. |
| `weekly_commission_area_state_unknown` | Legacy Tenant activation evidence was unavailable at cutoff. |
| `onyx_travel_benefit_synchronization_failed` | Independent travel synchronization failed. |
| `weekly_commission_calculation_run` | Whole wake summary including attempts, retries, outcomes, Tenant states, travel outcomes, and duration. |
| `weekly_commission_calculation_run_failed` | The run failed outside an individual Tenant/programme boundary. |

The labels `inactiveTenants` and `unknownTenants` are operationally accurate;
the persisted `AreaActivationStateRecord` name is legacy terminology keyed by
Tenant. Logs contain Tenant IDs and aggregate counts, not customer names,
emails, payment references, credentials, tokens, or webhook bodies.

The worker test `FailedProgramme_EmitsCalculationFailedEvent_AndSummaryStillReports`
asserts that a per-programme failure emits `weekly_commission_calculation_failed`
at error level with the Tenant and programme, that the wake summary
`weekly_commission_calculation_run` still reports the failure count, and that
no password/token material appears in the messages.

`OPERATIONAL BLOCKER`: repository logging is not proof of an owned production
alert destination. `App__WeeklyCommissions__ObservabilityReady=true` is a
manual evidence assertion used by preflight only. Set it only after the log
destination, alert rules, recipient, and test notification are verified.

## Deterministic preflight

The host-only, permission-protected preflight is read-only with respect to
business/ledger data and returns explicit blocker codes. `Ready` is true only
when `Blockers` is empty.

It checks:

- worker still disabled;
- monthly worker disabled;
- target cycle closed;
- `RunOnStart` would select exactly 14-20 August;
- exact AQGreen and Onyx versions, boundaries, currencies, and all rates;
- cutoff-applicable Tenant activation evidence;
- all configured Tenants use the host database;
- no target period already exists;
- no deleted network evidence;
- no unexpected pre-September monthly obligation evidence;
- recovery and observability evidence flags;
- build identifier for evidence capture.

It also returns the target/latest cycles, earliest safe time, active
participation/hold projection, topology detail, existing period counts, and
payment-timestamp applicability.

The projection additionally runs a network-buildability dry run for every
ready Tenant exactly as the worker would: it builds the AQGreen and Onyx
graphs with the same participation loads (`RecruiterCorrections` included) and
counts qualified participants per Tenant. Two further blocker codes close the
pre-enablement detection gap that the worker's own fail-closed behaviour would
only surface after enablement:

- `missing_activation_evidence` — active participation without a recorded
  activation time in the ready population;
- `network_not_buildable` — a graph that fails to build (missing activation
  evidence or a dangling placement) with the failure reason in the detail.

Post-cutoff activations (activated after the closed cycle's end) are reported
as informational counts (`EntryPostCutoffActivationExcluded` /
`OnyxPostCutoffActivationExcluded`), never as blockers: the calculator
deterministically excludes them from the closed cycle by cutoff semantics.

The preflight runs host-side with an empty session Tenant, so its projection
and area queries disable the ABP `MayHaveTenant`/`MustHaveTenant` filters
explicitly; without this the host session would hide every Tenant row and the
projection would appear empty in production. Regression tests cover the
dry-run graph, the activation-evidence blocker, and the post-cutoff
informational path.

Preflight does not replace these independent checks:

- `/api/health` database/Redis/key-store readiness;
- protected operations diagnostics for build, provider, migration head, and
  pending migrations;
- local/CI PostgreSQL advisory-lock and application-path test evidence;
- Render instance count/configuration inspection;
- production backup restore evidence;
- owned external alert delivery.

Multiple API instances are supported by the transaction advisory lock. The
exact count is deployment evidence, not a calculation input. Any Tenant with a
separate connection string blocks the current host worker because cross-database
enumeration has not been proven.

Two manual evidence settings default false and do not enable a worker:

```text
App__WeeklyCommissions__RecoveryVerified=false/unset
App__WeeklyCommissions__ObservabilityReady=false/unset
```

They must not be set merely to make preflight green. Retain the underlying
backup/restore and alert-delivery evidence.

## Missed-cycle handling

Current behavior is **A + C + D** from the investigated scenarios:

- A: the worker automatically processes the latest closed cycle only;
- C: older missing cycles require explicit reconciliation;
- D: enabling after a newer cycle closes can skip a valid older cycle.

It does not process all missed cycles and cannot accidentally create the same
cycle twice through normal execution. Current historical inputs are not complete
enough to calculate arbitrary old cycles from today's state.

Smallest safe MVP policy:

1. Preflight blocks if startup would not target the authorised first cycle.
2. Keep automatic historical backfill prohibited.
3. Preserve the gap, source-state, build, migration and provider evidence.
4. Finance/Ops and the business owner classify whether the gap requires no
   commission, manual financial reconciliation, or future authorised tooling.
5. Never delete a period or write manual ledger SQL to force a retry.

A general historical recalculation framework is not required for the controlled
first-cycle MVP and would be unsafe without complete cutoff evidence.

The production-like PostgreSQL E2E proves the no-backfill boundary
end-to-end: it seeds terms and activation evidence for a missed older closed
cycle and asserts the real worker run leaves that cycle with zero periods and
zero ledger rows while producing exactly one period for the latest closed
cycle. The admin `CalculateLatestClosedWeekAsync` path is equally
latest-only; there is no backfill entry point anywhere in the system.

## Blocker classification

| Concern | Classification | Current conclusion |
| --- | --- | --- |
| Terms boundary | Resolved engineering blocker | Opening-Friday selection and regression added. |
| Monthly-obligation completeness | Confirmed future-cycle blocker | Missing/deleted expected obligations can look compliant. Not applicable only to the clean first cycle; resolve before monthly standing can matter. |
| Yoco payment timestamp | Confirmed future-cycle/provider blocker | `createdDate` authority is unproven. First cycle blocks if unexpected monthly rows exist. |
| Production topology/preflight | Engineering implemented; production evidence blocked | Fail-closed blocker list and host topology check added. Production output remains unverified. |
| Missed-cycle recovery | Operational blocker | Latest-only is explicit; no automatic backfill. First-cycle preflight prevents silent skip. General recovery requires business authority. |
| Monthly due policy | Deferred business decision | Monthly worker remains disabled; no due day may be invented. |
| Payout/reconciliation controls | Not required for calculation MVP; required before payout/correction | Calculation sends no money. Release/payment stay manual and provider payout evidence remains limited. |
| PostgreSQL application-path retry | Resolved evidence gap for AQGreen L1 | Injected failure rollback plus two successful executions proven on PostgreSQL. |
| Production-like E2E | Resolved evidence gap | Real worker path on PostgreSQL proves exact L1 amount, latest-closed-only selection, no backfill of missed cycles, and idempotent replay. |
| Worker observability | Code safeguard resolved; operations blocked | Structured per-programme/summary/failure events asserted by tests; external alert ownership remains unverified. |
| Archived WIP | Reviewed; not adopted wholesale | Useful blocker/anomaly concepts only; implementation predates Area separation and AQGreen L3 correction. |

## Test coverage and remaining evidence

Existing layered evidence covers AQGreen L1-L3 and above-L3 cap, Onyx L1-L5,
inactive/unqualified domain behavior, payment-before-approval, activated members,
Tenant isolation, same-Tenant cross-Area AQGreen structure/progress, canonical
cycle resolution, PostgreSQL lock/uniqueness, application replay, worker failure
isolation, and disabled monthly automation.

New focused evidence covers:

- the opening terms boundary and immediate-next-Friday non-rewrite;
- exact-rate preflight and fail-closed operational gates;
- structured worker programme/summary logging plus the per-programme failure
  event and its non-PII assertion;
- real PostgreSQL application-path rollback and retry idempotency;
- the production-like PostgreSQL E2E: real worker path (Tenant enumeration,
  area activation resolution, advisory lock, calculator, per-programme
  transactions) over a qualified Level 1 AQGreen network (root + five
  confirmed-and-approved recruits) producing exactly R150 for the root in the
  latest closed cycle, idempotent replay without duplicate ledger rows, and a
  seeded older missed cycle that is never backfilled (Entry and Onyx);
- the preflight network dry-run tests: activation-evidence blocker, Level 1
  qualification counting, and post-cutoff exclusion as an informational
  report;
- the fresh-database contract: both PostgreSQL regression classes share a
  sequential xUnit collection (same immutable terms version slot on one CI
  database), self-clean all mutable ledger/participation data, and fail loudly
  on a reused database via their terms guards (append-only trigger), while CI
  always provisions a fresh database per job.

The E2E and application-path tests are wired into the `postgres-transactional-regression`
CI job with per-test result greps and provenance markers
(`weekly-commission-application-path-pg.ran`, `weekly-commission-e2e-pg.ran`)
so a silent short-circuit to the default SQLite harness fails the job.

`REQUIRED BEFORE CONTROLLED PRODUCTION TEST`:

- production health and migration/build diagnostics;
- production preflight with no blockers after 21 August and before a later cycle
  becomes latest;
- exact production terms and Tenant activation evidence;
- current restored-backup evidence;
- owned alert route tested end to end;
- read-only topology/customer-participation review;
- operator/Finance approval and abort owner.

`REQUIRED BEFORE LEAVING THE WORKER ENABLED`:

- monthly-obligation completeness when expected months exist;
- authoritative Yoco success-occurrence semantics;
- approved missed-cycle response;
- production-like weekly E2E including post-cutoff activation/placement, holds,
  unknown/inactive Tenant state and retry; the E2E, post-cutoff preflight test,
  and worker failure-boundary tests now cover the production-like journey,
  post-cutoff exclusion, retry, and per-Tenant/programme isolation; holds and
  unknown/inactive Tenant behaviour remain to be observed against production
  state during the controlled window;
- monitoring observed across a complete cycle.

`NOT REQUIRED FOR FIRST-CYCLE CALCULATION MVP`:

- automatic arbitrary historical backfill;
- automated payout provider integration;
- server-local Markdown diagnostics from the archived WIP;
- Area-fragmented programme graphs;
- enabling monthly obligations;
- database-provider migration.

## Safe enablement procedure for a later approved window

Do not perform these steps as part of readiness implementation.

1. Confirm the time is at or after 21 August 00:00 Johannesburg and before the
   next cycle closes.
2. Confirm Weekly and Monthly workers are both false.
3. Verify Render recovery evidence and set only the recovery preflight assertion.
4. Verify the external alert route and set only the observability assertion.
5. Verify health and protected operations diagnostics: expected build/image,
   PostgreSQL, latest migration, no pending migration.
6. Run host weekly preflight and retain its non-PII output. Stop unless
   `Ready=true` with an empty blocker list.
7. Obtain the separately authorised configuration-change approval.
8. Change only `App__WeeklyCommissions__Enabled=true`; keep monthly false.
9. Restart/redeploy one controlled API release. `RunOnStart` should calculate
   the target immediately.
10. Verify exactly one period per Tenant/programme, expected rules versions,
    evaluated/created/status counts and no failure alert.
11. Trigger or observe an idempotent repeat and verify no additional ledger rows.
12. Keep release/payment manual. Disable Weekly immediately after the controlled
    test unless continuous-operation blockers have separately been cleared.

Abort and disable on any unexpected cycle, unknown Tenant state, terms mismatch,
deleted evidence, duplicate/partial period, lock behavior difference, unexplained
total, reconciliation row, or alert. Do not delete ledger rows and rerun.

## Rollback and recovery

Disabling the worker prevents future wakes but does not undo a committed ledger.
Application rollback must not delete or mutate calculated periods. If a run
fails before commit, retry after correcting the cause. If commit outcome is
uncertain, inspect the unique period and complete ledger before retrying.

If a committed result is disputed, stop release/payment, preserve evidence,
inventory read-only, and use an authorised auditable reconciliation decision.
Database restoration is an environment-level recovery action and requires
payment/approval/ledger reconciliation for writes after the restore point.
