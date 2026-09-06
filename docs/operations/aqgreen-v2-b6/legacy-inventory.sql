\set ON_ERROR_STOP on
\if :{?b6_cutoff_utc}
\else
  \echo 'Required: --set=b6_cutoff_utc=YYYY-MM-DDTHH:MM:SSZ'
  \quit 2
\endif

-- AQGreen V2 B6 read-only inventory.
-- Run only against an authorised pseudonymised/data-minimised snapshot or a
-- disposable database. The role should also be CONNECT/SELECT-only. This script
-- emits counts and snapshot-key hashes; it never emits direct identifiers,
-- payment references, free-text reasons, or administrator identities.
BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY;
SET LOCAL statement_timeout = '5min';
SET LOCAL lock_timeout = '5s';

SELECT
    :'b6_cutoff_utc'::timestamptz AS "CutoffUtc",
    txid_current_snapshot()::text AS "DatabaseSnapshot",
    current_setting('transaction_read_only') AS "TransactionReadOnly",
    'AQGreenLegacyInventoryV2'::text AS "InventoryVersion";

-- Stable drift fingerprints. The salt is a schema/version discriminator, not a
-- secret. A future approved manifest must use a cryptographic digest over its
-- canonical bytes; this inventory fingerprint is for read-only drift detection.
SELECT
    COALESCE(md5(string_agg(
        concat_ws('|',
            'AQGreenLegacyInventoryV2',
            p."TenantId"::text,
            p."Id"::text,
            p."LegacyOrderingTieBreakOrdinal"::text,
            p."CustomerId"::text,
            COALESCE(p."RecruiterCustomerId"::text, '-'),
            p."Status"::text,
            p."StartedAt"::text,
            COALESCE(p."ActivatedAt"::text, '-'),
            p."IsDeleted"::text,
            COALESCE(p."DeletionTime"::text, '-')),
        E'\n' ORDER BY p."TenantId", p."Id")), md5('empty')) AS "ParticipationFingerprint",
    COUNT(*) AS "ParticipationRows"
FROM "EntryParticipations" p;

SELECT
    COALESCE(md5(string_agg(
        concat_ws('|',
            'AQGreenLegacyCorrectionInventoryV1',
            p."TenantId"::text,
            c."EntryParticipationId"::text,
            COALESCE(c."PreviousRecruiterCustomerId"::text, '-'),
            COALESCE(c."NewRecruiterCustomerId"::text, '-'),
            c."CorrectedAt"::text),
        E'\n' ORDER BY p."TenantId", c."EntryParticipationId", c."CorrectedAt", c."Id")),
        md5('empty')) AS "CorrectionFingerprint",
    COUNT(*) AS "CorrectionRows"
FROM "EntryRecruiterCorrections" c
JOIN "EntryParticipations" p ON p."Id" = c."EntryParticipationId";

-- Lifecycle inventory. Status values are repository enum values, not inferred
-- business labels: 0 awaiting joining, 1 awaiting activation, 2 active,
-- 3 payment-confirmed awaiting approval, 4 rejected.
SELECT
    p."TenantId",
    p."Status" AS "LifecycleStatus",
    p."IsDeleted",
    COUNT(*) AS "ParticipantCount"
FROM "EntryParticipations" p
GROUP BY p."TenantId", p."Status", p."IsDeleted"
ORDER BY p."TenantId", p."Status", p."IsDeleted";

