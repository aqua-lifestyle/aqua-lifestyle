using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    /// <inheritdoc />
    public partial class AddAQGreenV2GraduationEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql("""
                    LOCK TABLE public."OnyxGraduationDecisions",
                               public."AQGreenNetworkPlacements"
                    IN SHARE ROW EXCLUSIVE MODE;

                    DO $block$
                    BEGIN
                        IF EXISTS (
                            SELECT 1
                            FROM public."OnyxGraduationDecisions"
                            WHERE "TenantId" <= 0
                               OR "EvaluatedNetworkLevel" IS NULL)
                        THEN
                            RAISE EXCEPTION 'Cannot add AQGreen V2 graduation evidence: historical V1 graduation data is invalid.';
                        END IF;
                    END;
                    $block$;
                    """);
            }

            migrationBuilder.AlterColumn<int>(
                name: "EvaluatedNetworkLevel",
                table: "OnyxGraduationDecisions",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "EvaluatedLoanTermsVersion",
                table: "OnyxGraduationDecisions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GraduationRulesVersion",
                table: "OnyxGraduationDecisions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StructuralModel",
                table: "OnyxGraduationDecisions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_OnyxGraduationDecisions_TenantId_Id",
                table: "OnyxGraduationDecisions",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_AQGreenNetworkPlacements_TenantId_Id",
                table: "AQGreenNetworkPlacements",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateTable(
                name: "AQGreenV2GraduationEvidence",
                columns: table => new
                {
                    OnyxGraduationDecisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    Cutoff = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StructuralQualificationRulesVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EvidenceSchemaVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EvaluatedStructuralCompletionLevel = table.Column<int>(type: "integer", nullable: false),
                    QualifyingDepth1Count = table.Column<int>(type: "integer", nullable: false),
                    QualifyingDepth2Count = table.Column<int>(type: "integer", nullable: false),
                    EvidenceNodeCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AQGreenV2GraduationEvidence", x => x.OnyxGraduationDecisionId);
                    table.UniqueConstraint("AK_AQGreenV2GraduationEvidence_TenantId_OnyxGraduationDecision~", x => new { x.TenantId, x.OnyxGraduationDecisionId });
                    table.CheckConstraint("CK_AQGreenV2GraduationEvidence_Level_Range", "\"EvaluatedStructuralCompletionLevel\" IN (0, 1, 2, 3)");
                    table.CheckConstraint("CK_AQGreenV2GraduationEvidence_Result_NonNegative", "\"QualifyingDepth1Count\" >= 0 AND \"QualifyingDepth2Count\" >= 0 AND \"EvidenceNodeCount\" > 0");
                    table.CheckConstraint("CK_AQGreenV2GraduationEvidence_SchemaVersion_NotBlank", "length(trim(\"EvidenceSchemaVersion\")) > 0");
                    table.CheckConstraint("CK_AQGreenV2GraduationEvidence_StructuralVersion_NotBlank", "length(trim(\"StructuralQualificationRulesVersion\")) > 0");
                    table.CheckConstraint("CK_AQGreenV2GraduationEvidence_TenantId_Positive", "\"TenantId\" > 0");
                    table.ForeignKey(
                        name: "FK_AQGreenV2GraduationEvidence_OnyxGraduationDecisions_TenantI~",
                        columns: x => new { x.TenantId, x.OnyxGraduationDecisionId },
                        principalTable: "OnyxGraduationDecisions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AQGreenV2GraduationEvidenceNodes",
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
                    table.PrimaryKey("PK_AQGreenV2GraduationEvidenceNodes", x => new { x.EvidenceId, x.CanonicalOrdinal });
                    table.CheckConstraint("CK_AQGreenV2GraduationEvidenceNodes_CanonicalOrdinal_NonNegati~", "\"CanonicalOrdinal\" >= 0");
                    table.CheckConstraint("CK_AQGreenV2GraduationEvidenceNodes_CustomerId_Positive", "\"CustomerIdObserved\" > 0");
                    table.CheckConstraint("CK_AQGreenV2GraduationEvidenceNodes_ParticipationStatus_Range", "\"ParticipationStatusObserved\" IN (0, 1, 2, 3, 4)");
                    table.CheckConstraint("CK_AQGreenV2GraduationEvidenceNodes_TenantId_Positive", "\"TenantId\" > 0");
                    table.CheckConstraint("CK_AQGreenV2GraduationEvidenceNodes_UserId_Positive", "\"UserIdObserved\" > 0");
                    table.ForeignKey(
                        name: "FK_AQGreenV2GraduationEvidenceNodes_AQGreenNetworkPlacements_T~",
                        columns: x => new { x.TenantId, x.SourcePlacementId },
                        principalTable: "AQGreenNetworkPlacements",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AQGreenV2GraduationEvidenceNodes_AQGreenV2GraduationEvidenc~",
                        columns: x => new { x.TenantId, x.EvidenceId },
                        principalTable: "AQGreenV2GraduationEvidence",
                        principalColumns: new[] { "TenantId", "OnyxGraduationDecisionId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_OnyxGraduationDecisions_EvaluatedLoanTermsVersion_NotBlank",
                table: "OnyxGraduationDecisions",
                sql: "\"EvaluatedLoanTermsVersion\" IS NULL OR length(trim(\"EvaluatedLoanTermsVersion\")) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OnyxGraduationDecisions_GraduationRulesVersion_NotBlank",
                table: "OnyxGraduationDecisions",
                sql: "\"GraduationRulesVersion\" IS NULL OR length(trim(\"GraduationRulesVersion\")) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OnyxGraduationDecisions_StructuralModel_LevelShape",
                table: "OnyxGraduationDecisions",
                sql: "(\"StructuralModel\" = 1 AND \"EvaluatedNetworkLevel\" IS NOT NULL) OR (\"StructuralModel\" = 2 AND \"EvaluatedNetworkLevel\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OnyxGraduationDecisions_StructuralModel_Range",
                table: "OnyxGraduationDecisions",
                sql: "\"StructuralModel\" IN (1, 2)");

            if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql("""
                    ALTER TABLE public."OnyxGraduationDecisions"
                    ADD CONSTRAINT "CK_OnyxGraduationDecisions_VersionSnapshots_Required"
                    CHECK (
                        "GraduationRulesVersion" IS NOT NULL
                        AND "EvaluatedLoanTermsVersion" IS NOT NULL)
                    NOT VALID;
                    """);
            }
            else
            {
                migrationBuilder.AddCheckConstraint(
                    name: "CK_OnyxGraduationDecisions_VersionSnapshots_Required",
                    table: "OnyxGraduationDecisions",
                    sql: "\"GraduationRulesVersion\" IS NOT NULL AND " +
                         "\"EvaluatedLoanTermsVersion\" IS NOT NULL");
            }

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenV2GraduationEvidenceNodes_EvidenceId_SourcePlacement~",
                table: "AQGreenV2GraduationEvidenceNodes",
                columns: new[] { "EvidenceId", "SourcePlacementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenV2GraduationEvidenceNodes_TenantId_EvidenceId",
                table: "AQGreenV2GraduationEvidenceNodes",
                columns: new[] { "TenantId", "EvidenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenV2GraduationEvidenceNodes_TenantId_SourcePlacementId",
                table: "AQGreenV2GraduationEvidenceNodes",
                columns: new[] { "TenantId", "SourcePlacementId" });

            if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql("""
                    ALTER TABLE public."OnyxGraduationDecisions"
                        ALTER COLUMN "StructuralModel" DROP DEFAULT;

                    CREATE FUNCTION public."ValidateAQGreenV2GraduationGraph"()
                    RETURNS trigger
                    LANGUAGE plpgsql
                    SET search_path = pg_catalog
                    AS $function$
                    DECLARE
                        decision_id uuid;
                        structural_model integer;
                        decision_time timestamp with time zone;
                        header_count bigint;
                        header_tenant integer;
                        evidence_cutoff timestamp with time zone;
                        expected_node_count integer;
                        actual_node_count bigint;
                        minimum_ordinal integer;
                        maximum_ordinal integer;
                    BEGIN
                        IF TG_TABLE_NAME = 'OnyxGraduationDecisions' THEN
                            decision_id := CASE WHEN TG_OP = 'DELETE' THEN OLD."Id" ELSE NEW."Id" END;
                        ELSIF TG_TABLE_NAME = 'AQGreenV2GraduationEvidence' THEN
                            decision_id := CASE WHEN TG_OP = 'DELETE'
                                THEN OLD."OnyxGraduationDecisionId"
                                ELSE NEW."OnyxGraduationDecisionId" END;
                        ELSE
                            decision_id := CASE WHEN TG_OP = 'DELETE'
                                THEN OLD."EvidenceId"
                                ELSE NEW."EvidenceId" END;
                        END IF;

                        SELECT decision."StructuralModel", decision."DecidedAt"
                        INTO structural_model, decision_time
                        FROM public."OnyxGraduationDecisions" decision
                        WHERE decision."Id" = decision_id;

                        IF NOT FOUND THEN
                            RETURN NULL;
                        END IF;

                        SELECT COUNT(*)
                        INTO header_count
                        FROM public."AQGreenV2GraduationEvidence" evidence
                        WHERE evidence."OnyxGraduationDecisionId" = decision_id;

                        IF structural_model = 1 THEN
                            IF header_count <> 0 THEN
                                RAISE EXCEPTION 'Legacy V1 graduation decisions cannot own Placement V2 evidence.';
                            END IF;
                            RETURN NULL;
                        END IF;

                        IF structural_model <> 2 OR header_count <> 1 THEN
                            RAISE EXCEPTION 'Placement V2 graduation decisions require exactly one evidence header.';
                        END IF;

                        SELECT evidence."TenantId", evidence."Cutoff", evidence."EvidenceNodeCount"
                        INTO header_tenant, evidence_cutoff, expected_node_count
                        FROM public."AQGreenV2GraduationEvidence" evidence
                        WHERE evidence."OnyxGraduationDecisionId" = decision_id;

                        IF evidence_cutoff > decision_time THEN
                            RAISE EXCEPTION 'AQGreen V2 graduation evidence cutoff cannot follow the decision time.';
                        END IF;

                        SELECT COUNT(*), MIN(node."CanonicalOrdinal"), MAX(node."CanonicalOrdinal")
                        INTO actual_node_count, minimum_ordinal, maximum_ordinal
                        FROM public."AQGreenV2GraduationEvidenceNodes" node
                        WHERE node."EvidenceId" = decision_id;

                        IF actual_node_count <> expected_node_count
                           OR minimum_ordinal <> 0
                           OR maximum_ordinal <> expected_node_count - 1
                        THEN
                            RAISE EXCEPTION 'AQGreen V2 graduation evidence node count or canonical ordinals are incomplete.';
                        END IF;

                        IF EXISTS (
                            SELECT 1
                            FROM public."AQGreenV2GraduationEvidenceNodes" node
                            JOIN public."AQGreenNetworkPlacements" placement
                              ON placement."TenantId" = node."TenantId"
                             AND placement."Id" = node."SourcePlacementId"
                            WHERE node."EvidenceId" = decision_id
                              AND (node."TenantId" <> header_tenant
                                   OR placement."PlacedAt" > evidence_cutoff))
                        THEN
                            RAISE EXCEPTION 'AQGreen V2 graduation evidence references a placement after its cutoff or outside its Tenant.';
                        END IF;

                        RETURN NULL;
                    END;
                    $function$;

                    CREATE CONSTRAINT TRIGGER "TR_OnyxGraduationDecisions_ValidateV2Evidence"
                    AFTER INSERT OR UPDATE ON public."OnyxGraduationDecisions"
                    DEFERRABLE INITIALLY DEFERRED
                    FOR EACH ROW
                    EXECUTE FUNCTION public."ValidateAQGreenV2GraduationGraph"();

                    CREATE CONSTRAINT TRIGGER "TR_AQGreenV2GraduationEvidence_ValidateGraph"
                    AFTER INSERT OR UPDATE OR DELETE ON public."AQGreenV2GraduationEvidence"
                    DEFERRABLE INITIALLY DEFERRED
                    FOR EACH ROW
                    EXECUTE FUNCTION public."ValidateAQGreenV2GraduationGraph"();

                    CREATE CONSTRAINT TRIGGER "TR_AQGreenV2GraduationEvidenceNodes_ValidateGraph"
                    AFTER INSERT OR UPDATE OR DELETE ON public."AQGreenV2GraduationEvidenceNodes"
                    DEFERRABLE INITIALLY DEFERRED
                    FOR EACH ROW
                    EXECUTE FUNCTION public."ValidateAQGreenV2GraduationGraph"();

                    CREATE FUNCTION public."PreventAQGreenV2GraduationEvidenceMutation"()
                    RETURNS trigger
                    LANGUAGE plpgsql
                    SET search_path = pg_catalog
                    AS $function$
                    BEGIN
                        RAISE EXCEPTION 'AQGreen V2 graduation evidence is append-only.';
                    END;
                    $function$;

                    CREATE TRIGGER "TR_AQGreenV2GraduationEvidence_AppendOnly"
                    BEFORE UPDATE OR DELETE ON public."AQGreenV2GraduationEvidence"
                    FOR EACH ROW
                    EXECUTE FUNCTION public."PreventAQGreenV2GraduationEvidenceMutation"();
                    ALTER TABLE public."AQGreenV2GraduationEvidence"
                        ENABLE ALWAYS TRIGGER "TR_AQGreenV2GraduationEvidence_AppendOnly";

                    CREATE TRIGGER "TR_AQGreenV2GraduationEvidence_PreventTruncate"
                    BEFORE TRUNCATE ON public."AQGreenV2GraduationEvidence"
                    FOR EACH STATEMENT
                    EXECUTE FUNCTION public."PreventAQGreenV2GraduationEvidenceMutation"();
                    ALTER TABLE public."AQGreenV2GraduationEvidence"
                        ENABLE ALWAYS TRIGGER "TR_AQGreenV2GraduationEvidence_PreventTruncate";

                    CREATE TRIGGER "TR_AQGreenV2GraduationEvidenceNodes_AppendOnly"
                    BEFORE UPDATE OR DELETE ON public."AQGreenV2GraduationEvidenceNodes"
                    FOR EACH ROW
                    EXECUTE FUNCTION public."PreventAQGreenV2GraduationEvidenceMutation"();
                    ALTER TABLE public."AQGreenV2GraduationEvidenceNodes"
                        ENABLE ALWAYS TRIGGER "TR_AQGreenV2GraduationEvidenceNodes_AppendOnly";

                    CREATE TRIGGER "TR_AQGreenV2GraduationEvidenceNodes_PreventTruncate"
                    BEFORE TRUNCATE ON public."AQGreenV2GraduationEvidenceNodes"
                    FOR EACH STATEMENT
                    EXECUTE FUNCTION public."PreventAQGreenV2GraduationEvidenceMutation"();
                    ALTER TABLE public."AQGreenV2GraduationEvidenceNodes"
                        ENABLE ALWAYS TRIGGER "TR_AQGreenV2GraduationEvidenceNodes_PreventTruncate";
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql("""
                    LOCK TABLE public."OnyxGraduationDecisions",
                               public."AQGreenV2GraduationEvidence",
                               public."AQGreenV2GraduationEvidenceNodes"
                    IN ACCESS EXCLUSIVE MODE;

                    DO $block$
                    BEGIN
                        IF EXISTS (
                            SELECT 1
                            FROM public."OnyxGraduationDecisions"
                            WHERE "StructuralModel" = 2)
                           OR EXISTS (
                            SELECT 1
                            FROM public."AQGreenV2GraduationEvidence")
                        THEN
                            RAISE EXCEPTION 'Cannot remove AQGreen V2 graduation evidence after Placement V2 evidence has been recorded.';
                        END IF;

                        IF EXISTS (
                            SELECT 1
                            FROM public."OnyxGraduationDecisions"
                            WHERE "EvaluatedNetworkLevel" IS NULL)
                        THEN
                            RAISE EXCEPTION 'Cannot restore the required legacy network level because historical evidence is missing.';
                        END IF;
                    END;
                    $block$;

                    DROP TRIGGER IF EXISTS "TR_OnyxGraduationDecisions_ValidateV2Evidence"
                        ON public."OnyxGraduationDecisions";
                    DROP TRIGGER IF EXISTS "TR_AQGreenV2GraduationEvidence_ValidateGraph"
                        ON public."AQGreenV2GraduationEvidence";
                    DROP TRIGGER IF EXISTS "TR_AQGreenV2GraduationEvidenceNodes_ValidateGraph"
                        ON public."AQGreenV2GraduationEvidenceNodes";
                    DROP TRIGGER IF EXISTS "TR_AQGreenV2GraduationEvidence_AppendOnly"
                        ON public."AQGreenV2GraduationEvidence";
                    DROP TRIGGER IF EXISTS "TR_AQGreenV2GraduationEvidence_PreventTruncate"
                        ON public."AQGreenV2GraduationEvidence";
                    DROP TRIGGER IF EXISTS "TR_AQGreenV2GraduationEvidenceNodes_AppendOnly"
                        ON public."AQGreenV2GraduationEvidenceNodes";
                    DROP TRIGGER IF EXISTS "TR_AQGreenV2GraduationEvidenceNodes_PreventTruncate"
                        ON public."AQGreenV2GraduationEvidenceNodes";
                    DROP FUNCTION IF EXISTS public."ValidateAQGreenV2GraduationGraph"();
                    DROP FUNCTION IF EXISTS public."PreventAQGreenV2GraduationEvidenceMutation"();
                    """);
            }

            migrationBuilder.DropTable(
                name: "AQGreenV2GraduationEvidenceNodes");

            migrationBuilder.DropTable(
                name: "AQGreenV2GraduationEvidence");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_OnyxGraduationDecisions_TenantId_Id",
                table: "OnyxGraduationDecisions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OnyxGraduationDecisions_EvaluatedLoanTermsVersion_NotBlank",
                table: "OnyxGraduationDecisions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OnyxGraduationDecisions_GraduationRulesVersion_NotBlank",
                table: "OnyxGraduationDecisions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OnyxGraduationDecisions_StructuralModel_LevelShape",
                table: "OnyxGraduationDecisions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OnyxGraduationDecisions_StructuralModel_Range",
                table: "OnyxGraduationDecisions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OnyxGraduationDecisions_VersionSnapshots_Required",
                table: "OnyxGraduationDecisions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_AQGreenNetworkPlacements_TenantId_Id",
                table: "AQGreenNetworkPlacements");

            migrationBuilder.DropColumn(
                name: "EvaluatedLoanTermsVersion",
                table: "OnyxGraduationDecisions");

            migrationBuilder.DropColumn(
                name: "GraduationRulesVersion",
                table: "OnyxGraduationDecisions");

            migrationBuilder.DropColumn(
                name: "StructuralModel",
                table: "OnyxGraduationDecisions");

            migrationBuilder.AlterColumn<int>(
                name: "EvaluatedNetworkLevel",
                table: "OnyxGraduationDecisions",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
