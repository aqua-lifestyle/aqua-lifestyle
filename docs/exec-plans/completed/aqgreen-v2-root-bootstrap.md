# AQGreen V2 prospective-root bootstrap

> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

## Goal

Permit an already-authorised prospective AQGreen V2 root participant to complete the existing approval lifecycle so that its root-specific placement-tree scope, position-1 root placement, `Active` participation state, approval decision, member-role transition, and notification outbox intent commit atomically. Preserve the sponsored allocator, V1 behavior, disabled production cutover, and B5.3/B6 scope unchanged.

## Authoritative references

- `docs/aqua-system/aqgreen-network-placement-specification.md`, especially sections 9, 11, 13, 30, 31, and decision `AQG-V2-D03A`: prospective roots require an explicit privileged and audited bootstrap operation; multiple scopes per Tenant/AQGreen programme are allowed; each scope has exactly one root.
- `docs/aqua-system/04-payments-approval-and-yoco.md`: verified payment remains distinct from the Area approval/activation transition, whose transaction owns the unique decision, state change, role transition, and outbox intent.
- `docs/aqua-system/07-verification-decision-and-risk-register.md#p-business-decision-authority-convention`: implementation evidence cannot invent or widen business authority.
- The current user task authorises correcting the existing prospective-root bootstrap defect only and explicitly excludes B5.3/B6, migration, cutover, and general UI work.

## Confirmed decisions

- `AQG-V2-D03A` is `RESOLVED`: one explicit privileged/audited root per `PlacementTreeScope`; multiple root-specific scopes per Tenant/AQGreen programme are valid; ordinary allocation may not create roots.
- `AQG-V2-D15` is `RESOLVED`: Tenant is the hard topology boundary, a placement-tree scope is the tree boundary, and Area is administrative rather than topology identity.
- Payment confirmation does not activate participation. Approval and placement must commit together or both roll back.

## Assumptions

- This defect correction consumes an existing immutable `AuthorisedProspectiveRoot` attribution plus its matching confirmation. It does not introduce an API for granting that authority.
- A prospective root's stable scope identity is created by the privileged approval/bootstrap transaction and retained by the committed root placement. A failed transaction leaves no scope identity to reuse; an exact committed retry reuses the persisted placement.

## Current state

The correction is implemented, tested, and independently accepted. V2 approval now dispatches explicitly by attribution kind: sponsored participants continue through the normal placement-tree lock and allocator, while an `AuthorisedProspectiveRoot` requires the new host-only `BootstrapAQGreenRoot` permission and matching immutable root authority. Root scope, root placement, approval, `Active`, role synchronization, and outbox intent share the existing approval transaction.

The production gate and D10 selectors remain unchanged and disabled. The ADMIN SALES REVIEW UI GAP remains separate. B6 remains blocked until the continuous fresh-network E2E is run after merge.

Independent review first returned `REVISE BEFORE COMMIT` because the application-path PostgreSQL tests did not exercise the host-context denial branch where generic `Approve` and host `AllTenants` are granted but `BootstrapAQGreenRoot` is absent. The focused correction creates that exact host principal, invokes the real approval application service, requires the dedicated-authorization exception, and asserts that scope, placement, participation, activation timestamp, decision, role, and outbox state remain unchanged. Narrow independent re-review returned `ACCEPT` with recommendation `READY TO COMMIT`. The root-bootstrap review loop is `CLOSED`.

## Evidence

