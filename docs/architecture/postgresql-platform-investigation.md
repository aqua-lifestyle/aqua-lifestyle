# Aqua PostgreSQL Platform Investigation

Assessment date: 18 August 2026  
Repository: `main` at `b88aeb3a64c81f98994485d90b412b8e02c06c3a`  
Decision horizon: 12-24 months

## Claim Labels

- **FACT**: directly supported by current source, an executed local check, or cited official provider documentation.
- **INFERENCE**: conclusion derived from facts but not directly observed in production.
- **ASSUMPTION**: scenario input used because the real value is unavailable.
- **RECOMMENDATION**: proposed action or standard.
- **UNKNOWN**: material information not established by the available evidence.

## 1. Executive Summary

**RECOMMENDATION:** Upgrade the existing Render PostgreSQL resource to a paid flexible plan, initially `Basic-1gb` unless current metrics demonstrate that `Basic-256mb` has adequate headroom. Keep the API and database in Render Frankfurt, retain direct Npgsql connectivity initially, establish a tested recovery procedure, and defer database migration.

**FACT:** The repository declares a Render Free PostgreSQL database in Frankfurt, private-only access, a Starter Render API in the same region, and a pre-deploy migrator (`render.yaml:1-20`, `render.yaml:88-94`). Render Free PostgreSQL expires 30 days after creation, has no managed backup, and is deleted after a 14-day post-expiration upgrade grace period [R1].

**UNKNOWN:** The Render dashboard, deployed Blueprint drift, PostgreSQL version, creation time, current health, connection count, storage usage, workspace plan, and reported 19 August 2026 expiration were not accessed or independently verified.

**INFERENCE:** If the reported 19 August expiration is the Render dashboard deadline, this is a Free-plan lifecycle event, not a fundamental Render PostgreSQL defect. **OBSERVATION:** Render's live status page showed PostgreSQL operational when checked on 18 August; this was not retained as a dated status artifact. Upgrading the database instance, not merely the workspace, removes the 30-day expiry [R1, R2].

**FACT:** Aqua uses PostgreSQL as a correctness component. EF Core 8.0.8 uses Npgsql; migrations contain PL/pgSQL triggers, `TIMESTAMPTZ`, `DO` blocks and fail-closed exceptions; commission, monthly-obligation and checkout workflows use transaction-scoped `pg_advisory_xact_lock`; uniqueness and foreign keys are relied upon for idempotency and isolation. This rules out casual substitution with a merely PostgreSQL-compatible database.

**FACT:** Provider choice is not the present commission-engine blocker. Both programme workers default to disabled. Current application/operations blockers include commission terms boundary ambiguity, failure to prove expected monthly-obligation completeness, unverified Yoco occurrence-time semantics, missed-cycle recovery, incomplete preflight/topology evidence, unresolved monthly due policy, and incomplete payout evidence. Moving PostgreSQL does not correct any of these.

**INFERENCE:** The workload is currently small. The only repository-recorded production reconciliation reported 13 live customers, five AQGreen participations and 46 migrations on 11 August 2026 (`docs/operations/aqgreen-production-reconciliation/README.md:206-243`). The underlying output is not committed, current counts are unknown, and this report must not be treated as current production proof.

**FACT:** A local recovery experiment migrated PostgreSQL 16 to all 47 current migrations, backed up a 12 MB schema-shaped database, restored it into a fresh PostgreSQL 16 instance, retained 8 application triggers and 289 indexes, started the API with the programme workers explicitly disabled, and returned a healthy database/key-store response. Five real-PostgreSQL commission persistence and advisory-lock tests passed. This proves local schema/startup recoverability, not production-data recoverability.

## 2. Current Aqua Architecture

### Runtime and deployment

| Claim | Evidence |
| --- | --- |
| **FACT:** Next.js is hosted separately on Vercel; the ASP.NET Core/ABP API, PostgreSQL and Redis are declared on Render Frankfurt. | `docs/deployment.md:3-12`; `render.yaml:1-25`, `81-94` |
| **FACT:** The API is stateless with database-persisted ASP.NET Data Protection keys and a transactional email outbox. | `docs/deployment.md:62-88`, `104-121` |
| **FACT:** The Render pre-deploy command runs the dedicated migrator before API deployment. | `render.yaml:7-10`; `docs/deployment.md:181-185` |
| **FACT:** Redis is a cache, not the distributed correctness lock. Production configuration requires it, but business locks are PostgreSQL advisory locks. | `AqualLifeStyle.Web.Host/Startup/AqualLifeStyleWebHostModule.cs:38-51`; lock files cited below |
| **FACT:** The Blueprint database and Redis use empty IP allowlists, which deny external access and permit private Render connectivity. | `render.yaml:81-94`; Render Blueprint specification [R3] |

### Persistence architecture

| Claim | Evidence |
| --- | --- |
| **FACT:** `AqualLifeStyleDbContext` is the primary ABP Zero context and applies all EF configurations. | `AqualLifeStyle.EntityFrameworkCore/EntityFrameworkCore/AqualLifeStyleDbContext.cs:28-80`, `124-135` |
| **FACT:** Runtime and design-time database configuration use Npgsql. | `AqualLifeStyleDbContextConfigurer.cs:9-17`; `AqualLifeStyleDbContextFactory.cs:14-25` |
| **FACT:** Host and distinct tenant databases are migrated by `AqualLifeStyle.Migrator`. | `AqualLifeStyle.Migrator/MultiTenantMigrateExecuter.cs:58-110` |
| **FACT:** PostgreSQL-specific migration code includes row-locking triggers and append-only guards. | `20260801092352_AddAQGreenSchedulesAndOnyxGraduation.cs:148-185`; `20260809081746_AddAreaActivationStateHistory.cs:49-70`; `20260809201814_AddCommissionTermsVersions.cs:90-121` |
| **FACT:** Data Protection keys use the same host database through a dedicated runtime context, while schema ownership remains in the primary migration stream. | `DataProtectionKeyDbContext.cs:6-27`; `DataProtectionPersistenceServiceCollectionExtensions.cs:30-58` |

### Business state and boundaries

**FACT:** Tenant is the hard security/data boundary. Area is a business subdivision inside a Tenant (`AqualLifeStyleConsts.cs:9-11`; `Domain/Areas/Area.cs:7-16`; `docs/aqua-system/08-area-and-tenant-boundaries.md:5-21`).

**FACT:** Area identity and assignment use composite `(TenantId, AreaId)` foreign keys and partial unique indexes (`AreaConfiguration.cs:18-19`, `34-42`, `65-72`).

**FACT:** Some customer/user relationships remain protected principally by application/ABP tenant filtering rather than composite same-tenant foreign keys. For example, customer Area assignment references `CustomerId` alone on the customer side (`AreaConfiguration.cs:38-46`). This is an application/schema risk, not a hosting-provider distinction.

**FACT:** Commission activation records retain legacy Tenant-keyed "Area" semantics, while the newer Area entity is intra-Tenant (`AreaActivationStateResolver.cs:46-53`, `82-92`). Provider migration would not resolve this semantic debt.

## 3. Database Requirements

### Requirements matrix

