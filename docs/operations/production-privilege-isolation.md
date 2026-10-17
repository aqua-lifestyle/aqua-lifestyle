# Production privilege isolation

Starting source:72e09f6ed992c5608e6106d4368801cd292d2785, PR106. Assessment2026-10-17
(South Africa; evidence timestamps are UTC). **Acceptance: BLOCKED.** No production
configuration, credentials, grants, data, deployment or restricted operation changed.
The prior [runtime matrix](runtime-authority-qualification.md) remains the automatic
execution inventory. This assessment adds real access evidence and the write contract.

## Christian foundation and evidence

1Corinthians4:2 motivates faithful custody; Proverbs22:3 prudence; Proverbs11:1 honest
financial measures and reports; Romans13:10 avoiding harm; 1Thessalonians5:21 testing
claims. Applying these principles to process identities, database grants and evidence
is theological interpretation and engineering judgment. Scripture is not a database
standard or guarantee against compromise. The [safety contract](cicd-safety-contract.md)
separates those categories. No new business-policy decision is confirmed here.

## Verified production facts and access blocker

Authenticated Render GETs returned the selected API service and PostgreSQL18 database,
its environment, connection metadata and managed credential inventory. Safe evidence:
[provider readback](../verification/production-privilege-isolation-20261017/render-readonly.json).

- API service:srv-d9f0k8e1a83c73d8n1b0; selected DB:dpg-d9f0jne1a83c73d8m780-a.
- DATABASE_URL selects that database; ConnectionStrings__Default is absent. Its
  username matches Render databaseUser. **This is not evidence of ownership, role
  membership or administrative privilege.** One Render-managed credential is listed;
  SQL-created roles are not included in that provider inventory.
- Ordinary Production declares financial/background/weekly/monthly=false, Bird=true,
  Yoco=test, and holds Bird/Yoco/webhook/DataProtection credentials. Native deploy=off.
- External database access has two allowlist entries and no all-IPv4/all-IPv6 rule.
  Actual addresses, secret values, connection URLs and raw responses are not retained.

The owner authorized constrained catalog inspection for this task. An existing API
credential was used in memory with the authoritative external endpoint, TLS verify-full,
8-second connect/statement bounds, default read-only session and BEGIN READ ONLY /
ROLLBACK. Three bounded attempts returned psql exit2, SSL connection closed unexpectedly;
no catalog result was obtained. This is a **transport blocker**, not a failed privilege
check, and its cause is UNKNOWN. The allowlist may be relevant but causality was not
proven. No SSL downgrade, new credential, firewall change, SSH session, production
command/job, or new access route was created. See [safe failure metadata](../verification/production-privilege-isolation-20261017/catalog-readonly.json).

**Required dependency:** the database custodian must execute the reviewed catalog query
through an existing approved network/session, or separately authorize a constrained
observation route. Do not broaden public access merely to run this audit. Return only
whitelisted privilege metadata; no member records, password hashes, key XML, connection
strings or raw environment output. Actual owner/admin status remains UNKNOWN/BLOCKED.

## Essential normal writes and authority overlaps

Paths are relative to AqualLifeStyle/9.4.2/aspnet-core/src. This is a capability contract,
not a deployable GRANT list. Existing authorized routes must continue to work.

| Operation / source | Essential durable effects | Why a blanket table grant or denial is inadequate |
| --- | --- | --- |
| MyAccount/MyAccountAppService; Customers/CustomerPersonalDetailsUpdater | Customers.Name/Email; AbpUsers name, surname, contact/address and normalized identity fields; existing current-user/Tenant rules and verified-email restriction | Blanket Customers/Users denial breaks profiles. Name/email fields on other accounts can also change identity. SQL needs independently bound actor/target authority |
| MyAccount.ChangePassword; Authorization/Accounts/AccountAppService | Password hash, security stamp, failed-access/lockout/reset/confirmation state; verification tokens, throttles and notification outbox | UPDATE Password also permits administrator takeover without database-level actor checks. Reading all hashes is itself sensitive. Shared symmetric JWT signing and stamp access compound the boundary |
| AccountAppService.Register; UserRegistrationManager | User, initial Guest role, Customer, Tenant-consistent Area assignment and histories, verification/throttle/outbox records | Arbitrary INSERT can fabricate privileged roles/initial state; UPDATE-only controls are insufficient. Do not disable legitimate registration when enabled by authorized policy |
| Payments/ActiveProgrammeParticipantRoleSynchronizer; authorized Area approval | Participation/decision/placement/history, Customer membership, AbpUsers.Role/security stamp, AbpUserRoles, notification outbox atomically | Guest→Member is legitimate after authorized approval, not a payment callback or arbitrary role grant. Role-table denial breaks that workflow; broad DML permits escalation |
| Admin member/customer APIs; AdminUserRoleSynchronizer | Assigned-Area customer/member profile, suspension/tier/lifecycle, linked identity and Area history under existing permissions/locks | Administrative product operations remain supported; broker must verify Tenant/Area/action/ownership independently. Cross-Area topology is not administrative authority |
| Participation/start/checkout paths | Prospective participation, attribution, checkout intent/reference and initial states, permitted scheduling/locks | Same tables later represent payment/activation. INSERT and UPDATE transitions both require purpose, amount, time, Tenant and actor validation |
| Email/TransactionalEmailOutbox | Enqueue notification with valid tenant/recipient/template/idempotency; encrypted body | Unrestricted INSERT can forge Sent/provider receipts. API should obtain only validated enqueue authority, not raw result/claim columns or delivery keys |
| Framework audit/login and DataProtection key persistence | Audit/login event insertion; key-ring read/creation required by current normal runtime | Do not assume UPDATE/DELETE is needed on every infrastructure table. Determine cleanup/retention separately. API DataProtection certificate currently also decrypts queued mail; a purpose string is not process isolation |
| Migrations, seed and repair | Schema/history/privileged bootstrap and potentially cross-Tenant writes | Separate maintenance authority. Ordinary API needs compatible-schema reads, not ownership/DDL, migration history mutation, restore or unrestricted repair |
| Financial processors and periodic workers | Payment/ledger/period/obligation/eligibility changes with locks, receipts and history | Separate protected operation identity; flags are operational holds, not independently enforced grants. Financial confirmation does not activate participation |

