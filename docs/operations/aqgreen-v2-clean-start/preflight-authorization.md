# Exact production preflight authorization request — 27 October 2026

**PREPARED; NOT AUTHORIZED OR EXECUTED.** No real-data finding is asserted.
This requests one read-only assessment, not reset, DDL, deployment, financial
release or live-payment changes. Accepted B6 work remains intact.

CI status below records pre-publication evidence. The owner subsequently
authorized committing the candidate and existing CI execution; use the published
commit's GitHub check runs for actual results. That instruction explicitly does
not authorize this production-data preflight.

## Frozen SQL and source

Directory:
`AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.EntityFrameworkCore/Operations/CleanStart/`.

| Executed file | SHA-256 |
|---|---|
| preflight.sql | `12f2e7cee40682e6bf1d031211842143d0d0a8de5dcf79d5333a06555a1152d6` |
| state.sql, included by preflight.sql | `db3d36ac23172c2005aa6edd202ea6f9cb6b4635c75c1bdbd2889f47571fd100` |

Both files are unchanged. Approval must name both; the entry script alone is not
the complete query. Source: existing Render `dpg-d9f0jne1a83c73d8m780-a`, database
`aqualifestyle`, Tenant 1. Operator: Xolani-M. No production connection was made.

The 16 public tables read are:

- AQGreenJoiningCheckouts
- AQGreenMonthlyObligationCheckouts
- AQGreenNetworkPlacements
- AQGreenRecruitmentAttributions
- AbpAuditLogs (clean-start operator journal only)
- Customers
- EntryCommissionPeriods
- EntryMonthlyObligations
- EntryParticipations
- EntryWeeklyCommissions
- MemberPayments
- Memberships
- OnyxGraduationDecisions
- OnyxLoanAgreements
- YocoWebhookReceipts
- __EFMigrationsHistory

Metadata uses `pg_catalog.pg_database`, this session's `pg_stat_ssl` row,
`information_schema.columns` for the public schema, and `pg_catalog.pg_control_system()`.
Historical participation includes every status and deleted row before the approved
boundary. Guards also cover scoped customers and orphan/pre-boundary financial
activity. This is not an account-wide Yoco reconciliation. Future-boundary
participations are separately prohibited by the reset's fresh guard; this
preflight alone is not authority to reset.

## Permissions and read-only enforcement

The existing login needs CONNECT to the database, USAGE on public, SELECT on the
16 tables (including deleted rows and all columns used for source fingerprints),
visibility of the referenced metadata and EXECUTE on pg_control_system() and the
built-in observation/hash functions. It must see the complete Tenant population;
an RLS-filtered or partially authorized view cannot establish an empty population.
No INSERT, UPDATE, DELETE, TRUNCATE, CREATE, role creation or privilege change is
needed. If existing permissions are insufficient, stop; do not grant anything.
Existing write privileges are not authorized for use by this assessment.

Inspection confirms `REPEATABLE READ READ ONLY`, 30-second statement, 3-second
lock and 45-second idle-in-transaction limits, actual server-side TLS and expected
native database identity checks before population reads, and final ROLLBACK.
set_config changes are transaction local. The SQL only reads and hashes rows;
there is no persistent mutation, export, COPY, sequence advance or DDL. PostgreSQL
enforces read-only mode. Full-row hashing remains inside the server. Stop on any
timeout/error and disconnect; no retry or weaker TLS. One execution only.

## Output and channel authorization

Source rows processed inside PostgreSQL are RESTRICTED production data. Output is
CONFIDENTIAL aggregate/evidence metadata: population/archive/placement/legacy and
guard counts, fingerprints, receipt/boundary timestamps, migration count/latest
ID, schema/native identity, TLS/read-only indicators and snapshot/time evidence.
No names, member identifiers, payment references, individual amounts, raw
financial records or audit before-images are returned. Database/resource/reset
identifiers are operational metadata, not member identities.

**The unchanged SQL does not suppress small cells.** Exact counts and fingerprints
must stay in the named existing protected operator/reviewer channel. Do not paste
raw psql output into this conversation, GitHub, application logs, CI artifacts,
shell history or local result files. The conversation may receive only status,
validated non-identifying zero/nonzero guard indicators and safe aggregate counts;
suppress positive counts whenever disclosure could identify individuals. Zero
alone never proves test-data classification or absent obligations. No raw result
stream may pass through a tool whose output appears in chat. Stop if an existing
protected execution/output channel cannot be identified.

The protected authorization must fix: execution/output channel reference and its
custodian/access/retention; expected native identity digest from the existing
verified source record; reset_id; boundary_utc; UTC assessment window; operator
authority and Xolani-M's cancellation responsibility. These values are not filled
in the current classification template. Previously proposed boundary
`2026-10-29T22:00:00Z` remains unapproved and creates no activation authority.