| Requirement | Priority | Basis |
| --- | --- | --- |
| Genuine PostgreSQL supporting PL/pgSQL, triggers, partial indexes, transactional DDL and advisory transaction locks | MUST | **FACT:** Current migrations and locks use these features. |
| PostgreSQL 16 compatibility today; supported path to newer majors | MUST | **FACT:** local/CI PostgreSQL tests use `postgres:16-alpine`. **UNKNOWN:** production major version. Aqua does not currently require a version-specific PG17/18 feature. |
| ACID transactions with `ReadCommitted` and explicit `Serializable` support | MUST | **FACT:** checkout/approval paths explicitly select these isolation levels. |
| Transaction-scoped advisory locks on one connection/transaction | MUST | **FACT:** weekly commission, monthly scheduling and checkout transitions rely on `pg_advisory_xact_lock`. |
| Unique constraints, partial unique indexes and foreign keys enforced atomically | MUST | **FACT:** payment/event/cycle idempotency and boundaries depend on them. |
| Automated provider backup with known retention | MUST | **INFERENCE:** loss of payments, approvals, membership, key ring and ledger is not safely reconstructible. |
| Tested restore into an isolated database, with application validation | MUST | **FACT:** provider backup existence alone does not prove restorability; prior P0001 work required a restored backup. |
| No expiring/free production database | MUST | **FACT:** current declared Free resource expires and has no managed backup. |
| TLS for public links or private provider networking; no unrestricted public database | MUST | **FACT:** database contains identity, payment and programme state. |
| Bounded connection pools with capacity for all API replicas, workers, migrator and operations | MUST | **INFERENCE:** one API instance is likely far below 100 connections, but Npgsql defaults are not explicitly bounded in source. |
| Health, storage, connection, lock, CPU/memory and slow-query visibility | MUST | **FACT:** `/api/health` covers reachability, not capacity or query performance. |
| Point-in-time recovery | SHOULD now; MUST before materially higher payment/commission volume | **INFERENCE:** logical mistakes or corruption can occur between daily dumps. Render paid plans include 3-7 day PITR. |
| Automatic zonal failover | SHOULD, not currently proven MUST | **UNKNOWN:** the business has not approved RTO/RPO. Current evidence shows low volume and no 24/7 payout automation. |
| Provider pooling | NICE TO HAVE until measured | **INFERENCE:** Npgsql pooling and one API instance should be sufficient at current scale. |
| Read replicas, branching, cross-region DR, serverless scaling | NICE TO HAVE | No measured workload or recovery objective justifies these now. |

**FACT:** Aqua does not currently meet every MUST. `DATABASE_URL` is converted to `SSL Mode=Prefer;Trust Server Certificate=true` (`AppConfigurations.cs:39-49`, `59-83`), the API and migrator consume the same Render database credential, and no explicit Npgsql pool bound is configured. Current private Render networking limits exposure, but any cross-provider public connection is blocked until strict certificate/hostname verification is implemented and tested. The paid upgrade is urgent risk reduction, not by itself completion of the production standard.

### Transaction semantics

**FACT:** External payment-checkout creation is deliberately outside long database transactions. The application uses short transactions before and after the external call (`ClubMemberProgrammeParticipationAppService.cs:262-392`; `ClubMemberEntryMonthlyObligationAppService.cs:93-218`).

**FACT:** Approval/rejection uses a serializable transaction and a transaction-scoped participation lock (`AdminProgrammeParticipationAppService.cs:352-490`).

**FACT:** Webhook processing locks the checkout, validates replay facts, applies payment/programme state and stores the receipt within one unit of work (`YocoPaymentNotificationProcessor.cs:134-228`).

**RECOMMENDATION:** Use direct PostgreSQL connectivity initially. If a transaction pooler is later introduced, prove that lock acquisition and protected work remain in one explicit transaction. Never move these workflows to session-level advisory locks through transaction pooling.

### Workload scenarios

**UNKNOWN:** Current TPS, peak concurrency, database size, WAL rate, IOPS, query latency, connections, payment volume and growth forecast are unavailable.

**FACT:** The only production-scale claim in the repository reports 13 live customers, 10 Default-Tenant customers, five AQGreen participations and three active administrator assignments on 11 August 2026. It is carried-forward documentation, not current proof.

The following are capacity scenarios, not forecasts:

| Scenario | Members | Payment/approval writes | Commission shape | Likely database character |
| --- | ---: | --- | --- | --- |
| Current evidence envelope | Tens | Sporadic | Weekly, currently disabled | Tiny OLTP; operational correctness dominates capacity. |
| Near-term | Hundreds to low thousands | Tens to low hundreds/day | Weekly scans/writes plus monthly obligations | Small OLTP; indexes and bounded pools matter more than horizontal scale. |
| Moderate growth | Tens of thousands | Hundreds to low thousands/day | Larger weekly network reconstruction and ledger batches | Still conventional PostgreSQL; measure query plans, lock duration, WAL and maintenance. |

**INFERENCE:** No scenario supported by repository evidence requires Aurora, read replicas, sharding, serverless branching, or more than an ordinary managed PostgreSQL primary. The weekly engine may generate one commission row per active participation plus components, but runs once per closed cycle and is segmented by Tenant/programme.

### Availability consequences, not invented objectives

| Lost/unavailable state | Consequence |
| --- | --- |
| Payment/webhook receipt | Duplicate or missing payment application; manual provider reconciliation required. |
| Participation/approval | Incorrect eligibility or inability to progress a paid member. |
| Qualification/topology | Incorrect commission/travel outcome. |
| Commission ledger | Financial liability ambiguity; payout must stop pending reconciliation. |
| Data Protection keys | Outstanding identity links and protected pending email bodies become unusable. |

**RECOMMENDATION:** The business owner must approve RPO and RTO after considering these consequences. Until then, use conservative controls: continuous provider PITR where affordable, at least daily independent logical backup, no automatic payout after uncertain recovery, and a restore drill before worker enablement.

### Operational ownership

| Owner | Responsibilities |
| --- | --- |
| Application team | **RECOMMENDATION:** Schema/migrations, query/index quality, transaction boundaries, idempotency, Tenant/Area isolation, connection-pool limits, worker controls, application alerts, reconciliation, backup verification and restore acceptance. |
| Managed database provider | **RECOMMENDATION:** PostgreSQL process/host lifecycle, storage durability, physical backup/PITR machinery, platform patching, infrastructure metrics, documented maintenance and contracted failover where purchased. |
| Business/finance/operations | **RECOMMENDATION:** Approve RPO/RTO, retention, downtime windows, reconciliation decisions, worker enablement and payout resumption after incidents. |
| Specialist SRE/cloud expertise | **INFERENCE:** Required for hyperscaler VPC/private endpoints, KMS/IAM design, cross-region DR, logical-replication cutovers, HA failover exercises, advanced observability and 24/7 incident response. Aqua should not assume the application team can absorb this without explicit staffing. |

**FACT:** A managed provider does not own Aqua's logical corruption, incorrect commission rules, unsafe migrations, unbounded pools, missing alerts or proof that a restored database is business-correct.

### Governance and multi-database requirements

- **RECOMMENDATION:** Keep production data in an approved region and obtain provider DPA/security evidence appropriate to South African POPIA obligations before changing provider. **UNKNOWN:** The business's formal data-residency, retention, breach-notification and support-escalation requirements.
- **FACT:** The migrator can discover Tenants with distinct connection strings and migrate each distinct tenant database (`MultiTenantMigrateExecuter.cs:75-110`). **INFERENCE:** Current repository-recorded production evidence describes one Default Tenant and does not establish a separate tenant database.
- **RECOMMENDATION:** Before provisioning any separate tenant database, validate the candidate plan's database-creation rights, connection limits, roles/grants, backup coverage and restore procedure for every database. Physical provider recovery, logical exports and `pg_dump` scope differ; do not assume restoring the host database restores separately hosted tenant databases.
- **RECOMMENDATION:** Maintain a provider-exit procedure using portable logical backups, role/grant manifests and an extension inventory. Provider physical snapshots are recovery tools, not portable exit artifacts.

## 4. Current Production Database Assessment

### Established

