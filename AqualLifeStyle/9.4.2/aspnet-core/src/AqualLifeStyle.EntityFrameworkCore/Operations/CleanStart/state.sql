-- One shared PostgreSQL observation for preflight, reset and D10 baseline checks.
-- It returns no identities, references, amounts or source rows. The same result
-- is retained only in a transaction-local setting for the operator transaction.
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

, payment_evidence AS (
 SELECT encode(sha256(convert_to(jsonb_build_object(
  'payments',(SELECT coalesce(jsonb_agg(to_jsonb(m) ORDER BY m."Id"),'[]') FROM public."MemberPayments" m, settings s WHERE m."TenantId"=s.tenant AND
    (m."CustomerId" IN (SELECT "CustomerId" FROM p) OR m."Purpose" IN (0,1,3,4,7) AND m."InitiatedAt"<s.boundary)),
  'receipts',(SELECT coalesce(jsonb_agg(to_jsonb(y) ORDER BY y."Id"),'[]') FROM public."YocoWebhookReceipts" y, settings s WHERE y."TenantId"=s.tenant AND y."Programme"=0 AND y."ProcessedAt"<s.boundary),
  'joining',(SELECT coalesce(jsonb_agg(to_jsonb(c) ORDER BY c."Id"),'[]') FROM public."AQGreenJoiningCheckouts" c, settings s WHERE c."TenantId"=s.tenant AND (c."CustomerId" IN (SELECT "CustomerId" FROM p) OR c."CreatedAt"<s.boundary)),
  'monthly',(SELECT coalesce(jsonb_agg(to_jsonb(c) ORDER BY c."Id"),'[]') FROM public."AQGreenMonthlyObligationCheckouts" c, settings s WHERE c."TenantId"=s.tenant AND (c."CustomerId" IN (SELECT "CustomerId" FROM p) OR c."CreatedAt"<s.boundary)),
  'participationReferences',(SELECT coalesce(jsonb_agg(jsonb_build_object('id',"Id",'joining',"JoiningPaymentId",'registration',"RegistrationPaymentId",'activation',"ActivationPaymentId") ORDER BY "Id"),'[]') FROM p)
 )::text,'UTF8')),'hex') AS historical_digest,
 encode(sha256(convert_to(jsonb_build_object(
  'payments',(SELECT coalesce(jsonb_agg(to_jsonb(t) ORDER BY "Id"),'[]') FROM public."MemberPayments" t, settings s WHERE t."TenantId"=s.tenant),
  'receipts',(SELECT coalesce(jsonb_agg(to_jsonb(t) ORDER BY "Id"),'[]') FROM public."YocoWebhookReceipts" t, settings s WHERE t."TenantId"=s.tenant),
  'joining',(SELECT coalesce(jsonb_agg(to_jsonb(t) ORDER BY "Id"),'[]') FROM public."AQGreenJoiningCheckouts" t, settings s WHERE t."TenantId"=s.tenant),
  'monthlyCheckouts',(SELECT coalesce(jsonb_agg(to_jsonb(t) ORDER BY "Id"),'[]') FROM public."AQGreenMonthlyObligationCheckouts" t, settings s WHERE t."TenantId"=s.tenant),
  'commissions',(SELECT coalesce(jsonb_agg(to_jsonb(t) ORDER BY "Id"),'[]') FROM public."EntryWeeklyCommissions" t, settings s WHERE t."TenantId"=s.tenant),
  'loans',(SELECT coalesce(jsonb_agg(to_jsonb(t) ORDER BY "Id"),'[]') FROM public."OnyxLoanAgreements" t, settings s WHERE t."TenantId"=s.tenant),
  'graduations',(SELECT coalesce(jsonb_agg(to_jsonb(t) ORDER BY "Id"),'[]') FROM public."OnyxGraduationDecisions" t, settings s WHERE t."TenantId"=s.tenant),
  'obligations',(SELECT coalesce(jsonb_agg(to_jsonb(t) ORDER BY "Id"),'[]') FROM public."EntryMonthlyObligations" t, settings s WHERE t."TenantId"=s.tenant)
 )::text,'UTF8')),'hex') AS tenant_financial_digest,
 encode(sha256(convert_to((SELECT coalesce(string_agg(encode(sha256(convert_to(to_jsonb(a)::text,'UTF8')),'hex'),'' ORDER BY a."Id"),'') FROM public."AbpAuditLogs" a, settings s WHERE a."TenantId"=s.tenant),'UTF8')),'hex') AS audit_digest
)
SELECT set_config('aqua.clean_start.observed_state',jsonb_build_object(
 'participationFactsDigest',encode(sha256(convert_to((SELECT coalesce(jsonb_agg(to_jsonb(p)-'IsDeleted'-'DeletionTime' ORDER BY "Id"),'[]') FROM p)::text,'UTF8')),'hex'),
 'populationCount',population,'archivedCount',archived,'placementCount',placements,
 'legacyMembershipCount',memberships,'unclassifiedMembershipCount',unclassified,
 'paymentEvidenceDigest',(SELECT historical_digest FROM payment_evidence),
 'tenantFinancialRecordsDigest',(SELECT tenant_financial_digest FROM payment_evidence),
 'auditHistoryDigest',(SELECT audit_digest FROM payment_evidence),
 'paymentAdmissionMode',(SELECT a."ReturnValue"::jsonb->'receipt'->>'paymentAdmissionMode' FROM reset_log a ORDER BY a."Id" DESC LIMIT 1),
 'ownerTestOnlyAttestationDigest',(SELECT a."ReturnValue"::jsonb->'receipt'->>'ownerTestOnlyAttestationDigest' FROM reset_log a ORDER BY a."Id" DESC LIMIT 1),
 'resetPaymentEvidenceDigest',(SELECT a."ReturnValue"::jsonb->'receipt'->>'paymentEvidenceDigest' FROM reset_log a ORDER BY a."Id" DESC LIMIT 1),
 'resetTenantFinancialRecordsDigest',(SELECT a."ReturnValue"::jsonb->'receipt'->>'tenantFinancialRecordsDigest' FROM reset_log a ORDER BY a."Id" DESC LIMIT 1),
 'resetProtectedPaymentCount',(SELECT a."ReturnValue"::jsonb->'receipt'->'protectedPaymentCount' FROM reset_log a ORDER BY a."Id" DESC LIMIT 1),
 'protectedPaymentCount',payments,'realCommissionCount',real_commissions,'loanCount',loans,
 'graduationCount',graduations,'monthlyObligationCount',obligations,'payoutCount',payouts,
 'orphanTopologyCount',orphans,'invalidParticipationCount',invalid,
 'sourceDigest',encode(sha256(convert_to(jsonb_build_object(
   'participants',(SELECT coalesce(jsonb_agg(to_jsonb(p) ORDER BY "Id"),'[]') FROM p),
   'customers',(SELECT coalesce(jsonb_agg(to_jsonb(c) ORDER BY c."Id"),'[]') FROM public."Customers" c, settings s
     WHERE c."TenantId"=s.tenant AND (c."Id" IN (SELECT "CustomerId" FROM p) OR c."Id" IN (SELECT "Id" FROM legacy))),
   'placements',(SELECT coalesce(jsonb_agg(to_jsonb(placement) ORDER BY "Id"),'[]') FROM placement),
   'memberships',(SELECT coalesce(jsonb_agg(to_jsonb(legacy) ORDER BY "Id"),'[]') FROM legacy)
 )::text,'UTF8')),'hex'),
 'archiveDigest',encode(sha256(convert_to((SELECT coalesce(jsonb_agg(to_jsonb(p) ORDER BY "Id"),'[]') FROM p)::text,'UTF8')),'hex'),
 'resetAtUtc',(SELECT a."ExecutionTime" FROM reset_log a ORDER BY a."Id" DESC LIMIT 1),
 'boundaryUtc',(SELECT a."ReturnValue"::jsonb->'receipt'->>'boundary' FROM reset_log a ORDER BY a."Id" DESC LIMIT 1),
 'boundaryPolicy',(SELECT a."ReturnValue"::jsonb->'receipt'->>'boundaryPolicy' FROM reset_log a ORDER BY a."Id" DESC LIMIT 1),
 'boundaryAuthorizationDigest',(SELECT a."ReturnValue"::jsonb->'receipt'->>'boundaryAuthorizationDigest' FROM reset_log a ORDER BY a."Id" DESC LIMIT 1),
 'resetReceiptDigest',(SELECT CASE WHEN
   a."ReturnValue"::jsonb->'receipt'->>'resetId'=current_setting('aqua.clean_start.reset_id') AND
   (a."ReturnValue"::jsonb->'receipt'->>'resetAt')::timestamptz=a."ExecutionTime" AND
   a."ReturnValue"::jsonb->'receipt'->>'databaseIdentity'=(SELECT encode(sha256(convert_to(system_identifier::text||'|'||current_database()||'|'||
     (SELECT oid::text FROM pg_database WHERE datname=current_database()),'UTF8')),'hex') FROM pg_control_system()) AND
   a."Parameters"::jsonb->>'receiptDigest'=encode(sha256(convert_to((a."ReturnValue"::jsonb->'receipt')::text,'UTF8')),'hex') AND
   a."ReturnValue"::jsonb->'receipt'->>'undoDigest'=encode(sha256(convert_to((a."ReturnValue"::jsonb->'undo')::text,'UTF8')),'hex')
   THEN encode(sha256(convert_to(a."Parameters",'UTF8')),'hex') ELSE NULL END
   FROM reset_log a
   ORDER BY a."Id" DESC LIMIT 1)
)::text,true) AS clean_start_state FROM counts;
