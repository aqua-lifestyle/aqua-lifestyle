# AQGreen V2 isolated lean MVP

> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

## Goal

Reuse qualified code and prove authorized root → invitation → first-time joining
→ verified test payment → pending approval → atomic approval/placement → progress
→ weekly sales review → versioned calculation → unreleased test ledger, using
disposable infrastructure. No loans, Onyx graduation, real money, production,
B6/firewall work, B7, deployment, or Git publication.

## Authoritative references

- Current owner's isolated MVP instruction and repository AGENTS.md.
- Aqua system index; V2 specification sections 9, weekly sales eligibility and
  decision register; payment/approval document 04; Tenant/Area document 08.
- Repository validation and execution-plan instructions.

## Confirmed decisions

Existing V2 authority separates recruitment from placement and structural
qualification from weekly earnings. Invitation acceptance can confirm attribution;
payment does not activate; authorized approval and placement commit atomically.
The owner preserves independent 5/5/5 weekly thresholds and financial holds.
Production V1 authority and disabled V2 cutover remain intact.

## Assumptions

Three riskiest hypotheses, to challenge before expansion:

1. **Member value:** members and independent buyers value the goods/services at
   their actual prices without an earnings/recruitment promise. Test recent
   purchases, alternatives, repeat use and non-member demand through interviews.
2. **Operation:** first-time members and Area administrators can independently
   complete the journey and distinguish pending approval, placement, sales and
   unreleased earnings. Automated execution tests mechanics; observed tasks must
   test usability, comprehension, return-later behaviour and operator workload.
3. **Economics:** net commercial margins can cover fulfilment, returns, operating
   costs and all compensation without dependence on joining/recruitment fees.
   Reconcile independently verified customer orders and cost evidence. 5/5/5 and
   synthetic calculations do not establish demand, profitability or legality.

## Current state

Inspection baseline: branch `feature/admin-ui-remediation`, HEAD
`e9bd8e96d55d03466492ffd40dd657802ae36d4b`. No tracked changes at entry.
Untracked foundational-research worklog and `docs/research/` belong to prior work
and are preserved. Existing continuous E2E directly seeds attribution and includes
excluded loan/graduation work; it cannot prove first-time invitation acceptance.
At entry, `StartEntryAsync` resolved invitations but wrote no V2
attribution/confirmation; the local change below closes that gap.

IMPLEMENTED locally: default-disabled prospective V2 invitation join selection;
transactional participation + immutable attribution + member confirmation;
invitation/sponsor/scope checks; exact retry validation; dedicated demo selection
only behind the existing explicit non-production, loopback/database gate. The
test-only fresh six-person backend slice reaches verified-against-checkout
synthetic payment, pending approval, placement, progress, 5/5/5 review,
unreleased ledger, replay and an independent financial hold. This does not
represent a live provider or a self-directed browser completion.

## Evidence

REPOSITORY EVIDENCE: approval fails closed without immutable attribution and
confirmation. Existing constraints bind participant, sponsor and invitation by
Tenant and enforce unique attribution/confirmation. Docker and .NET 8 are available.
Execution results below are from the retained local logs and disposable
PostgreSQL records; they are separate from customer-learning evidence.

- EXPERIMENT RESULT: Release restore and relevant test-project Release build
  passed after source/test edits. The initial full-solution build passed before
  the final explicit UOW annotation; the current test-project build recompiles
  affected projects. The source has no new dependency or migration.
- EXPERIMENT RESULT: fresh PostgreSQL 16.15 final slice, 1 passed/0 failed/0
  skipped, then rerun with direct application-service joining and no harness
  outer transaction, 1 passed/0 failed/0 skipped. After tightening the
  wrong-amount rejection assertion, a further fresh PostgreSQL run passed
  1/1, retained as `lean-mvp-assertion-final.trx`. Logs/TRX:
  `/tmp/aqgreen-lean-mvp.rNbveh/lean-mvp-final.*` and
  `/tmp/aqgreen-lean-mvp.rNbveh/lean-mvp-direct.*`.
- EXPERIMENT RESULT: missing historical acceptance and missing sponsor scope
  negative paths, 1 passed/0 failed/0 skipped on separate disposable
  PostgreSQL, `/tmp/aqgreen-lean-mvp.rNbveh/lean-mvp-negatives.*`.
- Read-only PostgreSQL cross-check after the first fresh slice: six
  participations, six attributions, six confirmations, six placements,
  zero Onyx, zero loans, zero released/paid ledgers. The snapshot also records
  Level 1/R150 on 5/5/5, a separate 5/5/4/R0 control, and an R150 `Held`
  ledger for a synthetic overdue monthly obligation. There are 18 ledgers
  total across three test weeks and six members.
- The first three focused attempts FAILED before the final pass: no test-harness
  UOW for invitation read; then missing required synthetic user password hash;
  then missing FK-backed due-policy fixture. These were test setup defects,
  corrected without weakening business assertions. All failures remain logged.
- The broad Web test project passed 93, skipped the two explicit PostgreSQL-only
  MVP cases, failed 0. The focused PostgreSQL cases ran separately. The broad
  Application project was cancelled after more than 20 minutes in unrelated
  PostgreSQL migration fixtures, without a final result. The focused V1 joining,
  attribution and demo-selector compatibility run on final source/test build
  passed 53/53 with zero failures or skips, retained at
  `/tmp/aqgreen-lean-mvp.rNbveh/lean-compatibility.*`.
- `dotnet ef --version` is 10.0.9 versus EF Core 8.0.8; no model comparison is
  claimed from this incompatible CLI. No schema/model file changed. Existing
  tests migrate disposable PostgreSQL databases.
- NuGet advisory audit reports known direct/transitive findings including
  AngleSharp and core transitive packages. No dependency changed in this task;
  no clean dependency-audit claim is made.

Diagnose-bug labels: **OBSERVATION** — first-time `StartEntryAsync` followed
invitation resolution but produced no V2 attribution/confirmation.
**HYPOTHESIS** — later approval could not consume immutable acceptance evidence.
**EXPERIMENT RESULT** — selected fresh-network PostgreSQL join now persisted
exactly one matching attribution/confirmation per child; missing-history test
still fails closed. **ROOT CAUSE (PRE-EXISTING)** — the prospective join path
never bridged invitation acceptance to the V2 evidence model. No branch-owned
V1 or B6 policy was used as substitute authority.

## Open questions

No customer interview or human observation has occurred in this task. Actual
goods/services demand, source of commission funding, sustainable 5/5/5 economics
and South African consumer-law assessment remain unknown. Terminal sponsor policy
D14B and company-assisted policy remain unresolved; exclude those channels.

## Completed work

Loaded applicable authority/workflow/stateful/diagnosis/validation skills and
inspected current implementation, tests, branch, status, authority and provenance.
Prepared discovery and observed-task scripts, implemented the gap, ran the
working slice, concurrency/retry, missing-evidence negatives, read-only
durability cross-check and dependency audit. No actual user interviews or
human observations were performed.

## Next action

The local backend slice, focused validation, report and handoff are complete.
Future human pilot requires a separate, visibly fake browser checkout and real
consented observation; commercial activity remains gated by actual goods/services
economics and counsel.

## Git/branch context

Work remains local and unstaged. No commit, push, PR, merge or deployment authorized.
