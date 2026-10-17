# Production database authority qualification

Assessment: 2026-10-17. PR106 was re-fetched at
`fa846c757508622c57012f32f34348591549aa1b`, rather than assuming an earlier head.
**Production qualification: BLOCKED.** No effective production SQL identity or grants
were returned. No production role, credential, configuration, data or deployment changed.
The [isolation plan](production-privilege-isolation.md) remains PROPOSED infrastructure
and application work, not an installed control. Its legitimate-write matrix applies.

## Observed facts and diagnostic limits

Authenticated Render GETs identified API `srv-d9f0k8e1a83c73d8n1b0`, database
`dpg-d9f0jne1a83c73d8m780-a`, available PostgreSQL18/basic_1gb/Frankfurt, and the
still-live API commit `1251fb82979e18be53d1d0e7f5c6fab2d0c50a58`. The configured
DATABASE_URL matches this database and its provider-default username; no
ConnectionStrings__Default override is present. This is configuration evidence,
**not** `current_user`, `session_user`, ownership or authentication evidence.
One Render-managed credential does not enumerate SQL-created roles. No separate
maintenance/worker/financial connection variable was found in this API's environment;
other service identities, tenant connection overrides and secret-retrieval authority
remain UNKNOWN. The inspection did not read tenant connection strings or member rows.

The previous three attempts retained only a generic SSL closure and psql exit2.
Their exact failure phase cannot be recovered from that metadata. This task imposed
a bounded budget: one credential-free external TLS diagnostic, one authenticated
catalog attempt after successful TLS, and one existing-key SSH client-presence check.
No retries, new keys, forwarding, ephemeral instance, TLS downgrade or access-rule
change occurred. [Sanitized records](../verification/production-database-authority-20261017/README.md)
retain UTC timestamps, bounds, source distinctions and results, never raw credentials,
role names, addresses, query text from logs or provider payloads.

| Candidate cause / phase | Evidence | Conclusion |
| --- | --- | --- |
| DNS, refused TCP or connection timeout | Credential-free diagnostic resolved DNS at45ms, connected at226ms, accepted PostgreSQL SSL at408ms | PASS for that separate connection only. The authenticated attempt exited at1537ms, before its8-second connect deadline; no timeout/refusal diagnostic |
| TLS protocol, CA or hostname | Diagnostic verified certificate/hostname and TLS1.3 at611ms with SNI and system CA; no startup packet sent | PASS for this Node transport probe. It does not locate psql's failure phase or prove its TLS/authentication completed |
| GSS fallback | Authenticated psql explicitly disabled GSS encryption while requiring verify-full TLS | Same closure; disabling GSS is not a sufficient correction |
| External access rule | Two rules remain. Two HTTP IPv4 echoes agreed on an address outside them; no proxy env found | INFERENCE: allowlist/source routing is a plausible blocker. HTTP egress does not establish PostgreSQL TCP source; cause remains UNKNOWN |
| Authentication | Existing API credential supplied only in memory/env; psql exit2,173-byte generic SSL closure; no SQLSTATE/explicit auth error | UNKNOWN. Supplying a credential does not prove it was accepted |
| Database/role limits | Provider samples0–2 connections in prior-attempt window,2–3 in current window; configured maximum/reserved/per-role limits not returned | UNKNOWN. Sampling is not an instantaneous admission or catalog-limit check |
| Query execution / permissions | No identity, JSON catalog result or SQL error returned | UNKNOWN; do not call it a failed GRANT check, successful read-only transaction or SQL timeout |
| Network instability | Transport probe succeeded; catalog connection closed; no gateway/server correlation record | UNKNOWN; no evidence sufficient to attribute intermittent network loss |
| Existing private route | One noninteractive SSH check with existing key and strictly pinned published Frankfurt host key exited255 with timeout category | BLOCKED. Authentication, container shell compatibility and psql availability were not established; no database connection or app/migrator command executed |