- **FACT:** Git declares Render PostgreSQL `plan: free`, Frankfurt, database/user `aqualifestyle`, and private-only access (`render.yaml:88-94`).
- **FACT:** Documentation warns that Free data plans are evaluation-only (`docs/deployment.md:31`).
- **FACT:** No automated independent backup workflow or recurring restore test exists in visible GitHub Actions, Compose or the Blueprint.
- **FACT:** A prior operations report records a PostgreSQL 18.4 custom-format backup and successful isolated restore of production-shaped data (`docs/operations/aqgreen-production-reconciliation/README.md:206-243`). The archive and command output are not committed.
- **CARRIED FORWARD, NOT VERIFIED:** That report says the restored database contained 46 migrations, five AQGreen participations, two legacy backup rows and payment data.

### Unknown account state

- **UNKNOWN:** Exact product/tier actually deployed, because Blueprint drift was not inspected.
- **UNKNOWN:** PostgreSQL version, storage and CPU/memory utilization, connection count, database size, maintenance schedule and alerts.
- **UNKNOWN:** Whether any customer-managed backup currently exists and remains restorable.
- **UNKNOWN:** Whether the resource actually expires on 19 August 2026 or has already been upgraded.
- **UNKNOWN:** Current worker environment overrides. Repository defaults are disabled but repository state is not production state.

### Assessment

**RECOMMENDATION:** Treat the declared Free database as below Aqua's minimum production standard. Verify the dashboard immediately and, if still Free, upgrade before the reported deadline. Do not wait for a provider-selection project.

**INFERENCE:** `Basic-256mb` removes expiry and adds paid-database recovery but preserves the Free plan's 0.1 CPU/256 MB profile. `Basic-1gb` is the more defensible initial production choice because migrations, EF queries and future workers share database memory; the actual decision should be checked against current memory, CPU and query metrics.

## 5. Render Assessment

### Capabilities

| Area | Official current position | Aqua implication |
| --- | --- | --- |
| PostgreSQL | Full support for 13-18; new databases default to 18 [R4] | Compatible. Confirm existing major before any upgrade. Test major upgrades on a PITR clone. |
| Compute | Basic-256mb $6; Basic-1gb $19; Pro-4gb $55; storage $0.30/GB-month [R5, R6] | Existing 1 GB disk may make an upgrade about $6.30 or $19.30/month; dashboard quote is authoritative. |
| Connections | 100 direct below 8 GB RAM; larger tiers scale to 500 [R6] | Likely sufficient now; measure and bound Npgsql pools. |
| Pooling | Paid databases can enable free managed PgBouncer, transaction mode, 30,000 clients [R7] | Not needed initially. Direct route avoids session-semantics surprises. |
| Backup/PITR | Paid databases receive continuous PITR: 3 days on Hobby, 7 days on Pro/Scale/Enterprise. Restore creates a new database. Free has no managed backup [R8]. | A paid upgrade directly fixes the most urgent recovery gap. Repointing remains a manual, tested operation. |
| Logical exports | On-demand for paid databases, retained by Render for seven days; `pg_dump` remains available [R8] | Automate an independently retained logical backup; do not rely only on provider control plane. |
| HA | Pro/Accelerated database; async same-region standby; failover begins after 30 seconds; recent writes can be lost [R9] | Optional later. It is not zero-RPO and does not replace backup. |
| Maintenance | Non-HA databases usually have a few minutes of downtime; HA typically under one minute [R10] | Acceptable only if business confirms tolerance. |
| Security | AES-256 at rest; TLS for external connections; IP allowlists and private network [R11] | Current private-only same-region topology is a strength. Internal TLS is not explicitly established by cited docs. |
| Observability | CPU, memory, disk, connections, transactions, lock-delayed queries, slow queries, logs/metrics [R6, R12] | Enough for current stage if alerts and ownership are configured. |

### Free expiration

**FACT:** Render Free PostgreSQL expires 30 days after creation; after expiration it is inaccessible but upgradeable for 14 days, then deleted. Deleted database backups are not retained [R1, R8].

**INFERENCE:** A 19 August 2026 deadline implies creation around 20 July, subject to exact dashboard timestamps. This matches the Free lifecycle and is not evidence of general Render instability.

**RECOMMENDATION:** Upgrade the database instance. Upgrading only the workspace does not change the Free database lifecycle [R1].

### What a Render upgrade solves

- **FACT:** Removes 30-day expiry.
- **FACT:** Adds continuous PITR and on-demand logical exports.
- **FACT:** Allows managed pooling if later measured necessary.
- **FACT:** Allows storage/compute growth without changing database provider.
- **INFERENCE:** Avoids data-copy cutover, cross-provider networking, DNS/secret changes, migration freeze and rollback complexity.

### What a Render upgrade does not solve

- **FACT:** Basic tiers do not provide HA.
- **FACT:** PITR restore creates another database and requires deliberate application repointing.
- **FACT:** Render deletes backups with a deleted database; independent backup remains necessary.
- **FACT:** Render HA is asynchronous and may lose recent writes.
- **FACT:** It does not fix commission terms, obligation completeness, payment timestamps, missed cycles, payout evidence, preflight or business policy.
- **FACT:** It does not define Aqua's RTO/RPO or test its operational response.

## 6. Supabase Assessment

### Compatibility and operations

| Area | Official current position | Aqua implication |
| --- | --- | --- |
| PostgreSQL | PG17 is the current new-project/upgrade target; existing projects may still require a documented PG15-to-PG17 upgrade. PG14 support ended 1 July 2026 [S1, S2]. | Npgsql/EF Core, PL/pgSQL triggers and transaction advisory locks are compatible. Confirm the provisioned version; image/extension upgrades follow Supabase cadence. |
| Connections | Direct IPv6, shared session/transaction Supavisor, and paid dedicated transaction PgBouncer [S3, S4] | Render connectivity may require IPv4 add-on or session pooler. Direct Npgsql remains simplest. |
| Compute/storage | Pro plus Micro is effectively $25/month; Small about $30; 8 GB disk included, then $0.125/GB-month [S5, S6] | Competitive base price, but not cheaper than the least disruptive Render upgrade. |
| Daily backup | Pro 7 days, Team 14, Enterprise up to 30 [S7] | Better than Free Render, comparable basic operational coverage. |
| PITR | Add-on: 7 days $100/month, 14 days $200, 28 days $400; documented worst-case RPO two minutes [S5, S7] | Materially more expensive than Render's included short-window PITR. |
| Restore | In-place downtime; physical backup can clone to a new project [S7, S8] | Useful forensic path, still requires a tested runbook. |
| HA | Asynchronous read replicas are documented, but public docs do not establish automatic writer promotion/RTO for Pro [S9] | Do not infer HA from a read replica. Enterprise clarification would be needed. |
| Networking | CIDR restrictions; TLS enforcement; IPv4 add-on about $4/month; PrivateLink on Team/Enterprise [S10-S12] | A cross-provider public TLS route is more complex than current Render private networking. |
| Observability | Logs, database reports, metrics API; retention varies by plan [S13] | Good dashboard, but not a correctness advantage. |

### Supabase-specific value and cost

**FACT:** Aqua does not use Supabase Auth, generated REST/GraphQL, Realtime, Edge Functions, Storage or Supabase client SDKs.

**INFERENCE:** Supabase would add a polished database control plane, poolers, advisors, branches and restore tooling, but the application would pay the operational/security complexity of bundled schemas, roles and APIs without using the principal platform features.

**RECOMMENDATION:** If Supabase were selected, disable the Data API, enforce TLS, restrict network access, use a least-privileged application role, and audit grants/RLS/functions. PostgreSQL network restrictions do not protect Supabase HTTPS APIs [S10, S14].

**FACT:** Current Aqua URL conversion does not enforce strict TLS certificate validation. Supabase, Neon or any public hyperscaler endpoint therefore requires an application configuration change and a verified Npgsql `verify-full`-equivalent connection test before migration. This lowers the safety of a near-term cross-provider move.