-- Effective active population and recruiter facts at the supplied cutoff. The
-- derivation mirrors EffectiveProgrammeNetwork: effective recruiter at cutoff;
-- ordering time = max(ActivatedAt, effective correction time or StartedAt).
WITH effective AS (
    SELECT
        p."TenantId",
        p."Id" AS participation_id,
        p."CustomerId" AS customer_id,
        CASE
            WHEN applicable.has_row THEN applicable.new_recruiter_customer_id
            WHEN first_correction.has_row THEN first_correction.previous_recruiter_customer_id
            ELSE p."RecruiterCustomerId"
        END AS effective_recruiter_customer_id,
        GREATEST(
            p."ActivatedAt",
            COALESCE(applicable.corrected_at, p."StartedAt")) AS qualified_under_recruiter_at
    FROM "EntryParticipations" p
    LEFT JOIN LATERAL (
        SELECT
            TRUE AS has_row,
            c."PreviousRecruiterCustomerId" AS previous_recruiter_customer_id
        FROM "EntryRecruiterCorrections" c
        WHERE c."EntryParticipationId" = p."Id"
        ORDER BY c."CorrectedAt", c."Id"
        LIMIT 1
    ) first_correction ON TRUE
    LEFT JOIN LATERAL (
        SELECT
            TRUE AS has_row,
            c."NewRecruiterCustomerId" AS new_recruiter_customer_id,
            c."CorrectedAt" AS corrected_at
        FROM "EntryRecruiterCorrections" c
        WHERE c."EntryParticipationId" = p."Id"
          AND c."CorrectedAt" <= :'b6_cutoff_utc'::timestamptz
        ORDER BY c."CorrectedAt" DESC, c."Id" DESC
        LIMIT 1
    ) applicable ON TRUE
    WHERE p."Status" = 2
      AND NOT p."IsDeleted"
      AND p."ActivatedAt" IS NOT NULL
      AND p."ActivatedAt" <= :'b6_cutoff_utc'::timestamptz
)
SELECT
    e."TenantId",
    COUNT(*) AS "EffectiveParticipants",
    COUNT(*) FILTER (WHERE e.effective_recruiter_customer_id IS NULL) AS "CandidateRoots",
    COUNT(DISTINCT e.effective_recruiter_customer_id)
        FILTER (WHERE e.effective_recruiter_customer_id IS NOT NULL) AS "RecruitersWithChildren"
FROM effective e
GROUP BY e."TenantId"
ORDER BY e."TenantId";

-- Candidate-root network sizes. RootHash is a non-PII technical correlation key.
WITH RECURSIVE effective AS (
    SELECT
        p."TenantId",
        p."Id" AS participation_id,
        p."CustomerId" AS customer_id,
        CASE
            WHEN applicable.has_row THEN applicable.new_recruiter_customer_id
            WHEN first_correction.has_row THEN first_correction.previous_recruiter_customer_id
            ELSE p."RecruiterCustomerId"
        END AS effective_recruiter_customer_id,
        CASE
            WHEN applicable.has_row THEN 'CUTOFF_EFFECTIVE_CORRECTION'
            WHEN first_correction.has_row THEN 'ORIGINAL_STATE_FROM_FIRST_CORRECTION'
            ELSE 'CURRENT_ROW_NO_CORRECTION_HISTORY'
        END AS recruiter_evidence_source,
        EXISTS (
            SELECT 1
            FROM "EntryRecruiterCorrections" history
            WHERE history."EntryParticipationId" = p."Id"
              AND (history."PreviousRecruiterCustomerId" IS NOT NULL
                   OR history."NewRecruiterCustomerId" IS NOT NULL)
        ) AS has_historical_recruiter_evidence
    FROM "EntryParticipations" p
    LEFT JOIN LATERAL (
        SELECT TRUE AS has_row,
               c."PreviousRecruiterCustomerId" AS previous_recruiter_customer_id
        FROM "EntryRecruiterCorrections" c
        WHERE c."EntryParticipationId" = p."Id"
        ORDER BY c."CorrectedAt", c."Id" LIMIT 1
    ) first_correction ON TRUE
    LEFT JOIN LATERAL (
        SELECT TRUE AS has_row,
               c."NewRecruiterCustomerId" AS new_recruiter_customer_id
        FROM "EntryRecruiterCorrections" c
        WHERE c."EntryParticipationId" = p."Id"
          AND c."CorrectedAt" <= :'b6_cutoff_utc'::timestamptz
        ORDER BY c."CorrectedAt" DESC, c."Id" DESC LIMIT 1
    ) applicable ON TRUE
    WHERE p."Status" = 2 AND NOT p."IsDeleted"
      AND p."ActivatedAt" IS NOT NULL
      AND p."ActivatedAt" <= :'b6_cutoff_utc'::timestamptz
), roots AS (
    SELECT * FROM effective WHERE effective_recruiter_customer_id IS NULL
), walk AS (
    SELECT
        r."TenantId", r.participation_id AS root_id,
        r.participation_id, r.customer_id,
        r.recruiter_evidence_source,
        r.has_historical_recruiter_evidence,
        ARRAY[r.participation_id]::uuid[] AS path,
        FALSE AS cycle
    FROM roots r
    UNION ALL
    SELECT
        w."TenantId", w.root_id, child.participation_id, child.customer_id,
        w.recruiter_evidence_source,
        w.has_historical_recruiter_evidence,
        w.path || child.participation_id,
        child.participation_id = ANY(w.path)
    FROM walk w
    JOIN effective child
      ON child."TenantId" = w."TenantId"
     AND child.effective_recruiter_customer_id = w.customer_id
    WHERE NOT w.cycle
), memberships AS (
    SELECT
        "TenantId", participation_id,
        COUNT(DISTINCT root_id) AS reached_from_root_count
    FROM walk
    GROUP BY "TenantId", participation_id
)
SELECT
    w."TenantId",
    md5(concat_ws('|', 'AQGreenLegacyRootV1', w."TenantId"::text, w.root_id::text)) AS "RootHash",
    MAX(w.recruiter_evidence_source) AS "RootEvidenceSource",
    BOOL_OR(w.has_historical_recruiter_evidence) AS "RootHasHistoricalRecruiterEvidence",
    COUNT(DISTINCT w.participation_id) - 1 AS "Descendants",
    COUNT(DISTINCT w.participation_id)
        FILTER (WHERE m.reached_from_root_count > 1) AS "OverlappingParticipants",
    BOOL_OR(w.cycle) AS "CycleObserved"
