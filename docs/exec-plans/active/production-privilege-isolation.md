# Production privilege isolation

> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

Starting commit:72e09f6ed992c5608e6106d4368801cd292d2785, PR106, existing feature
branch and dedicated worktree, initially clean. The current owner authorizes
constrained read-only actual Render/PostgreSQL privilege inspection, repository
preparation and disposable tests. No live configuration, credentials, database
contents, access rules, services, deployments or financial operations may change.
This specifically extends the preceding task's no-production-database-session scope
only to catalog inspection; it does not authorize production data queries.

Scripture supplies stewardship, prudence, integrity, care and examination principles.
Their application to identities and grants is an engineering interpretation, not
empirical proof or a promise of immunity. Preserve unattended CI/CD without recurring
approval; persistent setup remains separately authorized.

## Plan

1. Read provider metadata and, if existing access permits, catalog-only PostgreSQL
   evidence in a bounded read-only transaction. Never print credentials or raw
   provider payloads; never create access or alter firewall/provider settings.
2. Trace essential authorized API writes and overlaps with privileged operations.
   Do not replace normal functionality with the previous readiness-only role.
3. Design independently enforced identities/custody and exercise realistic positives
   alongside privilege negatives in disposable PostgreSQL. Implement only changes
   supported by source and executable evidence; disclose incomplete boundaries.
4. Run proportionate security, PostgreSQL/runtime and CI validation. Obtain fresh
   Agent2 review of changed code/final source. Commit/push only PR106's branch under
   standing authorization. Report actual facts separately from fixture evidence.

## Initial observations

The starting source holds independent workers/direct financial/messaging services
but explicitly does not claim independent IAM. Ordinary production startup still
requires provider credentials. Previous role fixture grants readiness/audit/key/
enqueue only and is not a normal API grants recipe.

Source investigation: normal profile/password/registration/approval paths share
Customers, AbpUsers, roles and outbox tables with sensitive administrative effects.
Blanket denial of those tables disables legitimate functions; broad DML also permits
privileged mutation. Agent2 is independently reviewing these overlaps read-only.

Next: inspect authenticated metadata and constrained actual role catalog; then select
the smallest useful qualification that does not mislabel a partial grants list.

## Updated evidence and selected scope

Authenticated Render metadata reads succeeded: API DATABASE_URL username matches
provider databaseUser; one managed credential, not a complete PG role inventory;
two external allowlist entries with no global IPv4/IPv6 rule; Bird/Yoco/DataProtection
credential presence. No values retained. Three bounded catalog attempts using the
existing API credential, provider external endpoint, TLS verify-full and read-only
session/transaction returned SSL connection closed unexpectedly, psql exit2, no
catalog result. Ownership/privileges/custody remain UNKNOWN/BLOCKED; no access or
provider settings changed. Transport cause is UNKNOWN, not proven allowlist rejection.

Source/Agent2 review found shared normal/privileged identity, role, Customer, outbox,
signing and message-key authority. A broad table GRANT recipe cannot independently
authorize actors, and blanket denial breaks normal functionality. No application or
live grants were changed. Prepared catalog-only SQL, essential-write contract,
separate identity/custody requirements and full-workflow acceptance gaps.

Extended existing disposable actual-schema probe: owner/non-owner read-only audit,
profile-SQL denial under the readiness role, temporary column-grant compatibility,
Host-admin credential mutation and outbox receipt forgery rejecting naive policies,
then rollback/revoke/fingerprint and original runtime checks. Six added checks,
23 total expected. First run was incomplete: a later Bash syntax error occurred
while its script was being edited; current syntax check passes, rerun only
after edits stop. That failed/incomplete run is not 23-pass evidence. Agent2 prompted
column-only and other-user-schema audit coverage; added both and an explicit column
grant counterexample check. This proves no production or complete API isolation.

Next: freeze source, rerun all23 local checks, mandatory209/security mutation checks,
update safe evidence, commit/push this branch, complete CI and exact-head Agent2 review.

Frozen-source rerun passed23/23; safe log retained. Mandatory209 passed with zero
skips; mutation fixtures still reject170pass/1skip and forged/wrong-context evidence.
Bash/actionlint/diff/link checks passed. Agent2 interim review: no remaining material
findings; all209 identities unchanged and21 source pins match. Exact committed-head
review and complete hosted CI remain pending. No application behavior or provider
settings were changed. Production owner/grants/custody and complete normal workflows
under a new role remain UNKNOWN/BLOCKED.

## Review correction after80cdc591

Both80cdc591 hosted runs passed17/17 on attempt1; Agent2 independently reconciled
209 security,1395 application,95 web/84 opt-in skips,78 dedicated PostgreSQL and23
runtime checks plus16 non-Docker artifact digests per run. This is historical source
evidence, not proof of the correction below.

OBSERVATION / EXPERIMENT RESULT: Agent2's network-none PostgreSQL18 reproduction
set inherited search_path=public,pg_catalog and supplied a public.current_setting
function reading a synthetic application row. The exact audit emitted that marker
instead of actual transactionReadOnly. ROOT CAUSE / BRANCH-INTRODUCED: unqualified
built-in lookup trusted the session path; READ ONLY prevents writes, not reads or
spoofed output. An otherwise identical query with local pg_catalog,pg_temp returned
actual 'on'. No production observation was attempted.

Correction: pin local lookup before inspection. Fold the hostile function/role/table
fixture into the existing catalog runtime check, positively prove raw substitution,
require actual 'on'/18 and existing role facts from the corrected audit, then reset/
drop all hostile state before later checks. Preserve23 checks and all209 identities;
update only the changed source pins. Next: frozen-source probe/security/mutations,
commit/push, complete final CI and fresh exact-head Agent2 review. Acceptance remains
BLOCKED on actual production authority/custody and full normal-role workflows.

Corrected frozen-source probe passed23/23, including the positive hostile-lookup
precondition and actual audit rejection; safe log replaced. Mandatory209/no skips
and mutation fixtures passed under local dirty-worktree80cdc591 context with new
policy digest3f42bbf9a63ddee1e4458dcb6e22c7ee78f256d3b0573c1a0ad19750bf2f4149.
Syntax/actionlint/diff checks passed. Application/runtime images remain unchanged
from the explicitly carried-forward local runtime source; final hosted CI must
rebuild exact committed images and bind new results. Agent2 review/reconciliation
of80cdc591 is historical; fresh corrected-commit review remains required.