**FACT:** Supabase changed new-project defaults so new tables are no longer automatically exposed to Data/GraphQL APIs, with full enforcement scheduled for 30 October 2026 [S15]. That reduces but does not eliminate the need to disable an unused API and audit privileges.

### Verdict on Supabase

**RECOMMENDATION:** Do not migrate Aqua to Supabase now. It is compatible but does not provide a material application-specific benefit that outweighs migration and networking risk. Previous use is not evidence that it is the best current platform.

## 7. Alternative Providers

### Serious alternatives

| Provider | Current fit | Recovery/HA | Operational burden | Decision |
| --- | --- | --- | --- | --- |
| Neon Launch/Scale | Strong database-focused alternative; standard Npgsql and transaction locks. | Launch 7-day PITR; Scale up to 30 days. Multi-AZ storage and automatic compute recreation; Scale has SLA [N1-N4]. | Low. Novel separated compute/storage behavior and public endpoint controls differ by plan. | Best migration candidate if Render later fails recovery/availability/support needs. Not justified now. |
| AWS RDS PostgreSQL | Mature conventional PostgreSQL. | 1-35 day PITR; Multi-AZ synchronous standby and typical 60-120 second failover [A1-A3]. | Medium due to VPC, security groups, KMS, monitoring and cross-cloud networking. | Serious if the application moves to AWS or formal HA becomes mandatory. |
| Azure PostgreSQL Flexible Server | Natural ASP.NET cloud choice. | 7-35 day PITR; synchronous zonal HA, generally 60-120 second failover [Z1-Z3]. | Low-medium, especially if API also moves to Azure. | Serious only with an Azure application-platform decision. |
| Google Cloud SQL PostgreSQL | Mature community PostgreSQL. | PITR and regional synchronous HA, expected about 60 seconds [G1-G3]. | Low-medium; connector/IAM/network integration needed. | Serious only with a GCP application-platform decision. |

### Options not preferred now

| Provider | Reason |
| --- | --- |
| AWS Aurora PostgreSQL | **INFERENCE:** Approximately $390/month for a credible two-instance HA shape before I/O, plus higher compatibility/operational lock-in. Aqua has no measured Aurora-specific requirement. |
| Railway PostgreSQL | **FACT:** Railway describes the standard PostgreSQL template as unmanaged. HA/PgBouncer/PITR exist but Aqua would own a Patroni/etcd/HAProxy/pgBackRest topology [W1-W3]. This conflicts with the desired low operational burden. |
| Self-managed PostgreSQL | **RECOMMENDATION:** Reject. Backups, patching, failover, monitoring, storage and security would require specialist ownership that Aqua does not currently demonstrate. |

### Connection behavior across candidates

**FACT:** Aqua uses `pg_advisory_xact_lock`, not session advisory locks. Transaction pooling can preserve transaction-lock semantics only when acquisition and all protected work use the same explicit transaction/backend. Provider failover or a broken connection releases locks everywhere.

**RECOMMENDATION:** Regardless of provider, test Npgsql reconnect, commit ambiguity, lock reacquisition and retry behavior. Provider HA changes availability, not application idempotency or atomicity.

## 8. Commission Engine Database Requirements

### Current mechanics

- **FACT:** Closed cycles are Friday through Thursday in `Africa/Johannesburg` (`LatestClosedCommissionWeekResolver.cs:23-72`).
- **FACT:** The worker processes only the latest fully closed cycle and has no automatic historical backfill (`WeeklyCommissionCalculationWorker.cs:101-103`; `AdminCommissionAppService.cs:682-729`).
- **FACT:** Each Tenant/programme calculation uses a separate transaction; failures do not roll back other Tenant/programme work (`WeeklyCommissionCalculationWorker.cs:117-191`, `233-285`).
- **FACT:** Worker and admin calculation acquire the same transaction-scoped lock (`WeeklyCommissionCalculationLock.cs:30-50`).
- **FACT:** Database uniqueness allows one period per Tenant/boundary, one commission per participation/period and one component per commission/level (`EntryCommissionConfiguration.cs:23-30`, `56-61`, `100-107`; equivalent Onyx configuration).
- **FACT:** AQGreen qualification is capped at Level 3 (`EntryNetworkQualificationEvaluator.cs:7-18`, `92-126`).
- **FACT:** Tenant and programme filters are explicit and mixed-Tenant network input fails closed (`WeeklyCommissionCalculator.cs:120-148`, `226-242`; `EffectiveProgrammeNetwork.cs:251-274`).
- **FACT:** Onyx travel synchronization runs in a separate transaction after commission attempts (`WeeklyCommissionCalculationWorker.cs:172-191`, `264-285`).

### Failure scenarios

| Scenario | Database behavior | Correctness conclusion |
| --- | --- | --- |
| Worker starts, transaction begins, rows insert, commit succeeds, worker crashes | Rows and unique cycle identity persist; lock releases at commit. Retry should find existing period. | **INFERENCE:** Ledger duplication is prevented. Logging/monitoring may miss success; reconciliation must use database state. |
| Transaction begins, work occurs, connection fails before commit | PostgreSQL rolls back when the session/transaction terminates. | **FACT:** Partial rows are not committed. Client may still face commit-outcome ambiguity if failure occurs during commit. Retry must rely on unique keys and persisted state. |
| Worker completes but response/logging fails, then retries | Existing period path returns no new records; unique indexes are final protection. | **FACT:** Expected to be idempotent for a complete visible period. Existing partial/imported corruption is not validated as complete. |
| Two API instances run the cycle | The same Tenant/programme lock serializes them; the second observes the committed period. | **FACT:** Real PostgreSQL lock tests prove same-key serialization and release. Unique indexes protect against implementation mistakes. |
| Database failover during a run | Connection and transaction end; advisory lock disappears; uncommitted work rolls back. | **FACT/INFERENCE:** All providers behave this way. HA shortens outage but does not alter correctness requirements. |

### Provider-independent blockers

1. **FACT:** Terms are resolved against the Friday after `PeriodEnd`, apparently allowing new Friday terms to govern the preceding cycle (`CommissionTermsResolver.cs:51-86`). This conflicts with current explanatory documentation (`docs/aqua-system/03-commission-engine-explained.md:83-95`).
2. **FACT:** AQGreen calculation checks loaded obligations but does not prove every expected obligation exists; missing/soft-deleted obligations can behave as compliant (`WeeklyCommissionCalculator.cs:137-148`; `EntryWeeklyCommissionCalculator.cs:74-95`).
3. **FACT:** Yoco `createdDate` is mapped to payment `ConfirmedAt`, while repository analysis says it is not proven to be the authoritative successful-payment occurrence time (`YocoPaymentsController.cs:114-124`; `weekly-commission-temporal-input-matrix.md:305-308`).
4. **FACT:** Missed weekly/monthly cycles and first historical Onyx travel qualification lack an approved recovery path.
5. **FACT:** Weekly preflight `Ready` does not include topology confirmation and does not compare exact financial rates (`CommissionBootstrapDtos.cs:94-114`; `AdminCommissionBootstrapAppService.cs:522-602`).
6. **FACT:** Monthly due day remains unresolved and the monthly worker must remain disabled (`docs/aqua-system/06-operations-and-enablement-runbook.md:138-168`).
7. **FACT:** Commission paid recording lacks authoritative payout-provider evidence and a unique payout identity (`AdminCommissionAppService.cs:241-304`).

**RECOMMENDATION:** Do not enable either programme worker as part of a database upgrade or migration. Database platform work and engine enablement must remain separate approvals.

## 9. Backup and Recovery Requirements

### Required recovery model

**RECOMMENDATION:** Use three distinct protections:

