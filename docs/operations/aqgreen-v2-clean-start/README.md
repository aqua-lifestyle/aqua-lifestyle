# AQGreen V2 clean-start execution package

**PROPOSED execution package; production reset and release are not authorized.**
The owner's 27 October 2026 instruction establishes the clean-start objective and
classifies existing AQGreen records as test data, subject to preserving genuine
membership and financial obligations. It authorizes implementation and synthetic
validation only. This package does not assert that production eligibility passed.
The later [formal owner-attested classification](owner-attested-test-only-decision.md)
permits OWNER_ATTESTED_TEST_ONLY_V1 without independent merchant classification.
Provider mode remains NOT_INDEPENDENTLY_VERIFIED and all financial source records
and obligations must be preserved.

The status-only SQL and logical Render/TLS evidence are recorded in
[preflight-authorization.md](preflight-authorization.md). The actual 27 October
13:05 UTC diagnostic found payment presence alone; all nine other guards passed.
Those are observed snapshot statuses, not current reset eligibility. The new
branch verifies conservation during retirement and does not repeat merchant classification.

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
  owner-authorized classification and accepted conservation evidence must bind the exact `sourceDigest` and
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

Strict ZERO_PAYMENT_EXPOSURE_V1 still rejects any scoped payment/reference,
MemberPayment, Yoco receipt or checkout, including pending/deleted/terminated rows.
Explicit OWNER_ATTESTED_TEST_ONLY_V1 instead requires unchanged payment evidence,
matching historical fingerprints and verified before/after conservation of all
Tenant 1 financial records and prior audits. It requires the owner's attestation
digest and records provider mode as unverified. Both modes still reject real-money
commissions, loans, graduation funding, monthly obligations, Released/Paid
commissions, orphan links, unclassified membership, invalid lifecycle and
foreign/missing membership links. Immutable placements remain rejected.
Test-money commission rows are retained;
they are never reinterpreted as genuine earnings or removed. Guard counts may
overlap; they are not a monetary reconciliation. Unknown or omitted payment modes
cannot select owner-attested admission; its authority and conservation checks must
all be present. Strict zero-payment admission remains available separately.

## Operator inputs and proof still required

Xolani-M is the nominated operator. Aqua's existing B6 implementation acceptance
does not cover this new reset/admission contract. Retain the actual protected
identity/qualification/independence reference and obtain acceptance of this exact
clean-start package and source-bound preservation/retirement evidence. Disclose
the owner-attested classification without claiming independent merchant verification. Keep personal details and
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

The accepted status-only identity amendment applies to the read-only observation.
Native reset/D10 binding remains mandatory. Payment-presence FAIL remains a true
observation; the owner-attested retirement branch admits its preserved evidence
without pretending that the original zero-payment guard passed. Retirement
rechecks the nine other guards under locks and verifies all conservation hashes.

Use `classification.template.json` only as a preparation template. Complete its
real protected counterpart outside Git; unfilled/null fields cannot authorize a
reset. The status-only launcher's boundary, reset ID and approved UTC window are
defined in preflight-authorization.md; its output contains no counts. Retirement
requires `source_database`, `expected_database_identity_digest`
(native system/database/OID digest), `boundary_utc`, `reset_id`,
`expected_source_digest`, `classification_digest`,
`independent_acceptance_digest`, `owner_reset_authorization_digest`,
`recovery_evidence_digest`, `reset_sql_digest`, `no_genuine_memberships=true`,
`write_fence_evidence_digest`, `valid_from_utc` and `valid_until_utc`. Owner-attested
admission additionally requires `payment_admission_mode=OWNER_ATTESTED_TEST_ONLY_V1`
and `owner_test_only_attestation_digest`; neither authorizes production execution
without the separate exact reset authorization. The permitted
authorization window is at most 30 minutes; each SQL statement remains limited to
30 seconds. None of these variables carries credentials or member identities.
Supply credentials solely via the existing protected client environment.

Use only the reviewed status-only launcher for the amended preflight; it enforces
the provider endpoint, authenticated TLS and output whitelist. Retirement uses
existing `psql -X --no-password --set=ON_ERROR_STOP=1
--set=VERBOSITY=sqlstate --file=reset.sql` with separately approved protected inputs.
Disable shell tracing/history and result spooling. Never weaken TLS to recover a
connection. Verify reset COMMIT through the unchanged protected state/journal
procedure; the new pre-reset screen rejects a reused reset identity and is not
post-reset receipt acceptance. Native reset/D10 binding remains mandatory.

## Recovery safeguard and limits

The reset and audit before-images commit atomically. `restore.sql` is a separately
authorized, bounded reversal using those before-images. It verifies source identity,
journal hashes, unchanged archived population and unused recovery identity; it
refuses changed membership assignments, prospective participation and changed
financial source fingerprints, including late provider facts. Recovery must not
resurrect retired membership after new financial activity. It does not undo
migration 55 or change payment/financial records. Run only under a verified
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
2. Establish and verify the existing write fence; hold onboarding, financial
   processing, automatic payouts and V2 commissioning. Verify cancellation,
   recovery/cost and custody. Use the owner-attested classification and its frozen
   cohort/source fingerprint. Do not extend that approval to newly introduced
   membership or silently recapture/reapprove a changed dataset. Merchant mode
   stays explicitly unverified; no payment-classification investigation is required
   for this branch. The retirement transaction itself rechecks the nine other
   guards and conservation under locks; the completed diagnostic need not be repeated.
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
5. Populate the reviewed executor v3 owner-attested clean-start readback from real records,
   including independent-human evidence, reset acceptance before DDL authority,
   actual 54→55 application and forward-only hold-preserving recovery. Record
   owner attestation, NOT_INDEPENDENTLY_VERIFIED provider mode, conserved payment
   evidence and matching before/after financial fingerprints. Do not publish
   positive payment counts or claim genuine-exposure counts are verified zero.
   The original v2 verified-zero branch remains separately supported. Update its
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
The owner's test-only classification is recorded. Actual conservation/reset
acceptance, recovery/cost/custody readback, final source qualification and production
execution remain separate gates; no production mutation has occurred here.

