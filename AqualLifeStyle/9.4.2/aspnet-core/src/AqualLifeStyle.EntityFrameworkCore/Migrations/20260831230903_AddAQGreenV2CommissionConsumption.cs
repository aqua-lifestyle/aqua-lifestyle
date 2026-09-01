using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    /// <inheritdoc />
    public partial class AddAQGreenV2CommissionConsumption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CommissionDecisionRulesVersion",
                table: "EntryWeeklyCommissions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StructuralModel",
                table: "EntryWeeklyCommissions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_EntryWeeklyCommissions_TenantId_Id",
                table: "EntryWeeklyCommissions",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateTable(
                name: "AQGreenV2WeeklyCommissionEvidence",
                columns: table => new
                {
                    EntryWeeklyCommissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    EntryParticipationId = table.Column<Guid>(type: "uuid", nullable: false),
                    WeeklySalesEligibilityDecisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlacementTreeScopeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Cutoff = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PlacementRulesVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StructuralQualificationRulesVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SalesEligibilityRulesVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CommissionDecisionRulesVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EvidenceSchemaVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    QualifiedStructuralLevel = table.Column<int>(type: "integer", nullable: false),
                    CommissionedLevel = table.Column<int>(type: "integer", nullable: false),
                    QualifyingDepth1Count = table.Column<int>(type: "integer", nullable: false),
                    QualifyingDepth2Count = table.Column<int>(type: "integer", nullable: false),
                    QualifyingDepth3Count = table.Column<int>(type: "integer", nullable: false),
                    SalesApplicability = table.Column<int>(type: "integer", nullable: false),
                    SalesReviewStatus = table.Column<int>(type: "integer", nullable: true),
                    SalesThresholdResult = table.Column<int>(type: "integer", nullable: true),
                    SalesReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SalesReviewedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    EvidenceNodeCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AQGreenV2WeeklyCommissionEvidence", x => x.EntryWeeklyCommissionId);
                    table.UniqueConstraint("AK_AQGreenV2WeeklyCommissionEvidence_TenantId_EntryWeeklyCommi~", x => new { x.TenantId, x.EntryWeeklyCommissionId });
                    table.CheckConstraint("CK_AQGreenV2CommissionEvidence_CommissionGate", "(\"SalesApplicability\" = 1 AND \"QualifiedStructuralLevel\" = 0 AND \"CommissionedLevel\" = 0) OR (\"SalesApplicability\" = 2 AND \"SalesReviewStatus\" = 2 AND \"SalesThresholdResult\" IS NOT NULL AND \"SalesThresholdResult\" = 1 AND \"CommissionedLevel\" = \"QualifiedStructuralLevel\") OR ((\"SalesApplicability\" = 2 AND \"SalesReviewStatus\" = 2 AND \"SalesThresholdResult\" IS NOT NULL AND \"SalesThresholdResult\" = 2) OR (\"SalesApplicability\" = 2 AND \"SalesReviewStatus\" = 3)) AND \"CommissionedLevel\" = 0");
                    table.CheckConstraint("CK_AQGreenV2CommissionEvidence_Counts", "\"QualifyingDepth1Count\" BETWEEN 0 AND 5 AND \"QualifyingDepth2Count\" BETWEEN 0 AND 25 AND \"QualifyingDepth3Count\" BETWEEN 0 AND 125 AND \"EvidenceNodeCount\" BETWEEN 1 AND 156");
                    table.CheckConstraint("CK_AQGreenV2CommissionEvidence_Level_Range", "\"QualifiedStructuralLevel\" IN (0, 1, 2, 3) AND \"CommissionedLevel\" IN (0, 1, 2, 3) AND (\"CommissionedLevel\" = 0 OR \"CommissionedLevel\" = \"QualifiedStructuralLevel\")");
                    table.CheckConstraint("CK_AQGreenV2CommissionEvidence_Reviewer_Positive", "\"SalesApplicability\" = 1 OR \"SalesReviewedByUserId\" > 0");
                    table.CheckConstraint("CK_AQGreenV2CommissionEvidence_SalesApplicability", "\"SalesApplicability\" IN (1, 2)");
                    table.CheckConstraint("CK_AQGreenV2CommissionEvidence_SalesShape", "(\"SalesApplicability\" = 1 AND \"WeeklySalesEligibilityDecisionId\" IS NULL AND \"SalesReviewStatus\" IS NULL AND \"SalesThresholdResult\" IS NULL AND \"SalesReviewedAt\" IS NULL AND \"SalesReviewedByUserId\" IS NULL) OR (\"SalesApplicability\" = 2 AND \"WeeklySalesEligibilityDecisionId\" IS NOT NULL AND \"SalesReviewStatus\" = 2 AND \"SalesThresholdResult\" IS NOT NULL AND \"SalesThresholdResult\" IN (1, 2)) OR (\"SalesApplicability\" = 2 AND \"SalesReviewStatus\" = 3 AND \"WeeklySalesEligibilityDecisionId\" IS NOT NULL AND \"SalesThresholdResult\" IS NULL)");
                    table.CheckConstraint("CK_AQGreenV2CommissionEvidence_TenantId_Positive", "\"TenantId\" > 0");
                    table.CheckConstraint("CK_AQGreenV2CommissionEvidence_Versions_NotBlank", "length(trim(\"PlacementRulesVersion\")) > 0 AND length(trim(\"StructuralQualificationRulesVersion\")) > 0 AND length(trim(\"CommissionDecisionRulesVersion\")) > 0 AND length(trim(\"EvidenceSchemaVersion\")) > 0 AND (\"SalesApplicability\" = 1 OR length(trim(\"SalesEligibilityRulesVersion\")) > 0)");
                    table.CheckConstraint("CK_AQGreenV2CommissionEvidence_Versions_Supported", "\"PlacementRulesVersion\" = 'AQGreenPlacementV2' AND \"StructuralQualificationRulesVersion\" = 'AQGreenStructuralQualificationV1' AND \"CommissionDecisionRulesVersion\" = 'AQGreenWeeklyCommissionDecisionV1' AND \"EvidenceSchemaVersion\" = 'AQGreenV2WeeklyCommissionEvidenceV1' AND ((\"SalesApplicability\" = 1 AND \"SalesEligibilityRulesVersion\" IS NULL) OR (\"SalesApplicability\" = 2 AND \"SalesEligibilityRulesVersion\" = 'AQGreenWeeklySalesEligibilityV1'))");
                    table.ForeignKey(
                        name: "FK_AQGreenV2WeeklyCommissionEvidence_AQGreenWeeklySalesEligibi~",
                        columns: x => new { x.TenantId, x.WeeklySalesEligibilityDecisionId },
                        principalTable: "AQGreenWeeklySalesEligibilityDecisions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AQGreenV2WeeklyCommissionEvidence_EntryParticipations_Tenan~",
                        columns: x => new { x.TenantId, x.EntryParticipationId },
                        principalTable: "EntryParticipations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AQGreenV2WeeklyCommissionEvidence_EntryWeeklyCommissions_Te~",
                        columns: x => new { x.TenantId, x.EntryWeeklyCommissionId },
                        principalTable: "EntryWeeklyCommissions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AQGreenV2WeeklyCommissionEvidenceNodes",
                columns: table => new
                {
                    EvidenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CanonicalOrdinal = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    SourcePlacementId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipationStatusObserved = table.Column<int>(type: "integer", nullable: false),
                    ParticipationActivatedAtObserved = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ParticipationIsDeletedObserved = table.Column<bool>(type: "boolean", nullable: false),
                    CustomerIdObserved = table.Column<int>(type: "integer", nullable: false),
                    CustomerTenantMatchedObserved = table.Column<bool>(type: "boolean", nullable: false),
                    CustomerIsActiveObserved = table.Column<bool>(type: "boolean", nullable: false),
                    CustomerIsDeletedObserved = table.Column<bool>(type: "boolean", nullable: false),
                    UserIdObserved = table.Column<long>(type: "bigint", nullable: false),
                    UserTenantMatchedObserved = table.Column<bool>(type: "boolean", nullable: false),
                    UserIsActiveObserved = table.Column<bool>(type: "boolean", nullable: false),
                    UserIsDeletedObserved = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AQGreenV2WeeklyCommissionEvidenceNodes", x => new { x.EvidenceId, x.CanonicalOrdinal });
                    table.CheckConstraint("CK_AQGreenV2CommissionEvidenceNodes_Identity", "\"CustomerIdObserved\" > 0 AND \"UserIdObserved\" > 0");
                    table.CheckConstraint("CK_AQGreenV2CommissionEvidenceNodes_Ordinal", "\"CanonicalOrdinal\" BETWEEN 0 AND 155");
                    table.CheckConstraint("CK_AQGreenV2CommissionEvidenceNodes_Status", "\"ParticipationStatusObserved\" IN (0, 1, 2, 3, 4)");
                    table.CheckConstraint("CK_AQGreenV2CommissionEvidenceNodes_TenantId_Positive", "\"TenantId\" > 0");
                    table.ForeignKey(
                        name: "FK_AQGreenV2WeeklyCommissionEvidenceNodes_AQGreenNetworkPlacem~",
                        columns: x => new { x.TenantId, x.SourcePlacementId },
                        principalTable: "AQGreenNetworkPlacements",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AQGreenV2WeeklyCommissionEvidenceNodes_AQGreenV2WeeklyCommi~",
                        columns: x => new { x.TenantId, x.EvidenceId },
                        principalTable: "AQGreenV2WeeklyCommissionEvidence",
                        principalColumns: new[] { "TenantId", "EntryWeeklyCommissionId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_EntryWeeklyCommissions_DecisionVersion_Shape",
                table: "EntryWeeklyCommissions",
                sql: "(\"StructuralModel\" = 1 AND \"CommissionDecisionRulesVersion\" IS NULL) OR (\"StructuralModel\" = 2 AND \"CommissionDecisionRulesVersion\" IS NOT NULL AND \"CommissionDecisionRulesVersion\" = 'AQGreenWeeklyCommissionDecisionV1')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EntryWeeklyCommissions_StructuralModel_Range",
                table: "EntryWeeklyCommissions",
                sql: "\"StructuralModel\" IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenV2WeeklyCommissionEvidence_TenantId_EntryParticipati~",
                table: "AQGreenV2WeeklyCommissionEvidence",
                columns: new[] { "TenantId", "EntryParticipationId" });

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenV2WeeklyCommissionEvidence_TenantId_WeeklySalesEligi~",
                table: "AQGreenV2WeeklyCommissionEvidence",
                columns: new[] { "TenantId", "WeeklySalesEligibilityDecisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenV2WeeklyCommissionEvidenceNodes_EvidenceId_SourcePla~",
                table: "AQGreenV2WeeklyCommissionEvidenceNodes",
                columns: new[] { "EvidenceId", "SourcePlacementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenV2WeeklyCommissionEvidenceNodes_TenantId_EvidenceId",
                table: "AQGreenV2WeeklyCommissionEvidenceNodes",
                columns: new[] { "TenantId", "EvidenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenV2WeeklyCommissionEvidenceNodes_TenantId_SourcePlace~",
                table: "AQGreenV2WeeklyCommissionEvidenceNodes",
                columns: new[] { "TenantId", "SourcePlacementId" });

            if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql("""
                    CREATE FUNCTION public."ValidateAQGreenV2WeeklyCommissionGraph"()
                    RETURNS trigger
                    LANGUAGE plpgsql
                    SET search_path = pg_catalog
                    AS $function$
                    DECLARE
                        commission_id uuid;
                        structural_model integer;
                        ledger_tenant integer;
                        ledger_participation uuid;
                        ledger_period uuid;
                        ledger_customer integer;
                        ledger_qualified integer;
                        ledger_total numeric;
                        ledger_currency text;
                        ledger_terms_version text;
                        ledger_decision_version text;
                        ledger_payout_status integer;
                        ledger_calculated_at timestamp with time zone;
                        header_count bigint;
                        evidence_cutoff timestamp with time zone;
                        evidence_scope uuid;
                        evidence_participation uuid;
                        evidence_sales_decision uuid;
                        evidence_sales_applicability integer;
                        evidence_structural_rules_version text;
                        evidence_qualified integer;
                        evidence_commissioned integer;
                        evidence_sales_rules_version text;
                        evidence_sales_status integer;
                        evidence_sales_threshold integer;
                        evidence_sales_reviewed_at timestamp with time zone;
                        evidence_sales_reviewer bigint;
                        expected_node_count integer;
                        expected_depth1 integer;
                        expected_depth2 integer;
                        expected_depth3 integer;
                        period_start timestamp with time zone;
                        period_end timestamp with time zone;
                        period_terms_version text;
                        sales_participation uuid;
                        sales_week_start timestamp with time zone;
                        sales_rules_version text;
                        sales_status integer;
                        sales_threshold integer;
                        sales_reviewed_at timestamp with time zone;
                        sales_reviewer bigint;
                        component_count bigint;
                        component_max integer;
                        component_total numeric;
                        actual_node_count bigint;
                        minimum_ordinal integer;
                        maximum_ordinal integer;
                        anchor_path text;
                        actual_depth1 bigint;
                        actual_depth2 bigint;
                        actual_depth3 bigint;
                    BEGIN
                        IF TG_TABLE_NAME = 'EntryWeeklyCommissions' THEN
                            commission_id := CASE WHEN TG_OP = 'DELETE'
                                THEN OLD."Id" ELSE NEW."Id" END;
                        ELSIF TG_TABLE_NAME = 'AQGreenV2WeeklyCommissionEvidence' THEN
                            commission_id := CASE WHEN TG_OP = 'DELETE'
                                THEN OLD."EntryWeeklyCommissionId"
                                ELSE NEW."EntryWeeklyCommissionId" END;
                        ELSIF TG_TABLE_NAME = 'AQGreenV2WeeklyCommissionEvidenceNodes' THEN
                            commission_id := CASE WHEN TG_OP = 'DELETE'
                                THEN OLD."EvidenceId" ELSE NEW."EvidenceId" END;
                        ELSE
                            commission_id := CASE WHEN TG_OP = 'DELETE'
                                THEN OLD."EntryWeeklyCommissionId"
                                ELSE NEW."EntryWeeklyCommissionId" END;
                        END IF;

                        IF TG_TABLE_NAME = 'AQGreenV2WeeklyCommissionEvidenceNodes'
                           AND TG_OP = 'INSERT'
                        THEN
                            IF NOT EXISTS (
                               SELECT 1
                               FROM public."AQGreenNetworkPlacements" placement
                               JOIN public."EntryParticipations" participation
                                 ON participation."Id" = placement."ParticipantId"
                               JOIN public."Customers" customer
                                 ON customer."Id" = participation."CustomerId"
                               JOIN public."AbpUsers" app_user
                                 ON app_user."Id" = customer."UserId"
                               WHERE placement."Id" = NEW."SourcePlacementId"
                                 AND placement."TenantId" = NEW."TenantId"
                                 AND participation."Status" = NEW."ParticipationStatusObserved"
                                 AND participation."ActivatedAt" IS NOT DISTINCT FROM
                                     NEW."ParticipationActivatedAtObserved"
                                 AND participation."IsDeleted" =
                                     NEW."ParticipationIsDeletedObserved"
                                 AND customer."Id" = NEW."CustomerIdObserved"
                                 AND (customer."TenantId" = NEW."TenantId") =
                                     NEW."CustomerTenantMatchedObserved"
                                 AND customer."IsActive" =
                                     NEW."CustomerIsActiveObserved"
                                 AND customer."IsDeleted" =
                                     NEW."CustomerIsDeletedObserved"
                                 AND app_user."Id" = NEW."UserIdObserved"
                                 AND (app_user."TenantId" = NEW."TenantId") =
                                     NEW."UserTenantMatchedObserved"
                                 AND app_user."IsActive" =
                                     NEW."UserIsActiveObserved"
                                 AND app_user."IsDeleted" =
                                     NEW."UserIsDeletedObserved")
                            THEN
                                RAISE EXCEPTION 'Placement V2 commission evidence node claims do not match the observed source state.';
                            END IF;
                        END IF;

                        SELECT commission."StructuralModel",
                               commission."TenantId",
                               commission."EntryParticipationId",
                               commission."CommissionPeriodId",
                               commission."CustomerId",
                               commission."HighestCompletedLevel",
                               commission."TotalAmount",
                               commission."Currency",
                               commission."RulesVersion",
                               commission."CommissionDecisionRulesVersion",
                               commission."PayoutStatus",
                               commission."CalculatedAt"
                        INTO structural_model, ledger_tenant, ledger_participation,
                             ledger_period, ledger_customer, ledger_qualified, ledger_total,
                             ledger_currency, ledger_terms_version,
                             ledger_decision_version, ledger_payout_status,
                             ledger_calculated_at
                        FROM public."EntryWeeklyCommissions" commission
                        WHERE commission."Id" = commission_id;

                        IF NOT FOUND THEN
                            RETURN NULL;
                        END IF;

                        SELECT COUNT(*)
                        INTO header_count
                        FROM public."AQGreenV2WeeklyCommissionEvidence" evidence
                        WHERE evidence."EntryWeeklyCommissionId" = commission_id;

                        IF structural_model = 1 THEN
                            IF header_count <> 0 THEN
                                RAISE EXCEPTION 'Legacy V1 commissions cannot own Placement V2 evidence.';
                            END IF;
                            RETURN NULL;
                        END IF;

                        IF structural_model <> 2 OR header_count <> 1 THEN
                            RAISE EXCEPTION 'Placement V2 commissions require exactly one evidence header.';
                        END IF;

                        IF NOT EXISTS (
                            SELECT 1
                            FROM public."EntryParticipations" participation
                            WHERE participation."Id" = ledger_participation
                              AND participation."TenantId" = ledger_tenant
                              AND participation."CustomerId" = ledger_customer)
                        THEN
                            RAISE EXCEPTION 'Placement V2 commission ledger identity crosses its Tenant or participant customer.';
                        END IF;

                        SELECT evidence."Cutoff", evidence."PlacementTreeScopeId",
                               evidence."EntryParticipationId",
                               evidence."WeeklySalesEligibilityDecisionId",
                               evidence."QualifiedStructuralLevel",
                               evidence."CommissionedLevel",
                               evidence."EvidenceNodeCount",
                               evidence."SalesApplicability",
                               evidence."StructuralQualificationRulesVersion",
                               evidence."QualifyingDepth1Count",
                               evidence."QualifyingDepth2Count",
                               evidence."QualifyingDepth3Count",
                               evidence."SalesEligibilityRulesVersion",
                               evidence."SalesReviewStatus",
                               evidence."SalesThresholdResult",
                               evidence."SalesReviewedAt",
                               evidence."SalesReviewedByUserId"
                        INTO evidence_cutoff, evidence_scope, evidence_participation,
                             evidence_sales_decision, evidence_qualified,
                             evidence_commissioned, expected_node_count,
                             evidence_sales_applicability,
                             evidence_structural_rules_version,
                             expected_depth1, expected_depth2, expected_depth3,
                             evidence_sales_rules_version,
                             evidence_sales_status, evidence_sales_threshold,
                             evidence_sales_reviewed_at, evidence_sales_reviewer
                        FROM public."AQGreenV2WeeklyCommissionEvidence" evidence
                        WHERE evidence."EntryWeeklyCommissionId" = commission_id;

                        SELECT period."PeriodStart", period."PeriodEnd", period."RulesVersion"
                        INTO period_start, period_end, period_terms_version
                        FROM public."EntryCommissionPeriods" period
                        WHERE period."Id" = ledger_period
                          AND period."TenantId" = ledger_tenant;
                        IF NOT FOUND
                           OR evidence_cutoff IS DISTINCT FROM period_end
                           OR period_terms_version IS DISTINCT FROM ledger_terms_version
                           OR evidence_participation IS DISTINCT FROM ledger_participation
                           OR evidence_qualified IS DISTINCT FROM ledger_qualified
                           OR evidence_structural_rules_version IS DISTINCT FROM
                              'AQGreenStructuralQualificationV1'
                           OR ledger_decision_version IS DISTINCT FROM
                              'AQGreenWeeklyCommissionDecisionV1'
                        THEN
                            RAISE EXCEPTION 'Placement V2 commission evidence conflicts with its ledger or period.';
                        END IF;

                        IF evidence_sales_applicability = 2 THEN
                        SELECT decision."ParticipantId",
                               decision."CommissionWeekStartUtc",
                               decision."SalesEligibilityRulesVersion",
                               decision."ReviewStatus",
                               decision."ThresholdResult",
                               decision."ReviewedAt",
                               decision."ReviewedByUserId"
                        INTO sales_participation, sales_week_start,
                             sales_rules_version, sales_status, sales_threshold,
                             sales_reviewed_at, sales_reviewer
                        FROM public."AQGreenWeeklySalesEligibilityDecisions" decision
                        WHERE decision."Id" = evidence_sales_decision
                          AND decision."TenantId" = ledger_tenant;
                        IF NOT FOUND
                           OR sales_participation IS DISTINCT FROM ledger_participation
                           OR sales_week_start IS DISTINCT FROM period_start
                           OR sales_rules_version IS DISTINCT FROM
                              'AQGreenWeeklySalesEligibilityV1'
                           OR evidence_sales_rules_version IS DISTINCT FROM
                              sales_rules_version
                           OR evidence_sales_status IS DISTINCT FROM sales_status
                           OR evidence_sales_threshold IS DISTINCT FROM sales_threshold
                            OR evidence_sales_reviewed_at IS DISTINCT FROM sales_reviewed_at
                            OR evidence_sales_reviewer IS DISTINCT FROM sales_reviewer
                            OR sales_reviewed_at IS NULL
                            OR sales_reviewed_at > ledger_calculated_at
                            OR sales_reviewer IS NULL
                           OR NOT (
                               (sales_status = 2 AND sales_threshold IN (1, 2))
                               OR (sales_status = 3 AND sales_threshold IS NULL))
                        THEN
                            RAISE EXCEPTION 'Placement V2 commission evidence conflicts with its finalized weekly-sales decision.';
                        END IF;
                        ELSIF evidence_sales_applicability = 1 THEN
                            IF evidence_sales_decision IS NOT NULL
                               OR evidence_sales_rules_version IS NOT NULL
                               OR evidence_sales_status IS NOT NULL
                               OR evidence_sales_threshold IS NOT NULL
                               OR evidence_sales_reviewed_at IS NOT NULL
                               OR evidence_sales_reviewer IS NOT NULL
                            THEN
                                RAISE EXCEPTION 'Level 0 Placement V2 commission evidence must not contain weekly-sales evidence.';
                            END IF;
                        ELSE
                            RAISE EXCEPTION 'Placement V2 commission evidence has an unsupported sales applicability.';
                        END IF;

                        SELECT COUNT(*), COALESCE(MAX(component."Level"), 0),
                               COALESCE(SUM(component."Amount"), 0)
                        INTO component_count, component_max, component_total
                        FROM public."EntryCommissionComponents" component
                        WHERE component."EntryWeeklyCommissionId" = commission_id;
                        IF component_max IS DISTINCT FROM evidence_commissioned
                           OR component_count IS DISTINCT FROM evidence_commissioned::bigint
                           OR component_total IS DISTINCT FROM ledger_total
                           OR (evidence_commissioned = 0 AND
                               (ledger_total <> 0 OR ledger_payout_status <> 0))
                           OR (evidence_commissioned > 0 AND
                               (ledger_total <= 0 OR ledger_payout_status = 0))
                        THEN
                            RAISE EXCEPTION 'Placement V2 commissioned level, components, amount, or payout state is inconsistent.';
                        END IF;

                        IF evidence_commissioned > 0 AND NOT EXISTS (
                            SELECT 1
                            FROM public."EntryCommissionTermsVersions" terms
                            WHERE terms."Version" = ledger_terms_version)
                        THEN
                            RAISE EXCEPTION 'Placement V2 commission financial terms are unavailable.';
                        END IF;

                        IF EXISTS (
                            SELECT 1
                            FROM public."EntryCommissionComponents" component
                            JOIN public."EntryCommissionTermsVersions" terms
                              ON terms."Version" = ledger_terms_version
                            WHERE component."EntryWeeklyCommissionId" = commission_id
                              AND component."Amount" IS DISTINCT FROM CASE component."Level"
                                  WHEN 1 THEN terms."LevelOneComponentAmount"
                                  WHEN 2 THEN terms."LevelTwoComponentAmount"
                                  WHEN 3 THEN terms."LevelThreeComponentAmount"
                                  ELSE NULL END)
                        THEN
                            RAISE EXCEPTION 'Placement V2 commission components conflict with the recorded financial terms.';
                        END IF;

                        SELECT COUNT(*), MIN(node."CanonicalOrdinal"),
                               MAX(node."CanonicalOrdinal")
                        INTO actual_node_count, minimum_ordinal, maximum_ordinal
                        FROM public."AQGreenV2WeeklyCommissionEvidenceNodes" node
                        WHERE node."EvidenceId" = commission_id;
                        IF actual_node_count <> expected_node_count
                           OR minimum_ordinal <> 0
                           OR maximum_ordinal <> expected_node_count - 1
                        THEN
                            RAISE EXCEPTION 'Placement V2 commission evidence nodes or canonical ordinals are incomplete.';
                        END IF;

                        SELECT placement."CanonicalPath"
                        INTO anchor_path
                        FROM public."AQGreenV2WeeklyCommissionEvidenceNodes" node
                        JOIN public."AQGreenNetworkPlacements" placement
                          ON placement."TenantId" = node."TenantId"
                         AND placement."Id" = node."SourcePlacementId"
                        WHERE node."EvidenceId" = commission_id
                          AND node."CanonicalOrdinal" = 0
                          AND placement."ParticipantId" = ledger_participation
                          AND placement."PlacementTreeScopeId" = evidence_scope;
                        IF NOT FOUND THEN
                            RAISE EXCEPTION 'Placement V2 commission evidence has no valid anchor.';
                        END IF;

                        IF EXISTS (
                            SELECT 1
                            FROM public."AQGreenNetworkPlacements" placement
                            WHERE placement."TenantId" = ledger_tenant
                              AND placement."PlacementTreeScopeId" = evidence_scope
                              AND placement."PlacedAt" <= evidence_cutoff
                              AND placement."CanonicalPath" LIKE anchor_path || '%'
                              AND length(placement."CanonicalPath") -
                                  length(anchor_path) BETWEEN 0 AND 3
                              AND NOT EXISTS (
                                  SELECT 1
                                  FROM public."AQGreenV2WeeklyCommissionEvidenceNodes" node
                                  WHERE node."EvidenceId" = commission_id
                                    AND node."TenantId" = ledger_tenant
                                    AND node."SourcePlacementId" = placement."Id")
                        ) THEN
                            RAISE EXCEPTION 'Placement V2 commission evidence omits a placement from its bounded cutoff subtree.';
                        END IF;

                        IF EXISTS (
                            SELECT 1
                            FROM public."AQGreenV2WeeklyCommissionEvidenceNodes" node
                            JOIN public."AQGreenNetworkPlacements" placement
                              ON placement."TenantId" = node."TenantId"
                             AND placement."Id" = node."SourcePlacementId"
                            WHERE node."EvidenceId" = commission_id
                              AND (node."TenantId" <> ledger_tenant
                                   OR placement."PlacementTreeScopeId" <> evidence_scope
                                   OR placement."PlacedAt" > evidence_cutoff
                                   OR placement."RulesVersion" <>
                                      'AQGreenPlacementV2'
                                   OR placement."CanonicalPath" NOT LIKE
                                      anchor_path || '%'
                                   OR length(placement."CanonicalPath") -
                                      length(anchor_path) NOT BETWEEN 0 AND 3
                                   OR node."ParticipationStatusObserved" <> 2
                                   OR node."ParticipationActivatedAtObserved" IS NULL
                                   OR node."ParticipationActivatedAtObserved" > evidence_cutoff
                                   OR node."ParticipationIsDeletedObserved"
                                   OR NOT node."CustomerTenantMatchedObserved"
                                   OR NOT node."CustomerIsActiveObserved"
                                   OR node."CustomerIsDeletedObserved"
                                   OR NOT node."UserTenantMatchedObserved"
                                   OR NOT node."UserIsActiveObserved"
                                   OR node."UserIsDeletedObserved")
                        ) THEN
                            RAISE EXCEPTION 'Placement V2 commission evidence contains an invalid cutoff observation.';
                        END IF;

                        SELECT COUNT(*) FILTER (WHERE length(placement."CanonicalPath") - length(anchor_path) = 1),
                               COUNT(*) FILTER (WHERE length(placement."CanonicalPath") - length(anchor_path) = 2),
                               COUNT(*) FILTER (WHERE length(placement."CanonicalPath") - length(anchor_path) = 3)
                        INTO actual_depth1, actual_depth2, actual_depth3
                        FROM public."AQGreenV2WeeklyCommissionEvidenceNodes" node
                        JOIN public."AQGreenNetworkPlacements" placement
                          ON placement."TenantId" = node."TenantId"
                         AND placement."Id" = node."SourcePlacementId"
                        WHERE node."EvidenceId" = commission_id;
                        IF actual_depth1 <> expected_depth1
                           OR actual_depth2 <> expected_depth2
                           OR actual_depth3 <> expected_depth3
                        THEN
                            RAISE EXCEPTION 'Placement V2 commission structural counts conflict with its manifest.';
                        END IF;

                        IF evidence_structural_rules_version IS DISTINCT FROM
                            'AQGreenStructuralQualificationV1'
                           OR evidence_qualified IS DISTINCT FROM (CASE
                               WHEN actual_depth1 = 5 AND actual_depth2 = 25 AND
                                    actual_depth3 = 125 THEN 3
                               WHEN actual_depth1 = 5 AND actual_depth2 = 25 THEN 2
                               WHEN actual_depth1 = 5 THEN 1
                               ELSE 0
                           END)
                        THEN
                            RAISE EXCEPTION 'Placement V2 qualified structural level conflicts with the versioned structural qualification rules.';
                        END IF;

                        RETURN NULL;
                    END;
                    $function$;

                    CREATE CONSTRAINT TRIGGER "TR_EntryWeeklyCommissions_ValidateV2Evidence"
                    AFTER INSERT OR UPDATE ON public."EntryWeeklyCommissions"
                    DEFERRABLE INITIALLY DEFERRED
                    FOR EACH ROW
                    EXECUTE FUNCTION public."ValidateAQGreenV2WeeklyCommissionGraph"();
                    ALTER TABLE public."EntryWeeklyCommissions"
                        ENABLE ALWAYS TRIGGER "TR_EntryWeeklyCommissions_ValidateV2Evidence";

                    CREATE CONSTRAINT TRIGGER "TR_AQGreenV2CommissionEvidence_ValidateGraph"
                    AFTER INSERT OR UPDATE OR DELETE ON public."AQGreenV2WeeklyCommissionEvidence"
                    DEFERRABLE INITIALLY DEFERRED
                    FOR EACH ROW
                    EXECUTE FUNCTION public."ValidateAQGreenV2WeeklyCommissionGraph"();
                    ALTER TABLE public."AQGreenV2WeeklyCommissionEvidence"
                        ENABLE ALWAYS TRIGGER "TR_AQGreenV2CommissionEvidence_ValidateGraph";

                    CREATE CONSTRAINT TRIGGER "TR_AQGreenV2CommissionEvidenceNodes_ValidateGraph"
                    AFTER INSERT OR UPDATE OR DELETE ON public."AQGreenV2WeeklyCommissionEvidenceNodes"
                    DEFERRABLE INITIALLY DEFERRED
                    FOR EACH ROW
                    EXECUTE FUNCTION public."ValidateAQGreenV2WeeklyCommissionGraph"();
                    ALTER TABLE public."AQGreenV2WeeklyCommissionEvidenceNodes"
                        ENABLE ALWAYS TRIGGER "TR_AQGreenV2CommissionEvidenceNodes_ValidateGraph";

                    CREATE CONSTRAINT TRIGGER "TR_EntryCommissionComponents_ValidateV2Evidence"
                    AFTER INSERT OR UPDATE OR DELETE ON public."EntryCommissionComponents"
                    DEFERRABLE INITIALLY DEFERRED
                    FOR EACH ROW
                    EXECUTE FUNCTION public."ValidateAQGreenV2WeeklyCommissionGraph"();
                    ALTER TABLE public."EntryCommissionComponents"
                        ENABLE ALWAYS TRIGGER "TR_EntryCommissionComponents_ValidateV2Evidence";

                    CREATE FUNCTION public."PreventAQGreenV2WeeklyCommissionEvidenceMutation"()
                    RETURNS trigger
                    LANGUAGE plpgsql
                    SET search_path = pg_catalog
                    AS $function$
                    BEGIN
                        RAISE EXCEPTION 'AQGreen Placement V2 weekly commission evidence is append-only.';
                    END;
                    $function$;

                    CREATE TRIGGER "TR_AQGreenV2CommissionEvidence_AppendOnly"
                    BEFORE UPDATE OR DELETE ON public."AQGreenV2WeeklyCommissionEvidence"
                    FOR EACH ROW
                    EXECUTE FUNCTION public."PreventAQGreenV2WeeklyCommissionEvidenceMutation"();
                    ALTER TABLE public."AQGreenV2WeeklyCommissionEvidence"
                        ENABLE ALWAYS TRIGGER "TR_AQGreenV2CommissionEvidence_AppendOnly";

                    CREATE TRIGGER "TR_AQGreenV2CommissionEvidence_PreventTruncate"
                    BEFORE TRUNCATE ON public."AQGreenV2WeeklyCommissionEvidence"
                    FOR EACH STATEMENT
                    EXECUTE FUNCTION public."PreventAQGreenV2WeeklyCommissionEvidenceMutation"();
                    ALTER TABLE public."AQGreenV2WeeklyCommissionEvidence"
                        ENABLE ALWAYS TRIGGER "TR_AQGreenV2CommissionEvidence_PreventTruncate";

                    CREATE TRIGGER "TR_AQGreenV2CommissionEvidenceNodes_AppendOnly"
                    BEFORE UPDATE OR DELETE ON public."AQGreenV2WeeklyCommissionEvidenceNodes"
                    FOR EACH ROW
                    EXECUTE FUNCTION public."PreventAQGreenV2WeeklyCommissionEvidenceMutation"();
                    ALTER TABLE public."AQGreenV2WeeklyCommissionEvidenceNodes"
                        ENABLE ALWAYS TRIGGER "TR_AQGreenV2CommissionEvidenceNodes_AppendOnly";

                    CREATE TRIGGER "TR_AQGreenV2CommissionEvidenceNodes_PreventTruncate"
                    BEFORE TRUNCATE ON public."AQGreenV2WeeklyCommissionEvidenceNodes"
                    FOR EACH STATEMENT
                    EXECUTE FUNCTION public."PreventAQGreenV2WeeklyCommissionEvidenceMutation"();
                    ALTER TABLE public."AQGreenV2WeeklyCommissionEvidenceNodes"
                        ENABLE ALWAYS TRIGGER "TR_AQGreenV2CommissionEvidenceNodes_PreventTruncate";

                    CREATE FUNCTION public."PreventAQGreenV2WeeklyCommissionDecisionMutation"()
                    RETURNS trigger
                    LANGUAGE plpgsql
                    SET search_path = pg_catalog
                    AS $function$
                    BEGIN
                        IF TG_OP = 'DELETE' THEN
                            IF OLD."StructuralModel" = 2 THEN
                                RAISE EXCEPTION 'AQGreen Placement V2 weekly commission decisions cannot be deleted.';
                            END IF;
                            RETURN OLD;
                        END IF;

                        IF (OLD."StructuralModel" = 2 OR NEW."StructuralModel" = 2) AND (
                            OLD."TenantId" IS DISTINCT FROM NEW."TenantId"
                            OR OLD."EntryParticipationId" IS DISTINCT FROM NEW."EntryParticipationId"
                            OR OLD."CustomerId" IS DISTINCT FROM NEW."CustomerId"
                            OR OLD."CommissionPeriodId" IS DISTINCT FROM NEW."CommissionPeriodId"
                            OR OLD."StructuralModel" IS DISTINCT FROM NEW."StructuralModel"
                            OR OLD."CommissionDecisionRulesVersion" IS DISTINCT FROM NEW."CommissionDecisionRulesVersion"
                            OR OLD."HighestCompletedLevel" IS DISTINCT FROM NEW."HighestCompletedLevel"
                            OR OLD."TotalAmount" IS DISTINCT FROM NEW."TotalAmount"
                            OR OLD."Currency" IS DISTINCT FROM NEW."Currency"
                            OR OLD."RulesVersion" IS DISTINCT FROM NEW."RulesVersion"
                            OR OLD."CalculatedAt" IS DISTINCT FROM NEW."CalculatedAt"
                            OR OLD."CreationTime" IS DISTINCT FROM NEW."CreationTime"
                            OR OLD."CreatorUserId" IS DISTINCT FROM NEW."CreatorUserId"
                            OR OLD."IsDeleted" IS DISTINCT FROM NEW."IsDeleted"
                            OR OLD."DeletionTime" IS DISTINCT FROM NEW."DeletionTime"
                            OR OLD."DeleterUserId" IS DISTINCT FROM NEW."DeleterUserId")
                        THEN
                            RAISE EXCEPTION 'AQGreen Placement V2 weekly commission calculation facts are immutable.';
                        END IF;
                        RETURN NEW;
                    END;
                    $function$;

                    CREATE TRIGGER "TR_EntryWeeklyCommissions_ProtectV2Decision"
                    BEFORE UPDATE OR DELETE ON public."EntryWeeklyCommissions"
                    FOR EACH ROW
                    EXECUTE FUNCTION public."PreventAQGreenV2WeeklyCommissionDecisionMutation"();
                    ALTER TABLE public."EntryWeeklyCommissions"
                        ENABLE ALWAYS TRIGGER "TR_EntryWeeklyCommissions_ProtectV2Decision";

                    CREATE FUNCTION public."PreventAQGreenV2CommissionComponentMutation"()
                    RETURNS trigger
                    LANGUAGE plpgsql
                    SET search_path = pg_catalog
                    AS $function$
                    DECLARE
                        old_is_v2 boolean := false;
                        new_is_v2 boolean := false;
                    BEGIN
                        IF TG_OP <> 'INSERT' THEN
                            SELECT EXISTS (
                                SELECT 1 FROM public."EntryWeeklyCommissions" commission
                                WHERE commission."Id" = OLD."EntryWeeklyCommissionId"
                                  AND commission."StructuralModel" = 2)
                            INTO old_is_v2;
                        END IF;
                        IF TG_OP <> 'DELETE' THEN
                            SELECT EXISTS (
                                SELECT 1 FROM public."EntryWeeklyCommissions" commission
                                WHERE commission."Id" = NEW."EntryWeeklyCommissionId"
                                  AND commission."StructuralModel" = 2)
                            INTO new_is_v2;
                        END IF;
                        IF old_is_v2 OR (TG_OP = 'UPDATE' AND new_is_v2) THEN
                            RAISE EXCEPTION 'AQGreen Placement V2 weekly commission components are append-only.';
                        END IF;
                        RETURN CASE WHEN TG_OP = 'DELETE' THEN OLD ELSE NEW END;
                    END;
                    $function$;

                    CREATE TRIGGER "TR_EntryCommissionComponents_ProtectV2"
                    BEFORE UPDATE OR DELETE ON public."EntryCommissionComponents"
                    FOR EACH ROW
                    EXECUTE FUNCTION public."PreventAQGreenV2CommissionComponentMutation"();
                    ALTER TABLE public."EntryCommissionComponents"
                        ENABLE ALWAYS TRIGGER "TR_EntryCommissionComponents_ProtectV2";

                    CREATE FUNCTION public."PreventAQGreenV2CommissionComponentTruncate"()
                    RETURNS trigger
                    LANGUAGE plpgsql
                    SET search_path = pg_catalog
                    AS $function$
                    BEGIN
                        IF EXISTS (
                            SELECT 1 FROM public."EntryWeeklyCommissions"
                            WHERE "StructuralModel" = 2)
                        THEN
                            RAISE EXCEPTION 'AQGreen Placement V2 weekly commission components cannot be truncated.';
                        END IF;
                        RETURN NULL;
                    END;
                    $function$;

                    CREATE TRIGGER "TR_EntryCommissionComponents_PreventV2Truncate"
                    BEFORE TRUNCATE ON public."EntryCommissionComponents"
                    FOR EACH STATEMENT
                    EXECUTE FUNCTION public."PreventAQGreenV2CommissionComponentTruncate"();
                    ALTER TABLE public."EntryCommissionComponents"
                        ENABLE ALWAYS TRIGGER "TR_EntryCommissionComponents_PreventV2Truncate";

                    CREATE FUNCTION public."PreventAQGreenV2CommissionPeriodMutation"()
                    RETURNS trigger
                    LANGUAGE plpgsql
                    SET search_path = pg_catalog
                    AS $function$
                    BEGIN
                        IF EXISTS (
                            SELECT 1 FROM public."EntryWeeklyCommissions" commission
                            WHERE commission."CommissionPeriodId" = OLD."Id"
                              AND commission."StructuralModel" = 2)
                           AND (TG_OP = 'DELETE' OR
                               OLD."TenantId" IS DISTINCT FROM NEW."TenantId"
                               OR OLD."PeriodStart" IS DISTINCT FROM NEW."PeriodStart"
                               OR OLD."PeriodEnd" IS DISTINCT FROM NEW."PeriodEnd"
                               OR OLD."TimeZoneId" IS DISTINCT FROM NEW."TimeZoneId"
                               OR OLD."CalculatedAt" IS DISTINCT FROM NEW."CalculatedAt"
                               OR OLD."RulesVersion" IS DISTINCT FROM NEW."RulesVersion"
                               OR OLD."IsDeleted" IS DISTINCT FROM NEW."IsDeleted"
                               OR OLD."DeletionTime" IS DISTINCT FROM NEW."DeletionTime"
                               OR OLD."DeleterUserId" IS DISTINCT FROM NEW."DeleterUserId")
                        THEN
                            RAISE EXCEPTION 'AQGreen Placement V2 weekly commission period facts are immutable.';
                        END IF;
                        RETURN CASE WHEN TG_OP = 'DELETE' THEN OLD ELSE NEW END;
                    END;
                    $function$;

                    CREATE TRIGGER "TR_EntryCommissionPeriods_ProtectV2"
                    BEFORE UPDATE OR DELETE ON public."EntryCommissionPeriods"
                    FOR EACH ROW
                    EXECUTE FUNCTION public."PreventAQGreenV2CommissionPeriodMutation"();
                    ALTER TABLE public."EntryCommissionPeriods"
                        ENABLE ALWAYS TRIGGER "TR_EntryCommissionPeriods_ProtectV2";
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql("""
                    LOCK TABLE public."EntryWeeklyCommissions",
                               public."EntryCommissionComponents",
                               public."EntryCommissionPeriods",
                               public."AQGreenV2WeeklyCommissionEvidence",
                               public."AQGreenV2WeeklyCommissionEvidenceNodes"
                    IN ACCESS EXCLUSIVE MODE;

                    DO $block$
                    BEGIN
                        IF EXISTS (
                            SELECT 1 FROM public."EntryWeeklyCommissions"
                            WHERE "StructuralModel" = 2)
                           OR EXISTS (
                            SELECT 1 FROM public."AQGreenV2WeeklyCommissionEvidence")
                        THEN
                            RAISE EXCEPTION 'Cannot remove AQGreen Placement V2 commission evidence after V2 decisions have been recorded.';
                        END IF;
                        IF EXISTS (
                            SELECT 1 FROM public."EntryWeeklyCommissions"
                            WHERE "CommissionDecisionRulesVersion" IS NOT NULL)
                        THEN
                            RAISE EXCEPTION 'Cannot remove AQGreen commission decision versioning after versioned decisions have been recorded.';
                        END IF;
                    END;
                    $block$;

                    DROP TRIGGER IF EXISTS "TR_EntryWeeklyCommissions_ValidateV2Evidence"
                        ON public."EntryWeeklyCommissions";
                    DROP TRIGGER IF EXISTS "TR_AQGreenV2CommissionEvidence_ValidateGraph"
                        ON public."AQGreenV2WeeklyCommissionEvidence";
                    DROP TRIGGER IF EXISTS "TR_AQGreenV2CommissionEvidenceNodes_ValidateGraph"
                        ON public."AQGreenV2WeeklyCommissionEvidenceNodes";
                    DROP TRIGGER IF EXISTS "TR_EntryCommissionComponents_ValidateV2Evidence"
                        ON public."EntryCommissionComponents";
                    DROP TRIGGER IF EXISTS "TR_AQGreenV2CommissionEvidence_AppendOnly"
                        ON public."AQGreenV2WeeklyCommissionEvidence";
                    DROP TRIGGER IF EXISTS "TR_AQGreenV2CommissionEvidence_PreventTruncate"
                        ON public."AQGreenV2WeeklyCommissionEvidence";
                    DROP TRIGGER IF EXISTS "TR_AQGreenV2CommissionEvidenceNodes_AppendOnly"
                        ON public."AQGreenV2WeeklyCommissionEvidenceNodes";
                    DROP TRIGGER IF EXISTS "TR_AQGreenV2CommissionEvidenceNodes_PreventTruncate"
                        ON public."AQGreenV2WeeklyCommissionEvidenceNodes";
                    DROP TRIGGER IF EXISTS "TR_EntryWeeklyCommissions_ProtectV2Decision"
                        ON public."EntryWeeklyCommissions";
                    DROP TRIGGER IF EXISTS "TR_EntryCommissionComponents_ProtectV2"
                        ON public."EntryCommissionComponents";
                    DROP TRIGGER IF EXISTS "TR_EntryCommissionComponents_PreventV2Truncate"
                        ON public."EntryCommissionComponents";
                    DROP TRIGGER IF EXISTS "TR_EntryCommissionPeriods_ProtectV2"
                        ON public."EntryCommissionPeriods";
                    DROP FUNCTION IF EXISTS public."ValidateAQGreenV2WeeklyCommissionGraph"();
                    DROP FUNCTION IF EXISTS public."PreventAQGreenV2WeeklyCommissionEvidenceMutation"();
                    DROP FUNCTION IF EXISTS public."PreventAQGreenV2WeeklyCommissionDecisionMutation"();
                    DROP FUNCTION IF EXISTS public."PreventAQGreenV2CommissionComponentMutation"();
                    DROP FUNCTION IF EXISTS public."PreventAQGreenV2CommissionComponentTruncate"();
                    DROP FUNCTION IF EXISTS public."PreventAQGreenV2CommissionPeriodMutation"();
                    """);
            }

            migrationBuilder.DropTable(
                name: "AQGreenV2WeeklyCommissionEvidenceNodes");

            migrationBuilder.DropTable(
                name: "AQGreenV2WeeklyCommissionEvidence");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_EntryWeeklyCommissions_TenantId_Id",
                table: "EntryWeeklyCommissions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EntryWeeklyCommissions_DecisionVersion_Shape",
                table: "EntryWeeklyCommissions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EntryWeeklyCommissions_StructuralModel_Range",
                table: "EntryWeeklyCommissions");

            migrationBuilder.DropColumn(
                name: "CommissionDecisionRulesVersion",
                table: "EntryWeeklyCommissions");

            migrationBuilder.DropColumn(
                name: "StructuralModel",
                table: "EntryWeeklyCommissions");
        }
    }
}
