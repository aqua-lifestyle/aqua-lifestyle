# AQGreen V2 — D10 production commissioning engineering

> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It cannot confirm or supersede a business-policy decision.

Goal: implement the remaining production selection mechanisms offline, without commissioning production. Owner accepts independent Verdict A on 8132c50/f33ed63; preserve those commits and accepted evidence. Production remains suspended.

Authority: current owner instruction; system index/specification sections 26–27/34/B7; existing B6 FINAL_ANALYSIS and separately verified review/authority conventions. D10's actual scope/effective instant is still an owner input, not inferred by code.

Scope: explicit whole-Tenant cutover at a canonical weekly boundary; exact historical native identities and applied placement digest; independently verified D10/B6/reconciliation/prerequisite evidence supplied through restricted operator-pinned files. Partial/mixed financial populations are unsupported and fail closed because the existing financial selector is Tenant-wide. Existing B6 verification/application machinery is not rebuilt. No new database entity/migration or HTTP commissioning endpoint.

Current implementation: source/digest/authority binding, runtime gates for joining/approval/progress/sales/commission/graduation, current database/schema/runtime assembly matching through the existing read-only snapshot reader, existing-state protection against removing cutover configuration, write fence before effective time, separate worker authority/configuration proof and irreversible recovery suspension. No authority/package is configured or activated in production.

State: **IMPLEMENTED**, **TESTED** for the supported offline scope. The new diff needs focused independent review; production commissioning, production readiness and B7 completion are not claimed. Production API remains suspended. The existing production `Commissioning:Enabled` default is false.

Completed changes (paths below are relative to `AqualLifeStyle/9.4.2/aspnet-core/`):

- `src/AqualLifeStyle.Core/Domain/AQGreen/AQGreenD10Commissioning.cs`: explicit D10 decision and separate trusted, independently verified authority input. Exact source/base/final B6 manifests, accepted components, all three D09 populations, native snapshot mappings, applied canonical placement membership, UTC effective boundary, capability choices, release/configuration identities and prerequisite evidence are bound with existing canonical JSON/tuple/SHA-256 conventions.
- `src/AqualLifeStyle.Application/ProgrammeParticipations/AQGreenD10ProductionSelectors.cs`: production joining/approval/progress/sales/commission/graduation selectors; read-only schema/database/runtime and population checks; historical V1 cutoff preservation; pre-effective write fence; legacy-only omission and incompatible post-boundary financial-history denial; independent automatic-worker authority. No historical repair, participation creation or ledger rewrite is part of selection.
- `src/AqualLifeStyle.Web.Host/Startup/AQGreenD10OperatorEvidenceSource.cs`: independently pinned restricted files, immutable process selection, changed-input/custody/version/duplicate-property denial. It consumes trusted operator verification; a self-hash does not authenticate a reviewer or establish that B6 was executed.
- `src/AqualLifeStyle.Web.Host/Startup/AqualLifeStyleWebHostModule.cs`, `Startup.cs`, `ApplicationRecoveryMiddleware.cs`, `src/AqualLifeStyle.Core/Configuration/ApplicationRecoveryMode.cs`, and `src/AqualLifeStyle.Web.Host/appsettings.json`: production wiring/default-off flag, pre-worker startup validation and one-way suspension for both host configuration roots. Configuration reload cannot exit recovery.
- `src/AqualLifeStyle.Application/Admin/ProgrammeParticipations/AdminProgrammeParticipationAppService.cs`, `Admin/Commissions/AdminAQGreenWeeklySalesEligibilityAppService.cs`, and `src/AqualLifeStyle.Web.Host/Commissions/WeeklyCommissionCalculationWorker.cs`: recheck V2 approval inside its existing locked transaction, preserve commissioned sponsor evidence, scope weekly review to the cutover, and preflight before AQGreen or Onyx worker processing.
- `test/AqualLifeStyle.Tests/WebHost/AQGreenD10CommissioningTests.cs`, `AQGreenD10SyntheticEvidence.cs`, `test/AqualLifeStyle.Web.Tests/Integration/AQGreenD10CutoverPostgreSqlTests.cs`, and the existing `AQGreenPlacementV2ApprovalPostgreSqlTests.cs`: reuse approval fixtures and add focused synthetic binding/application tests. Existing fixture timestamp default is unchanged.
- `docs/aqua-system/06-operations-and-enablement-runbook.md`: actual disabled commissioning inputs, operator trust/custody boundary, supported scope, recovery and rollback limitations.