Authoritative source links:
[profile](../../AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Application/MyAccount/MyAccountAppService.cs),
[identity synchronization](../../AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Application/Customers/CustomerPersonalDetailsUpdater.cs),
[registration](../../AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Application/Authorization/Accounts/AccountAppService.cs),
[approved promotion](../../AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Application/Payments/ActiveProgrammeParticipantRoleSynchronizer.cs),
[EF Tenant/Area foreign key](../../AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.EntityFrameworkCore/EntityFrameworkCore/AqualLifeStyleDbContext.cs),
[token issuer](../../AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Web.Core/Controllers/TokenAuthController.cs),
[token validation](../../AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Web.Host/Startup/AuthConfigurer.cs),
[shared message protector](../../AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Application/Email/TransactionalEmailBodyProtector.cs).

## Proposed persistent identities and enforcement

All changes below are PROPOSED initial infrastructure/application work, not release-by-
release authorization. None is installed or activated in production by this task.

| Identity | Required capability | Must be independently absent |
| --- | --- | --- |
| Schema owner (NOLOGIN) / separate maintenance login | Own reviewed application objects; maintenance may SET the owner role and run qualified schema changes under separate policy | API membership/SET/ADMIN OPTION, shared passwords, privileged default/PUBLIC grants; no migration/restore credential in API or CD image/env/mount/cache/metadata access |
| Ordinary API | CONNECT/USAGE, reviewed scoped reads and narrowly validated ordinary commands; compatible-schema readiness; required audit/login/key persistence | SUPERUSER, CREATEDB, CREATEROLE, REPLICATION, BYPASSRLS; object ownership/inherited or settable owner role; schema CREATE/TEMP/escalation routines; raw privileged state/identity/receipt writes; dispatcher/financial/provider administration keys |
| Identity/authorization command service | Authenticate independent actor/permission/Tenant/Area and own password, registration, promotion/admin transitions transactionally | API must not mint the authority this service trusts. Current API signs symmetric JWTs and can read stamp/hash state; merely trusting those JWTs/userId/session GUCs does not isolate a compromised API |
| Dispatcher | Claim/decrypt/send/record exact outbox messages via scoped routines; Bird credential and private message key only here | API cannot retrieve those credentials, change claim/result fields, forge enqueue authorization or reach provider send endpoints. API may encrypt using a worker public key; current shared DataProtection setup must first be replaced/qualified |
| Financial command/worker | Exact authorized financial purpose/reference/amount/currency/Tenant/cycle and protected records/receipts; scheduling/calculation only when commissioned | API cannot obtain worker DB/provider credentials or mint capabilities. Share no broad Customer/Users/ledger writer merely to preserve an API path; keep atomic orchestration within the trusted service |
| Deployment identity / external broker | Exact service/project revision deployment after independently verified SHA/run/attempt; authenticated durable receipts | Production DB, owner/maintenance/restore, runtime secret reads/settings/worker enablement, Bird/Yoco operations; candidate workflow must not redefine verification or receive a broad provider token |

