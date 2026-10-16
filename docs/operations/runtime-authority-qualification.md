# Runtime authority qualification

Starting source: `700fadb58ced50ae0d63319ad66db3f0674687ad`, PR106. The current owner
instruction authorizes repository corrections and isolated qualification, not a live
release, provider setting/credential change, production database session or operation.
**Production runtime authority: BLOCKED.** Source guards are not independent IAM.

## Governing foundation and evidence vocabulary

The [CI/CD safety contract](cicd-safety-contract.md) supplies the Christian ethical
foundation. Proverbs22:3 motivates prudence; Luke16:10 faithfulness in small and large
responsibilities; 1Corinthians4:2 stewardship; Proverbs11:1 financial integrity;
1Thessalonians5:21 examination; Romans13:10 care against harming others. Applying
these principles to process identities, dispatch, retries and least privilege is a
theological interpretation and an engineering decision, not a biblical specification
of a provider or architecture. Scripture does not guarantee technical immunity.
Formal security guidance, hypotheses, source inspection, isolated experiments and
live observations remain different evidence categories; no certification is claimed.

PASS means the stated bounded test/inspection succeeded; FAIL means contrary evidence;
UNKNOWN means evidence is absent; BLOCKED means an unmet boundary prevents acceptance.
IMPLEMENTED and TESTED never imply DEPLOYED, ENABLED or PRODUCTION VERIFIED. A
configuration value is a declaration, not proof that a powerful process cannot act.

## Observed production and causal finding

Authenticated GET-only inspection at2026-10-16T19:18:23Z still identifies API source
`1251fb82979e18be53d1d0e7f5c6fab2d0c50a58`. API200 reports Healthy DB/Redis/key-store,
Production, no recovery header. Render declarations: recovery=false; Bird=true;
Yoco=test; Bird/Yoco credentials present; financial, background jobs, weekly, monthly,
demo, pilot, D10 commissioning and demo-seed flags=false. No live message, payment,
worker run or database privilege was observed. Relevant startup/worker source is
unchanged between that deployed source and the starting qualified source; this is
source comparison, not attestation of running binaries. See the safe
[evidence package](../verification/runtime-authority-20261016/README.md).

ROOT CAUSE / PRE-EXISTING: the host set ABP IsJobExecutionEnabled=false but registered
four independent periodic workers unconditionally. Their bodies had separate, partial
guards. A direct outbox call bypassed registration entirely. The Bird gateway used
an Enabled options snapshot with no execution-time recovery/background check. Weekly/
monthly execution lacked the financial hold. Live Yoco and legacy referral-award paths
also lacked the financial hold. Seventeen discriminating tests failed before the
correction, including controlled Bird HTTP acceptance despite held configuration.
These are source/isolated execution findings, not claims of production exploitation.
An additional direct-financial-service negative failed before its correction, proving
that a worker or callback gate alone cannot constrain IoC-resolved services. Calculators,
monthly scheduler, travel synchronizer and payment-application entry points now check
the same financial hold. Explicitly isolated pilot test-money paths retain their
existing representation checks; they grant no real provider or ordinary payment use.
The disposable demo calculator uses a default-disabled composition capability,
installed only after validating the dedicated demo environment, loopback database
with its exact disposable name and loopback client. It revalidates those constraints
at execution. This preserves existing AQGreen simulation without enabling global
financial authority; Onyx and recovery remain held. A plain demo Boolean cannot
grant that calculator exception.

Key implementation boundaries are the shared
[execution guards](../../AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Core/Configuration/RuntimeOperationGuards.cs),
[worker registration](../../AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Web.Host/Startup/AqualLifeStyleWebHostModule.cs),
[outbox processor](../../AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Application/Email/TransactionalEmailOutbox.cs),
[Bird adapter](../../AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Web.Host/Email/BirdTransactionalEmailDeliveryGateway.cs),
[financial calculator](../../AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Application/Admin/Commissions/WeeklyCommissionCalculator.cs)
and [validated demo capability](../../AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Web.Host/AQGreenV2Demo/AQGreenV2DemoCalculationMode.cs).

## Capability matrix: trigger, grant and hold