FROM walk w
JOIN memberships m
  ON m."TenantId" = w."TenantId"
 AND m.participation_id = w.participation_id
GROUP BY w."TenantId", w.root_id
ORDER BY w."TenantId", "RootHash";

-- V1 overflow and equal-time ordering inventory. Equal recruiter-ordering times
-- remain reviewable even though immutable participation identity is available as
-- a total tie-breaker in the proposed V2 comparator.
WITH effective AS (
    SELECT
        p."TenantId", p."Id" AS participation_id,
        p."CustomerId" AS customer_id,
        CASE
            WHEN applicable.has_row THEN applicable.new_recruiter_customer_id
            WHEN first_correction.has_row THEN first_correction.previous_recruiter_customer_id
            ELSE p."RecruiterCustomerId"
        END AS effective_recruiter_customer_id,
        GREATEST(p."ActivatedAt", COALESCE(applicable.corrected_at, p."StartedAt"))
            AS qualified_under_recruiter_at
    FROM "EntryParticipations" p
    LEFT JOIN LATERAL (
        SELECT TRUE AS has_row,
               c."PreviousRecruiterCustomerId" AS previous_recruiter_customer_id
        FROM "EntryRecruiterCorrections" c
        WHERE c."EntryParticipationId" = p."Id"
        ORDER BY c."CorrectedAt", c."Id" LIMIT 1
    ) first_correction ON TRUE
    LEFT JOIN LATERAL (
        SELECT TRUE AS has_row,
               c."NewRecruiterCustomerId" AS new_recruiter_customer_id,
               c."CorrectedAt" AS corrected_at
        FROM "EntryRecruiterCorrections" c
        WHERE c."EntryParticipationId" = p."Id"
          AND c."CorrectedAt" <= :'b6_cutoff_utc'::timestamptz
        ORDER BY c."CorrectedAt" DESC, c."Id" DESC LIMIT 1
    ) applicable ON TRUE
    WHERE p."Status" = 2 AND NOT p."IsDeleted"
      AND p."ActivatedAt" IS NOT NULL
      AND p."ActivatedAt" <= :'b6_cutoff_utc'::timestamptz
), child_groups AS (
    SELECT
        "TenantId", effective_recruiter_customer_id,
        COUNT(*) AS child_count
    FROM effective
    WHERE effective_recruiter_customer_id IS NOT NULL
    GROUP BY "TenantId", effective_recruiter_customer_id
), equal_order AS (
    SELECT
        "TenantId", effective_recruiter_customer_id,
        qualified_under_recruiter_at, COUNT(*) AS equal_count
    FROM effective
    WHERE effective_recruiter_customer_id IS NOT NULL
    GROUP BY "TenantId", effective_recruiter_customer_id, qualified_under_recruiter_at
    HAVING COUNT(*) > 1
)
SELECT
    tenant_id AS "TenantId",
    classification AS "Classification",
    SUM(affected_count) AS "AffectedCount"