- Baseline: canonical `main`, isolated worktree, and `origin/main` all resolve to `ea2c77041ae0ecf56af523f5fc4bb7a89d7053e9`; canonical `main` was clean when the worktree was created.
- Accepted current validation: dedicated missing-permission regression 1/1; full `AQGreenPlacementV2ApprovalPostgreSqlTests` 31/31; affected authorization/application evidence 96/96 carried forward; full Application 1,223/1,223 carried forward from the unchanged production implementation; Web 90/90 carried forward; Release build `PASS`; EF Core 8.0.8 reports no pending model changes; `git diff --check` `PASS`.
- Prior fresh PostgreSQL evidence at this baseline: the approval suite passed 24/24 and attribution/allocator suites passed 80/80; the prospective-root approval test proved rejection with no side effects.
- Source inspection: current D10 selectors retain `LegacyV1`; the test-only V2 gate is the integration boundary for this work.
- `2026-09-01T12:21:35Z` to `2026-09-01T12:34:03Z`, exit `0`: `REPRO_PG=true REPRO_PG_CONNECTION='<local PostgreSQL 16 validation database>' dotnet test test/AqualLifeStyle.Web.Tests/AqualLifeStyle.Web.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~AQGreenPlacementV2ApprovalPostgreSqlTests" --logger "trx;LogFileName=aqgreen-v2-approval-postgresql-final.trx" --results-directory TestResults` passed 30, failed 0, skipped 0 on PostgreSQL 16.15. This includes root success/retry, same-root concurrency, Tenant/host denial, injected rollback and retry, fresh root-to-first-child transition, ordinary missing-sponsor rejection, and existing sponsored approval regressions.
- `2026-09-01`, exit `0`: the new `EnabledV2Approval_HostWithoutDedicatedBootstrapPermissionCannotBootstrapAuthorisedRoot` regression passed 1, failed 0, skipped 0 on the local PostgreSQL 16 validation database. It proves host context with `Approve` and `AllTenants` granted but `BootstrapAQGreenRoot` absent reaches the dedicated authorization branch, returns `AbpAuthorizationException`, and leaves scope, placement, participation activation, approval decision, role, and outbox state unchanged. The first invocation was unavailable at Web test startup because `REPRO_PG_CONNECTION` was not supplied; the corrected invocation reached and passed the test.
- `2026-09-01`, exit `0`: the full `AQGreenPlacementV2ApprovalPostgreSqlTests` class passed 31, failed 0, skipped 0 on the same PostgreSQL 16 validation database, including the new dedicated-permission regression and the retained root/sponsored regression surface.
- `2026-09-01`, exit `0`: `dotnet build test/AqualLifeStyle.Web.Tests/AqualLifeStyle.Web.Tests.csproj --configuration Release --no-restore --consoleloggerparameters:ErrorsOnly` succeeded with 0 errors and 3 warnings.
- `2026-09-01T12:21:35Z` to `2026-09-01T12:34:03Z`, exit `0`: `dotnet test test/AqualLifeStyle.Tests/AqualLifeStyle.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~AquaRolePermissionsTests|FullyQualifiedName~AdminProgrammeParticipationAppServiceTests|FullyQualifiedName~AdminProgrammeParticipationV2AppServiceTests" --logger "trx;LogFileName=aqgreen-root-bootstrap-application-final.trx" --results-directory TestResults` passed 96, failed 0, skipped 0.
- `2026-09-01T12:37:23Z` to `2026-09-01T12:38:57Z`, exit `0`: the fresh-root-to-first-sponsored-child test passed alone against a newly created empty PostgreSQL 16.15 database. TRX duration was 52.535 seconds; the console's aggregate duration display was inaccurate. The test created all business state through domain/application persistence, not direct SQL fabrication.
- `2026-09-01T12:39:07Z` to `2026-09-01T13:09:39Z`: the combined full-solution test command timed out after the Web project passed 90/90; the application test project was still running PostgreSQL fixtures, so the combined command is not credited as a complete pass.
- `2026-09-01T13:09:39Z` to `2026-09-01T14:02:38Z`, exit `0`: `dotnet test test/AqualLifeStyle.Tests/AqualLifeStyle.Tests.csproj --configuration Release --no-build --logger "trx;LogFileName=backend-application-full-final.trx" --results-directory TestResults` passed 1,223, failed 0, skipped 0. Together with the completed 90/90 Web project from the combined run, both backend test projects passed on the final source state.
- `2026-09-01T14:05:54Z` to `2026-09-01T14:07:00Z`, exit `0`: `dotnet build AqualLifeStyle.sln --configuration Release --no-restore --consoleloggerparameters:ErrorsOnly` succeeded with 0 errors and 86 existing warnings.
- EF Core 8.0.8 `migrations has-pending-model-changes` returned `No changes have been made to the model since the last migration.` The machine-global EF 10.0.9 tool was rejected as incompatible before running the check with a temporary EF 8.0.8 tool.
- `dotnet list package --vulnerable --include-transitive` completed and reported existing repository advisories. This branch changes no package or lock file and introduces no dependency advisory.
- Provisional run before the final test correction: the focused PostgreSQL set passed 7 and failed 1 because the cross-Tenant assertion expected `AbpAuthorizationException` while the Tenant query filter failed earlier with the framework's base `AbpException`. The test was corrected to assert the actual fail-closed boundary, rebuilt, and the final source-state suites above passed.
- One bounded post-test review found no branch-owned defect in arbitrary root admission, sponsored fallback, duplicate-root races, Tenant isolation, transaction rollback, root representation, V1 behavior, D10, or migration history.
- The bounded independent-review-correction check confirmed that the new regression uses the real approval application service in host context, explicitly proves `Approve` and `AllTenants` granted with `BootstrapAQGreenRoot` absent, identifies the dedicated permission branch by its exception message, and queries PostgreSQL durable state for zero scope, placement, activation, decision, role, or outbox mutation. This correction changed no production source, shared authorization fixture, migration, historical migration, D10 selector, or direct-SQL happy-path state.

## Open questions

- None for this bounded correction. Root-authority creation remains outside this task; this implementation only consumes existing immutable authority.

## Completed work

- Verified clean baseline and created isolated branch/worktree.
- Resolved root uniqueness as one root per root-specific scope, not one root per Tenant/programme.
- Confirmed that legacy migration/cutover decisions `D03B`, `D09`, and `D10` do not block this prospective, disabled-gate path.
- Added host-only `Aqua.Admin.ProgrammeParticipations.BootstrapAQGreenRoot`; Tenant/Area administrators retain sponsored approval but cannot bootstrap roots.
- Added an explicit prospective-root branch in the existing V2 approval transaction. It validates root attribution and confirmation, creates the scope and null-parent/null-slot/empty-path root using PostgreSQL server time, and reuses the committed placement exactly on retry.
- Preserved the sponsored allocator unchanged; missing sponsor placement still fails closed.
- Added PostgreSQL application-path coverage for root success, retry, concurrency, authorization/Tenant boundaries, rollback, fresh child transition, and sponsored regression.
- Added the independent-review correction: a host application-path denial case with `Approve` and `AllTenants` granted, `BootstrapAQGreenRoot` absent, and zero durable approval side effects.
- No schema or migration change was required; no historical migration was modified.

## Next action

Commit, push, and merge the accepted root-bootstrap scope through required CI. After merge, the next system task is the AQGreen V2 continuous fresh-network E2E. Do not begin B6 or the B5.3 admin sales-review UI as part of this delivery.

## Git/branch context

- Repository/worktree: `/home/wtc/Downloads/newAqua/aqua-lifestyle-aqgreen-v2-root-bootstrap`
- Branch: `fix/aqgreen-v2-root-bootstrap`
- Base and current `HEAD`: `ea2c77041ae0ecf56af523f5fc4bb7a89d7053e9`
- Dirty/staged/untracked state: intended source, tests, authorization, and this execution plan are modified/untracked; nothing staged.
- Commit/push/PR/merge state: authorized for this delivery; pending required Git and CI checks.