1. Provider-managed PITR for operator/application mistakes and recent corruption.
2. Independently retained logical backups for provider deletion, portability and forensic recovery.
3. Periodic isolated restore drills that validate migration history, constraints/triggers, application startup, key-ring access, payment identities, ledger totals and Tenant/Area isolation.

**RECOMMENDATION:** Daily independent logical backup is an appropriate initial cadence because current volume appears low, but this is not an approved RPO. Before automated commission or materially higher payments, the business must choose an RPO and decide whether Render's 3-day PITR and underlying WAL cadence are sufficient.

### Local recovery experiment

Date: 18 August 2026. Production was not accessed. Both programme worker flags were explicitly false.

| Step | Result |
| --- | --- |
| Start isolated PostgreSQL | `postgres:16-alpine`, loopback-only ports. |
| Apply application migrations | 47 migrations; approximately 23 seconds. |
| Source shape | 12 MB; one seeded Tenant; three seeded customers; no real payment/commission data. |
| Backup | PostgreSQL custom format; 300 KB; SHA-256 recorded during experiment; 0.42 seconds. Temporary archive deleted afterward. |
| Fresh restore | `pg_restore --clean --if-exists --no-owner`; 1.87 seconds. |
| Schema validation | 47 migrations, 8 non-internal triggers and 289 indexes. |
| Representative queries | Tenant/customer/payment/commission tables were queryable. Payment and commission counts were zero because no production data was used. |
| Application startup | Staging API started against restored database with monthly and weekly workers disabled; `/api/health` reported database and Data Protection key store healthy. |
| Commission semantics | Five focused PostgreSQL persistence/advisory-lock tests passed in 28 seconds. |

Reproduction outline used in this assessment, with local-only placeholder credentials:

```bash
docker run --name <source> -e POSTGRES_DB=aqua_recovery \
  -e POSTGRES_USER=aqua_local -e POSTGRES_PASSWORD=<local-only> \
  -p 127.0.0.1:<source-port>:5432 postgres:16-alpine

ASPNETCORE_ENVIRONMENT=Production \
ConnectionStrings__Default='Host=127.0.0.1;Port=<source-port>;Database=aqua_recovery;Username=aqua_local;Password=<local-only>' \
AQUA_INITIAL_ADMIN_PASSWORD='<local-only>' \
dotnet src/AqualLifeStyle.Migrator/bin/Release/net8.0/AqualLifeStyle.Migrator.dll -q

docker exec <source> pg_dump -U aqua_local -d aqua_recovery -Fc -f /tmp/aqua_recovery.dump
docker cp <source>:/tmp/aqua_recovery.dump /tmp/opencode/aqua_recovery.dump
docker cp /tmp/opencode/aqua_recovery.dump <target>:/tmp/aqua_recovery.dump
docker exec <target> pg_restore -U aqua_local -d aqua_restored \
  --clean --if-exists --no-owner /tmp/aqua_recovery.dump

dotnet test test/AqualLifeStyle.Tests/AqualLifeStyle.Tests.csproj \
  --configuration Release --no-build \
  --filter 'FullyQualifiedName~WeeklyCommissionCalculationPostgreSqlTests|FullyQualifiedName~WeeklyCommissionCalculationLockPostgreSqlTests'
```

**FACT:** The temporary archive checksum was `096e99660d2ac7b73987f8eb72892cd768da5ef9bbb3660f09929b38c4e3ea6d` and the archive and containers were deleted after validation. The tool-session output is the direct execution evidence; no durable test artifact was committed. A production drill must retain an authorised evidence record in controlled storage.

Failure points observed:

- **FACT:** Running the migrator in Development loaded the repository's ignored `.env` and selected its local connection instead of the supplied recovery connection. The experiment stopped without reaching that unrelated database and was rerun in Production with an explicit local connection.
- **FACT:** Initial Staging startup attempts lacked explicit auth/CORS settings because Staging-specific defaults are incomplete. Supplying non-secret local validation settings allowed startup.
- **INFERENCE:** A real recovery runbook must specify working directory/environment/configuration and must never depend on ambient `.env` state.

Limitations:

- **UNKNOWN:** Production backup size, data volume, role/grant fidelity, restore duration and provider-download time.
- **UNKNOWN:** Whether current production has all 47 migrations/triggers or can start after restore.
- **NOT VERIFIED:** Real payment rows, commission totals, PII, outbox decryption with production certificates, Yoco/Bird connectivity, production DNS/cutover, provider PITR and production RTO/RPO.

## 10. Cost Analysis

Prices are public list prices on 18 August 2026, before tax, support, egress, monitoring exports, restored-instance overlap and discounts. Dynamic calculator values require confirmation before purchase.

| Candidate | Small/current-scale scenario | Moderate/HA scenario | Notes |
| --- | ---: | ---: | --- |
| Render paid | Existing 1 GB disk: Basic-256mb about $6.30; Basic-1gb about $19.30. New defaults can be about $10.50/$23.50. | Pro-4gb + 100 GB about $85 primary; HA roughly $170, plus possible $25 workspace requirement. | 3-day Hobby or 7-day higher-workspace PITR included. Dashboard quote required. |
| Supabase | Pro+Micro about $25; Pro+Small about $30. | Small + 7-day PITR about $130; Medium/100 GB about $86.50 before PITR; read replica adds similar compute/storage. | PITR starts at $100/month. No documented Pro writer auto-failover guarantee. |
| Neon | Launch low-use always-on example about $25; Scale about $46. | Launch 0.5 CU/50 GB about $66; Scale about $109. | Usage-sensitive compute/history storage. Scale adds SLA/network/telemetry controls. |
| AWS RDS PostgreSQL | Single-AZ `db.t4g.small` + 20 GB about $26 in `us-east-1`. | Multi-AZ `db.m7g.large` + 100 GB about $269. | Excludes cross-cloud networking and AWS operational services. |
| Azure Flexible Server | Burstable B2s + 32 GB about $53 in East US. | General Purpose 2 vCPU/8 GB, zonal HA/100 GB about $283. | Exact target region likely differs; use calculator. |
| Google Cloud SQL | Shared-core + 10 GB about $27 in `us-central1`. | 2 vCPU/8 GB regional HA/100 GB about $236. | Lowest-confidence dynamic estimate; reproduce in calculator. |
| Railway | $20 Pro minimum for a small standalone node. | HA is usage-based; three data nodes alone about $68 at moderate shape, before six coordination/proxy services. | Standard database is unmanaged. |
| Aurora PostgreSQL | One continuously active 0.5 ACU Serverless v2 instance about $46 plus I/O. | Writer + cross-AZ replica/100 GB about $390 plus I/O. | Not justified by current requirements. |

**INFERENCE:** At Aqua's apparent scale, engineering time and migration/cutover risk dominate the difference between a $19 Render database and a $25-$46 alternative. HA changes the cost class to roughly $170-$390/month and should be driven by an approved availability requirement, not platform fashion.

## 11. Risk Analysis

| Risk | Likelihood | Impact | Provider dependence | Response |
| --- | --- | --- | --- | --- |
| Free Render database expires/deletes | High if report is accurate | Critical | Current plan | Upgrade immediately after dashboard verification; create authorised backup first where possible. |
| No proven current production restore | Medium | Critical | Operational | Run provider PITR/logical restore drill with controlled production backup. |
| Commission correctness gaps | High if enabled now | Critical | None | Keep workers disabled; resolve and test blockers. |
| Commit outcome unknown after disconnect | Low-medium | High | All providers | Durable idempotency, uniqueness and reconciliation; test retries/failover. |
| Basic Render instance resource pressure | Unknown | Medium-high | Plan sizing | Capture CPU, memory, disk, connections, locks and slow queries; resize before enabling workers if needed. |
| Non-HA maintenance/zonal outage | Certain maintenance; unknown failure | Medium-high | Selected topology | Obtain downtime tolerance; add HA only if justified. |
| Cross-provider migration inconsistency | Medium | Critical | Migration strategy | Freeze writes or use validated logical replication; checksums/counts and rollback window. Avoid now. |
| Supabase Data API exposure | Low if disabled, higher if misconfigured | High | Supabase | Disable API; grants/RLS audit. |
| Provider deletion includes backups | Possible operator error | Critical | Render and others vary | Independent logical backup in separate account/location. |
| Data Protection certificate absent after restore | Medium without runbook | High | Operational | Preserve database keys plus current/previous certificates and validate email/auth links. |