The PostgreSQL custodian must first inspect actual role flags/membership/ownership,
PUBLIC and default ACLs, table **and column** rights, sequence/function rights and
executable security-definer paths. The supplied [catalog query](../../tools/operations/database-authority-readonly.sql)
reads no application rows/function bodies/passwords. It covers known public application
objects and aggregates other user schemas/definers; it is not a full proof of every
extension, provider IAM, network, parameter ACL or external-secret escalation path.
The transaction sets `search_path=pg_catalog,pg_temp` locally before any inspection:
read-only alone does not prevent a shadowed built-in from reading application rows
or spoofing metadata. A disposable hostile-search-path regression proves that raw
lookup selects the synthetic row-reading function while the audit returns actual
catalog settings. The temporary role setting, function and table are then removed.

Provision separate credentials only after authorization. Move ownership only for an
explicit reviewed application-object inventory; never blanket REASSIGN provider-owned
extensions/objects. Non-owner is necessary, not sufficient. Review indirect membership,
SET ROLE, PUBLIC/default grants and every executable privileged routine. RLS requires
non-owner/no-BYPASSRLS plus independently authenticated scope; an API-selected Tenant
variable or application filter is not independent scope. The composite Tenant/Area FK
preserves relational consistency, not actor authorization.

Do not publish a broad API GRANT script until the operation mapping is implemented and
its full workflow positives pass. The previous readiness-only role is intentionally
not that script. The observed normal startup requires provider settings; a credential-
free ordinary API composition and independent identity/dispatch/financial command
boundaries remain application work, not settings that can honestly be declared ready.

## Executable qualification and limits

The existing internal-network PostgreSQL18/Redis actual-image probe retains its17
startup/restart/DDL/role/history/financial/claim/configuration checks. It now adds six:

1. The read-only catalog query distinguishes schema owner from a distinct non-owner,
   including membership/SET and column-only privileges.
2. The readiness-only role rejects profile SQL: it cannot be called a normal API role.
3. A temporary name/password column grant permits a synthetic profile SQL update;
   the audit reports table UPDATE=false but column UPDATE=true.
4. The same password grant permits synthetic Host administrator credential mutation:
   **unsafe proposal rejected**, not isolation established.
5. Existing unrestricted outbox INSERT permits a fabricated Sent/provider receipt:
   **unsafe proposal rejected**, with no provider call or message delivery.
6. Every counterexample rolls back; temporary grants are removed and business-state
   fingerprints match before the ordinary startup/restart checks execute.

The SQL compatibility positive is not an HTTP/user journey. Existing full application,
web and dedicated member/Area PostgreSQL CI remain required for regression evidence;
their usual test identities are not a qualified least-privilege production role. Full
profile/password/registration/member/admin/payment/approval execution under the selected
new authority model is **NOT EXECUTED / BLOCKED**, not inferred from these fixtures.
Maintenance secret custody/metadata retrieval and actual provider egress denials are
also unproven; a local isolated container proves only its selected mounts/network.

## Exact next infrastructure actions

| Action / responsible custodian | Verify before any live change | Recovery / consequence |
| --- | --- | --- |
| Database custodian: obtain catalog readback through an already permitted network/session | Same API principal/database; read-only transaction; ownership, memberships, table/column/default/PUBLIC/definer rights | Keep current settings unchanged if unavailable. Do not weaken TLS or widen allowlists for convenience |
| Architecture/identity custodian: implement selected command/issuer and write contracts | Full authorized customer/password/registration/Area approval/admin workflows; transactional history/outbox; raw administrator-password, role, initial-state and cross-Tenant/Area denials | Preserve current authorized behavior; no partial privilege rollout or silent fallback. Roll back only compatible reviewed code/config under separate authority |
| PostgreSQL custodian: prepare non-owner/maintenance/dispatch/financial principals and exact ACL migration | Disposable full-schema workflow suite using distinct actual logins; DDL/owner/SET/escalation negatives, column and enqueue-fraud denials | Preserve private prior grants/ownership evidence and tested backups. Stop admission on failure; never grant owner to make health green |
| Render/secret/network custodian: prepare persistent separate services/keys/egress | Ordinary API lacks worker/migrator/provider/private message keys and any route to obtain them; restart cannot acquire them | No key rotation, new user, service restart or provider changes authorized here. Render-managed credential creation can change the default user; treat it as explicit infrastructure work |
| Release/security custodian: independently protected verification and deploy-only broker | Ordinary PR edits/removals/wrong SHA/attempt cannot obtain provider capability; exact target/method constraints; durable authentic receipts | Missing capability stops automation before provider mutation. Never add recurring approval/token/dispatch or replace missing receipts with fabricated bootstrap |
| Operations custodian: qualify actual profile/backup/key recovery before changing current recovery-only release admission | Actual catalog/IAM/egress evidence, full workflow negatives and separately authorized observed provider rollout | Current gate remains until equivalent actual isolation is proven. No production readiness inferred from simulated safety |

The [infrastructure plan](production-infrastructure-plan.md) remains the provider-target
and governance reference. Once persistent trusted configuration is qualified, ordinary
merges still follow CI→Render→API verification→Vercel→production verification without
additional approval. This task neither deploys nor claims that live sequence succeeded.
