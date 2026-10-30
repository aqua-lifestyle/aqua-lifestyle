-- REVIEWED PREPARATION ONLY. Separate exact production reset authority is required.
-- All psql variables below come from the protected, independently accepted package.
-- No credentials, identities or approval records belong in command arguments.
\set ON_ERROR_STOP on
\if :{?boundary_policy}
\else
\set boundary_policy CANONICAL_FINANCIAL_WEEK_V1
\endif
\if :{?boundary_authorization_digest}
\else
\set boundary_authorization_digest ''
\endif
\if :{?payment_admission_mode}
\else
\set payment_admission_mode ZERO_PAYMENT_EXPOSURE_V1
\endif
\if :{?owner_test_only_attestation_digest}
\else
\set owner_test_only_attestation_digest ''
\endif
\o /dev/null
BEGIN ISOLATION LEVEL SERIALIZABLE READ WRITE;
SET LOCAL statement_timeout='30s';
SET LOCAL lock_timeout='3s';
SET LOCAL idle_in_transaction_session_timeout='45s';
-- Block concurrent onboarding, payment receipts, audit appends and financial writes
-- for the bounded reset. A lock timeout aborts; there is no automatic retry.
LOCK TABLE public."EntryParticipations",public."Customers",public."Memberships",
 public."AQGreenNetworkPlacements",public."AQGreenRecruitmentAttributions",
 public."MemberPayments",public."YocoWebhookReceipts",public."AQGreenJoiningCheckouts",
 public."AQGreenMonthlyObligationCheckouts",public."EntryWeeklyCommissions",
 public."OnyxLoanAgreements",public."OnyxGraduationDecisions",public."EntryMonthlyObligations",
 public."AbpAuditLogs",public."__EFMigrationsHistory" IN SHARE ROW EXCLUSIVE MODE;
SELECT set_config('aqua.clean_start.tenant','1',true),
 set_config('aqua.clean_start.boundary',:'boundary_utc',true),
 set_config('aqua.clean_start.boundary_policy',:'boundary_policy',true),
 set_config('aqua.clean_start.boundary_authority',:'boundary_authorization_digest',true),
 set_config('aqua.clean_start.reset_id',:'reset_id',true),
 set_config('aqua.clean_start.expected_source',:'expected_source_digest',true),
 set_config('aqua.clean_start.expected_identity',:'expected_database_identity_digest',true),
 set_config('aqua.clean_start.database',:'source_database',true),
 set_config('aqua.clean_start.classification',:'classification_digest',true),
 set_config('aqua.clean_start.reviewer',:'independent_acceptance_digest',true),
 set_config('aqua.clean_start.owner',:'owner_reset_authorization_digest',true),
 set_config('aqua.clean_start.recovery',:'recovery_evidence_digest',true),
 set_config('aqua.clean_start.sql',:'reset_sql_digest',true),
 set_config('aqua.clean_start.no_genuine_memberships',:'no_genuine_memberships',true),
 set_config('aqua.clean_start.valid_from',:'valid_from_utc',true),
 set_config('aqua.clean_start.valid_until',:'valid_until_utc',true),
 set_config('aqua.clean_start.fence',:'write_fence_evidence_digest',true),
 set_config('aqua.clean_start.payment_mode',:'payment_admission_mode',true),
 set_config('aqua.clean_start.owner_test_attestation',:'owner_test_only_attestation_digest',true);