FROM (
    SELECT "TenantId" AS tenant_id, 'V1_PARENT_OVER_FIVE_CHILDREN'::text AS classification,
           COUNT(*)::bigint AS affected_count
    FROM child_groups WHERE child_count > 5 GROUP BY "TenantId"
    UNION ALL
    SELECT "TenantId", 'EQUAL_EFFECTIVE_ORDERING_TIME', SUM(equal_count)::bigint
    FROM equal_order GROUP BY "TenantId"
) report
GROUP BY tenant_id, classification
ORDER BY tenant_id, classification;

-- Current V1 structural interpretation at the cutoff. This reports only grouped
-- levels and counts; per-participant comparison belongs in the secure simulation
-- process and must not be published with personal identifiers.
WITH RECURSIVE effective AS (
    SELECT
        p."TenantId", p."Id" AS participation_id,
        p."LegacyOrderingTieBreakOrdinal" AS legacy_ordering_tie_break_ordinal,
        p."CustomerId" AS customer_id,
        CASE
            WHEN applicable.has_row THEN applicable.new_recruiter_customer_id
            WHEN first_correction.has_row THEN first_correction.previous_recruiter_customer_id
            ELSE p."RecruiterCustomerId"
        END AS effective_recruiter_customer_id,
        GREATEST(p."ActivatedAt", COALESCE(applicable.corrected_at, p."StartedAt"))
            AS qualified_under_recruiter_at
    FROM "EntryParticipations" p
    LEFT JOIN LATERAL (
        SELECT TRUE AS has_row,
               c."PreviousRecruiterCustomerId" AS previous_recruiter_customer_id
        FROM "EntryRecruiterCorrections" c
        WHERE c."EntryParticipationId" = p."Id"
        ORDER BY c."CorrectedAt", c."Id" LIMIT 1
    ) first_correction ON TRUE
    LEFT JOIN LATERAL (
        SELECT TRUE AS has_row,
               c."NewRecruiterCustomerId" AS new_recruiter_customer_id,
               c."CorrectedAt" AS corrected_at
        FROM "EntryRecruiterCorrections" c
        WHERE c."EntryParticipationId" = p."Id"
          AND c."CorrectedAt" <= :'b6_cutoff_utc'::timestamptz
        ORDER BY c."CorrectedAt" DESC, c."Id" DESC LIMIT 1
    ) applicable ON TRUE
    WHERE p."Status" = 2 AND NOT p."IsDeleted"
      AND p."ActivatedAt" IS NOT NULL
      AND p."ActivatedAt" <= :'b6_cutoff_utc'::timestamptz
), ranked_edges AS (
    SELECT
        e.*,
        ROW_NUMBER() OVER (
            PARTITION BY e."TenantId", e.effective_recruiter_customer_id
            ORDER BY e.qualified_under_recruiter_at,
                     e.legacy_ordering_tie_break_ordinal) AS child_ordinal
    FROM effective e
    WHERE e.effective_recruiter_customer_id IS NOT NULL
), selected_edges AS (
    SELECT * FROM ranked_edges WHERE child_ordinal <= 5
), walk AS (
    SELECT
        anchor."TenantId", anchor.participation_id AS anchor_id,
        anchor.participation_id, anchor.customer_id, 0 AS relative_depth
    FROM effective anchor
    UNION ALL
    SELECT
        w."TenantId", w.anchor_id,
        child.participation_id, child.customer_id, w.relative_depth + 1
    FROM walk w
    JOIN selected_edges child
      ON child."TenantId" = w."TenantId"
     AND child.effective_recruiter_customer_id = w.customer_id
    WHERE w.relative_depth < 3
), depth_counts AS (
    SELECT
        "TenantId", anchor_id,
        COUNT(*) FILTER (WHERE relative_depth = 1) AS depth_1,
        COUNT(*) FILTER (WHERE relative_depth = 2) AS depth_2,
        COUNT(*) FILTER (WHERE relative_depth = 3) AS depth_3
    FROM walk
    GROUP BY "TenantId", anchor_id
), levels AS (
    SELECT
        "TenantId", anchor_id,
        CASE
            WHEN depth_1 = 5 AND depth_2 = 25 AND depth_3 = 125 THEN 3
            WHEN depth_1 = 5 AND depth_2 = 25 THEN 2
            WHEN depth_1 = 5 THEN 1
            ELSE 0
        END AS v1_level
    FROM depth_counts
)
SELECT
    "TenantId",
    v1_level AS "CurrentV1StructuralLevel",
    COUNT(*) AS "ParticipantCount"
