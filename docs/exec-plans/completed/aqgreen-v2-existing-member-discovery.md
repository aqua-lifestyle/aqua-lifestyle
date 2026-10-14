# AQGreen V2 existing-member discovery

> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

## Goal and authority

Owner instruction, 13 October 2026: complete read-only discovery of legacy AQGreen
assignments, including customers without modern participation, while preserving
Tenant/Area restrictions, pagination, uncertainty and the accepted repeat-joining
guard. Synthetic PostgreSQL validation only; no migration execution, production
operations, reconciliation writes, activation, deployment or Git publication.

References: [system index](../../aqua-system/README.md),
[historical integrity](../../aqua-system/05-data-history-migrations-and-legacy-members.md),
[Tenant/Area boundaries](../../aqua-system/08-area-and-tenant-boundaries.md),
and [previous progress](../completed/aqgreen-v2-production-engineering.md).
No new business decision is made here. D03B/D09/B6 and D10/B7 remain unauthorized.

## Implementation

- Extend existing `GetLegacyAQGreenReconciliationAsync`; retain its permission and
  authorized-Area resolver. Customer-first left joins match participation by both
  Tenant and Customer. Shared/own-Tenant AQGreen membership assignments are included;
  an unverifiable referenced assignment is an explicit uncertainty candidate.
- Include existing AQGreen participations independently of payment schedule.
  Discovery is an inventory for reconciliation, not a declaration of debt,
  membership validity or activation. Recorded V2 attribution/placement rules identify
  V2 evidence; the presence of an `EntryParticipation` alone does not establish V2.
  Unsupported markers and unversioned existing participation remain unverifiable;
  coherent recorded V2 facts are separately classified, without eligibility authority.
- Return one row per Tenant/customer, deterministic Tenant/Customer ordering and a
  stable `aqgreen:{TenantId}:{CustomerId}` discovery identity. Preserve existing
  participation IDs. Nullable participation amounts/IDs represent absent records;
  issue codes explain missing, conflicting and unverifiable evidence.
- Read page-owned payment/checkout/provenance facts with no tracking. Do not return
  details or identifiers from ownership-mismatched payment/checkout facts.
- Reuse PostgreSQL web-test startup, adding opt-in
  `REPRO_PG_SCHEMA_FROM_MODEL=true` for `EnsureCreated` on the disposable database.
  Existing test startup behavior is otherwise retained; no migrations executed.

## Git context and evidence

- Persistent worktree: `/home/wtc/Downloads/newAqua/aqua-lifestyle-v2-production-candidate`.
- Branch `feature/aqgreen-v2-main-integration`; base/current HEAD
  `e11edda8bd55f2d5717cd00a56bb613ae9a628d5`.
- Prior joining guard, its tests, document update and completed worklog are existing
  uncommitted work; preserved. No staging, commit, push, merge or deployment.
- Final Release solution build passed after the checkout follow-up (0 errors).
  One existing advisory remains for the unchanged test-project AngleSharp 1.1.2
  package; earlier builds also emitted existing compiler/analyzer warnings.
  Final build log: `build-final-reviewed.log`.
- Initial PostgreSQL run: 11 passed, 1 failed, 0 skipped. Host permission assertions
  denied access correctly, but the positive host assertion searched only the default
  page of a growing synthetic database. Fixture request now uses an explicit larger
  page; assertions/authorization are preserved. Read-only transaction and complete
  non-audit table snapshot checks passed in that run.
- `IMPLEMENTED` and `TESTED`: final focused PostgreSQL run passed **14/14**, no
  failed/skipped cases, on disposable PostgreSQL **16.15**. API tests exercise
  legacy-only and overlapping records, absent/unsupported version evidence,
  conflicting/missing/ownership-mismatched payment facts, unverifiable membership
  ownership, actual permission and Tenant/Area denials, revoked Area access,
  deterministic pagination, and coherent recorded V2 facts.
- Read-only evidence: successful application invocation in an explicit PostgreSQL
  READ ONLY transaction, no changed EF entries, plus byte-equivalent before/after
  snapshots of every public table except ordinary `AbpAuditLogs`, across direct and
  HTTP discovery. No `__EFMigrationsHistory` table exists in this model-created
  fixture. This proves query/read-only behavior, not migration or production safety.
- Existing SQLite reconciliation compatibility regression passed **1/1**. Accepted
  MVP/integration and joining-guard evidence is carried forward, not rerun.
- Final inspection added explicit issue codes for withheld checkout ownership and
  payment links. A focused PostgreSQL follow-up selects that new case and reruns
  the read-only assertion against the final code: **2/2 passed**, no failures/skips.
  Earlier 14 cases
  cover unchanged paths and are carried forward at the preceding source revision.
  Follow-up TRX: `postgresql-checkout-followup.trx`. This is 15 distinct PostgreSQL
  cases with passing evidence across the suite/follow-up, not a single 15-case run.
- Commands: final PostgreSQL web tests selected
  `FullyQualifiedName~AQGreenExistingMemberDiscoveryPostgreSqlTests` with Release,
  `--no-restore`, `REPRO_PG=true`, `REPRO_PG_SCHEMA_FROM_MODEL=true`, and the task's
  disposable loopback `REPRO_PG_CONNECTION`. Compatibility selected
  `FullyQualifiedName~TenantAdministrator_CannotSeeAnotherAreasLegacyReconciliation`
  from `AqualLifeStyle.Tests` using Release `--no-build` after successful compilation.
  TRX: `postgresql-discovery-final.trx` and `legacy-discovery-compatibility.trx`.
  Logs/TRX remain in backend `TestResults/existing-member-discovery/`, outside `/tmp`.
- Full MVP/backend/frontend suites, dependency reinvestigation, migration execution,
  provider acceptance, B6/B7 and production checks not run. No package changes.
- Synthetic setup intentionally creates/corrupts fixture records; discovery itself
  performs no business writes. Test container is stopped/removed after execution.
- Final diff, staged state, untracked files and whitespace reviewed. Current API
  documentation describes nullable fields, classification, scope and evidence limits.

## Remaining requirements and next action

B6 requires approved population/root mapping under D03B/D09, authoritative identity,
sponsor, payment, approval/activation and effective Area evidence, approved exception
handling and a controlled whole-scope simulation. Discovery does not create any of
those facts. B7/D10 cutover authority remains separate.

Next engineering task: build a frozen-input, read-only evidence export for the
approved reconciliation population, preserving source identities and unknowns for
offline whole-scope simulation; do not infer or execute a migration.