The two tables form one inventory keyed by ID. Each row answers trigger, credential,
grantor, disablement and restart/reconnection behavior, then effects, Tenant/Area,
evidence and unknowns. Paths below are relative to
`AqualLifeStyle/9.4.2/aspnet-core/src/`. No new recurring release authorization exists.
"Settings custodian" means the persistent configuration owner; candidate application
code shares its process capabilities unless a separate external boundary exists.

| ID / component | Execution trigger | Credential/capability; grantor | Hold and deployment/restart behavior |
| --- | --- | --- | --- |
| R01 API bootstrap/DI | Container entrypoint → Program.Main → Startup → ABP module Pre/Initialize/PostInitialize | Process environment, DB/Redis, JWT and key-decryption certificate; Render/runtime custodian | Configuration validates before ABP initialization. Production WebCore sets SkipDbSeed. Same persistent held settings on restart do not grant workers. Malicious code can bypass source checks; external IAM remains necessary |
| R02 schema preflight/D10 startup | WebHostModule.PostInitialize | Host DB SELECT; database custodian; D10 source from separately controlled operator evidence | Exact migration history mismatch rejects startup without migrate/repair. D10 reads authority/population and fails if unresolved existing V2 state. No cutover is granted by deployment |
| R03 host/tenant seed and demo fixture | EF module PostInitialize if SkipDbSeed=false; SeedHelper from explicit migrator; demo seeder in demo-only host | Schema/DML/administrator bootstrap credentials; maintainer | Production/recovery API skips host seed. Demo/pilot validation forbids accidental Production selection. New API image omits migrator executable. Maintenance overlay and identity are explicit; restart does not include them |
| R04 EF migrator | Separately invoked maintenance image/Program/MultiTenantMigrateExecuter | Separate schema-owner/migrator role; database custodian | Recovery denies before bootstrap. Ordinary API Compose has no migration service/dependency/connection. Dedicated overlay requires migration credentials only when loaded. DB role permissions, not image absence, must deny arbitrary API DDL |
| R05 container health CLI | Docker HEALTHCHECK invokes Program --health-check | HTTP GET to local API by default; configured HEALTHCHECK_URL controlled by operator | Returns before creating a host, so no second DI/startup/worker instance. Failed health returns nonzero. A custom URL is outbound-network authority and must be allowlisted by infrastructure |
| R06 API health/dependencies/key ring | GET /api/health; Data Protection key use/rotation | DB connectivity/SELECT key IDs, Redis PING; certificate and persistent key-store DML for actual key creation | Readiness probe neither seeds nor repairs nor reads key XML. Key-ring machinery can create encrypted DataProtectionKeys separately from the readiness query. Network reconnect restores existing capabilities, never independent permission |
| R07 ABP jobs/framework timers | ABP job manager and framework startup/cache lifecycle | Same process DB/Redis; persistent BackgroundJobs declaration | IsJobExecutionEnabled requires recovery off and BackgroundJobs=true; pilot disables it. No custom BackgroundJob/queue consumers found. Framework cleanup/cache/key rotation are not proven globally disabled by this flag |
| R08 Yoco stale monitor | Independent periodic worker, default15min scan | Host DB aggregate SELECT, logs; DB/runtime custodian | Registration AND body now require recovery off/background=true. Pilot denies. No provider call or payment application; missing background declaration holds it |
| R09 Bird outbox worker/processor |30s timer; direct ProcessPendingAsync; retry/stale claim recovery | DB outbox SELECT/claim/update and body decryption; Bird gateway capability; DB/provider custodians | Registration/body/direct processor require recovery off/background=true/Bird=true and valid delivery cutoff. Recheck before each claim/send. Held processing throws before initial query/claim. Configuration change cannot revoke an already accepted external request |
| R10 direct ABP/Bird send | Framework IEmailSender → BirdAbpEmailSender → gateway; any direct gateway call | Bird key, enabled options, HttpClient; provider/runtime custodian | Gateway now requires execution-time background/Bird/recovery/cutoff in addition to options. Disabled/missing authority rejects before HTTP. Direct sender cannot bypass through registration omission. No raw provider data logged |
| R11 weekly commissions/travel | RunOnStart=true and periodic timer; calculator/synchronizer services | Host-wide tenant enumeration, per-programme DB writes, advisory lock; database + commissioning custodians | Worker registration/body require background AND financial AND weekly switches, recovery off; pilot/D10 guards preserved. It never sends money, but writes periods/ledger/eligibility. Missing enablement holds. Separate identity/grants required for stronger authority |
| R12 monthly obligations | Periodic timer; direct scheduler/service | DB participation/obligation writes, scheduling lock and effective policy; DB/commissioning custodian | Worker registration/body require background AND financial AND monthly switches, recovery off; pilot denies. Unresolved due policy does not schedule, but assessment of existing obligations is intentional when authorized. Restart cannot grant missing flags |
| R13 Yoco checkout | Authorized member request → real checkout adapter | Yoco test/live key + supported source reference and callback URLs; provider custodian | Recovery/demo deny real provider. Mode/key prefix and response mode must match. Live mode now requires financial commission hold released; test remains an explicitly distinct provider path, not business payment. Test key still grants outbound provider calls unless network/credential custody denies them |
| R14 Yoco webhook/payment application | Signed raw-body callback, provider retry; processor | Webhook verification secret; persisted checkout identity, amount/purpose/currency/mode; provider + DB custodian | Invalid/missing secret/signature/config rejects; recovery denies. Live notification now checks financial hold before repositories, held callback503. Test notification matches checkout but never applies business payment. Duplicate/conflicting facts retain existing receipt/idempotency/transaction rules |
| R15 legacy enquiry referral event | Authorized ConvertToCustomer request → deferred EnquiryConverted event after owning commit | Ordinary DB DML on referral/facilitator/leader/customer; persistent financial hold; DB/role custodian | Conversion with referral now checks financial hold BEFORE conversion/event publication; handler independently checks BEFORE transaction. Without referral, ordinary conversion remains possible. No silent zero award/default or deferred unreceipted grant |
| R16 other application events | AreaSpaceApproved; Membership created/updated/deleted | Same process DB/cache; original user/permission + DB custodian | Explicit Tenant-scoped handler, idempotent Area linking; membership event invalidates cache. No startup producer found. Background flag is not a general event-bus disable switch; authorizing request/transaction still matters |
| R17 administrative/financial APIs | Authenticated HTTP or IoC-resolved application service; ABP interceptors | JWT/session, roles/permissions, Area assignment and DB DML; authorized admin + DB custodian | Existing Tenant/Area checks and financial commission/bootstrapping guards remain. Recovery interceptor/middleware restrict entry points. Deployment does not create legitimate admin permission, but process JWT/DB authority is security-sensitive and must be isolated |
| R18 external infrastructure and retry | DB/Redis reconnect; Render/Vercel release retry; container replacement | Persistent runtime connections or release provider capability; separate infrastructure custodian | Reconnect reacquires same role, not a new grant. CD serializes its own runs, binds exact SHA/run/attempt/provider IDs, blocks ambiguous POST/receipt/source drift, never migrates/resumes/configures/replays money or messages. External admin operations remain outside Actions concurrency |