DO $identity$
DECLARE key text; actual_identity text;
BEGIN
 IF current_setting('aqua.clean_start.boundary_policy') NOT IN
      ('CANONICAL_FINANCIAL_WEEK_V1','OWNER_AUTHORIZED_PROSPECTIVE_UTC_V1') OR
    (current_setting('aqua.clean_start.boundary_policy')='OWNER_AUTHORIZED_PROSPECTIVE_UTC_V1' AND
      (current_setting('aqua.clean_start.boundary_authority') !~ '^[0-9a-f]{64}$' OR
       current_setting('aqua.clean_start.boundary_authority')=repeat('0',64))) OR
    (current_setting('aqua.clean_start.boundary_policy')='CANONICAL_FINANCIAL_WEEK_V1' AND
      current_setting('aqua.clean_start.boundary_authority')<>'') THEN
  RAISE EXCEPTION 'CLEAN_START_EXPLICIT_BOUNDARY_AUTHORITY_REQUIRED'; END IF;
 IF current_setting('aqua.clean_start.payment_mode') NOT IN ('ZERO_PAYMENT_EXPOSURE_V1','OWNER_ATTESTED_TEST_ONLY_V1') OR
    (current_setting('aqua.clean_start.payment_mode')='OWNER_ATTESTED_TEST_ONLY_V1' AND
     (current_setting('aqua.clean_start.owner_test_attestation') !~ '^[0-9a-f]{64}$' OR
      current_setting('aqua.clean_start.owner_test_attestation')=repeat('0',64))) OR
    (current_setting('aqua.clean_start.payment_mode')='ZERO_PAYMENT_EXPOSURE_V1' AND
     current_setting('aqua.clean_start.owner_test_attestation')<>'') THEN
  RAISE EXCEPTION 'CLEAN_START_EXPLICIT_PAYMENT_CLASSIFICATION_REQUIRED'; END IF;
 IF current_setting('aqua.clean_start.reset_id') !~ '^[A-Za-z0-9.-]{1,80}$' OR
    current_setting('aqua.clean_start.no_genuine_memberships') <> 'true' THEN
  RAISE EXCEPTION 'CLEAN_START_CLASSIFICATION_REQUIRED'; END IF;
 IF transaction_timestamp() NOT BETWEEN current_setting('aqua.clean_start.valid_from')::timestamptz AND
    current_setting('aqua.clean_start.valid_until')::timestamptz OR
    current_setting('aqua.clean_start.valid_until')::timestamptz>
      current_setting('aqua.clean_start.valid_from')::timestamptz+interval '30 minutes' THEN
  RAISE EXCEPTION 'CLEAN_START_AUTHORIZED_WINDOW_INVALID'; END IF;
 FOREACH key IN ARRAY ARRAY['classification','reviewer','owner','recovery','sql','fence'] LOOP
  IF current_setting('aqua.clean_start.'||key) !~ '^[0-9a-f]{64}$' OR
     current_setting('aqua.clean_start.'||key)=repeat('0',64) THEN
   RAISE EXCEPTION 'CLEAN_START_AUTHORITY_REQUIRED'; END IF;
 END LOOP;
 SELECT encode(sha256(convert_to(system_identifier::text||'|'||current_database()||'|'||
  (SELECT oid::text FROM pg_database WHERE datname=current_database()),'UTF8')),'hex')
 INTO actual_identity FROM pg_control_system();
 IF current_database()<>current_setting('aqua.clean_start.database') OR
    actual_identity<>current_setting('aqua.clean_start.expected_identity') THEN
  RAISE EXCEPTION 'CLEAN_START_SOURCE_IDENTITY_CHANGED'; END IF;
 IF EXISTS(SELECT 1 FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
   WHERE n.nspname='public' AND c.relname=ANY(ARRAY['EntryParticipations','Customers','Memberships',
    'AQGreenNetworkPlacements','AQGreenRecruitmentAttributions','MemberPayments','YocoWebhookReceipts',
    'AQGreenJoiningCheckouts','AQGreenMonthlyObligationCheckouts','EntryWeeklyCommissions','OnyxLoanAgreements',
    'OnyxGraduationDecisions','EntryMonthlyObligations','EntryCommissionPeriods','AbpAuditLogs','__EFMigrationsHistory'])
    AND pg_catalog.row_security_active(c.oid)) THEN
  RAISE EXCEPTION 'CLEAN_START_FULL_VISIBILITY_REQUIRED'; END IF;
 IF (SELECT count(*) FROM public."__EFMigrationsHistory")<>54 OR
    (SELECT max("MigrationId") FROM public."__EFMigrationsHistory")<>'20261005194901_AddIsolatedAQGreenPilotEvidence' OR
    to_regclass('public."AQGreenSalesEvidenceInvalidations"') IS NOT NULL THEN
  RAISE EXCEPTION 'CLEAN_START_REQUIRES_REVIEWED_PRE_DDL_SCHEMA'; END IF;
 IF current_setting('aqua.clean_start.boundary')::timestamptz < transaction_timestamp() OR
    (current_setting('aqua.clean_start.boundary_policy')='CANONICAL_FINANCIAL_WEEK_V1' AND
      (extract(isodow FROM current_setting('aqua.clean_start.boundary')::timestamptz AT TIME ZONE 'Africa/Johannesburg')<>5 OR
       (current_setting('aqua.clean_start.boundary')::timestamptz AT TIME ZONE 'Africa/Johannesburg')::time<>time '00:00:00')) THEN
  RAISE EXCEPTION 'CLEAN_START_BOUNDARY_OR_POPULATION_CHANGED'; END IF;