## 12. Decision Matrix

Scores are 1 (poor) to 5 (strong). Weighted total is out of 100. They compare realistic current configurations, not maximum enterprise configurations.

Score anchors: 1 means the candidate materially fails the current requirement; 3 means acceptable with meaningful gaps/manual work; 5 means strong documented support for the requirement. Half points distinguish a material but non-disqualifying difference. Scores remain architecture judgement, not provider guarantees.

| Criterion | Weight | Why this weight exists |
| --- | ---: | --- |
| PostgreSQL correctness/compatibility | 18 | Money, eligibility, triggers and locks require exact semantics. |
| Recovery quality | 17 | Irreplaceable payment, approval, ledger and key-ring state. |
| Reliability/HA | 12 | Outage interrupts payment/admin workflows, but approved RTO is unknown and automation is disabled. |
| Security/networking | 9 | Identity/payment data must not be casually exposed. |
| Operational burden | 10 | Small team; specialist database/SRE ownership is not evident. |
| Migration risk | 10 | Current data is financially meaningful and production recovery evidence is incomplete. |
| Cost now | 9 | Current scale appears very small. |
| Cost at growth | 6 | Moderate growth should remain conventional PostgreSQL. |
| Observability | 5 | Needed for capacity, locks and recovery, but external tooling can supplement. |
| Developer experience | 2 | Helpful, not a substitute for correctness. |
| Lock-in/exit | 2 | Standard PostgreSQL keeps exit practical. |

| Candidate | Correctness 18 | Recovery 17 | Reliability 12 | Security 9 | Ops 10 | Migration 10 | Cost now 9 | Growth cost 6 | Observe 5 | DX 2 | Exit 2 | Weighted total |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Render paid, direct | 5.0 | 4.0 | 3.0 | 4.5 | 4.5 | 5.0 | 5.0 | 4.0 | 3.5 | 4.5 | 5.0 | **87.0** |
| Neon Scale | 4.5 | 5.0 | 4.0 | 5.0 | 4.5 | 3.0 | 3.5 | 3.0 | 4.5 | 4.0 | 3.5 | **84.2** |
| AWS RDS PostgreSQL Multi-AZ | 5.0 | 5.0 | 4.5 | 5.0 | 3.0 | 2.5 | 2.0 | 2.5 | 5.0 | 3.0 | 4.5 | **80.4** |
| Supabase Pro/Small | 5.0 | 3.5 | 2.0 | 4.0 | 4.0 | 3.0 | 4.0 | 3.0 | 4.0 | 4.0 | 4.0 | **73.9** |
| Railway Pro standalone | 5.0 | 3.0 | 2.0 | 3.5 | 2.0 | 2.5 | 4.0 | 4.0 | 2.5 | 4.0 | 4.5 | **66.2** |

**ASSUMPTION:** Render's score models a paid Basic primary with included PITR, not HA. Neon models Scale because Launch lacks the network/support/SLA controls used in its stronger case. RDS models Multi-AZ and its approximately $269/month cost. The matrix intentionally compares purchasable strategies rather than equal hardware; the separate cost table exposes current and HA cost shapes. Decimal totals support, but do not determine, the recommendation.

**CHALLENGE:** If Aqua requires automatic zonal failover with an explicit SLA immediately, Render Basic's reliability score falls below the minimum and Neon Scale or a colocated hyperscaler becomes stronger. No such approved requirement was found.

## 13. Challenge of the Recommended Decision

### Strategy A: Stay on Render and upgrade

**Strongest case:** Lowest migration risk, private same-region API/database networking, ordinary PostgreSQL compatibility, paid PITR, adequate likely capacity, one provider for API/database operations.

**Counterargument:** Basic is non-HA, Render PITR retention is short, HA is asynchronous, and the control plane is less database-specialized than Neon/hyperscalers.

**Result:** Still best now because current urgent defect is the Free plan, not demonstrated platform failure.

### Strategy B: Move to Supabase

**Strongest case:** Good PostgreSQL dashboard, inexpensive base compute, multiple poolers, branches, advisors, daily backups and optional PITR.

**Counterargument:** Aqua uses none of Supabase's application platform, 7-day PITR adds $100/month, Pro writer HA is not clearly documented, cross-provider networking replaces current private connectivity, and Data API/grants add an irrelevant security surface.

**Result:** Rejected for this horizon unless a wider application architecture intentionally adopts Supabase services.

### Strategy C: Move elsewhere

**Strongest case:** Neon Scale offers stronger managed recovery architecture at moderate cost; RDS/Azure/Cloud SQL offer conventional synchronous zonal HA and clearer failover contracts.

**Counterargument:** Neon introduces a different compute/storage operating model and migration; hyperscalers add networking/account/SRE complexity and substantial HA cost. None fixes the engine blockers.

**Result:** Neon is the preferred future shortlist candidate. Hyperscaler PostgreSQL should follow an application-platform move, not cause one.

### Strategy D: Stay temporarily and fix only blockers

**Strongest case:** Avoid infrastructure churn while engine correctness is resolved.

**Disproof:** If the 19 August Free expiry is accurate, doing nothing risks immediate outage and eventual deletion without managed backup.

**Revised strategy:** Make the smallest infrastructure correction now: upgrade the existing database. Defer migration and HA decisions while fixing application blockers and measuring the workload.

### Attempt to disprove Render

Render would lose if any of the following becomes a confirmed MUST within 12-24 months:

- synchronous zero/near-zero-RPO zonal failover;
- recovery retention longer than seven days without independent tooling;
- a contractual database SLA/support requirement unavailable at an acceptable Render tier;
- a target application cloud where cross-provider networking is materially worse;
- measured workload/cost showing Render materially inferior at required capacity.

**UNKNOWN:** None of these has been established. Therefore migration now would solve hypothetical future requirements while increasing present risk.

## 14. Final Recommendation

**Decision: B. Upgrade Render.**

1. Verify the dashboard resource and deadline without changing it.
2. Obtain an authorised logical backup before upgrade if still accessible; retain it outside the database resource. Validate its archive listing immediately. Attempt an isolated restore before upgrade if the exact expiration window safely permits it; do not allow a lengthy drill to cause Free-resource deletion.
3. Upgrade the database instance to `Basic-1gb` unless current metrics support another paid size.
4. Keep direct private connectivity and both programme workers disabled.
5. Verify migration head, health, critical counts/invariants and backup/PITR visibility.
6. If emergency timing prevented a full pre-upgrade restore, perform an isolated Render PITR or logical restore drill within 48 hours, not two weeks, and record measured restore/startup times. Paid Render remains transitional and below the minimum standard until this passes.
7. Capture 30 days of database metrics before deciding on pooling, Pro compute, HA or another provider.
8. Resolve engine blockers independently. Do not combine worker enablement with database upgrade.

**RECOMMENDATION:** Re-evaluate Neon Scale and colocated managed PostgreSQL only when Aqua has approved RTO/RPO, measured workload and a reason Render cannot meet them. Schedule a decision checkpoint in 3-6 months, not an automatic migration.

## 15. What We Would Gain

