# Production database authority observation

> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

Current PR106 head re-fetched from GitHub and origin:
fa846c757508622c57012f32f34348591549aa1b. Existing feature branch/worktree initially
clean. Owner authorizes bounded read-only production catalog observation and isolated
qualification. No production deployment, migration, role/grant change, credential
rotation, access-rule/service configuration change or restricted operation is authorized.
Use Bash/Node/.NET/SQL only; no Python source or commands.

Plan: inspect the prior failure/client and current provider route; distinguish DNS/TCP,
TLS, authentication, capacity and query phases before any retry. Prefer an existing
approved observation route, not broader credentials/network rules. Extend/qualify
catalog-only evidence where current query is incomplete: actual current_user and
session_user, ownership, role membership, effective table/column/sequence/routine
rights, schema/DDL capability, RLS and configured maintenance/worker identity mapping.
Compare legitimate API write requirements without publishing a blanket grant recipe.
Use disposable PostgreSQL for all mutation/denial experiments. Fresh CI and exact-head
Agent2 review are required for repository changes.

Production connection budget: initially zero database connections this task. Permit
at most one bounded, credential-free TCP/PostgreSQL-SSL/TLS diagnostic after provider
route readback; permit at most one authenticated catalog session only if that route
is verified. No automatic retry, TLS downgrade, alternative-password guessing, new
credential/key, service instance or public allowlist change. A distinct existing
approved private route may be inspected without creating access; document any extra
bounded session explicitly before using it. Otherwise preserve BLOCKED and identify
exact custodian/network/session dependency.

Initial observations: previous evidence retained only generic SSL closure/exit2; it
cannot establish a precise phase/cause. Existing client is16.14/OpenSSL3.0.13; its174-byte
wrapper changes library lookup, not PG connection settings. Current authenticated GETs
succeeded and live API remains1251fb8. DB18/basic_1gb/Frankfurt is available. Two
public IP rules remain; two independent HTTP IPv4 echoes agree on an address outside
those rules. No proxy environment is present. HTTP egress is not proof of the PostgreSQL
TCP source, and this alone does not prove historical failures were allowlist rejection.
There is an existing local SSH agent/key, but Render authorization/compatible container
access is unverified. No production database connection has yet been attempted.

The single bounded external diagnostic at07:26:59Z succeeded through DNS (45ms),
TCP (226ms), PostgreSQL SSL acceptance (408ms), and verified TLS1.3 (611ms). No startup
packet, credential or SQL was sent. This excludes a timeout/certificate/hostname failure
for this observation, not for all historical attempts. Authentication/startup/query
and protocol-level allowlist rejection remain untested. Existing Render gateway
known-host key matches the published Frankfurt fingerprint; SSH authorization and
usable catalog client are still unknown, and no SSH session was opened. Prepare and
verify the complete catalog-only query in disposable PostgreSQL before using the one
permitted authenticated catalog connection on the verified external TLS route.

The expanded query passed an isolated PostgreSQL18/no-network fixture, including
current/session distinction after SET ROLE, non-owner/schema/role denials, column-only
and sequence privileges, reserved worker routine EXECUTE separation, FORCE/active RLS,
policy inventory and rollback of normal synthetic profile SQL. These are component
permission tests, not a qualified production API grant recipe or full API workflow.

The one authenticated external attempt at07:33:53Z returned exit2 after1537ms, the same
173-byte generic SSL closure category, no SQLSTATE and no identity/catalog result.
No timeout, certificate, explicit authentication/access-rule/capacity/SQL error was
reported. GSS encryption was disabled while verify-full TLS remained mandatory; this
rules out GSS fallback as a sufficient fix. No further external DB attempt is allowed
without new evidence/access. Historical root cause remains unproven.

Next bounded route check: one noninteractive SSH connection using the existing local
key/agent and existing known-host entry verified against Render's published Frankfurt
fingerprint. Read only whether a catalog client exists; do not start the application,
migrator, worker or ephemeral service, create/register keys, forward ports, upload code,
read member rows or mutate configuration. This uses an existing permission if accepted;
it does not create production access. If unavailable, record the precise dependency.

The SSH check exited255 after4445ms with a timeout category. No authenticated command,
client presence or private catalog route was established. No more production database
attempts were made. Provider connection samples around the prior/current windows
were0–2/2–3; a FATAL-filter read returned zero records/hasMore=false in each window.
Those are sampled/filter-scoped facts, not authentication, capacity-limit or routing
proof. Two initial log GETs had a400 request shape; correcting the owner field enabled
the subsequent reads. Unfiltered100-row pages were incomplete and unused for absence.

IMPLEMENTED: expanded catalog identity/role/ownership/table/sequence/routine/RLS/default
ACL/connection metadata, including PostgreSQL18 MAINTAIN privilege. Production attempt
metadata retains its earlier SQL hash; no version is claimed to have executed there.
Added a maintained13-group PostgreSQL18 fixture and required existing-container-CI
invocation/artifact. All209 mandatory identities remain; source pins updated for the
changed controls and new probe. Candidate control remains non-independent.

The fixture's first cross-login negative caught initdb's default loopback trust rule.
Corrected with explicit SCRAM host initdb auth and independently random credentials;
the subsequent13 checks pass, including real authentication and SQL42501 denials.
Agent2 also identified readiness polling on the temporary Unix-only initdb server;
changed the poll to TCP127.0.0.1 before executing catalog/fixture SQL. No production
settings were affected. These are primitive tests, not full application-role or Tenant
workflow qualification. The operational report preserves production UNKNOWN/BLOCKED,
minimum-change contingent plan and exact custodian/approved-route access requirement.

Next: freeze source, repeat focused checks after review corrections, run fresh complete
CI on the committed/pushed PR106 head, and obtain exact-final Agent2 technical review.
Final evidence and CI/review links belong in the PR report; never relabel old-head CI.

TESTED before commit: Node24.21.0 maintained catalog13/13 and mandatory209/209 (no
skips), local adversarial classification/custody fixture, six-skill/sixteen-case agent
validator, workflow actionlint and diff check. Policy SHA256 is
8523d8b7242c4df1d05f3fc09e8ea8f4377cbb7971bb30ea03cb3409da287bed;
final SQL SHA256 is551c55a2be8b444a4d60bbb57ef98912aa05c64387b8eec5d01abe5a038e2e06.
Agent2 independently reproduced the Unix-ready/TCP-not-ready startup race and verified
the prior13-group SCRAM primitive run. Exact-final review/hosted CI remain pending at
this recorded source snapshot. No Python source or commands were used for this task.
