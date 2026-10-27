-- Separate owner recovery authorization and a verified write fence are mandatory.
-- Only restores this reset's before-images, before ANY prospective admissions.
\set ON_ERROR_STOP on
BEGIN ISOLATION LEVEL SERIALIZABLE READ WRITE;
SET LOCAL statement_timeout='30s'; SET LOCAL lock_timeout='3s';
LOCK TABLE public."EntryParticipations",public."Customers",public."AQGreenNetworkPlacements",
 public."AbpAuditLogs" IN SHARE ROW EXCLUSIVE MODE;
SELECT set_config('aqua.clean_start.reset_id',:'reset_id',true),
 set_config('aqua.clean_start.undo_authority',:'owner_undo_authorization_digest',true),
 set_config('aqua.clean_start.fence',:'write_fence_evidence_digest',true),
 set_config('aqua.clean_start.expected_archive',:'expected_archive_digest',true);
DO $restore$
DECLARE receipt jsonb; undo jsonb; envelope jsonb; boundary timestamptz; actual_digest text;
BEGIN
 IF current_setting('aqua.clean_start.undo_authority') !~ '^[0-9a-f]{64}$' OR
    current_setting('aqua.clean_start.undo_authority')=repeat('0',64) OR
    current_setting('aqua.clean_start.fence') !~ '^[0-9a-f]{64}$' OR
    current_setting('aqua.clean_start.fence')=repeat('0',64) THEN
  RAISE EXCEPTION 'CLEAN_START_RECOVERY_AUTHORIZATION_REQUIRED'; END IF;
 SELECT a."Parameters"::jsonb,a."ReturnValue"::jsonb->'receipt',a."ReturnValue"::jsonb->'undo'
  INTO STRICT envelope,receipt,undo FROM public."AbpAuditLogs" a
  WHERE a."TenantId"=1 AND a."ServiceName"='AQGreen.CleanStart' AND a."MethodName"='ArchiveDisposableTests'
   AND a."Parameters"::jsonb->>'resetId'=current_setting('aqua.clean_start.reset_id');
 boundary := (receipt->>'boundary')::timestamptz;
 SELECT encode(sha256(convert_to(system_identifier::text||'|'||current_database()||'|'||
  (SELECT oid::text FROM pg_database WHERE datname=current_database()),'UTF8')),'hex') INTO actual_digest FROM pg_control_system();
 IF actual_digest<>receipt->>'databaseIdentity' OR
  encode(sha256(convert_to(receipt::text,'UTF8')),'hex')<>envelope->>'receiptDigest' OR
  encode(sha256(convert_to(undo::text,'UTF8')),'hex')<>receipt->>'undoDigest' OR EXISTS
  (SELECT 1 FROM public."AbpAuditLogs" a WHERE a."TenantId"=1 AND a."ServiceName"='AQGreen.CleanStart'
   AND a."MethodName"='RestoreClassifiedTests' AND a."Parameters"::jsonb->>'resetId'=receipt->>'resetId') THEN
  RAISE EXCEPTION 'CLEAN_START_RECOVERY_JOURNAL_CHANGED_OR_REPLAYED'; END IF;
 SELECT encode(sha256(convert_to(coalesce(jsonb_agg(to_jsonb(p) ORDER BY "Id"),'[]')::text,'UTF8')),'hex')
 INTO actual_digest FROM public."EntryParticipations" p WHERE "TenantId"=1 AND "StartedAt"<boundary;
 IF actual_digest<>receipt->>'archiveDigest' OR actual_digest<>current_setting('aqua.clean_start.expected_archive') OR
  EXISTS(SELECT 1 FROM public."EntryParticipations" WHERE "TenantId"=1 AND "StartedAt">=boundary) OR
  EXISTS(SELECT 1 FROM public."Customers" c JOIN jsonb_array_elements(undo->'memberships') m
   ON c."Id"=(m->>'id')::integer WHERE c."TenantId"<>1 OR c."MembershipId" IS NOT NULL OR
    encode(sha256(convert_to((to_jsonb(c)-'MembershipId')::text,'UTF8')),'hex')<>m->>'otherFieldsDigest') THEN
  RAISE EXCEPTION 'CLEAN_START_RECOVERY_STATE_CHANGED'; END IF;
 UPDATE public."Customers" c SET "MembershipId"=(m->>'membershipId')::integer
  FROM jsonb_array_elements(undo->'memberships') m WHERE c."TenantId"=1 AND c."Id"=(m->>'id')::integer;
 UPDATE public."EntryParticipations" p SET "IsDeleted"=(v->>'isDeleted')::boolean,
  "DeletionTime"=(v->>'deletionTime')::timestamptz FROM jsonb_array_elements(undo->'participants') v
  WHERE p."TenantId"=1 AND p."Id"=(v->>'id')::uuid;
 INSERT INTO public."AbpAuditLogs"("TenantId","ServiceName","MethodName","Parameters","ExecutionTime","ExecutionDuration")
 VALUES(1,'AQGreen.CleanStart','RestoreClassifiedTests',jsonb_build_object('resetId',receipt->>'resetId',
  'authority',current_setting('aqua.clean_start.undo_authority'))::text,transaction_timestamp(),0);
END $restore$;
COMMIT;
