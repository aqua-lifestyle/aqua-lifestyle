# AQGreen V2 — B7 offline release controls

> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It cannot confirm or supersede a business-policy decision.

Goal: qualify the remaining release/recovery mechanics that can safely execute offline. Current owner direction permits local commits and synthetic PostgreSQL/application execution; production API stays suspended. No production access, providers, publication, B6 production execution or B7/D10 activation.

Authority: current owner instruction; system index/documents 05–08; placement specification B7 and D10; existing B6 manifest/review machinery. D10 remains unresolved. No production selector/financial-effective-time policy is invented.

Preserved: e11edda integration baseline, 33cdbbe checkpoint and earlier accepted MVP/source artifacts. Completed recorded Area supplement consolidated as 8132c50; its seven focused PostgreSQL results are carried forward, not rerun.

Existing controls reused: SkipDbSeed, ABP background-job switch, commissioning interceptor, worker enable flags, Bird disabled behavior, provider gateways, migration discovery/history and fail-closed rollback guards, disabled V2 joining/approval/progress and LegacyV1 financial selectors, existing read-only discovery/frozen export/review bindings and ledger constraints.

Concrete gaps: API startup invokes host seed/repair paths; normal Production validation requires Bird/provider commissioning even for recovery; tenant migration failures were swallowed while reporting success. Recovery now has a focused operational suspension path. Production/recovery startup suppresses API seeding and checks exact migration history before workers. Recovery disables jobs/workers, providers and business application/controller operations; only existing authentication, health and authorized reconciliation reads/export remain available. Recovery is not B6 or D10 authority.

State: IMPLEMENTED and TESTED for offline release/recovery suspension; ready for focused independent review of this change. Full B7 is not complete or authorized. No production state has been observed or changed.

Validation: Release builds of the backend test projects and dependencies succeeded; existing compiler/analyzer/advisory warnings remain. Five retained TRX files establish 117 distinct cases with latest outcomes Passed, no unresolved failures or skips. These are 113 unit/component/mock-provider cases and four real PostgreSQL migration/release cases, not 117 PostgreSQL cases. New recovery controls, existing Production validation, host/tenant migration failure reporting, existing worker defaults, V1 commission holds/Tenant authorization, checkout contracts and legacy repeat-joining guards passed. The 11 Bird cases use an HTTP handler, not Bird delivery.

PostgreSQL release execution reused the existing migration fixture on disposable PostgreSQL 16 with loopback publication and an isolated Redis 7 instance from existing local images. It applied the actual migrations to synthetic data, launched the actual Release API with Production configuration/recovery enabled, exercised anonymous denials and explicit Host authentication/authorized frozen export, verified the package/count/unknown evidence, denied activation/calculation/payout/webhook/unknown routes even to that reviewer, and shut down normally (exit 0). Business-table fingerprints stayed identical. Key-ring/security/audit tables are separately excluded as infrastructure writes; no claim of a wholly read-only API is made.

A custom-format synthetic pg_dump restored into a separate empty database; all business fingerprints and release-history preflight matched, and the dump SHA-256 remained unchanged. Actual API startup rejected a future schema (exit 1); read-only preflight rejected missing/gapped history, with the synthetic test transaction rolled back; invoking the migrator in recovery returned exit 1 before initialization. Existing contradictory-payment migration failure and confirmed-payment rollback protection passed on PostgreSQL. Host/tenant failure-to-stop cases also passed at the migrator orchestration boundary. No production recovery is established by these results.

Initial actual-host fixture failures were retained: unreachable Redis, missing retained language baseline, implicit Default-Tenant login instead of Host login, and a null security stamp in the existing migration-only account fixture. The controlled fixture now supplies existing Redis/languages and a valid synthetic account/explicit Host login before the API starts. No startup seed, authentication bypass, assertion weakening or business-history fabrication was introduced. Final actual-host execution passed. Source compile mistakes were corrected before successful builds. No accepted MVP walkthrough or B6 qualification suite was repeated.