Selected-database log reads with a FATAL filter returned zero rows and hasMore=false
for22:30–22:43Z on October16 and07:32–07:36Z on October17. Earlier unfiltered pages
were incomplete and cannot support absence claims. Empty filtered results do not prove
the request reached PostgreSQL or exclude authentication/gateway rejection. No raw
log messages were retained. Render documents that IP rules may reject after initial
TCP/protocol exchange: [IP rules](https://render.com/docs/inbound-ip-rules).
See [TLS/network routes](https://render.com/docs/postgresql-creating-connecting),
[managed credentials](https://render.com/docs/postgresql-credentials),
[metrics](https://api-docs.render.com/reference/get-active-connections), and
[SSH prerequisites](https://render.com/docs/ssh).

## Required catalog evidence and verdicts

The [reviewed query](../../tools/operations/database-authority-readonly.sql) uses
BEGIN READ ONLY, trusted `pg_catalog,pg_temp` lookup and ROLLBACK. It reads catalogs,
not application rows, password hashes, function bodies, policy expressions, secret GUC
values or active SQL. It returns actual current/session identities; database/schema/
relation/sequence/routine ownership; role flags and membership ADMIN/INHERIT/SET;
effective table DML/TRUNCATE/REFERENCES/TRIGGER/MAINTAIN and any-column rights; exact
column rights on selected sensitive objects; sequence USAGE/SELECT/UPDATE; executable
user routines including SECURITY DEFINER; RLS enabled/FORCE/effective status and policy
metadata; default ACLs, setting names, role connection limits and connection counts.
Default-ACL rows are explicit overrides, not an inventory of implicit PUBLIC defaults;
the effective privilege functions include rights inherited from PUBLIC and roles.
Names/OIDs are private operational metadata; alias before publishing. A null RLS-active
field on a non-table relation means not applicable, not a successful RLS check.

| Production boundary | Verdict | Missing evidence |
| --- | --- | --- |
| Current/session API principal and database/schema owner | UNKNOWN | Successful result from same configured API login plus attestation of selected database, endpoint, source and live revision |
| Privileged roles, ownership, inherited/SET/admin paths | UNKNOWN | Catalog role flags, memberships and ownership; no ownership inference from provider username |
| Effective table, column, sequence and migration authority | UNKNOWN | Catalog result and reviewed comparison to essential writes; no production mutation tests authorized |
| RLS/BYPASSRLS and Tenant/Area enforcement | UNKNOWN | Actual catalog/policy review and independently bound actor scope. RLS enabled or application filters alone do not prove scope |
| API exclusion from reserved maintenance/worker routines | UNKNOWN | Actual EXECUTE and role reachability; routine bodies reviewed privately by custodian if needed |
| Maintenance, messaging and financial role-to-service custody | UNKNOWN | Catalog role inventory **and** separately attested private credential-to-process mapping. Role names do not prove use; one Render-managed credential is not a complete inventory |
| Observation route from this execution host | BLOCKED | Catalog attempt returned no identity/results; existing SSH route unavailable |
| Production authority qualification | BLOCKED | All above evidence remains required; no verified production violation is asserted |

The query is an observation tool, not an independent verifier or complete escalation
audit. Extension/provider IAM, parameter ACLs, executable routine implementations,
tenant-specific connections and external credential custody still require review.
An observer using the same API credential is not a query executed inside the actual
API process. Source inspection found no application SET ROLE/session authorization
initialization, but that does not replace runtime/catalog observation.

## Least-privileged route and exact access dependency

Preferred next action: the database custodian runs the reviewed, immutable-hash query
**once through an existing allowlisted host/session**, with the existing API database
credential, external hostname/SNI and verify-full TLS. No owner/password view,
pg_monitor grant, new login, production command execution or broader credential is
needed to attempt these catalog reads. If a required field is denied, retain that
failure and seek a narrowly scoped custodian readback; do not grant more authority to
the API. The credential must remain outside repository/logs. Supply PGHOST/PGPORT/
PGDATABASE/PGUSER/PGPASSWORD securely in the custodian's existing execution environment;
do not paste them into commands, tickets or PR comments. A bounded invocation is:

```bash
umask 077
PGCONNECT_TIMEOUT=8 PGSSLMODE=verify-full PGSSLSNI=1 \
PGGSSENCMODE=disable PGSSLROOTCERT=/etc/ssl/certs/ca-certificates.crt \
PGOPTIONS='-c default_transaction_read_only=on -c statement_timeout=8000 -c lock_timeout=1000 -c idle_in_transaction_session_timeout=15000' \
timeout 30s psql -X -qAt --no-password --set=ON_ERROR_STOP=1 \
  --file tools/operations/database-authority-readonly.sql > catalog-private.json
```

The custodian must first verify the source hash, exact target and private destination,
and then return aliased metadata, timestamp, SQL hash, exit status, current/session
identity equality and their privately attested mapping to the API configuration.
Do not turn a missing or partial result into empty permissions. Do not attach raw output.
An already-authorized in-region observer is an alternative only if its existing TLS,
client and credential controls are adequate; Render documents self-signed internal TLS
that does not support verify-full. This task does not silently lower TLS requirements.
Existing SSH keys alone do not establish Render authorization or a usable client.

If neither existing route exists, the owner/network/secret custodian must separately
authorize a constrained observation route to this database (approved source, reviewed
client, secret custody and TLS identity verification), or provide the catalog readback.
No public wildcard allowlist, new privileged API credential or production image change
is justified by this audit. This is initial observability infrastructure, not recurring
release approval. No additional production connection is authorized by this worklog.

## Minimum-change privilege plan

Apply no production ACL changes before actual readback. Compare the resulting
privileges to the [essential normal-write matrix](production-privilege-isolation.md#essential-normal-writes-and-authority-overlaps).
Profiles, passwords, registration, authorized Area approval, audit/login and validated
outbox enqueue must remain functional. Blanket denial breaks valid requests; broad
Users/roles/outbox DML permits privilege or receipt forgery. Existing disposable
counterexamples reject those proposals; they are not evidence that production has them.

1. If the API is owner or can inherit/SET an owner/admin role, prepare a separate
   NOLOGIN application-object owner and independently held maintenance login. Remove
   only proven unnecessary API ownership/membership/DDL/MAINTAIN privileges after full
   compatibility testing. Preserve provider-owned extensions and reviewed backups;
   no blanket REASSIGN, broad REVOKE or production migration is authorized here.
2. Retain required CONNECT/schema USAGE, scoped reads and validated ordinary write
   commands. Review PUBLIC/default ACLs and every executable definer path. Separate
   administrative identity, financial commands and validated outbox enqueue/dispatch
   where table/column grants cannot express actor/purpose authority. Shared JWT keys
   or API-selected scope variables are not an independent actor boundary.
3. Maintenance, dispatcher and financial credentials must be distinct, independently
   held and unavailable from API env/mounts/images/provider metadata. This is separate
   infrastructure/application work; no production custody has been proven here.
4. Before any authorized rollout, execute full profile/password/registration/approval
   and Tenant/Area workflows under the proposed role, plus negative grants/routines,
   restart and recovery cases on the actual PostgreSQL schema. Keep old private ACL/
   ownership evidence for controlled rollback. Do not fix denial by granting owner.

The new [disposable probe](../../tools/operations/database-authority-probe.mjs) proves13
catalog/permission primitives on PostgreSQL18 with network=none, SCRAM TCP login,
distinct synthetic passwords and no host data mounts/ports. It proves actual SQLSTATE
42501 denials, cross-login authentication rejection, maintenance-only DDL, reserved
worker execution separation, successful rolled-back synthetic profile SQL and exclusion
of secret setting values/function bodies. A permissive synthetic RLS policy demonstrates
metadata reporting only, not Tenant isolation or a qualified normal API grant recipe.
It does not prove production secret custody or full application behavior. CI executes
it in the existing required container job and retains its evidence alongside the23
actual-image runtime checks. The209 mandatory case identities remain unchanged.

The automatic merge→CI→Render→API verification→Vercel sequence and release gate are
unchanged. No approval/token/switch/dispatch has been added. Candidate-owned CI and
source pins still lack [independent verifier custody](mandatory-security-verification.md).
No simulated test, empty log search or passing CI establishes production readiness.