FROM levels
GROUP BY "TenantId", v1_level
ORDER BY "TenantId", v1_level;

-- Recruiter correction history and anomalies. No reasons or actor IDs are emitted.
WITH ordered AS (
    SELECT
        p."TenantId", p."Id" AS participation_id,
        p."StartedAt" AS started_at,
        p."RecruiterCustomerId" AS current_recruiter_customer_id,
        c."Id" AS correction_id,
        c."PreviousRecruiterCustomerId" AS previous_recruiter_customer_id,
        c."NewRecruiterCustomerId" AS new_recruiter_customer_id,
        c."CorrectedAt" AS corrected_at,
        LAG(c."NewRecruiterCustomerId") OVER (
            PARTITION BY c."EntryParticipationId"
            ORDER BY c."CorrectedAt", c."Id") AS prior_new_recruiter,
        ROW_NUMBER() OVER (
            PARTITION BY c."EntryParticipationId"
            ORDER BY c."CorrectedAt", c."Id") AS correction_ordinal,
        COUNT(*) OVER (
            PARTITION BY c."EntryParticipationId", c."CorrectedAt") AS same_time_count,
        LAST_VALUE(c."NewRecruiterCustomerId") OVER (
            PARTITION BY c."EntryParticipationId"
            ORDER BY c."CorrectedAt", c."Id"
            ROWS BETWEEN UNBOUNDED PRECEDING AND UNBOUNDED FOLLOWING) AS final_new_recruiter
    FROM "EntryRecruiterCorrections" c
    JOIN "EntryParticipations" p ON p."Id" = c."EntryParticipationId"
), classified AS (
    SELECT *,
        CASE
            WHEN same_time_count > 1 THEN 'AMBIGUOUS_EQUAL_CORRECTION_TIME'
            WHEN corrected_at < started_at THEN 'CORRECTION_BEFORE_START'
            WHEN previous_recruiter_customer_id IS NOT DISTINCT FROM new_recruiter_customer_id
                THEN 'NO_OP_CORRECTION'
            WHEN correction_ordinal > 1
             AND previous_recruiter_customer_id IS DISTINCT FROM prior_new_recruiter
                THEN 'DISCONTINUOUS_CORRECTION_CHAIN'
            WHEN final_new_recruiter IS DISTINCT FROM current_recruiter_customer_id
                THEN 'CORRECTION_CURRENT_STATE_MISMATCH'
            ELSE 'VALID_CORRECTION_EVIDENCE'
        END AS classification
    FROM ordered
)
SELECT
    "TenantId",
    classification AS "Classification",
    COUNT(*) AS "CorrectionRows",
    COUNT(DISTINCT participation_id) AS "AffectedParticipants",
    string_agg(
        DISTINCT md5(concat_ws('|', 'AQGreenLegacyCorrectionAnomalyV1',
            "TenantId"::text, participation_id::text)),
        ',' ORDER BY md5(concat_ws('|', 'AQGreenLegacyCorrectionAnomalyV1',
            "TenantId"::text, participation_id::text))) AS "AffectedTechnicalHashes"
