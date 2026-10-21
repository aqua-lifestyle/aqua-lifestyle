# AQGreen late sales-evidence invalidation

> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It cannot confirm or supersede a business-policy decision.

## Goal and authority

Implement the owner's 21 October 2026 bounded correction: append-only invalidation of finalized confirmed sales evidence; linked commission reconciliation hold at release/payment boundaries; preserve original reviews, calculations and settled payment history. Specification sections 24.6 and 36 (late invalidation) remain applicable. No adjustment/reversal policy is introduced.

## Context and plan

- Separate branch `fix/aqgreen-sales-invalidation-hold`, based on pinned source `0c1d861f8fc199bc04f6b1009f748d5ab83815fc` in a separate worktree. Pinned release and Agent 2 work remain unchanged.
- Reuse finalized evidence IDs, immutable V2 commission-to-review binding, existing review permissions/Area scope, PostgreSQL transaction locks and append-only protections.
- Add one evidence-invalidation table. Its existence is an authoritative pending reconciliation hold for all commissions referencing that decision, including future calculations. No reconciliation completion or payout override is added.
- Serialize invalidation and payout checks on the existing review lock. Keep existing per-commission payout serialization.
- Validate additive migration, immutable history, exact/conflicting/concurrent retries, permission/Tenant/Area denial, settled preservation and unaffected controls on disposable PostgreSQL; carry forward accepted MVP evidence and rerun affected amount/spillover regressions.

## Current state

IMPLEMENTED and focused PostgreSQL TESTED. One append-only invalidation table binds the original Tenant/review/evidence ID to reason, actor and server time. Exact retries return the first record; conflicting reasons fail. The existing immutable V2 snapshot supplies the commission link. Release and payment APIs check the pending reconciliation hold after acquiring the existing review lock; the original payout state is not rewritten. Settled records are flagged, never reversed. The authorized review response exposes evidence IDs and the scoped invalidation list; commission results expose an explicit reconciliation flag. New calculations reject known-invalid evidence.

The additive migration has composite Tenant/review/evidence referential integrity, uniqueness, append-only guards and a populated-table downgrade refusal. Existing API permissions, Area authority stabilization, financial commissioning, test-money and pilot restrictions remain intact. No adjustment, reconciliation completion or hold override was introduced. Local schema/testing only; no production operations, push or merge authorized.

## Evidence and next action

- Accepted prior 228 passing MVP tests remain carried-forward non-production evidence; they were not presented as live-provider qualification.
- Final focused command from the backend directory: `dotnet test test/AqualLifeStyle.Tests/AqualLifeStyle.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~AQGreenSalesInvalidationPostgreSqlTests --logger 'trx;LogFileName=invalidation-accepted.trx' --results-directory TestResults/sales-invalidation`: 10 passed, 0 failed, 0 skipped on disposable PostgreSQL 16.
- Covered: earned/released/paid preservation; both payout APIs denied; exact concurrent and conflicting retries; anonymous, permission, Host, Tenant and Area denials; matching Area authorization; wrong/held evidence; unaffected commission release/payment recording; known-invalid calculation denial; committed-hold lock waiter; append-only and uniqueness/foreign-key denials; guarded downgrade and empty downgrade/reapply.
- Initial new-fixture failures (missing activation times, future calculation time and review/calculation ordering) were corrected without weakening domain or database assertions; logs are retained in ignored TestResults.
- Release solution build, API publish, EF pending-model check and diff checks passed. No new packages were added. Existing dependency advisories remain in the ignored audit log.
- Relevant existing financial/spillover run: 144 passed, 1 failed, 0 skipped. The one failure was a trigger-probe expectation: the new FK rejected bare TRUNCATE before the intended trigger. The probe now uses CASCADE, keeps the exact P0001 rejection and unchanged-row assertions, and passed its focused rerun (1 passed, 0 failed, 0 skipped). No application assertion or integrity guard was weakened.
- This run covered all 31 EntryWeeklyCommission domain cases, including R150/R400/R1,650 V1 and V2 totals; 27 PostgreSQL allocator cases, including slots 1–5, parent-major overflow and concurrent writers; 16 structural evaluator PostgreSQL cases; 14 existing admin commission cases; and the sales-review/reader/authorization suites. The 144 unchanged passing cases are carried forward rather than rerun after the test-only TRUNCATE correction.
- Web suite with CI's explicit `ASPNETCORE_ENVIRONMENT=Development`: 95 passed, 0 failed, 84 skipped. Skip reasons: 24 opt-in PostgreSQL member authorization, 2 isolated MVP proofs, 37 model-created PostgreSQL cases, 14 REPRO_PG cases and 7 isolated-pilot cases. These skips and existing guarded no-op cases do not establish fresh PostgreSQL/browser/live-provider qualification. Prior accepted 228-test evidence remains carried forward.
- The first web attempt was cancelled after using the default Production profile without disposable seed data. A broad full application run was also cancelled to avoid repeating unrelated historical migration matrices; it has no complete qualification verdict. Both logs remain available. Complete hosted CI remains a pre-merge requirement; no push was authorized.
- Ignored evidence: `AqualLifeStyle/9.4.2/aspnet-core/TestResults/sales-invalidation/`. The original 228-test evidence and pinned worktree remain untouched.
- Completed: focused implementation and local validation. No further application defect was demonstrated within this scope. No reconciliation completion/adjustment or production readiness is claimed. No production migration, money transfer, deployment, push or merge occurred. Agent 2 and the pinned release worktree were not modified.