END $identity$;
\ir state.sql
DO $guard$
DECLARE s jsonb := current_setting('aqua.clean_start.observed_state')::jsonb; key text;
BEGIN
 PERFORM set_config('aqua.clean_start.before',s::text,true);
 IF EXISTS (SELECT 1 FROM public."EntryParticipations" WHERE "TenantId"=1 AND
      "StartedAt">=current_setting('aqua.clean_start.boundary')::timestamptz) THEN
  RAISE EXCEPTION 'CLEAN_START_BOUNDARY_OR_POPULATION_CHANGED'; END IF;
 IF s->>'sourceDigest'<>current_setting('aqua.clean_start.expected_source') THEN
  RAISE EXCEPTION 'CLEAN_START_APPROVED_DATASET_CHANGED'; END IF;
 IF EXISTS(SELECT 1 FROM public."AbpAuditLogs" WHERE "TenantId"=1 AND "ServiceName"='AQGreen.CleanStart' AND
  "MethodName"='ArchiveDisposableTests' AND CASE WHEN "TenantId"=1 AND "ServiceName"='AQGreen.CleanStart' AND
  "MethodName"='ArchiveDisposableTests' THEN "Parameters"::jsonb->>'resetId' END=current_setting('aqua.clean_start.reset_id')) THEN
  RAISE EXCEPTION 'CLEAN_START_RESET_REPLAY'; END IF;
 IF (s->>'placementCount')::bigint<>0 THEN
  RAISE EXCEPTION 'CLEAN_START_IMMUTABLE_V2_PLACEMENT_EXISTS'; END IF;
 FOREACH key IN ARRAY ARRAY['paymentEvidenceDigest','tenantFinancialRecordsDigest','auditHistoryDigest','participationFactsDigest'] LOOP
  IF s->>key IS NULL OR s->>key !~ '^[0-9a-f]{64}$' THEN
   RAISE EXCEPTION 'CLEAN_START_PRESERVATION_EVIDENCE_REQUIRED'; END IF;
 END LOOP;
 FOREACH key IN ARRAY ARRAY['unclassifiedMembershipCount','realCommissionCount',
  'loanCount','graduationCount','monthlyObligationCount','payoutCount','orphanTopologyCount','invalidParticipationCount'] LOOP
  IF NOT s ? key OR s->key='null'::jsonb OR (s->>key)::bigint<>0 THEN
   RAISE EXCEPTION 'CLEAN_START_PROTECTED_OR_UNCLASSIFIED_EXPOSURE'; END IF;
 END LOOP;
 IF NOT s ? 'protectedPaymentCount' OR s->'protectedPaymentCount'='null'::jsonb OR
    (s->>'protectedPaymentCount')::bigint<0 OR
    ((s->>'protectedPaymentCount')::bigint<>0 AND current_setting('aqua.clean_start.payment_mode')<>'OWNER_ATTESTED_TEST_ONLY_V1') THEN
  RAISE EXCEPTION 'CLEAN_START_PROTECTED_OR_UNCLASSIFIED_EXPOSURE'; END IF;
END $guard$;
-- Retain only the before-images of the fields/projection this reset changes.
-- These remain in the protected database audit journal, never in stdout/files.
DO $undo$
DECLARE undo jsonb;
BEGIN
 IF clock_timestamp()>current_setting('aqua.clean_start.valid_until')::timestamptz THEN
  RAISE EXCEPTION 'CLEAN_START_AUTHORIZED_WINDOW_EXPIRED'; END IF;
 SELECT jsonb_build_object(
  'participants',(SELECT coalesce(jsonb_agg(jsonb_build_object('id',"Id",'isDeleted',"IsDeleted",'deletionTime',"DeletionTime")),'[]')
    FROM public."EntryParticipations" WHERE "TenantId"=1 AND NOT "IsDeleted"),
  'memberships',(SELECT coalesce(jsonb_agg(jsonb_build_object('id',c."Id",'membershipId',c."MembershipId",
      'otherFieldsDigest',encode(sha256(convert_to((to_jsonb(c)-'MembershipId')::text,'UTF8')),'hex'))),'[]')
    FROM public."Customers" c JOIN public."Memberships" m ON c."MembershipId"=m."Id"
    WHERE c."TenantId"=1 AND m."MembershipType"=2 AND EXISTS
     (SELECT 1 FROM public."EntryParticipations" p WHERE p."TenantId"=1 AND p."CustomerId"=c."Id")),
  'placements','[]'::jsonb
 ) INTO undo;
 PERFORM set_config('aqua.clean_start.undo',undo::text,true);
END $undo$;
-- V2 placement immutability is preserved: a nonempty projection already aborted.
-- Preserve identities and every Customer field except the disposable AQGreen
-- catalog assignment; other programmes and every other Tenant are untouched.
UPDATE public."Customers" c SET "MembershipId"=NULL
 FROM public."Memberships" m WHERE c."TenantId"=1 AND c."MembershipId"=m."Id" AND m."MembershipType"=2
 AND EXISTS (SELECT 1 FROM public."EntryParticipations" p WHERE p."TenantId"=1 AND p."CustomerId"=c."Id");