## Capability matrix: effects, scope and proof

| ID | Financial/customer mutation / external communication | Tenant/Area boundary | Evidence and verdict; remaining unknown |
| --- | --- | --- | --- |
| R01 | Authentication/audit/key-ring activity possible; no intended business bootstrap | ABP session/filters and application authorization; DB role can exceed these | PASS source and bounded restart fixture; production grants/IAM UNKNOWN → BLOCKED |
| R02 | Reads schema/V2 population; no migration/repair | Host-wide read deliberately validates all relevant V2 state | PASS source/schema-negative regressions; manual schema drift and authentic live D10 custody UNKNOWN |
| R03 | Seed can create admin/role/customer/programme data under maintenance; no provider dispatch intended | Host/tenant seeding is privileged across scopes | PASS Production skip and held-state fixture; direct arbitrary SQL still requires DB denial, live UNKNOWN |
| R04 | DDL, history and seeding across host and tenant databases | Privileged migration scope, never ordinary API authority | PASS separate image/config and isolated SQLSTATE42501 tests; actual live role membership/ownership UNKNOWN |
| R05 | HTTP only, no host initialization | No customer authorization bypass from health CLI | PASS source and actual image health execution; custom live HEALTHCHECK_URL/network policy UNKNOWN |
| R06 | Health SELECT/PING; actual key ring may write key records | Health returns coarse status/build, not customer records | PASS source/health tests; health cannot attest privilege isolation. Key decryption means a compromised process may read queued bodies |
| R07 | Framework job effects depend on installed types; cache/audit/key maintenance distinct | Framework host authority, not proof of Tenant isolation | PASS ABP setting; worker bypass corrected; full framework dependency supply-chain behavior UNKNOWN |
| R08 | Aggregate read/log only; no checkout HTTP | Explicit aggregate monitor can span Tenants; no administration grant | PASS disabled/recovery/missing-config negatives and authorized monitor positive; live alert delivery UNKNOWN |
| R09 | Claims/decrypts/sends/marks sent/failure/terminal-alert records | Deliberately disables MayHaveTenant for host outbox; message Tenant evidence retained. No Area administration is granted by delivery | PASS direct/worker/provider controlled negatives; held real outbox restart fixture. Production enqueue-only vs dispatcher grants and body-key isolation UNKNOWN |
| R10 | Sends transactional email externally; direct ABP path has no durable delivery receipt | Recipient/reference supplied by authorized caller; adapter is not a Tenant/Area authorizer | PASS controlled HTTP rejection with keys present; actual Bird acceptance/recipient delivery NOT TESTED. Credential/key/egress separation BLOCKED |
| R11 | Writes AQGreen/Onyx periods, ledger and travel eligibility; no external payout | Enumerates Tenants host-wide, then explicit Tenant/programme and cutoff Area evidence; not an Area-admin API | PASS held worker negatives and existing calculation/PG regressions; live least-privilege worker role and commissioning UNKNOWN |
| R12 | Creates/assesses obligations; does not apply payment | Host scheduler explicitly reads scoped participants; separate payment/approval authority | PASS held worker/policy tests; live scheduler role grants UNKNOWN |
| R13 | Creates provider checkout and local checkout intent; live external financial capability | Member/customer ownership, Tenant, source purpose and checkout linkage retained | PASS live-hold controlled-provider negative + test positives; test mode still network-capable; live key custody/egress BLOCKED |
| R14 | Live payment/receipt/obligation/status mutation and queued notifications; test evidence only in test mode | Exact checkout-derived Tenant/customer/purpose; payment confirmation is not participation activation; Area approval remains separate | PASS live-hold and existing purpose/amount/duplicate/PG regressions; production webhook secret/signature deployment and direct SQL safety UNKNOWN |
| R15 | Referrals, rank/award balances and membership linkage; no HTTP delivery | Event Tenant validated; repositories use explicit Tenant; this does not establish arbitrary-role DB isolation | FAIL before correction; PASS controlled financial-hold negative and conversion precommit invariant; live historical event/replay state UNKNOWN |
| R16 | Area link or cache invalidation; no external provider | Tenant must match event and stored entity; original Area decision retains original authorization | PASS source and existing tests; event bus is not an independent privilege boundary |
| R17 | Authorized customer/admin mutations, approvals and commissioned financial actions; outbox enqueue | Tenant hard boundary, Area scoped admin, Host exceptions explicit. DB credentials/JWT signer can bypass UI permissions | Existing authorization/PG regressions required; production DB/JWT/certificate custody and compromised-runtime resistance BLOCKED |
| R18 | Intended code deployment only; actual provider keys may also change settings/secrets | Fixed deployment service/project; release broker must prevent wider authority | PASS controller synthetic retries/negatives; live deploy-only IAM, immutable external receipts, verifier custody and end-to-end rollout BLOCKED |