Validation on 2026-10-13:

| Evidence | Actual result | Boundary |
| --- | --- | --- |
| Release build of `test/AqualLifeStyle.Web.Tests/AqualLifeStyle.Web.Tests.csproj` and referenced backend/host/tests | Passed, zero errors; existing warnings remain | No dependency changes or renewed dependency investigation |
| `AQGreenD10CommissioningTests`, `ApplicationRecoveryTests`, `WeeklyCommissionCalculationWorkerTests` | 63 passed, zero failed/skipped | Unit/component; authority denial, custody/pins, expiry, worker controls and HTTP/provider recovery behavior |
| Current-build D10 unit rerun after the final financial-history guard | 22 passed, zero failed/skipped | Overlaps the 63; not 22 additional cases |
| New D10 PostgreSQL application cutover cases | 5 passed, zero failed/skipped | Explicit accepted synthetic inputs; actual proxied approval, locks, UOWs, placement/role/outbox and PostgreSQL rollback/retry; scope/source/effective/recovery denials, no startup side effects, legacy-only omission and unchanged incompatible ledger |
| Affected existing approval PostgreSQL compatibility cases | 3 passed, zero failed/skipped | Default V1 approval, missing V2 attribution denial, atomic V2 approval/exact retry |
| Final diff whitespace check | Passed | No deployment or production evidence |

Raw local evidence remains ignored under `AqualLifeStyle/9.4.2/aspnet-core/TestResults/d10-offline/`: `build.log`, `d10-final-unit.trx`, `d10-current-unit.trx`, `d10-final-postgresql.trx`, and `d10-approval-compatibility.trx`. These are synthetic test artifacts, not an operational B6 package. The earlier fixture attempts failed on Production-vs-Development test bootstrap, duplicate test registration and incomplete synthetic scope. Those fixture problems were corrected without weakening production gates; the final results above passed.

PostgreSQL 16 ran only in an owned disposable loopback container. Tests used `ASPNETCORE_ENVIRONMENT=Development`, explicit synthetic `ConnectionStrings__Default` / `REPRO_PG_CONNECTION`, `REPRO_PG=true`, `REPRO_PG_SCHEMA_FROM_MODEL=true`, and `REPRO_TRANSACTIONAL=true`. Schemas were created from the existing EF model; no migration was executed. D10 cases were filtered by `FullyQualifiedName~D10Cutover`; the three compatibility cases ran separately. The test-only authority factory explicitly models independently verified inputs; it does not run operational B6 verification, prove human approval, or qualify historical facts. No full MVP/B6/recovery qualification was repeated.

Remaining production dependencies: actual schema/database/runtime/configuration identities; verified restorable backup and key/certificate custody; authoritative scoped B6 records, final operational verification, separately approved reconciliation execution and applied native mapping/placement comparison; owner-approved D10 Tenant/Area/effective/capability/exception boundary and write fence; separate provider/worker commissioning; controlled recovery/restart authorization. Partial populations, unresolved/quarantined components, incompatible historical ledgers and monthly automation remain unsupported and fail closed. V1 remains available for uncommissioned Tenants without V2 placement state. Removing authority after V2 placement state exists requires suspension/exception review, not fallback or destructive rollback.

Git context: `feature/aqgreen-v2-main-integration`, parent `f33ed631cb0d17927796a7814d22f09e3d75ce13`; verified `8132c50` and accepted integration `e11edda8bd55f2d5717cd00a56bb613ae9a628d5` preserved. Owner authorizes a local implementation commit only; no push or merge.

Next action: focused independent review of the new commissioning diff. Once separately authorized, recover only the schema/backup-compatible suspended API and resume authoritative B6 work before supplying any D10 inputs or enabling dependent V2/provider/financial activity. This work authorizes none of those production operations.
