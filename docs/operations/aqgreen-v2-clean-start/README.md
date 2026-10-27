# AQGreen V2 clean-start execution package

**PROPOSED execution package; production reset and release are not authorized.**
The owner's 27 October 2026 instruction establishes the clean-start objective and
classifies existing AQGreen records as test data, subject to preserving genuine
membership and financial obligations. It authorizes implementation and synthetic
validation only. This package does not assert that production eligibility passed.

The status-only read-only SQL, 16-table access scope, logical Render/TLS identity
amendment and separate Yoco/business cross-check are prepared in
[preflight-authorization.md](preflight-authorization.md). No production assessment
has been performed or authorized by preparing that request.

Application base: `73890aa1b7c843238bbca1b450000d43d8ef8b5b`; executor base:
`caf8b4bb34572feecccad30fdd8eb5b19b8116a9`. Changes are in the adjacent
`aqua-lifestyle-clean-start` and `aqua-deployment-clean-start` worktrees. They require
normal independent review, authorized publication/merge and complete exact-source
CI before production admission. Frozen B6 source `82fc64894bcc9cc459e4077c17af6aa714d7f747`
and its artifact acceptance remain intact. No historical B6 evidence is asserted.

## Exact reset and exclusions

The executable files are in
`AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.EntityFrameworkCore/Operations/CleanStart/`.
`state.sql` is also embedded in EF and reused by D10, preventing query drift.

- Scope: **every Tenant 1 EntryParticipation before the approved canonical V2
  boundary**, including deleted, rejected, pending and inactive rows. A protected,
  independently accepted classification must bind the exact `sourceDigest` and
  certify that every scoped membership is disposable test activity. SQL cannot
  infer real-world test classification from absence of payments.
- `reset.sql` changes only `EntryParticipations.IsDeleted/DeletionTime` and the
  AQGreen `Customers.MembershipId` assignment for those exact participants. It
  retains the participation rows, every other Customer field, all User/role and
  administrator access, configuration and other Tenants/programmes.
- No financial, payment, Yoco, approval, correction, attribution or existing audit
  row is deleted. Nothing is truncated. There is **no placement deletion**:
  existing immutable V2 placements cause rejection. Their append-only triggers
  are never disabled. The reset appends one protected `AbpAuditLogs` journal.
- All original changed-field values remain server-side in that journal's
  `ReturnValue`, with receipt/before-image hashes. Its access and retention must
  remain restricted. Never export that field or put it in public evidence/logs.
- A shared source fingerprint covers full scoped participation and Customer rows, placements
  and legacy AQGreen assignments. It must still match under transaction locks.
  Newly created/post-boundary participations abort the reset. Approval/recovery/
  write-fence references must be independently authenticated, not just self-hashed.

The guard rejects any scoped payment reference, MemberPayment, Yoco receipt or
checkout (including pending, deleted or terminated), real-money commission ledger,
loan, graduation funding decision, monthly obligation, Released/Paid commission,
orphan relationship, unclassified legacy AQGreen assignment, invalid lifecycle or
foreign/missing membership reference. Test-money commission rows are retained;
they are never reinterpreted as genuine earnings or removed. Guard counts may
overlap; zero is the required safety predicate, not a monetary reconciliation.
Unproven/unknown classification is a stop, not a disposable default.

## Operator inputs and proof still required

Xolani-M is the nominated operator. Aqua's existing B6 implementation acceptance
does not cover this new reset/admission contract. Retain the actual protected
identity/qualification/independence reference and obtain acceptance of this exact
clean-start package and source-bound classification. Keep personal details and
member/transaction data out of repositories and this conversation.

The protected operator record must contain all of:

1. Exact final application/executor revisions, migration inventory, reset/state/
   recovery SQL digests and successful tests/CI. Migration 55 source is unchanged.
2. Actual verified PostgreSQL source identity and TLS session, schema/history
   identity, 54 applied migrations, transaction snapshot/time, full-population
   source fingerprint and guard statuses from the status-only launcher. The 26 October
   metadata verification is carried forward, not a fresh eligibility assessment.
3. Classification and independent acceptance bound to that exact population;
   no genuine membership in the archived scope. Missing values do not mean zero.
4. Operator/data/security/platform authority, restricted custody/retention for
   sensitive reset/audit/business evidence and an actual financial write fence.
   The reduced status-only preflight receipt needs no raw-results storage route;
   it does not authorize workstation storage of sensitive production evidence.
