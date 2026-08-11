-- ============================================================================
-- READ-ONLY INVENTORY: AQGreen funeral-cover migration guard (P0001)
-- Reproduces the guard in migration 20260809043240 as a pure SELECT.
-- Read-only. No mutations, no DDL, no locks beyond MVCC snapshot.
--
-- Output: one row per scanned EntryParticipation (JoiningPaymentAmount > 0
-- AND IsDeleted = FALSE), with explicit C01..C15 boolean columns that match
-- the migration predicate semantically, plus classification evidence.
-- ============================================================================

SELECT
    -- identity
    p."Id"                     AS "ParticipationId",
    p."TenantId",
    p."CustomerId",
    p."Status",
    CASE p."Status"
        WHEN 0 THEN 'AwaitingJoiningPayment'
        WHEN 1 THEN 'AwaitingActivationPayment'
        WHEN 2 THEN 'Active'
        WHEN 3 THEN 'PaymentConfirmedAwaitingApproval'
        WHEN 4 THEN 'Rejected'
    END                        AS "StatusLabel",
    p."StartedAt",
    p."ActivatedAt",

    -- scanned predicate inputs
    p."JoiningPaymentAmount",
    p."JoiningInstallmentAmount",
    p."Currency",
    p."TermsVersion",
    p."TermsEffectiveFrom",
    p."JoiningPaymentId",
    p."RegistrationPaymentId",
    p."ActivationPaymentId",
    p."IsDeleted"              AS "ParticipationIsDeleted",

    -- ---- customer ownership evidence (C01) ----
    (SELECT c2."Id" FROM "Customers" c2
      WHERE c2."Id" = p."CustomerId"
        AND c2."TenantId" = p."TenantId"
        AND c2."IsDeleted" = FALSE
      LIMIT 1) IS NOT NULL     AS "HasLiveCustomer",
    (SELECT c3."Id" FROM "Customers" c3
      WHERE c3."Id" = p."CustomerId" AND c3."IsDeleted" = FALSE
      LIMIT 1) IS NOT NULL     AS "CustomerExistsAnyTenant",
    (SELECT c4."TenantId" FROM "Customers" c4
      WHERE c4."Id" = p."CustomerId" AND c4."IsDeleted" = FALSE
      LIMIT 1)                 AS "CustomerActualTenantId",

    -- ---- qualifying payment evidence (guard semantics, incl. live customer join) ----
    (
        SELECT 1 FROM "MemberPayments" payment
        JOIN "Customers" customer
          ON customer."Id" = p."CustomerId"
         AND customer."TenantId" = p."TenantId"
         AND customer."IsDeleted" = FALSE
        WHERE payment."Id" = p."JoiningPaymentId"
          AND payment."TenantId" = p."TenantId"
          AND payment."CustomerId" = p."CustomerId"
          AND payment."Purpose" = 7
          AND payment."Status" = 1
          AND payment."Amount" = 1200.00
          AND payment."Currency" = 'ZAR'
          AND payment."ConfirmedAt" IS NOT NULL
          AND payment."ConfirmedAt" >= p."StartedAt"
          AND payment."IsDeleted" = FALSE
        LIMIT 1
    ) IS NOT NULL              AS "QualifyingSingleExists",

    (
        SELECT 1 FROM "MemberPayments" first_payment
        JOIN "MemberPayments" second_payment
          ON second_payment."Id" = p."ActivationPaymentId"
        JOIN "Customers" customer
          ON customer."Id" = p."CustomerId"
         AND customer."TenantId" = p."TenantId"
         AND customer."IsDeleted" = FALSE
        WHERE first_payment."Id" = p."RegistrationPaymentId"
          AND first_payment."Id" <> second_payment."Id"
          AND first_payment."TenantId" = p."TenantId"
          AND second_payment."TenantId" = p."TenantId"
          AND first_payment."CustomerId" = p."CustomerId"
          AND second_payment."CustomerId" = p."CustomerId"
          AND first_payment."Purpose" = 7
          AND second_payment."Purpose" = 7
          AND first_payment."Status" = 1
          AND second_payment."Status" = 1
          AND first_payment."Amount" = 600.00
          AND second_payment."Amount" = 600.00
          AND first_payment."Currency" = 'ZAR'
          AND second_payment."Currency" = 'ZAR'
          AND first_payment."ConfirmedAt" IS NOT NULL
          AND second_payment."ConfirmedAt" IS NOT NULL
          AND GREATEST(first_payment."ConfirmedAt", second_payment."ConfirmedAt") >= p."StartedAt"
          AND first_payment."IsDeleted" = FALSE
          AND second_payment."IsDeleted" = FALSE
        LIMIT 1
    ) IS NOT NULL              AS "QualifyingPairExists",

    (
        SELECT 1 FROM "MemberPayments" payment
        JOIN "Customers" customer
          ON customer."Id" = p."CustomerId"
         AND customer."TenantId" = p."TenantId"
         AND customer."IsDeleted" = FALSE
        WHERE payment."Id" = p."RegistrationPaymentId"
          AND payment."TenantId" = p."TenantId"
          AND payment."CustomerId" = p."CustomerId"
          AND payment."Purpose" = 7
          AND payment."Status" = 1
          AND payment."Amount" = 600.00
          AND payment."Currency" = 'ZAR'
          AND payment."ConfirmedAt" IS NOT NULL
          AND payment."ConfirmedAt" >= p."StartedAt"
          AND payment."IsDeleted" = FALSE
        LIMIT 1
    ) IS NOT NULL              AS "QualifyingReg600Exists",

    (
        SELECT 1 FROM "MemberPayments" payment
        JOIN "Customers" customer
          ON customer."Id" = p."CustomerId"
         AND customer."TenantId" = p."TenantId"
         AND customer."IsDeleted" = FALSE
        WHERE payment."Id" = p."ActivationPaymentId"
          AND payment."TenantId" = p."TenantId"
          AND payment."CustomerId" = p."CustomerId"
          AND payment."Purpose" = 7
          AND payment."Status" = 1
          AND payment."Amount" = 600.00
          AND payment."Currency" = 'ZAR'
          AND payment."ConfirmedAt" IS NOT NULL
          AND payment."ConfirmedAt" >= p."StartedAt"
          AND payment."IsDeleted" = FALSE
        LIMIT 1
    ) IS NOT NULL              AS "QualifyingAct600Exists",

    -- ---- confirmed qualifying joining payment diagnostics ----
    (
        SELECT COUNT(*) FROM "MemberPayments" payment
        JOIN "Customers" customer
          ON customer."Id" = p."CustomerId"
         AND customer."TenantId" = p."TenantId"
         AND customer."IsDeleted" = FALSE
        WHERE payment."TenantId" = p."TenantId"
          AND payment."CustomerId" = p."CustomerId"
          AND payment."Purpose" = 7
          AND payment."Status" = 1
          AND payment."IsDeleted" = FALSE
          AND payment."ConfirmedAt" IS NOT NULL
          AND payment."ConfirmedAt" >= p."StartedAt"
    )                          AS "ConfirmedQualifyingPaymentCount",
    (
        SELECT COALESCE(SUM(payment."Amount"), 0) FROM "MemberPayments" payment
        JOIN "Customers" customer
          ON customer."Id" = p."CustomerId"
         AND customer."TenantId" = p."TenantId"
         AND customer."IsDeleted" = FALSE
        WHERE payment."TenantId" = p."TenantId"
          AND payment."CustomerId" = p."CustomerId"
          AND payment."Purpose" = 7
          AND payment."Status" = 1
          AND payment."IsDeleted" = FALSE
          AND payment."ConfirmedAt" IS NOT NULL
          AND payment."ConfirmedAt" >= p."StartedAt"
    )                          AS "ConfirmedQualifyingPaymentTotal",
    (
        SELECT MIN(payment."ConfirmedAt") FROM "MemberPayments" payment
        JOIN "Customers" customer
          ON customer."Id" = p."CustomerId"
         AND customer."TenantId" = p."TenantId"
         AND customer."IsDeleted" = FALSE
        WHERE payment."TenantId" = p."TenantId"
          AND payment."CustomerId" = p."CustomerId"
          AND payment."Purpose" = 7
          AND payment."Status" = 1
          AND payment."IsDeleted" = FALSE
          AND payment."ConfirmedAt" IS NOT NULL
          AND payment."ConfirmedAt" >= p."StartedAt"
    )                          AS "EarliestQualifyingConfirmedAt",
    (
        SELECT MAX(payment."ConfirmedAt") FROM "MemberPayments" payment
        JOIN "Customers" customer
          ON customer."Id" = p."CustomerId"
         AND customer."TenantId" = p."TenantId"
         AND customer."IsDeleted" = FALSE
        WHERE payment."TenantId" = p."TenantId"
          AND payment."CustomerId" = p."CustomerId"
          AND payment."Purpose" = 7
          AND payment."Status" = 1
          AND payment."IsDeleted" = FALSE
          AND payment."ConfirmedAt" IS NOT NULL
          AND payment."ConfirmedAt" >= p."StartedAt"
    )                          AS "LatestQualifyingConfirmedAt",

    -- ============ C01..C15 ============
    (NOT EXISTS (
        SELECT 1 FROM "Customers" customer
        WHERE customer."Id" = p."CustomerId"
          AND customer."TenantId" = p."TenantId"
          AND customer."IsDeleted" = FALSE
    ))                         AS "C01_NoLiveCustomer",

    (p."JoiningPaymentAmount" <> 1200.00)              AS "C02_AmountNot1200",
    (p."Currency" <> 'ZAR')                            AS "C03_CurrencyNotZAR",
    (p."TermsEffectiveFrom" < TIMESTAMPTZ '2026-07-26 00:00:00+00') AS "C04_TermsBefore20260726",

    (p."TermsVersion" NOT IN (
        '2026-07-single-1200',
        '2026-08-single-1200',
        '2026-08-flexible-1200'
    ))                         AS "C05_UnknownTermsVersion",

    (p."TermsVersion" IN ('2026-07-single-1200', '2026-08-single-1200')
     AND p."JoiningInstallmentAmount" <> 0.00)         AS "C06_SingleTermsWithInstalment",

    (p."TermsVersion" = '2026-08-flexible-1200'
     AND p."JoiningInstallmentAmount" <> 600.00)       AS "C07_FlexibleTermsWrongInstalment",

    (p."StartedAt" < p."TermsEffectiveFrom"
     AND NOT EXISTS (
         SELECT 1
         FROM "AQGreenMigrationBackup" legacy_backup
         WHERE legacy_backup."ParticipationId" = p."Id"
           AND legacy_backup."OldTermsEffectiveFrom" IS NOT NULL
           AND p."StartedAt" >= legacy_backup."OldTermsEffectiveFrom"
     ))                                         AS "C08_StartedBeforeTermsEffective",

    (p."JoiningPaymentId" IS NOT NULL
     AND (p."RegistrationPaymentId" IS NOT NULL
          OR p."ActivationPaymentId" IS NOT NULL))     AS "C09_MixedPaymentRefs",

    (p."RegistrationPaymentId" IS NOT NULL
     AND p."ActivationPaymentId" = p."RegistrationPaymentId") AS "C10_DuplicateRegActRef",

    (p."Status" IN (2, 3, 4)
     AND NOT (
         EXISTS (
             SELECT 1 FROM "MemberPayments" payment
             JOIN "Customers" customer
               ON customer."Id" = p."CustomerId"
              AND customer."TenantId" = p."TenantId"
              AND customer."IsDeleted" = FALSE
             WHERE payment."Id" = p."JoiningPaymentId"
               AND payment."TenantId" = p."TenantId"
               AND payment."CustomerId" = p."CustomerId"
               AND payment."Purpose" = 7
               AND payment."Status" = 1
               AND payment."Amount" = 1200.00
               AND payment."Currency" = 'ZAR'
               AND payment."ConfirmedAt" IS NOT NULL
               AND payment."ConfirmedAt" >= p."StartedAt"
               AND payment."IsDeleted" = FALSE
         )
         OR EXISTS (
             SELECT 1 FROM "MemberPayments" first_payment
             JOIN "MemberPayments" second_payment
               ON second_payment."Id" = p."ActivationPaymentId"
             JOIN "Customers" customer
               ON customer."Id" = p."CustomerId"
              AND customer."TenantId" = p."TenantId"
              AND customer."IsDeleted" = FALSE
             WHERE first_payment."Id" = p."RegistrationPaymentId"
               AND first_payment."Id" <> second_payment."Id"
               AND first_payment."TenantId" = p."TenantId"
               AND second_payment."TenantId" = p."TenantId"
               AND first_payment."CustomerId" = p."CustomerId"
               AND second_payment."CustomerId" = p."CustomerId"
               AND first_payment."Purpose" = 7
               AND second_payment."Purpose" = 7
               AND first_payment."Status" = 1
               AND second_payment."Status" = 1
               AND first_payment."Amount" = 600.00
               AND second_payment."Amount" = 600.00
               AND first_payment."Currency" = 'ZAR'
               AND second_payment."Currency" = 'ZAR'
               AND first_payment."ConfirmedAt" IS NOT NULL
               AND second_payment."ConfirmedAt" IS NOT NULL
               AND GREATEST(first_payment."ConfirmedAt", second_payment."ConfirmedAt") >= p."StartedAt"
               AND first_payment."IsDeleted" = FALSE
               AND second_payment."IsDeleted" = FALSE
         )
     ))                        AS "C11_NoQualifyingPaymentForActive",

    (EXISTS (
        SELECT 1 FROM "MemberPayments" payment
        WHERE payment."Id" = p."JoiningPaymentId"
          AND payment."Status" = 1
     )
     AND NOT EXISTS (
        SELECT 1 FROM "MemberPayments" payment
        JOIN "Customers" customer
          ON customer."Id" = p."CustomerId"
         AND customer."TenantId" = p."TenantId"
         AND customer."IsDeleted" = FALSE
        WHERE payment."Id" = p."JoiningPaymentId"
          AND payment."TenantId" = p."TenantId"
          AND payment."CustomerId" = p."CustomerId"
          AND payment."Purpose" = 7
          AND payment."Status" = 1
          AND payment."Amount" = 1200.00
          AND payment."Currency" = 'ZAR'
          AND payment."ConfirmedAt" IS NOT NULL
          AND payment."ConfirmedAt" >= p."StartedAt"
          AND payment."IsDeleted" = FALSE
     ))                        AS "C12_JoiningPaymentDoesNotQualify",

    (EXISTS (
        SELECT 1 FROM "MemberPayments" payment
        WHERE payment."Id" = p."RegistrationPaymentId"
          AND payment."Status" = 1
     )
     AND NOT EXISTS (
        SELECT 1 FROM "MemberPayments" payment
        JOIN "Customers" customer
          ON customer."Id" = p."CustomerId"
         AND customer."TenantId" = p."TenantId"
         AND customer."IsDeleted" = FALSE
        WHERE payment."Id" = p."RegistrationPaymentId"
          AND payment."TenantId" = p."TenantId"
          AND payment."CustomerId" = p."CustomerId"
          AND payment."Purpose" = 7
          AND payment."Status" = 1
          AND payment."Amount" = 600.00
          AND payment."Currency" = 'ZAR'
          AND payment."ConfirmedAt" IS NOT NULL
          AND payment."ConfirmedAt" >= p."StartedAt"
          AND payment."IsDeleted" = FALSE
     ))                        AS "C13_RegPaymentDoesNotQualify",

    (EXISTS (
        SELECT 1 FROM "MemberPayments" payment
        WHERE payment."Id" = p."ActivationPaymentId"
          AND payment."Status" = 1
     )
     AND NOT EXISTS (
        SELECT 1 FROM "MemberPayments" payment
        JOIN "Customers" customer
          ON customer."Id" = p."CustomerId"
         AND customer."TenantId" = p."TenantId"
         AND customer."IsDeleted" = FALSE
        WHERE payment."Id" = p."ActivationPaymentId"
          AND payment."TenantId" = p."TenantId"
          AND payment."CustomerId" = p."CustomerId"
          AND payment."Purpose" = 7
          AND payment."Status" = 1
          AND payment."Amount" = 600.00
          AND payment."Currency" = 'ZAR'
          AND payment."ConfirmedAt" IS NOT NULL
          AND payment."ConfirmedAt" >= p."StartedAt"
          AND payment."IsDeleted" = FALSE
     ))                        AS "C14_ActPaymentDoesNotQualify",

    (EXISTS (
        SELECT 1 FROM "MemberPayments" first_payment
        JOIN "MemberPayments" second_payment
          ON second_payment."Id" = p."ActivationPaymentId"
        WHERE first_payment."Id" = p."RegistrationPaymentId"
          AND first_payment."Status" = 1
          AND second_payment."Status" = 1
     )
     AND NOT EXISTS (
        SELECT 1 FROM "MemberPayments" first_payment
        JOIN "MemberPayments" second_payment
          ON second_payment."Id" = p."ActivationPaymentId"
        JOIN "Customers" customer
          ON customer."Id" = p."CustomerId"
         AND customer."TenantId" = p."TenantId"
         AND customer."IsDeleted" = FALSE
        WHERE first_payment."Id" = p."RegistrationPaymentId"
          AND first_payment."Id" <> second_payment."Id"
          AND first_payment."TenantId" = p."TenantId"
          AND second_payment."TenantId" = p."TenantId"
          AND first_payment."CustomerId" = p."CustomerId"
          AND second_payment."CustomerId" = p."CustomerId"
          AND first_payment."Purpose" = 7
          AND second_payment."Purpose" = 7
          AND first_payment."Status" = 1
          AND second_payment."Status" = 1
          AND first_payment."Amount" = 600.00
          AND second_payment."Amount" = 600.00
          AND first_payment."Currency" = 'ZAR'
          AND second_payment."Currency" = 'ZAR'
          AND first_payment."ConfirmedAt" IS NOT NULL
          AND second_payment."ConfirmedAt" IS NOT NULL
          AND GREATEST(first_payment."ConfirmedAt", second_payment."ConfirmedAt") >= p."StartedAt"
          AND first_payment."IsDeleted" = FALSE
          AND second_payment."IsDeleted" = FALSE
     ))                        AS "C15_PairDoesNotQualify",

    -- ---- readable trigger summary ----
    ARRAY(
        SELECT code FROM (VALUES
            ('C01', (NOT EXISTS (SELECT 1 FROM "Customers" c WHERE c."Id" = p."CustomerId" AND c."TenantId" = p."TenantId" AND c."IsDeleted" = FALSE))),
            ('C02', (p."JoiningPaymentAmount" <> 1200.00)),
            ('C03', (p."Currency" <> 'ZAR')),
            ('C04', (p."TermsEffectiveFrom" < TIMESTAMPTZ '2026-07-26 00:00:00+00')),
            ('C05', (p."TermsVersion" NOT IN ('2026-07-single-1200','2026-08-single-1200','2026-08-flexible-1200'))),
            ('C06', (p."TermsVersion" IN ('2026-07-single-1200','2026-08-single-1200') AND p."JoiningInstallmentAmount" <> 0.00)),
            ('C07', (p."TermsVersion" = '2026-08-flexible-1200' AND p."JoiningInstallmentAmount" <> 600.00)),
            ('C08', (p."StartedAt" < p."TermsEffectiveFrom"
                     AND NOT EXISTS (
                         SELECT 1
                         FROM "AQGreenMigrationBackup" legacy_backup
                         WHERE legacy_backup."ParticipationId" = p."Id"
                           AND legacy_backup."OldTermsEffectiveFrom" IS NOT NULL
                           AND p."StartedAt" >= legacy_backup."OldTermsEffectiveFrom"
                     ))),
            ('C09', (p."JoiningPaymentId" IS NOT NULL AND (p."RegistrationPaymentId" IS NOT NULL OR p."ActivationPaymentId" IS NOT NULL))),
            ('C10', (p."RegistrationPaymentId" IS NOT NULL AND p."ActivationPaymentId" = p."RegistrationPaymentId")),
            ('C11', (p."Status" IN (2,3,4) AND NOT (
                (SELECT 1 FROM "MemberPayments" payment JOIN "Customers" customer ON customer."Id" = p."CustomerId" AND customer."TenantId" = p."TenantId" AND customer."IsDeleted" = FALSE WHERE payment."Id" = p."JoiningPaymentId" AND payment."TenantId" = p."TenantId" AND payment."CustomerId" = p."CustomerId" AND payment."Purpose" = 7 AND payment."Status" = 1 AND payment."Amount" = 1200.00 AND payment."Currency" = 'ZAR' AND payment."ConfirmedAt" IS NOT NULL AND payment."ConfirmedAt" >= p."StartedAt" AND payment."IsDeleted" = FALSE LIMIT 1) IS NOT NULL
                OR (SELECT 1 FROM "MemberPayments" f JOIN "MemberPayments" s ON s."Id" = p."ActivationPaymentId" JOIN "Customers" customer ON customer."Id" = p."CustomerId" AND customer."TenantId" = p."TenantId" AND customer."IsDeleted" = FALSE WHERE f."Id" = p."RegistrationPaymentId" AND f."Id" <> s."Id" AND f."TenantId" = p."TenantId" AND s."TenantId" = p."TenantId" AND f."CustomerId" = p."CustomerId" AND s."CustomerId" = p."CustomerId" AND f."Purpose" = 7 AND s."Purpose" = 7 AND f."Status" = 1 AND s."Status" = 1 AND f."Amount" = 600.00 AND s."Amount" = 600.00 AND f."Currency" = 'ZAR' AND s."Currency" = 'ZAR' AND f."ConfirmedAt" IS NOT NULL AND s."ConfirmedAt" IS NOT NULL AND GREATEST(f."ConfirmedAt", s."ConfirmedAt") >= p."StartedAt" AND f."IsDeleted" = FALSE AND s."IsDeleted" = FALSE LIMIT 1) IS NOT NULL
            ))),
            ('C12', (EXISTS (SELECT 1 FROM "MemberPayments" payment WHERE payment."Id" = p."JoiningPaymentId" AND payment."Status" = 1) AND NOT EXISTS (SELECT 1 FROM "MemberPayments" payment JOIN "Customers" customer ON customer."Id" = p."CustomerId" AND customer."TenantId" = p."TenantId" AND customer."IsDeleted" = FALSE WHERE payment."Id" = p."JoiningPaymentId" AND payment."TenantId" = p."TenantId" AND payment."CustomerId" = p."CustomerId" AND payment."Purpose" = 7 AND payment."Status" = 1 AND payment."Amount" = 1200.00 AND payment."Currency" = 'ZAR' AND payment."ConfirmedAt" IS NOT NULL AND payment."ConfirmedAt" >= p."StartedAt" AND payment."IsDeleted" = FALSE))),
            ('C13', (EXISTS (SELECT 1 FROM "MemberPayments" payment WHERE payment."Id" = p."RegistrationPaymentId" AND payment."Status" = 1) AND NOT EXISTS (SELECT 1 FROM "MemberPayments" payment JOIN "Customers" customer ON customer."Id" = p."CustomerId" AND customer."TenantId" = p."TenantId" AND customer."IsDeleted" = FALSE WHERE payment."Id" = p."RegistrationPaymentId" AND payment."TenantId" = p."TenantId" AND payment."CustomerId" = p."CustomerId" AND payment."Purpose" = 7 AND payment."Status" = 1 AND payment."Amount" = 600.00 AND payment."Currency" = 'ZAR' AND payment."ConfirmedAt" IS NOT NULL AND payment."ConfirmedAt" >= p."StartedAt" AND payment."IsDeleted" = FALSE))),
            ('C14', (EXISTS (SELECT 1 FROM "MemberPayments" payment WHERE payment."Id" = p."ActivationPaymentId" AND payment."Status" = 1) AND NOT EXISTS (SELECT 1 FROM "MemberPayments" payment JOIN "Customers" customer ON customer."Id" = p."CustomerId" AND customer."TenantId" = p."TenantId" AND customer."IsDeleted" = FALSE WHERE payment."Id" = p."ActivationPaymentId" AND payment."TenantId" = p."TenantId" AND payment."CustomerId" = p."CustomerId" AND payment."Purpose" = 7 AND payment."Status" = 1 AND payment."Amount" = 600.00 AND payment."Currency" = 'ZAR' AND payment."ConfirmedAt" IS NOT NULL AND payment."ConfirmedAt" >= p."StartedAt" AND payment."IsDeleted" = FALSE))),
            ('C15', (EXISTS (SELECT 1 FROM "MemberPayments" f JOIN "MemberPayments" s ON s."Id" = p."ActivationPaymentId" WHERE f."Id" = p."RegistrationPaymentId" AND f."Status" = 1 AND s."Status" = 1) AND NOT EXISTS (SELECT 1 FROM "MemberPayments" f JOIN "MemberPayments" s ON s."Id" = p."ActivationPaymentId" JOIN "Customers" customer ON customer."Id" = p."CustomerId" AND customer."TenantId" = p."TenantId" AND customer."IsDeleted" = FALSE WHERE f."Id" = p."RegistrationPaymentId" AND f."Id" <> s."Id" AND f."TenantId" = p."TenantId" AND s."TenantId" = p."TenantId" AND f."CustomerId" = p."CustomerId" AND s."CustomerId" = p."CustomerId" AND f."Purpose" = 7 AND s."Purpose" = 7 AND f."Status" = 1 AND s."Status" = 1 AND f."Amount" = 600.00 AND s."Amount" = 600.00 AND f."Currency" = 'ZAR' AND s."Currency" = 'ZAR' AND f."ConfirmedAt" IS NOT NULL AND s."ConfirmedAt" IS NOT NULL AND GREATEST(f."ConfirmedAt", s."ConfirmedAt") >= p."StartedAt" AND f."IsDeleted" = FALSE AND s."IsDeleted" = FALSE)))
        ) AS t(code, flag)
        WHERE flag
    )                          AS "TriggerConditions",

    -- ---- linked payment facts (evidence, ownership included, no PII) ----
    (
        SELECT COALESCE(jsonb_agg(jsonb_build_object(
            'LinkedAs',
            CASE mp."Id"
                WHEN p."JoiningPaymentId" THEN 'Joining'
                WHEN p."RegistrationPaymentId" THEN 'Registration'
                ELSE 'Activation'
            END,
            'PaymentId', mp."Id",
            'Purpose', mp."Purpose",
            'Amount', mp."Amount",
            'Currency', mp."Currency",
            'Status', mp."Status",
            'ConfirmedAt', mp."ConfirmedAt",
            'TenantId', mp."TenantId",
            'CustomerId', mp."CustomerId",
            'IsDeleted', mp."IsDeleted"
        ) ORDER BY mp."ConfirmedAt"), '[]'::jsonb)
        FROM "MemberPayments" mp
        WHERE mp."Id" IN (p."JoiningPaymentId", p."RegistrationPaymentId", p."ActivationPaymentId")
    )                          AS "LinkedPaymentFacts",

    (
        SELECT COALESCE(jsonb_agg(jsonb_build_object(
            'PaymentId', mp."Id",
            'Purpose', mp."Purpose",
            'Amount', mp."Amount",
            'Currency', mp."Currency",
            'Status', mp."Status",
            'ConfirmedAt', mp."ConfirmedAt",
            'IsDeleted', mp."IsDeleted"
        ) ORDER BY mp."ConfirmedAt"), '[]'::jsonb)
        FROM "MemberPayments" mp
        WHERE mp."TenantId" = p."TenantId"
          AND mp."CustomerId" = p."CustomerId"
          AND mp."Purpose" = 7
          AND mp."Status" = 1
          AND mp."IsDeleted" = FALSE
    )                          AS "CustomerConfirmedJoiningPayments",

    -- ---- approval/decision evidence ----
    (
        SELECT jsonb_build_object(
            'DecisionCount', COUNT(*),
            'ApprovedCount', COUNT(*) FILTER (WHERE d."Approved"),
            'RejectedCount', COUNT(*) FILTER (WHERE NOT d."Approved"),
            'LatestDecidedAt', MAX(d."DecidedAt")
        )
        FROM "EntryParticipationApprovalDecisions" d
        WHERE d."EntryParticipationId" = p."Id"
    )                          AS "ApprovalDecisionEvidence",

    -- ---- legacy boundary evidence (persisted by 20260726162000) ----
    b."OldTermsVersion"        AS "LegacyOldTermsVersion",
    b."OldTermsEffectiveFrom"  AS "LegacyOldTermsEffectiveFrom",
    (b."ParticipationId" IS NOT NULL) AS "WasMigratedFromLegacy"

FROM "EntryParticipations" p
LEFT JOIN "AQGreenMigrationBackup" b
    ON b."ParticipationId" = p."Id"
WHERE p."JoiningPaymentAmount" > 0.00
  AND p."IsDeleted" = FALSE
ORDER BY p."TenantId", p."Status", p."StartedAt";