By upgrading Render:

- No Free-plan expiry.
- Provider-managed short-window PITR and logical exports.
- More predictable capacity on `Basic-1gb`.
- No data copy or application connection-provider change.
- Existing private same-region networking and deployment topology remain intact.
- Lowest chance of disturbing payments, approvals, Tenant/Area state and ledger history.

## 16. What We Would Lose

Compared with moving to Neon Scale or hyperscaler HA:

- No automatic HA on Basic.
- Shorter provider PITR retention.
- Less explicit failover/SLA posture.
- Fewer database-specialist branching/recovery features.

Compared with staying Free:

- Approximately $19-$24/month for the recommended initial database shape rather than $0.

**INFERENCE:** This is an appropriate trade: a production database carrying payment and commission state cannot reasonably optimize for zero database cost.

## 17. Migration/Upgrade Plan

### Phase 1 - preparation

- **Automate:** read-only inventory queries, schema/migration head, table counts, key constraints and digest scripts.
- **Manual approval:** database tier and budget; backup handling; maintenance window.
- **Never without explicit production approval:** `pg_dump`, IP allowlist changes, tier upgrade, restore, connection change, migration, worker flag change.
- Confirm database identity, region, plan, version, storage, expiry, current connections and current worker settings.
- Confirm approved region, DPA/POPIA posture and support escalation owner.
- Define abort criteria and an operator/approver pair.

### Phase 2 - protect current state

- Create a custom-format logical backup from an authorised Render-side process or temporarily authorised endpoint.
- Record archive checksum, PostgreSQL client/server versions, start/end time and encryption/storage location without exposing credentials or data.
- Verify the archive with `pg_restore --list` and restore it to an isolated matching-major PostgreSQL instance when the deadline permits. If expiry risk forces upgrade first, record that exception and complete the restore within 48 hours.

### Phase 3 - upgrade database

- Upgrade the existing database to paid `Basic-1gb`; do not merely upgrade the workspace.
- Expect a restart/brief interruption.
- Do not enable PgBouncer, HA, workers or a PostgreSQL major upgrade in the same change.
- Treat the paid database as transitional until the full restore, pool and credential controls below pass.

### Phase 4 - schema and application verification

- Verify `/api/health`, migration history, triggers, indexes and database version.
- Verify API deployment still uses Render's private `connectionString`.
- Confirm Data Protection key-store readiness and preserve certificate settings.
- Confirm both programme workers remain false/unset as intended.
- Bound aggregate Npgsql pools for the API replica count while reserving connections for migrations and operations; verify against live connection metrics.
- Design and test separate least-privileged runtime and migration roles. The existing single Blueprint `DATABASE_URL` does not yet provide this separation; changing it is a reviewed application/deployment task, not part of the emergency tier click.

### Phase 5 - data-integrity verification

- Compare pre/post counts and digests for Customers, Areas/assignments, participations, payments, checkouts, webhook receipts, approval decisions, obligations, commission periods/rows/components, outbox and key ring.
- Verify each payment provider/external-reference identity remains unique.
- Verify commission period and component uniqueness.
- Verify every participation's Tenant and Area resolvability with reviewed queries.
- Verify P0001/legacy backup invariants without modifying history.

### Phase 6 - recovery verification

- Confirm PITR is visible and retention matches the workspace plan.
- Restore to a new database at a safe recovery point.
- Start a non-production API against it with external delivery and programme workers disabled.
- Run representative payment/commission read-only queries and application PostgreSQL tests.
- Record actual backup, restore, validation and startup times.

### Phase 7 - operations hardening

- Alert on storage, connections, CPU/memory, lock waits, slow queries and database health.
- Retain an independent daily logical backup outside the Render database lifecycle.
- Assign named owners for backup success, restore tests and incident decisions.
- Set quarterly restore tests initially; increase frequency before automated financial processing.
- Do not use a cross-provider public endpoint until Aqua replaces `SSL Mode=Prefer;Trust Server Certificate=true` with strict certificate and hostname verification and proves the deployed Npgsql connection.

### Phase 8 - later sizing/HA decision

- After 30 days of metrics, decide whether Basic-1gb is adequate.
- Model Pro-4gb HA only after RTO/RPO approval.
- Test Npgsql reconnect/commit ambiguity before HA enablement.

### Phase 9 - no decommission

- No old database is decommissioned because this plan upgrades in place.
- Do not delete any backup or resource until retention and restore evidence are independently confirmed.

## 18. Rollback Plan

### Upgrade rollback

**FACT:** Render compute/storage changes and storage increases are not equivalent to an application rollback; storage cannot be reduced [R6].

- If the paid instance is unhealthy, stop writes by taking the API out of service rather than allowing uncertain payment/commission transitions.
- Diagnose configuration/version/capacity before repointing.
- If data is damaged, restore PITR to a new database; Render does not overwrite the source.
- Validate the restored database before manually changing the API connection.
- Keep the original suspended/intact during the rollback window.
- Reconcile Yoco events, checkouts and approvals occurring around the cutover before reopening writes.
- Keep programme workers disabled throughout.

### If a future provider migration is approved

- Use a write freeze for this small workload unless measured downtime makes logical replication necessary.
- Provision and migrate schema first, then roles/grants, then data.
- Compare row counts, key financial digests, sequences and constraints.
- Run the migrator in no-op/current-head mode and start a non-production API.
- Cut over one connection setting under manual approval.
- Keep the old database read-only and recoverable for an approved rollback window.
- Never dual-write financial state without a separately designed protocol.

## 19. Minimum Production Database Standard

### MUST HAVE

- Managed genuine PostgreSQL compatible with all current migrations, triggers and transaction advisory locks.
- Paid, non-expiring resource.
- Automated provider backups with explicit retention.
- Successful isolated restore test using an actual production backup under controlled access. An emergency paid upgrade may precede this only to avoid imminent Free-resource deletion; the database remains in a documented transitional state until the restore passes.
- Documented backup owner, restore owner and recovery steps.
- Private networking or restricted TLS endpoint with hostname/certificate verification.
- Least-privileged application and migration credentials; secrets outside source/logs.
- Encryption at rest and secure backup storage.
- Capacity/alerts for storage, connections, CPU/memory, lock waits and availability.
- Bounded aggregate connection capacity across all API instances and operational tools.
- Preservation of Data Protection keys and their current/previous encryption certificates.
- Workers disabled until separately approved; recovery cannot accidentally execute financial jobs.

### SHOULD HAVE

- PITR with a short, known recovery window.
- Independent logical backup outside the provider resource lifecycle.
- Quarterly restore exercise now, more frequently when payment/commission volume rises.
- Deletion protection or equivalent two-person operational control.
- Slow-query and PostgreSQL lock visibility.
- Documented RPO/RTO approved by business/finance/operations.
- Automated HA when approved RTO cannot tolerate maintenance or zonal replacement.

### NICE TO HAVE

- Managed PgBouncer after measured connection pressure.
- Read replicas for measured reporting contention.
- Database branches for representative non-production tests.
- Cross-region DR after an approved regional-disaster requirement.
- Advanced performance advisors and long-term telemetry export.

## 20. Open Questions