5. Recovery evidence and explicit confirmation of zero incremental charges for
   any backup action. **No backup/export/recovery API has been invoked here.**
6. UTC reset/deployment window, maximum 30-second SQL statements, 3-second lock
   waits, 45-second idle transaction timeout, and Xolani-M's cancellation duty.
   On cancellation/error disconnect and verify rollback; never retry blindly.
7. Exact first genuine root Customer identity, outside the archived test population,
   authorized Area(s), canonical UTC effective boundary and capability selection.
   Keep that identity in the protected D10 decision only.

The retained programme boundary is **Friday 00:00 Africa/Johannesburg** (Thursday
22:00 UTC), not a UTC Monday. The next boundary after 27 October is
`2026-10-29T22:00:00Z`; this is a proposed activation time, not authorization or a
claim that prerequisites will be ready. Reset, actual receipt acceptance, DDL,
deployment and D10 authorization must finish beforehand. The owner must approve
the actual reset window (at most 30 minutes), deployment window and cancellation
responsibility; if readiness misses the boundary, stop and select another approved
canonical boundary. The reset's SQL receipt is checked against the same domain rule.

Archived test identities retain login/administrator access. Their old participation
and immutable audit references are not recycled into genuine membership. Choose
the first genuine root and subsequent member identities outside that archived
cohort; no uniqueness or audit constraint is relaxed to reuse a test participation.

The status-only preflight is a proposed amendment requiring review and owner
reauthorization of its new artifact hashes, logical-source identity mode and UTC
window. Prior raw-count query approval is not authority for this changed contract.
No production preflight was executed while preparing the amendment.

Use `classification.template.json` only as a preparation template. Complete its
real protected counterpart outside Git; unfilled/null fields cannot authorize a
reset. The status-only launcher's boundary, reset ID and approved UTC window are
defined in preflight-authorization.md; its output contains no counts. The unchanged
reset still requires `source_database`, `expected_database_identity_digest`
(native system/database/OID digest), `boundary_utc`, `reset_id`,
`expected_source_digest`, `classification_digest`,
`independent_acceptance_digest`, `owner_reset_authorization_digest`,
`recovery_evidence_digest`, `reset_sql_digest`, `no_genuine_memberships=true`,
`write_fence_evidence_digest`, `valid_from_utc` and `valid_until_utc`. The permitted
authorization window is at most 30 minutes; each SQL statement remains limited to
30 seconds. None of these variables carries credentials or member identities.
Supply credentials solely via the existing protected client environment.

Use only the reviewed status-only launcher for the amended preflight; it enforces
the provider endpoint, authenticated TLS and output whitelist. The unchanged
reset uses existing `psql -X --no-password --set=ON_ERROR_STOP=1
--set=VERBOSITY=sqlstate --file=reset.sql` with separately approved protected inputs.
Disable shell tracing/history and result spooling. Never weaken TLS to recover a
connection. Verify reset COMMIT through the unchanged protected state/journal
procedure; the new pre-reset screen rejects a reused reset identity and is not
post-reset receipt acceptance. Native reset/D10 binding remains mandatory.

## Recovery safeguard and limits

The reset and audit before-images commit atomically. `restore.sql` is a separately
authorized, bounded reversal using those before-images. It verifies source identity,
journal hashes, unchanged archived population and unused recovery identity; it
refuses changed membership assignments or any prospective participation. It does
not undo migration 55 or change payment/financial records. Run only under a verified
write fence, before admitting new members. A reset ID remains consumed after undo.

