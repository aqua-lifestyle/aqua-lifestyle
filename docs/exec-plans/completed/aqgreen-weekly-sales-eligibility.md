# AQGreen weekly sales eligibility foundation

> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

## Goal

Implement the dormant B5.3 AQGreen weekly-sales eligibility foundation without
implementing B5.4 consumption, enabling D10, selecting Placement V2, changing V1
commission behavior, or introducing a production worker.

## Authoritative references

- Current B5.3 implementation brief supplied for this work.
- `docs/aqua-system/aqgreen-network-placement-specification.md`, as corrected by
  the current brief where older review outcome language conflated review status
  and threshold result.
- Root `AGENTS.md` and `docs/development/validation.md` engineering constraints.

## Confirmed decisions

- Rules version: `AQGreenWeeklySalesEligibilityV1`.
- Threshold: each of Spray, OneLitre, and FiveLitre must independently be at least
  five; there is no substitution or carry-forward.
- Canonical week: Friday 00:00 Africa/Johannesburg inclusive through the next
  Friday exclusive.
- `Confirmed` stores authoritative quantities and may evaluate to `Met` or
  `NotMet`; `Rejected` stores no quantities and no threshold result.
- Natural identity is Tenant, AQGreen participation, week start, and rules
  version. It is not attached to commission-ledger creation.
- Manual evidence is an opaque, normalized technical reference only.
- The dedicated production review-write gate is disabled by default.
- Current scope fails closed to a host/SystemAdmin test path requiring the
  dedicated permission and `Aqua.Admin.AllTenants`; no Area policy is guessed.
- B5.4 receives only a narrow internal finalized-decision reader and is not
  implemented or enabled here.

## Assumptions

- PostgreSQL 16 remains the authoritative production provider for stateful
  integrity and transaction-scoped advisory locking.
- Future verified commerce can supply the same aggregate quantity value object
  through a separately authorised adapter; no commerce source is selected now.

## Current state

- Implementation: `COMPLETE`.
- Validation: `COMPLETE`.
- Review: `COMPLETE`.
- Final independent acceptance: `ACCEPT`.
- Recommendation: `READY TO COMMIT`.
- Ready for commit and PR.
- Production/default review writes remain disabled.
- No production enablement.
- First independent review: `REVISE BEFORE COMMIT`.
- Correction pass: `COMPLETE`.
- First seven substantive findings: `FIXED` and independently re-reviewed.
- Pre-acceptance closure review: `REVISE BEFORE COMMIT` for
  evidence/documentation closure only; no implementation defect was identified.
- EF Core CLI: `8.0.8`.
- Model drift: `PASS`.
- Result: `No changes have been made to the model since the last migration.`
- Evidence:
  `AqualLifeStyle/9.4.2/aspnet-core/artifacts/b53-validation/13-ef8-model-drift.log`.

## Evidence

- The interrupted worktree contained no retained TestResults, TRX, validation
  logs, or EF-tool artifact that could independently substantiate its earlier
  terminal-only pass reports. Those historical reports are superseded and are
  not credited for the correction pass.
- Credited validation:

  | Validation | Result | Failed | Skipped | Configuration |
  | --- | --- | ---: | ---: | --- |
  | B5.3 | 31/31 `PASS` | 0 | 0 | - |
  | B4 | 36/36 `PASS` | 0 | 0 | - |
  | B5.2 | 61/61 `PASS` | 0 | 0 | - |
  | V1 | 42/42 `PASS` | 0 | 0 | - |
  | Full Application | 1194/1194 `PASS` | 0 | 0 | Debug |
  | Full Web | 84/84 `PASS` | 0 | 0 | Release |

- Release build: `PASS`; configuration: Release; exit: 0; errors: 0; warnings: 1.
  The warning is the pre-existing NU1902 AngleSharp advisory.
- EF Core 8 model drift validation: `PASS` with CLI 8.0.8 and exit 0. The retained
  ignored evidence records the timestamp, working directory, exact commands, and
  actual command output.
- Earlier implementation review found and fixed branch-owned defects before the
  first independent review: PostgreSQL-only checks in SQLite model creation; a
  sealed ABP app service that prevented authorization/UoW proxy construction;
  reader exception normalization for corrupt negative quantities; incomplete
  tracked-mutation protection; a stale Serializable snapshot after advisory-lock
  waiting; and EF classifying client-assigned evidence GUIDs as existing rows.
- The first independent review required audit-parameter redaction, stronger
  PostgreSQL state and trigger protections, deterministic lock-wait evidence,
  isolated authorization tests, terminology correction, and truthful active-plan
  status. The first seven substantive findings were fixed and independently
  re-reviewed.

## Open questions

- Detailed sales evidence and retention/deletion policy: `UNRESOLVED`.
- Area review authority: `UNRESOLVED`.
- D10: `UNRESOLVED`.
- B5.4: not started.
- Verified Commerce: future.

## Completed work

- Added versioned evaluator and explicit review/threshold state model.
- Added held-to-final lifecycle and minimal append-only evidence provenance.
- Added dedicated permission, disabled gate, host-only scope seam, DTO boundary,
  idempotent/conflict-aware application orchestration, and advisory lock.
- Added composite Tenant-coherent keys, state checks, deferred evidence check,
  mutation/truncate triggers, guarded Down, and fail-closed internal reader.
- Corrected the system specification's older confirmed/rejected conflation.

## Next action

Commit the accepted B5.3 scope, push the feature branch, and open a PR. Await
required CI and do not merge without authorization.

- No commit.
- No PR.
- No deployment.
- No enablement.
- Production review writes and financial consumption remain disabled.
- Validation procedure reference: `docs/development/validation.md`.

## Git/branch context

- Worktree: `/home/wtc/Downloads/newAqua/aqua-lifestyle-aqgreen-v2-weekly-sales`
- Branch: `feat/aqgreen-v2-weekly-sales-eligibility`
- Baseline: `origin/main` at `657145bb97b938f513d804f21303355301536336`.