Ignored evidence: backend TestResults/b7-offline/{b7-focused.trx,b7-release-and-compatibility.trx,b7-final-recovery.trx,b7-recovery-host.trx,b7-bird-boundary.trx} and corresponding logs/synthetic process logs. Owned API processes, Redis and PostgreSQL containers were terminated/removed; no unrelated resources were touched. Earlier seven historical-supplement PostgreSQL results remain carried-forward evidence for 8132c50.

Changed files (backend paths relative to AqualLifeStyle/9.4.2/aspnet-core):

- src/AqualLifeStyle.Core/Configuration/ApplicationRecoveryMode.cs
- src/AqualLifeStyle.EntityFrameworkCore/EntityFrameworkCore/DatabaseReleasePreflight.cs
- src/AqualLifeStyle.Application/ProgrammeParticipations/PilotApplicationBoundaryInterceptor.cs
- src/AqualLifeStyle.Application/Payments/Yoco/YocoCheckoutGateway.cs
- src/AqualLifeStyle.Application/Payments/Yoco/YocoPaymentNotificationProcessor.cs
- src/AqualLifeStyle.Web.Core/AqualLifeStyleWebCoreModule.cs
- src/AqualLifeStyle.Web.Host/Startup/ApplicationRecoveryMiddleware.cs
- src/AqualLifeStyle.Web.Host/Startup/Startup.cs
- src/AqualLifeStyle.Web.Host/Startup/AqualLifeStyleWebHostModule.cs
- src/AqualLifeStyle.Web.Host/Startup/DeploymentConfigurationValidator.cs
- src/AqualLifeStyle.Web.Host/Commissions/WeeklyCommissionCalculationWorker.cs
- src/AqualLifeStyle.Web.Host/ProgrammeEngine/EntryMonthlyObligationWorker.cs
- src/AqualLifeStyle.Web.Host/Email/TransactionalEmailOutboxWorker.cs
- src/AqualLifeStyle.Web.Host/appsettings.json
- src/AqualLifeStyle.Migrator/Program.cs
- src/AqualLifeStyle.Migrator/MultiTenantMigrateExecuter.cs
- test/AqualLifeStyle.Tests/WebHost/ApplicationRecoveryTests.cs
- test/AqualLifeStyle.Tests/Migrator/MigrationFailureReleaseTests.cs
- test/AqualLifeStyle.Tests/EntityFrameworkCore/AQGreenMigrationRollbackTests.cs
- test/AqualLifeStyle.Tests/EntityFrameworkCore/AQGreenRecoveryReleasePostgreSqlTests.cs

Documentation: docs/aqua-system/06-operations-and-enablement-runbook.md and this worklog. The consolidated historical supplement commit changes its seven recorded files; accepted artifacts are not rewritten.

Remaining production-dependent requirements: verify the actual suspended build/schema/baseline and database-provider version; retain and restore-test an authorized current backup with the real key ring/certificates; approve matching release artifacts and recovery configuration/access/stop operator; obtain scoped read-only production evidence during authorized recovery, complete authoritative B6 source/review/exception/migration requirements, and resolve D10's Tenant/population/financial effective boundary and applicable consumer/lifecycle scope. The non-demo production V2 selector/authority wiring is still absent; it cannot be completed safely by inventing D10 policy or treating offline review as B6 execution approval. Future wiring must reuse the existing final B6 manifests and verified authority, then receive offline qualification and independent review. Normal V1 paths remain compatible and no V2 gate was enabled.

Limits: history equality is not schema-drift proof; PostgreSQL 16 synthetic backup/recovery is not proof for the current production provider/data; emergency suspension does not cancel in-flight transactions. No deployment/image qualification, production restore, real provider delivery, B6 production execution or B7 activation occurred. Dependency investigations and full MVP acceptance were not restarted.

Next: focused independent review of this release/recovery correction. Then obtain separate approval for verified-backup/schema-compatible API recovery in suspension, followed by scoped B6 evidence/reconciliation. Provider/worker commissioning and D10 activation remain separate owner-gated steps.