## Implemented correction and isolation limits

RuntimeOperationGuards consolidates existing hold meanings; no new release activation,
token or approval was introduced. Both periodic registration and body enforce holds.
Processor/gateway checks protect direct dispatch as well as scheduling. Direct financial
orchestration also checks its financial hold before dependency access. Positive tests
explicitly grant synthetic capabilities to controlled substitutes; their ABP scheduler
stays disabled. Existing identity, payment-vs-approval, V1/V2 and financial-policy
semantics remain governed by current documents; guards do not confirm unresolved policy.

The default API Docker target no longer contains the migrator. A separate maintenance
target and `docker-compose.maintenance.yml` require an explicit maintenance invocation
and separately supplied DB connection. Ordinary Compose no longer runs migrations on
API start or requires migration credentials. **This is packaging/configuration separation,
not proof the runtime cannot issue SQL DDL.** The current fixture's restricted role
establishes that DB enforcement is possible on actual application tables. It grants
readiness/audit/key-store operations and enqueue only, not full membership/admin DML;
it is not a deployable production grants recipe.

Flags can be edited by a powerful configuration custodian or bypassed by modified
application code. A running call already holding DB authority or a provider request
already accepted cannot be revoked by the next flag check. No exactly-once provider
delivery or linearizable operational revocation is claimed. Containment of an
in-flight request requires independently enforced identity/egress revocation and
careful receipt reconciliation. Recover neither by blind retries nor by erasing history.