-- Preserve participation source rows and their approval/correction/payment FKs.
UPDATE public."EntryParticipations" SET "IsDeleted"=true,
 "DeletionTime"=coalesce("DeletionTime",transaction_timestamp()) WHERE "TenantId"=1 AND NOT "IsDeleted";
\ir state.sql
DO $receipt$
DECLARE s jsonb := current_setting('aqua.clean_start.observed_state')::jsonb;
 before_state jsonb := current_setting('aqua.clean_start.before')::jsonb; receipt text;
BEGIN
 IF clock_timestamp()>current_setting('aqua.clean_start.valid_until')::timestamptz THEN
  RAISE EXCEPTION 'CLEAN_START_AUTHORIZED_WINDOW_EXPIRED'; END IF;
 IF (s->>'populationCount')::bigint<>(s->>'archivedCount')::bigint OR
    (s->>'placementCount')::bigint<>0 OR (s->>'legacyMembershipCount')::bigint<>0 THEN
  RAISE EXCEPTION 'CLEAN_START_RESET_POSTCONDITION_FAILED'; END IF;
 IF s->>'paymentEvidenceDigest' IS DISTINCT FROM before_state->>'paymentEvidenceDigest' OR
    s->>'tenantFinancialRecordsDigest' IS DISTINCT FROM before_state->>'tenantFinancialRecordsDigest' OR
    s->>'auditHistoryDigest' IS DISTINCT FROM before_state->>'auditHistoryDigest' OR
    s->>'participationFactsDigest' IS DISTINCT FROM before_state->>'participationFactsDigest' OR
    s->'protectedPaymentCount' IS DISTINCT FROM before_state->'protectedPaymentCount' THEN
  RAISE EXCEPTION 'CLEAN_START_PROTECTED_FINANCIAL_OR_AUDIT_RECORD_CHANGED'; END IF;
 receipt := jsonb_build_object('resetId',current_setting('aqua.clean_start.reset_id'),
  'tenantId',1,'archiveDigest',s->>'archiveDigest','archiveCount',s->'archivedCount',
  'beforeDigest',current_setting('aqua.clean_start.expected_source'),
  'classification',current_setting('aqua.clean_start.classification'),
  'reviewer',current_setting('aqua.clean_start.reviewer'),'owner',current_setting('aqua.clean_start.owner'),
  'recovery',current_setting('aqua.clean_start.recovery'),'sql',current_setting('aqua.clean_start.sql'),
  'fence',current_setting('aqua.clean_start.fence'),'boundary',current_setting('aqua.clean_start.boundary'),
  'boundaryPolicy',CASE WHEN current_setting('aqua.clean_start.boundary_policy')='OWNER_AUTHORIZED_PROSPECTIVE_UTC_V1'
    THEN current_setting('aqua.clean_start.boundary_policy') END,
  'boundaryAuthorizationDigest',nullif(current_setting('aqua.clean_start.boundary_authority'),''),
  'databaseIdentity',current_setting('aqua.clean_start.expected_identity'),
  'paymentAdmissionMode',current_setting('aqua.clean_start.payment_mode'),
  'ownerTestOnlyAttestationDigest',nullif(current_setting('aqua.clean_start.owner_test_attestation'),''),
  'providerModeVerification','NOT_INDEPENDENTLY_VERIFIED',
  'protectedPaymentCount',s->'protectedPaymentCount',
  'paymentEvidenceDigest',s->>'paymentEvidenceDigest',
  'tenantFinancialRecordsDigest',s->>'tenantFinancialRecordsDigest',
  'auditHistoryDigest',s->>'auditHistoryDigest',
  'resetAt',transaction_timestamp(),
  'undoDigest',encode(sha256(convert_to(current_setting('aqua.clean_start.undo'),'UTF8')),'hex'))::text;
 INSERT INTO public."AbpAuditLogs"("TenantId","ServiceName","MethodName","Parameters","ReturnValue","ExecutionTime","ExecutionDuration")
 VALUES(1,'AQGreen.CleanStart','ArchiveDisposableTests',
  jsonb_build_object('resetId',current_setting('aqua.clean_start.reset_id'),
   'receiptDigest',encode(sha256(convert_to(receipt,'UTF8')),'hex'))::text,
  jsonb_build_object('receipt',receipt::jsonb,'undo',current_setting('aqua.clean_start.undo')::jsonb)::text,
  transaction_timestamp(),0);
END $receipt$;
COMMIT;
\o
SELECT 'CLEAN_START_RESET_COMMITTED';
-- The operator must read back state/receipt after COMMIT before declaring success.