## Exact validation commands

From `AqualLifeStyle/9.4.2/aspnet-core`:

`dotnet build AqualLifeStyle.sln -c Release --no-restore`

`dotnet publish src/AqualLifeStyle.Web.Host/AqualLifeStyle.Web.Host.csproj -c Release --no-build --no-restore -o TestResults/sales-invalidation/api-publish`

`dotnet test test/AqualLifeStyle.Tests/AqualLifeStyle.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~EntryWeeklyCommissionTests|FullyQualifiedName~AdminCommissionAppServiceTests|FullyQualifiedName~AdminAQGreenWeeklySalesEligibility|FullyQualifiedName~AQGreenWeeklySalesEligibility|FullyQualifiedName~AQGreenPlacementAllocatorPostgreSqlTests|FullyQualifiedName~AQGreenStructuralCompletionEvaluatorPostgreSqlTests'`

`dotnet test test/AqualLifeStyle.Tests/AqualLifeStyle.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~AQGreenWeeklySalesEligibilityPostgreSqlTests.ReplicaRoleCannotBypassAnyWeeklySalesIntegrityTrigger`

`ASPNETCORE_ENVIRONMENT=Development dotnet test test/AqualLifeStyle.Web.Tests/AqualLifeStyle.Web.Tests.csproj -c Release --no-build --no-restore`

EF tooling used the existing cached 8.0.8 CLI: `migrations has-pending-model-changes --context AqualLifeStyleDbContext --project src/AqualLifeStyle.EntityFrameworkCore --startup-project src/AqualLifeStyle.EntityFrameworkCore --configuration Release --no-build` returned no pending changes. Migration up/down/reapply and populated downgrade denial executed inside the focused disposable PostgreSQL tests.

## Handoff

Only a local commit is authorized. Protected publication/review, hosted CI, production migration and release each remain subject to their separate authorizations. B6 acceptance and explicit D10 commissioning remain separate gates. Apply the additive migration before new code; once invalidations exist, an older binary that ignores these holds is not a safe rollback target. Existing dependency advisories are recorded in `dependency-audit.log`; package versions and dependency configuration were not changed.

## Changed files

- `AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Application/Admin/Commissions/AdminAQGreenWeeklySalesEligibilityAppService.cs`
- `AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Application/Admin/Commissions/AdminCommissionAppService.cs`
- `AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Application/Admin/Commissions/Dto/AQGreenWeeklySalesEligibilityDtos.cs`
- `AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Application/Admin/Commissions/Dto/AdminCommissionDtos.cs`
- `AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Application/Admin/Commissions/IAdminAQGreenWeeklySalesEligibilityAppService.cs`
- `AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Core/Domain/AQGreen/AQGreenSalesEvidenceInvalidation.cs`
- `AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Core/Domain/AQGreen/IAQGreenSalesEvidencePayoutGuard.cs`
- `AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.EntityFrameworkCore/EntityFrameworkCore/AQGreenSalesEvidencePayoutGuard.cs`
- `AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.EntityFrameworkCore/EntityFrameworkCore/AQGreenWeeklySalesEligibilityDecisionReader.cs`
- `AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.EntityFrameworkCore/EntityFrameworkCore/AqualLifeStyleDbContext.cs`
- `AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.EntityFrameworkCore/EntityFrameworkCore/EntityConfigurations/AQGreenSalesEvidenceInvalidationConfiguration.cs`
- `AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.EntityFrameworkCore/Migrations/20261021051549_AddAQGreenSalesEvidenceInvalidation.Designer.cs`
- `AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.EntityFrameworkCore/Migrations/20261021051549_AddAQGreenSalesEvidenceInvalidation.cs`
- `AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.EntityFrameworkCore/Migrations/AqualLifeStyleDbContextModelSnapshot.cs`
- `AqualLifeStyle/9.4.2/aspnet-core/test/AqualLifeStyle.Tests/EntityFrameworkCore/AQGreenSalesInvalidationPostgreSqlTests.cs`
- `AqualLifeStyle/9.4.2/aspnet-core/test/AqualLifeStyle.Tests/EntityFrameworkCore/AQGreenWeeklySalesEligibilityPostgreSqlTests.cs`
- `docs/aqua-system/03-commission-engine-explained.md`
- `docs/exec-plans/completed/aqgreen-sales-invalidation-hold.md`