1. **UNKNOWN:** Is the deployed database still Free, and is 19 August 2026 the exact dashboard expiration timestamp?
2. **UNKNOWN:** What are the deployed PostgreSQL version, size, disk use, peak connections, CPU/memory, WAL rate and slow queries?
3. **UNKNOWN:** What backup exists now, who can access it, where is it retained, and has it been restored since the latest migration?
4. **UNKNOWN:** What maximum tolerable data loss and downtime will the business approve for payments, approvals and commission ledger state?
5. **UNKNOWN:** Is planned maintenance downtime acceptable, making non-HA reasonable for the next stage?
6. **UNKNOWN:** Are Render workspace and HA eligibility/pricing consistent with the current Blueprint documentation conflict?
7. **UNKNOWN:** What is the exact aggregate Npgsql pool capacity across current/future API instances?
8. **UNKNOWN:** Is Tenant-keyed commission activation intentionally retained after Area separation?
9. **UNKNOWN:** What is the authorised commission terms boundary rule?
10. **UNKNOWN:** What monthly due day and missed-cycle recovery policy does the business approve?
11. **UNKNOWN:** What timestamp from Yoco is authoritative for payment occurrence at a commission cutoff?
12. **UNKNOWN:** Who owns production database alerts, restore drills, reconciliation and payout evidence?
13. **UNKNOWN:** What formal POPIA/DPA, data-residency and provider support-escalation requirements apply?
14. **UNKNOWN:** Will Aqua ever use a separate database per Tenant, and who will own backup/restore across all such databases?

## 21. Evidence and Sources

### Repository evidence

- `AGENTS.md`
- `render.yaml`
- `docker-compose.yml`
- `docs/deployment.md`
- `docs/development/validation.md`
- `docs/operations/aqgreen-production-reconciliation/README.md`
- `docs/aqua-system/03-commission-engine-explained.md`
- `docs/aqua-system/05-data-history-migrations-and-legacy-members.md`
- `docs/aqua-system/06-operations-and-enablement-runbook.md`
- `docs/aqua-system/07-verification-decision-and-risk-register.md`
- `docs/aqua-system/08-area-and-tenant-boundaries.md`
- `docs/verification/weekly-commission-temporal-input-matrix.md`
- Current EF Core mappings, migrations, workers, application services and PostgreSQL tests cited inline.
- Git history: P0001 correction `925aabd`; Area separation `d8618b3`; AQGreen Level 3 correction `144f9c3`; weekly automation `0f9580b`; current `main` `b88aeb3`; archived unreviewed WIP `df70e6b`.

### Render official sources

- [R1] Free instances: https://render.com/docs/free#free-postgres
- [R2] Status: https://status.render.com/
- [R3] Blueprint specification: https://render.com/docs/blueprint-spec
- [R4] PostgreSQL versions/upgrades: https://render.com/docs/postgresql-upgrading
- [R5] Pricing: https://render.com/pricing#render-postgres
- [R6] PostgreSQL creation, storage, metrics and connections: https://render.com/docs/postgresql-creating-connecting
- [R7] Connection pooling: https://render.com/docs/postgresql-connection-pooling
- [R8] Recovery and backups: https://render.com/docs/postgresql-backups
- [R9] High availability: https://render.com/docs/postgresql-high-availability
- [R10] Platform maintenance: https://render.com/docs/platform-maintenance
- [R11] Private networking: https://render.com/docs/private-network
- [R12] Logging/metrics retention: https://render.com/docs/logging and https://render.com/docs/service-metrics

### Supabase official sources

- [S1] PG14 retirement: https://supabase.com/changelog/45827-deprecation-notice-support-for-postgres-14-ending-on-1st-july-2026
- [S2] Upgrades: https://supabase.com/docs/guides/platform/upgrading
- [S3] Connecting: https://supabase.com/docs/guides/database/connecting-to-postgres
- [S4] Connection management: https://supabase.com/docs/guides/database/connection-management
- [S5] Pricing: https://supabase.com/pricing
- [S6] Compute and disk: https://supabase.com/docs/guides/platform/compute-and-disk
- [S7] Backups/PITR: https://supabase.com/docs/guides/platform/backups
- [S8] Clone project: https://supabase.com/docs/guides/platform/clone-project
- [S9] Read replicas: https://supabase.com/docs/guides/platform/read-replicas
- [S10] Network restrictions: https://supabase.com/docs/guides/platform/network-restrictions
- [S11] SSL enforcement: https://supabase.com/docs/guides/platform/ssl-enforcement
- [S12] IPv4 and PrivateLink: https://supabase.com/docs/guides/platform/ipv4-address and https://supabase.com/docs/guides/platform/privatelink
- [S13] Metrics/logs: https://supabase.com/docs/guides/monitoring-and-debugging/metrics and https://supabase.com/docs/guides/monitoring-and-debugging/logs
- [S14] Data API security: https://supabase.com/docs/guides/api/securing-your-api
- [S15] Data API exposure change: https://supabase.com/changelog/45329-breaking-change-tables-not-exposed-to-data-and-graphql-api-automatically

### Other provider official sources

- [N1] Neon plans: https://neon.com/docs/introduction/plans
- [N2] Neon backup/restore: https://neon.com/docs/guides/backup-restore
- [N3] Neon HA: https://neon.com/docs/introduction/high-availability
- [N4] Neon pricing: https://neon.com/pricing
- [A1] RDS PostgreSQL pricing: https://aws.amazon.com/rds/postgresql/pricing/
- [A2] RDS PITR: https://docs.aws.amazon.com/AmazonRDS/latest/UserGuide/USER_PIT.html
- [A3] RDS Multi-AZ/failover: https://docs.aws.amazon.com/AmazonRDS/latest/UserGuide/Concepts.MultiAZSingleStandby.html and https://docs.aws.amazon.com/AmazonRDS/latest/UserGuide/Concepts.MultiAZ.Failover.html
- [Z1] Azure PostgreSQL pricing: https://azure.microsoft.com/en-us/pricing/details/postgresql/flexible-server/
- [Z2] Azure backup/restore: https://learn.microsoft.com/en-us/azure/postgresql/backup-restore/concepts-backup-restore
- [Z3] Azure reliability/HA: https://learn.microsoft.com/en-us/azure/reliability/reliability-database-postgresql
- [G1] Cloud SQL pricing: https://cloud.google.com/sql/pricing
- [G2] Cloud SQL PITR: https://docs.cloud.google.com/sql/docs/postgres/backup-recovery/pitr
- [G3] Cloud SQL HA: https://docs.cloud.google.com/sql/docs/postgres/high-availability
- [W1] Railway PostgreSQL: https://docs.railway.com/databases/postgresql
- [W2] Railway HA: https://docs.railway.com/databases/postgresql-ha
- [W3] Railway PITR: https://docs.railway.com/volumes/point-in-time-recovery
- PostgreSQL advisory locks: https://www.postgresql.org/docs/current/explicit-locking.html#ADVISORY-LOCKS

## Final Practical Answers

### Given the current Aqua application, what database platform should we use for the next 12-24 months, and why?

**RECOMMENDATION:** Use paid Render PostgreSQL, initially `Basic-1gb`, for the next 12-24 months. It preserves genuine PostgreSQL semantics, current private same-region connectivity and the lowest-risk deployment path; paid Render adds the immediate missing controls, non-expiry and PITR, at a cost proportionate to the evidenced workload. Supabase adds little because Aqua does not use its application platform. Neon is the best future migration candidate if measured recovery/availability needs exceed Render. Hyperscaler HA is premature without an application-cloud decision and approved RTO/RPO.

### What am I actually blocked on right now?

**FACT/RECOMMENDATION:** Infrastructure is blocked on confirming and upgrading the reportedly expiring Free Render database and proving a current restorable backup. Commission enablement is separately blocked on terms-boundary correctness, obligation completeness, payment timestamp authority, topology/preflight evidence, missed-cycle recovery, monthly due policy, observability and payout/reconciliation controls. You are not blocked on selecting a new database provider.

### What do I not need to solve yet?

**RECOMMENDATION:** Do not solve sharding, read replicas, cross-region DR, Aurora, Supabase application services, database branching, serverless scale-to-zero, external PgBouncer or hyperscaler migration yet. Do not enable workers, migrate production or combine infrastructure hardening with commission-engine activation.