FROM classified
GROUP BY "TenantId", classification
ORDER BY "TenantId", classification;

-- Candidate graph anomalies at the cutoff.
WITH RECURSIVE effective AS (
    SELECT
        p."TenantId", p."Id" AS participation_id, p."CustomerId" AS customer_id,
        CASE
            WHEN applicable.has_row THEN applicable.new_recruiter_customer_id
            WHEN first_correction.has_row THEN first_correction.previous_recruiter_customer_id
            ELSE p."RecruiterCustomerId"
        END AS effective_recruiter_customer_id,
        p."ActivatedAt"
    FROM "EntryParticipations" p
    LEFT JOIN LATERAL (
        SELECT TRUE AS has_row,
               c."PreviousRecruiterCustomerId" AS previous_recruiter_customer_id
        FROM "EntryRecruiterCorrections" c
        WHERE c."EntryParticipationId" = p."Id"
        ORDER BY c."CorrectedAt", c."Id" LIMIT 1
    ) first_correction ON TRUE
    LEFT JOIN LATERAL (
        SELECT TRUE AS has_row,
               c."NewRecruiterCustomerId" AS new_recruiter_customer_id
        FROM "EntryRecruiterCorrections" c
        WHERE c."EntryParticipationId" = p."Id"
          AND c."CorrectedAt" <= :'b6_cutoff_utc'::timestamptz
        ORDER BY c."CorrectedAt" DESC, c."Id" DESC LIMIT 1
    ) applicable ON TRUE
    WHERE p."Status" = 2 AND NOT p."IsDeleted"
      AND p."ActivatedAt" IS NOT NULL
      AND p."ActivatedAt" <= :'b6_cutoff_utc'::timestamptz
), walk AS (
    SELECT
        e."TenantId", e.participation_id AS origin_id,
        e.participation_id, e.customer_id,
        e.effective_recruiter_customer_id,
        ARRAY[e.participation_id]::uuid[] AS path,
        FALSE AS cycle
    FROM effective e
    UNION ALL
    SELECT
        w."TenantId", w.origin_id,
        sponsor.participation_id, sponsor.customer_id,
        sponsor.effective_recruiter_customer_id,
        w.path || sponsor.participation_id,
        sponsor.participation_id = ANY(w.path)
    FROM walk w
    JOIN effective sponsor
      ON sponsor."TenantId" = w."TenantId"
     AND sponsor.customer_id = w.effective_recruiter_customer_id
    WHERE w.effective_recruiter_customer_id IS NOT NULL
      AND NOT w.cycle
), anomaly AS (
    SELECT e."TenantId", e.participation_id, 'DANGLING_SPONSOR'::text AS classification
    FROM effective e
    WHERE e.effective_recruiter_customer_id IS NOT NULL
      AND NOT EXISTS (
          SELECT 1 FROM effective sponsor
          WHERE sponsor."TenantId" = e."TenantId"
            AND sponsor.customer_id = e.effective_recruiter_customer_id)
    UNION ALL
    SELECT e."TenantId", e.participation_id, 'CROSS_TENANT_SPONSOR'::text
    FROM effective e
    WHERE e.effective_recruiter_customer_id IS NOT NULL
      AND EXISTS (
          SELECT 1 FROM effective sponsor
          WHERE sponsor."TenantId" <> e."TenantId"
            AND sponsor.customer_id = e.effective_recruiter_customer_id)
    UNION ALL
    SELECT DISTINCT w."TenantId", w.origin_id, 'CYCLE'::text
    FROM walk w WHERE w.cycle
    UNION ALL
    SELECT p."TenantId", p."Id", 'ACTIVE_MISSING_ACTIVATION'::text
    FROM "EntryParticipations" p
    WHERE p."Status" = 2 AND p."ActivatedAt" IS NULL AND NOT p."IsDeleted"
    UNION ALL
    SELECT p."TenantId", p."Id", 'SOFT_DELETED_PARTICIPATION'::text
    FROM "EntryParticipations" p WHERE p."IsDeleted"
    UNION ALL
    SELECT p."TenantId", p."Id", 'LEGACY_UNKNOWN_ACQUISITION'::text
    FROM "EntryParticipations" p
    LEFT JOIN "AQGreenRecruitmentAttributions" a
      ON a."TenantId" = p."TenantId" AND a."ParticipantId" = p."Id"
    WHERE p."Status" = 2 AND NOT p."IsDeleted" AND a."Id" IS NULL
), duplicate_customer AS (
    SELECT p."TenantId", MIN(p."Id"::text)::uuid AS participation_id,
           'DUPLICATE_CUSTOMER_PARTICIPATION'::text AS classification
    FROM "EntryParticipations" p
    GROUP BY p."TenantId", p."CustomerId"
    HAVING COUNT(*) > 1
)
SELECT
    report."TenantId",
    report.classification AS "Classification",
    COUNT(*) AS "AffectedCount",
    string_agg(
        md5(concat_ws('|', 'AQGreenLegacyAnomalyV1', report."TenantId"::text,
            report.participation_id::text)),
        ',' ORDER BY report.participation_id) AS "AffectedTechnicalHashes"