From the reviewed directory, after approval and protected variable binding:

```bash
psql -X --no-password --set=ON_ERROR_STOP=1 --set=VERBOSITY=sqlstate \
  --set=source_database=aqualifestyle \
  --set=expected_database_identity_digest="$B6_APPROVED_DATABASE_IDENTITY_DIGEST" \
  --set=boundary_utc="$B6_APPROVED_BOUNDARY_UTC" \
  --set=reset_id="$B6_APPROVED_RESET_ID" \
  --file=preflight.sql
```

This is an operator invocation template, not an approved executable operation.
Use the existing protected credential/TLS environment with tracing/history and
spooling disabled. No credentials in arguments, no stdout-to-file redirection,
no production configuration changes. Raw output belongs only in the approved
protected session; permitted redacted results may be reported separately.

## Required authoritative cross-check — not performed

The owner's classification is intent, not proof of zero financial exposure.
No authoritative merchant-history or outstanding-obligation record/reference was
provided or accessed. Do not certify clean start from database zeros, IsTestMoney
flags, current Yoco mode, missing webhooks or prior synthetic payment tests.

The existing authorized Yoco account custodian must review complete relevant
live merchant history through the assessment cutoff: all pages/accounts used by
the business, successful/pending/unmatched transactions, settlements, refunds,
disputes and chargebacks. Privately reconcile identifiers/references/purpose,
amount/currency and status with Aqua records where authorized. An empty search
or missing metadata does not prove completeness. Any real/unmatched/uncertain
item touching the disposable population is a STOP. Refund/cancellation alone
does not establish absence of continuing obligations.

The business/finance custodian must check genuine membership/benefit rights,
cash/manual/off-system receipts, unpaid/held commissions, loans, graduation
funding, monthly obligations, payouts and unresolved customer/provider claims.
Cover the entire classified population and unlinked historical AQGreen activity,
including deleted/inactive records. Under the existing write fence, record
provider/business cutoff and observation times, completeness, source references
and exceptions; bind acceptance to the actual SQL fingerprint/snapshot. Resolve
cutoff/in-flight-payment gaps before acceptance. No identities, transaction
details or raw financial records belong here.

This SQL request does not authorize agent access to provider transactions or
off-system financial records. Obtain a protected signed custodian review reference,
or separately authorize precise read-only provider/business-record access and
channel. Aqua must accept the actual classification/cross-check and new clean-start
contract independently; existing B6 implementation acceptance is retained.

## Release gate and recovery status

Local carried-forward evidence: 126 backend/PostgreSQL cases, one PostgreSQL
joining flow, 137 executor cases and five workflow cases passed. The executor's
real PostgreSQL concurrent authorization-use test remains SKIPPED locally because
AQUA_TEST_DATABASE_URL was unset. Existing CI supplies that variable and a
disposable PostgreSQL service; configuration is not executed evidence.

Both candidates remain uncommitted worktrees, at application base
`73890aa1b7c843238bbca1b450000d43d8ef8b5b` and executor base
`caf8b4bb34572feecccad30fdd8eb5b19b8116a9`. There is no published exact-source
commit/run for these changes. **The mandatory release gates are not all satisfied.**
Authorized publication/review and fresh application/executor CI must establish
all required checks, the unchanged 240-case mandatory inventory, explicit
PostgreSQL execution/markers and the concurrent authorization-use result. Schema
readback remains UNVERIFIED. No local skip is waived or reported as passed.

Existing Render TLS/PITR availability is carried forward; operation-specific
protected backup identity/cutoff/recovery readiness, custody, retention, zero
incremental charges and effective write fence remain UNVERIFIED. No backup,
export or restore was requested. Do not charge, provision or export to resolve
this silently. Tested in-database before-images can reverse only this reset before
prospective admissions and while integrity checks pass; they share the source
database's failure domain and are not an independent backup. No automatic
migration Down or rollback to legacy financial processing.

## Separate production authorization after actual evidence acceptance

Protected backup (existing facility only; zero additional charge and protected
custody confirmed) → controlled classified-test archival reset → committed reset
readback/Aqua acceptance → separate migration-55 authorization and retained SQL →
database verification (55 exact history entries and append-only/hold checks) →
Render in held CLEAN_START mode and authenticated readiness → matching Vercel →
separately authorized D10/root commissioning → controlled live Yoco validation.
Automatic payouts stay off; financial worker commissioning/release stays separately
gated. Any mismatch, real exposure, unresolved exception, failed check, unknown
recovery/cost/custody or broken fence stops before the next mutation.

Exact reset/exclusions, migration SQL hash, configuration and stop/recovery
details remain in [README.md](README.md). Approving this read-only preflight does
not authorize any part of the production mutation sequence.