## Single executable cutover proposal — OWNER_ATTESTED_TEST_ONLY_V1

This is one coordinated owner authorization package, executed by Xolani-M with
Aqua available for the actual retirement-evidence checkpoint. It grants no action
until the final exact-source operation authorization and protected inputs exist.

1. Pin the final application/executor commits, hashes and exact-source CI results.
   Confirm the frozen owner-attested cohort/source digest, native database pin,
   schema, protected input locations, operator cancellation and a reset subwindow
   of at most 30 minutes. Verify the existing Render recovery point/retention and
   zero incremental cost of the chosen actions. No paid clone/export/restore is
   included or presumed free. Verify the effective write fence and worker/payout
   holds before mutation. If any input/control is missing, stop before reset.
2. In the approved TLS environment (`verify-full`, system CA, SNI; no credential
   arguments), execute the exact reviewed reset once. The protected psql input
   file supplies the previously listed authority/identity/digest/window values and
   `payment_admission_mode=OWNER_ATTESTED_TEST_ONLY_V1` plus the owner-attestation
   digest. It contains no database credentials or downloaded source records.

   ```bash
   export PGOPTIONS='-c default_transaction_read_only=off -c statement_timeout=30000 -c lock_timeout=3000 -c idle_in_transaction_session_timeout=45000'
   timeout --signal=TERM --kill-after=5s 120s psql -X -qAt --no-password \
     --set=ON_ERROR_STOP=1 --set=VERBOSITY=sqlstate \
     --file="$AQGREEN_APPROVED_RESET_INPUTS" \
     --file=AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.EntityFrameworkCore/Operations/CleanStart/reset.sql
   ```

   Retain source rows/before-images only in the protected database and approved
   operational channel. Read back the actual committed state/journal. Aqua accepts
   conservation and retirement, with the owner-attested basis disclosed. A timeout
   or missing COMMIT acknowledgement is UNKNOWN until readback; never retry blindly.
3. Only after that checkpoint and exact DDL authority, apply retained migration 55
   once, with the same bounded statement/lock safeguards and a write-authorized
   session. Do not use a read-only session for this explicitly authorized step.

   ```bash
   export PGOPTIONS='-c default_transaction_read_only=off -c statement_timeout=30000 -c lock_timeout=3000 -c idle_in_transaction_session_timeout=45000'
   timeout --signal=TERM --kill-after=5s 120s psql -X -qAt --no-password \
     --set=ON_ERROR_STOP=1 --set=VERBOSITY=sqlstate \
     --file=AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.EntityFrameworkCore/Operations/CleanStart/migration-55.reviewed.sql
   ```

   Verify the actual 54-to-55 append, unchanged historical migration entries and
   new invalidation/append-only guards. Keep all financial/commissioning holds.
4. Record the actual v3 readback and its protected acceptance through the existing
   executor governance review; UNVERIFIED cannot deploy. After approved main merges
   and exact main-push CI, use the existing executor once:

   ```bash
   gh workflow run deploy.yml --repo aqua-lifestyle/aqua-deployment --ref main \
     -f ci_run_id="$AQGREEN_APPROVED_MAIN_CI_RUN" \
     -f ci_run_attempt="$AQGREEN_APPROVED_MAIN_CI_ATTEMPT" \
     -f sha="$AQGREEN_APPROVED_MAIN_SHA" -f qualification_only=false
   ```

   It verifies Render before Vercel and preserves source/provenance/replay and
   forward-recovery checks. A failed Render check blocks Vercel. Verify actual
   production identity, health, authentication/Area boundaries and deployment pairing.
5. Install the separately pinned D10 Decision/VerifiedAuthority/CleanStartEvidence
   through the existing restricted operator route, outside Git, with private file
   permissions and explicit first genuine root outside the retired cohort. Include
   actual reset/payment-conservation fields and the deployment/configuration hashes.
   Preserve the canonical next effective boundary, `2026-10-29T22:00:00Z`, unless
   the owner selects another valid future Friday 00:00 Africa/Johannesburg boundary.
   No premature membership activation or backdated financial calculation is allowed.
6. Under the same explicitly scoped commissioning/live-payment authorization,
   admit the selected root and verify one controlled genuine Yoco joining payment,
   its immutable receipt, awaiting-approval state, authorized Area approval and
   new placement. Bind the actual customer/Area, payment ceiling/currency, window
   and cancellation owner in protected inputs. Synthetic tests are not live-payment
   evidence. Verify retired records cannot qualify; inspect legitimate commission
   holds and, at an eligible closed week, actual calculation against real facts.
   Never fabricate a qualifying network or sales to force an earned result.
   Automatic payouts and uncommissioned financial workers remain off.

Stop on any non-payment guard, visibility/source mismatch, changed protected
record, missing acceptance, replay, cost uncertainty or expired window. Before
new admissions/financial changes, separately authorized `restore.sql` can restore
the exact retained before-images. Afterwards preserve migration 55 and all source
facts, keep holds and use forward recovery; no financial rollback or migration Down.
