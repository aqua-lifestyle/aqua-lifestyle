-- Status-only database screen. Counts never leave PostgreSQL.
-- Canonical exposure CTEs are kept identical to state.sql by a regression check.
WITH settings AS (
 SELECT current_setting('aqua.clean_start.tenant')::integer AS tenant,
        current_setting('aqua.clean_start.boundary')::timestamptz AS boundary
), p AS MATERIALIZED (
 SELECT e.* FROM public."EntryParticipations" e, settings s
 WHERE e."TenantId"=s.tenant AND e."StartedAt"<s.boundary
), placement AS MATERIALIZED (
 SELECT n.* FROM public."AQGreenNetworkPlacements" n, settings s
 WHERE n."TenantId"=s.tenant AND n."ParticipantId" IN (SELECT "Id" FROM p)
), legacy AS MATERIALIZED (
 SELECT c."Id", c."MembershipId" FROM public."Customers" c
 JOIN public."Memberships" m ON m."Id"=c."MembershipId", settings s
 WHERE c."TenantId"=s.tenant AND m."MembershipType"=2 AND
 NOT EXISTS (SELECT 1 FROM public."EntryParticipations" future
  WHERE future."TenantId"=s.tenant AND future."CustomerId"=c."Id" AND future."StartedAt">=s.boundary)
), operator_log AS MATERIALIZED (
 SELECT a.* FROM public."AbpAuditLogs" a, settings s WHERE a."TenantId"=s.tenant
 AND a."ServiceName"='AQGreen.CleanStart' AND a."MethodName"='ArchiveDisposableTests'
), reset_log AS MATERIALIZED (
 SELECT * FROM operator_log WHERE "Parameters"::jsonb->>'resetId'=current_setting('aqua.clean_start.reset_id')
), counts AS (
 SELECT
 (SELECT count(*) FROM p) AS population,
 (SELECT count(*) FROM p WHERE "IsDeleted") AS archived,
 (SELECT count(*) FROM placement) AS placements,
 (SELECT count(*) FROM legacy) AS memberships,
 (SELECT count(*) FROM legacy WHERE "Id" NOT IN (SELECT "CustomerId" FROM p)) AS unclassified,
 (SELECT count(*) FROM public."MemberPayments" m, settings s WHERE m."TenantId"=s.tenant AND
   (m."CustomerId" IN (SELECT "CustomerId" FROM p) OR m."Purpose" IN (0,1,3,4,7) AND m."InitiatedAt"<s.boundary))
 + (SELECT count(*) FROM public."YocoWebhookReceipts" y, settings s
    WHERE y."TenantId"=s.tenant AND y."Programme"=0 AND y."ProcessedAt"<s.boundary)
 + (SELECT count(*) FROM public."AQGreenJoiningCheckouts" c, settings s WHERE c."TenantId"=s.tenant AND
    (c."CustomerId" IN (SELECT "CustomerId" FROM p) OR c."CreatedAt"<s.boundary))
 + (SELECT count(*) FROM public."AQGreenMonthlyObligationCheckouts" c, settings s WHERE c."TenantId"=s.tenant AND
    (c."CustomerId" IN (SELECT "CustomerId" FROM p) OR c."CreatedAt"<s.boundary))
 + (SELECT count(*) FROM p WHERE "JoiningPaymentId" IS NOT NULL OR "RegistrationPaymentId" IS NOT NULL OR "ActivationPaymentId" IS NOT NULL) AS payments,
 (SELECT count(*) FROM public."EntryWeeklyCommissions" c, settings s WHERE c."TenantId"=s.tenant AND
   (c."EntryParticipationId" IN (SELECT "Id" FROM p) OR c."CreationTime"<s.boundary) AND NOT c."IsTestMoney") AS real_commissions,
 (SELECT count(*) FROM public."OnyxLoanAgreements" l, settings s WHERE l."TenantId"=s.tenant AND
   (l."EntryParticipationId" IN (SELECT "Id" FROM p) OR l."CreationTime"<s.boundary)) AS loans,
 (SELECT count(*) FROM public."OnyxGraduationDecisions" g, settings s WHERE g."TenantId"=s.tenant AND
   (g."EntryParticipationId" IN (SELECT "Id" FROM p) OR g."CreationTime"<s.boundary)) AS graduations,
 (SELECT count(*) FROM public."EntryMonthlyObligations" o, settings s WHERE o."TenantId"=s.tenant AND
   (o."EntryParticipationId" IN (SELECT "Id" FROM p) OR o."CreationTime"<s.boundary)) AS obligations,
 (SELECT count(*) FROM public."EntryWeeklyCommissions" c, settings s WHERE c."TenantId"=s.tenant AND
   (c."EntryParticipationId" IN (SELECT "Id" FROM p) OR c."CreationTime"<s.boundary) AND c."PayoutStatus" IN (3,4)) AS payouts,
 (SELECT count(*) FROM public."AQGreenNetworkPlacements" n, settings s WHERE n."TenantId"=s.tenant AND
   NOT EXISTS (SELECT 1 FROM public."EntryParticipations" e WHERE e."Id"=n."ParticipantId" AND e."TenantId"=s.tenant))
 + (SELECT count(*) FROM public."AQGreenRecruitmentAttributions" n, settings s WHERE n."TenantId"=s.tenant AND
   NOT EXISTS (SELECT 1 FROM public."EntryParticipations" e WHERE e."Id"=n."ParticipantId" AND e."TenantId"=s.tenant)) AS orphans,
 (SELECT count(*) FROM p WHERE "Status" NOT IN (0,1,2,3,4) OR "Status" IS NULL)
 + (SELECT count(*) FROM p WHERE NOT EXISTS (SELECT 1 FROM public."Customers" c, settings s
      WHERE c."Id"=p."CustomerId" AND c."TenantId"=s.tenant))
 + (SELECT count(*) FROM public."Customers" c LEFT JOIN public."Memberships" m ON m."Id"=c."MembershipId", settings s
    WHERE c."TenantId"=s.tenant AND c."MembershipId" IS NOT NULL AND
      (m."Id" IS NULL OR m."TenantId" IS NOT NULL AND m."TenantId"<>s.tenant))
 + (SELECT count(*) FROM public."EntryWeeklyCommissions" c LEFT JOIN public."EntryCommissionPeriods" r
    ON r."Id"=c."CommissionPeriodId", settings s WHERE c."TenantId"=s.tenant AND
    (c."EntryParticipationId" IN (SELECT "Id" FROM p) OR c."CreationTime"<s.boundary) AND
    (r."Id" IS NULL OR r."TenantId"<>s.tenant OR r."PeriodEnd">=s.boundary)) AS invalid
)
, verdict AS (
 SELECT
 CASE WHEN current_setting('transaction_read_only')='on' AND
   current_setting('transaction_isolation')='repeatable read' THEN 'PASS' ELSE 'UNKNOWN' END AS transaction_status,
 CASE WHEN (SELECT count(*) FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
   WHERE n.nspname='public' AND c.relname=ANY(ARRAY['EntryParticipations','Customers','Memberships',
    'AQGreenNetworkPlacements','AQGreenRecruitmentAttributions','MemberPayments','YocoWebhookReceipts',
    'AQGreenJoiningCheckouts','AQGreenMonthlyObligationCheckouts','EntryWeeklyCommissions','OnyxLoanAgreements',
    'OnyxGraduationDecisions','EntryMonthlyObligations','EntryCommissionPeriods','AbpAuditLogs','__EFMigrationsHistory']))=16
   AND NOT EXISTS(SELECT 1 FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
    WHERE n.nspname='public' AND c.relname=ANY(ARRAY['EntryParticipations','Customers','Memberships',
     'AQGreenNetworkPlacements','AQGreenRecruitmentAttributions','MemberPayments','YocoWebhookReceipts',
     'AQGreenJoiningCheckouts','AQGreenMonthlyObligationCheckouts','EntryWeeklyCommissions','OnyxLoanAgreements',
     'OnyxGraduationDecisions','EntryMonthlyObligations','EntryCommissionPeriods','AbpAuditLogs','__EFMigrationsHistory'])
     AND pg_catalog.row_security_active(c.oid)) THEN 'PASS' ELSE 'UNKNOWN' END AS visibility_status,
 CASE WHEN (SELECT count(*) FROM public."__EFMigrationsHistory")=54 AND
   (SELECT max("MigrationId") FROM public."__EFMigrationsHistory")='20261005194901_AddIsolatedAQGreenPilotEvidence' AND
   (SELECT encode(sha256(convert_to(string_agg("MigrationId",chr(10) ORDER BY "MigrationId"),'UTF8')),'hex')
     FROM public."__EFMigrationsHistory")='64884df1307d040e0ba9cf863e3227a5b507dad364bc78624c58a877b5bf6fa9' AND
   to_regclass('public."AQGreenSalesEvidenceInvalidations"') IS NULL THEN 'PASS' ELSE 'FAIL' END AS schema_status,
 CASE WHEN extract(isodow FROM (SELECT boundary FROM settings) AT TIME ZONE 'Africa/Johannesburg')=5 AND
   ((SELECT boundary FROM settings) AT TIME ZONE 'Africa/Johannesburg')::time=time '00:00:00' AND
   (SELECT boundary FROM settings)>transaction_timestamp() AND
   NOT EXISTS(SELECT 1 FROM public."EntryParticipations",settings s WHERE "TenantId"=s.tenant AND "StartedAt">=s.boundary) AND
   NOT EXISTS(SELECT 1 FROM reset_log) THEN 'PASS' ELSE 'FAIL' END AS scope_status,
 CASE WHEN placements>0 OR unclassified>0 OR payments>0 OR real_commissions>0 OR loans>0 OR
   graduations>0 OR obligations>0 OR payouts>0 OR orphans>0 OR invalid>0 THEN 'FAIL'
   WHEN population IS NULL OR archived IS NULL OR placements IS NULL OR memberships IS NULL OR
   unclassified IS NULL OR payments IS NULL OR real_commissions IS NULL OR loans IS NULL OR
   graduations IS NULL OR obligations IS NULL OR payouts IS NULL OR orphans IS NULL OR invalid IS NULL
   THEN 'UNKNOWN' ELSE 'PASS' END AS exposure_status
 FROM counts
), effective AS (
 SELECT transaction_status,visibility_status,
   CASE WHEN visibility_status='PASS' THEN schema_status ELSE 'UNKNOWN' END AS schema_status,
   CASE WHEN visibility_status='PASS' AND transaction_status='PASS' THEN scope_status ELSE 'UNKNOWN' END AS scope_status,
   CASE WHEN exposure_status='FAIL' THEN 'FAIL'
     WHEN visibility_status='PASS' AND transaction_status='PASS' THEN exposure_status ELSE 'UNKNOWN' END AS exposure_status
 FROM verdict
), screen AS (
 SELECT *,CASE WHEN 'FAIL' IN (schema_status,scope_status,exposure_status) THEN 'FAIL'
   WHEN 'UNKNOWN' IN (transaction_status,visibility_status,schema_status,scope_status,exposure_status) THEN 'UNKNOWN'
   ELSE 'PASS' END AS database_status FROM effective
)
SELECT set_config('aqua.clean_start.preflight_receipt',jsonb_build_object(
 'contract','AQGreenCleanStartScreenV2','databaseScreen',database_status,
 'overall',CASE WHEN database_status='FAIL' THEN 'FAIL' ELSE 'UNKNOWN' END,
 'checks',jsonb_build_object('transaction',transaction_status,'visibility',visibility_status,
   'schema',schema_status,'scope',scope_status,'financialAndStructuralExposure',exposure_status,
   'testDataClassification','UNKNOWN','externalBusinessObligations','UNKNOWN'),
 'sourceFingerprint',CASE WHEN database_status='PASS' THEN encode(sha256(convert_to(jsonb_build_object(
   'participants',(SELECT coalesce(jsonb_agg(to_jsonb(p) ORDER BY "Id"),'[]') FROM p),
   'customers',(SELECT coalesce(jsonb_agg(to_jsonb(c) ORDER BY c."Id"),'[]') FROM public."Customers" c, settings s
     WHERE c."TenantId"=s.tenant AND (c."Id" IN (SELECT "CustomerId" FROM p) OR c."Id" IN (SELECT "Id" FROM legacy))),
   'placements',(SELECT coalesce(jsonb_agg(to_jsonb(placement) ORDER BY "Id"),'[]') FROM placement),
   'memberships',(SELECT coalesce(jsonb_agg(to_jsonb(legacy) ORDER BY "Id"),'[]') FROM legacy)
 )::text,'UTF8')),'hex') ELSE NULL END,
 'databaseFingerprint',encode(sha256(convert_to(current_database()||':'||
   (SELECT oid::text FROM pg_database WHERE datname=current_database()),'UTF8')),'hex'),
 'schemaFingerprint',encode(sha256(convert_to((SELECT string_agg(table_name||':'||column_name||':'||data_type||':'||is_nullable,
   chr(10) ORDER BY table_name,ordinal_position) FROM information_schema.columns WHERE table_schema='public'),'UTF8')),'hex'),
 'snapshotFingerprint',encode(sha256(convert_to(txid_current_snapshot()::text,'UTF8')),'hex'),
 'observedAtUtc',transaction_timestamp()
)::text,true) AS receipt FROM screen;