This journal is not an independent disaster backup: it shares the database's
failure domain and administrative access. Render continuously backs up paid
PostgreSQL databases, but recovery creates another database instance and cannot
restore the immediately preceding ten minutes. Logical exports expire after seven
days. The available recovery window and zero incremental export/recovery cost must
be confirmed for this account before any such operation. No free recovery or
successful restore is claimed. See [Render recovery documentation](https://render.com/docs/postgresql-backups).

## Coordinated production sequence — separately authorized operations

1. Independently review/publish the exact clean-start application/executor changes
   and obtain fresh full exact-source CI. Keep executor schema readback UNVERIFIED
   until actual operations below have completed. No synthetic record qualifies.
2. Establish the existing write fence; hold onboarding, financial processing,
   automatic payouts and V2 commissioning. Verify operator cancellation and the
   recovery/cost/custody records. Run the separately reauthorized status-only
   preflight launcher using existing TLS; retain only its reduced receipt and bind
   its source fingerprint to authoritative disposable-test classification. UNKNOWN
   external obligations/classification require actual independent business review.
3. Obtain **separate exact reset authorization** for `reset.sql`, source identity,
   approved fingerprint, classification, recovery, write fence and UTC window.
   Execute once using the corresponding protected psql variables. It requires the
   reviewed pre-DDL 54-migration schema and rolls back on any mismatch. Read back
   the committed receipt/state; independently accept the result.
4. Obtain **separate exact migration authorization**. Apply only retained
   `migration-55.reviewed.sql`, SHA-256
   `1f45ef7b67fa6eae747ca70edfed05679ef63cf4cfe2f2cf308b9d31b8c1faea`.
   Verify 55 exact history entries, one target migration, unchanged previous 54,
   invalidation table/key/validation and append-only guards. No automatic startup
   migrations. Do not regenerate or edit the retained SQL.
5. Populate the reviewed executor v2 clean-start schema readback from real records,
   including independent-human evidence, reset acceptance before DDL authority,
   actual 54→55 application and forward-only hold-preserving recovery. Update its
   pinned digest through protected executor review. The executor preserves existing
   administrator merge, CI freshness, exact-source, anti-replay, Render-first and
   Vercel provenance controls. It still cannot execute unapproved schema changes.
6. Obtain the coordinated release authorization; deploy the exact candidate to
   existing Render with `AdmissionMode=CLEAN_START`, commissioning disabled,
   normal recovery mode false and financial/background/weekly/monthly operations
   explicitly false. The captured clean-start HTTP fence blocks business requests
   and callbacks while allowing authentication, health and protected deployment
   readiness; provider startup validation remains mandatory. Verify that actual
   fence before proceeding. Keep the old process fenced during configuration/build
   transition; do not restart it into an unfenced legacy runtime. Verify
   source/health/schema and holds, then deploy matching
   Vercel and verify API pairing, authentication/Areas and production alias.
   Any failed readback leaves recovery/financial/V2 holds in place; no automatic
   rollback to legacy financial processing and no migration Down.
7. Prepare protected D10 Decision/VerifiedAuthority/CleanStartEvidence inputs,
   with actual post-reset state, receipt, classification/independent acceptance,
   source/schema/assembly/configuration digests and canonical effective time.
   Explicitly set `AdmissionMode=CLEAN_START`; use `CleanStartEvidencePath/Sha256`,
   never a fabricated B6 manifest. Historical collections are empty; root Customer
   identity is explicit. Initial workers, graduation and automatic payouts stay off.
8. Obtain **separate D10/first-root/controlled live-Yoco authorization**. Commission
   at the approved boundary by controlled restart with the pinned D10 inputs and
   explicitly authorized financial operations for payment processing; background,
   weekly/monthly automation and automatic payouts remain disabled. Configuration
   digests bind financial/background flags; a reload cannot clear the initial
   HTTP fence. Host-only `AQGreenCleanStartRoot.AuthorizeRoot` creates
   only the owner-selected prospective root's unpaid participation and immutable
   root attribution; ordinary payment confirmation and authorized Area approval
   still control activation/placement. Subsequent members use normal invitations,
   joining, real checkout verification and approval. Do not enable pilot/demo modes.
   No fake payment, test gateway or synthetic ledger is permitted in production.
9. Verify the controlled real-payment result independently. Keep commission holds,
   invalid-sales guards and payout release checks. Weekly automation, graduation,
   financial release and automatic payouts require their own later authority.
   The existing `FinancialWorkersCommissioningEvidenceDigest` remains a separate
   verified-authority gate: initial clean-start evidence omits it, so workers are
   held. A separately accepted worker-commissioning record and matching pinned
   configuration can enable weekly processing later. It does not authorize
   automatic payouts, which CLEAN_START continues to reject.

## Status

Implementation/non-production results and executable SQL identities are recorded
in [validation.md](validation.md) and
`docs/exec-plans/active/aqgreen-v2-clean-start-20261027.md`.
Production classification, clean-start preflight, recovery/cost readback, package
acceptance, source publication/CI and execution approvals remain UNVERIFIED.