## Persistent production privilege-separation requirements

1. Qualify the live API role using constrained catalog-only evidence from the database
   custodian: role flags, memberships, owner privileges, schema/sequence/table/function
   ACLs, RLS/BYPASSRLS and escalation paths. Do not retrieve member/payment data. API
   must not inherit schema-owner/migrator/restore or broad financial-writer authority.
2. Provision a separate dispatcher identity with narrowly scoped outbox claim/result
   procedures and message-decryption access, no general administrator/payment DML.
   Ordinary API enqueues; it must not possess Bird send credentials. Put delivery
   keys behind that identity or a protected exact-operation broker, with egress policy.
   Current normal Production validation still requires Bird/Yoco configuration; a
   separately tested credential-free normal API composition is not installed here.
3. Qualify distinct financial calculator/payment-application identities or protected
   procedures with immutable source/amount/purpose/tenant/cycle authority. Ordinary API
   deployment alone must not acquire their grants or live Yoco capability. Preserve
   approved test-money paths without converting test events into business payment.
4. Keep schema migration/restore capability outside API/release runner custody. Remove
   secret retrieval, metadata-service or role-membership routes back to privileged
   identities; restrict provider tokens to exact methods/targets or use a separate
   machine-enforced broker. An image split alone does not constrain arbitrary code.
5. Preserve Tenant/Area authorization under the selected grants/procedures and test
   full member/admin workflows, payment-vs-approval, history and concurrency against
   isolated PostgreSQL. Do not issue broad table grants merely to make health pass.
6. Independently protect verifier/policy/credential issuance from ordinary PRs; main-
   only environment matching is insufficient. Establish durable external receipts,
   monitored drift and tested backup/key recovery. The owner's two admin accounts
   remain one human, not independent review. See the
   [infrastructure plan](production-infrastructure-plan.md) for exact live blockers.

These are persistent boundaries, not approval for each release. None were applied
to production. Production role/IAM evidence, worker process identity, credential-free
composition, restore drill and effective egress policy remain UNKNOWN/BLOCKED.

| Persistent action | Current evidence → required state | Consequence / application authority | Verification and recovery |
| --- | --- | --- | --- |
| API database principal | Actual role/ACLs UNKNOWN; fixture only → non-owner API with reviewed workflow-specific grants and no financial/schema escalation | Database custodian must authorize/provision it. Scripts can apply reviewed grants after authorization; this task does not | Catalog-only role/ACL evidence plus full isolated PostgreSQL user workflows and denial probes. On failure, stop admission and restore the last qualified restricted profile; do not grant owner to make health green |
| Dispatcher composition and egress | Bird credential is present in ordinary live API → separate dispatcher identity, scoped claim/result operations, message-key access and Bird credential outside API | Requires a credential-free normal API composition and persistent worker/broker/network configuration. Owner authorization required before live changes | API must reject sends even with modified local flags and be unable to retrieve the dispatch key; dispatcher tests must preserve Tenant evidence. On failure, keep dispatch held; never replay pending mail merely to recover |
| Financial/provider authority | Held flags and test-Yoco declared; DB/provider effective rights UNKNOWN → distinct protected calculator/payment capabilities with immutable purpose/amount/Tenant evidence | Financial and infrastructure custodians define narrow grants. No live financial enablement is authorized or required for ordinary code deployment | Cross-Tenant/Area, replay, conflict, history and unauthorized mutation negatives against actual selected grants. On failure, stop processing and reconcile authorized receipts; no synthetic payment or history reconstruction |
| Migration and restoration | API image now omits migrator; live ownership/escalation UNKNOWN → separate maintenance identity and tested backup/key recovery outside API/release credentials | Packaging is implemented. Live identity/secret custody and restore drill need owner authorization; maintenance remains separate from recurring releases | API SQLSTATE42501 DDL/role/history denials, image contents, no startup migration and isolated restore rehearsal. Recover using the approved backup procedure under separate authority; no automatic destructive rollback |
| Admission, receipts and provider readiness | Candidate verifier custody, empty release secrets and current profile mismatch remain documented blockers → external policy/credential boundary, persistent provider authentication and a qualified runtime profile | Repository CI cannot install independent custody. Owner must authorize persistent GitHub/provider configuration. No release-by-release switch is introduced | Ordinary PR removal/replacement must fail at the external boundary; then observe an authorized exact-revision Render/API/Vercel rollout. Ambiguous attempts stay blocked pending reconciliation; preserve receipts and do not issue duplicate provider POSTs |