FROM (
    SELECT * FROM anomaly
    UNION ALL
    SELECT * FROM duplicate_customer
) report
GROUP BY report."TenantId", report.classification
ORDER BY report."TenantId", report.classification;

-- Historical financial, structural-decision, loan, and graduation inventory.
SELECT
    c."TenantId",
    'AQGREEN_COMMISSION'::text AS "EvidenceKind",
    c."PayoutStatus"::text AS "State",
    COUNT(*) AS "EvidenceCount",
    COUNT(DISTINCT c."EntryParticipationId") AS "AffectedParticipants"
FROM "EntryWeeklyCommissions" c
GROUP BY c."TenantId", c."PayoutStatus"
UNION ALL
SELECT
    c."TenantId", 'AQGREEN_HISTORICAL_STRUCTURAL_LEVEL',
    c."HighestCompletedLevel"::text, COUNT(*), COUNT(DISTINCT c."EntryParticipationId")
FROM "EntryWeeklyCommissions" c
GROUP BY c."TenantId", c."HighestCompletedLevel"
UNION ALL
SELECT
    l."TenantId", 'ONYX_LOAN_AGREEMENT', l."Status"::text,
    COUNT(*), COUNT(DISTINCT l."EntryParticipationId")
FROM "OnyxLoanAgreements" l
GROUP BY l."TenantId", l."Status"
UNION ALL
SELECT
    l."TenantId", 'ONYX_LOAN_OFFER', l."Status"::text,
    COUNT(*), COUNT(DISTINCT l."EntryParticipationId")
FROM "OnyxLoanAgreements" l
WHERE l."Status" = 0
GROUP BY l."TenantId", l."Status"
UNION ALL
SELECT
    g."TenantId", 'AQGREEN_TO_ONYX_GRADUATION', g."StructuralModel"::text,
    COUNT(*), COUNT(DISTINCT g."EntryParticipationId")
FROM "OnyxGraduationDecisions" g
GROUP BY g."TenantId", g."StructuralModel"
UNION ALL
SELECT
    o."TenantId", 'ONYX_GRADUATED_PARTICIPATION', o."Status"::text,
    COUNT(*), COUNT(DISTINCT o."EntryParticipationId")
FROM "OnyxParticipations" o
WHERE o."AdmissionRoute" = 1
GROUP BY o."TenantId", o."Status"
ORDER BY 1, 2, 3;

ROLLBACK;