The [infrastructure plan](production-infrastructure-plan.md) records the corresponding
GitHub, Render and Vercel current settings, evidence, required changes and recovery
steps. None of these rows claims that the fixture is a complete production role model.

## Deployment compatibility and commands

The isolated normal Production fixture demonstrates startup/restart with
recovery=false, financial/background enablement=false, and no dispatcher privileges.
That proves recovery is not intrinsically necessary for this bounded held startup.
It does not qualify the actual Render role, credentials or full user workflows.
Current Render cannot yet satisfy equivalent independent isolation, so the release
controller's recovery-only admission is retained. Do not remove it solely because
these guards pass. A normal deployment profile can replace it after effective
capability evidence and a separately reviewed controller/profile change; no recurring
authorization or artificial recovery prerequisite is intended.

Use only disposable local resources:

```bash
docker build -f docker/api/Dockerfile -t aqualifestyle-runtime-api:qualification .
docker build -f docker/api/Dockerfile --target migrator -t aqualifestyle-runtime-migrator:qualification .
docker pull postgres:18-alpine
docker pull redis:7-alpine
bash tools/deployment/runtime-restart-probe.sh
```

The probe accepts no target/configuration arguments, requires local Unix Docker,
creates an internal network and fresh PostgreSQL18 (matching production's major
version)/Redis, initializes the real schema through the separate maintenance image,
uses a distinct API role, and checks insufficient_privilege SQLSTATE42501 for DDL,
role assumption, history/payment/commission/admin/outbox-claim mutation. It compares
business table fingerprints beyond the30s dispatch interval through initial start,
actual container restart and fresh replacement. Pending mail remains unclaimed;
the internal network prevents provider access. Missing required delivery configuration
must stop startup. Audit/login/key tables are excluded explicitly because legitimate
infrastructure writes can occur; no claim of zero DB writes is made. Cleanup removes
only the probe's fresh resources. Local image tags must be rebuilt from reviewed source.

For explicitly authorized maintenance only, load both Compose files and use the
maintenance image with the separate migration connection. Ordinary `docker compose up`
has no migrator dependency. Compose configuration is not a production role provisioner.

## Validation and remaining acceptance

Negative tests cover background hold on each independent worker, missing configuration,
recovery monitor, financial worker hold, direct outbox/gateway denial with provider key
present, live checkout/callback hold and referral award before deferred mutation.
The ordinary member/Area PostgreSQL regression and existing transactional payment and
worker suites remain required. Full CI includes a mandatory actual-image runtime
probe within Validate container images; missing/failed probe fails that existing
required job. Explicit security inventory pins must be updated transparently for the
workflow change; candidate custody remains UNVERIFIED.

Test counts, failures, exact final commit, hosted run/attempt and fresh Agent2 verdict
are recorded in the evidence report after execution. Local/synthetic/restricted-role
fixture PASS cannot establish live security or production readiness. Known pre-existing
NuGet advisory findings remain disclosed; this change does not alter package versions
or claim the backend dependency audit is clean. Complete automatic live deployment
still requires the infrastructure actions above and an observed authorized rollout.
