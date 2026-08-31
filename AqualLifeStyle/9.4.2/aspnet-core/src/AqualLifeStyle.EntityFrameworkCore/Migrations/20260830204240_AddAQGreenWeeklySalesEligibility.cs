using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    /// <inheritdoc />
    public partial class AddAQGreenWeeklySalesEligibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AQGreenWeeklySalesEligibilityDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommissionWeekStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SalesEligibilityRulesVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReviewStatus = table.Column<int>(type: "integer", nullable: false),
                    ReviewedSprayQuantity = table.Column<int>(type: "integer", nullable: true),
                    ReviewedOneLitreQuantity = table.Column<int>(type: "integer", nullable: true),
                    ReviewedFiveLitreQuantity = table.Column<int>(type: "integer", nullable: true),
                    ThresholdResult = table.Column<int>(type: "integer", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AQGreenWeeklySalesEligibilityDecisions", x => x.Id);
                    table.UniqueConstraint("AK_AQGreenWeeklySalesEligibilityDecisions_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_AQGreenWeeklySalesDecisions_Quantity_NonNegative", "(\"ReviewedSprayQuantity\" IS NULL OR \"ReviewedSprayQuantity\" >= 0) AND (\"ReviewedOneLitreQuantity\" IS NULL OR \"ReviewedOneLitreQuantity\" >= 0) AND (\"ReviewedFiveLitreQuantity\" IS NULL OR \"ReviewedFiveLitreQuantity\" >= 0)");
                    table.CheckConstraint("CK_AQGreenWeeklySalesDecisions_CanonicalWeek", "EXTRACT(ISODOW FROM (\"CommissionWeekStartUtc\" AT TIME ZONE 'Africa/Johannesburg')) = 5 AND (\"CommissionWeekStartUtc\" AT TIME ZONE 'Africa/Johannesburg')::time = TIME '00:00:00'");
                    table.CheckConstraint("CK_AQGreenWeeklySalesDecisions_FinalizedAfterWeekClose", "\"ReviewedAt\" IS NULL OR \"ReviewedAt\" >= \"CommissionWeekStartUtc\" + INTERVAL '7 days'");
                    table.CheckConstraint("CK_AQGreenWeeklySalesDecisions_Reviewer_Positive", "\"ReviewedByUserId\" IS NULL OR \"ReviewedByUserId\" > 0");
                    table.CheckConstraint("CK_AQGreenWeeklySalesDecisions_RulesVersion_NotBlank", "length(trim(\"SalesEligibilityRulesVersion\")) > 0");
                    table.CheckConstraint("CK_AQGreenWeeklySalesDecisions_RulesVersion_Supported", "\"SalesEligibilityRulesVersion\" = 'AQGreenWeeklySalesEligibilityV1'");
                    table.CheckConstraint("CK_AQGreenWeeklySalesDecisions_StateShape", "(\"ReviewStatus\" = 1 AND \"ReviewedSprayQuantity\" IS NULL AND \"ReviewedOneLitreQuantity\" IS NULL AND \"ReviewedFiveLitreQuantity\" IS NULL AND \"ThresholdResult\" IS NULL AND \"ReviewedAt\" IS NULL AND \"ReviewedByUserId\" IS NULL AND \"RejectionReason\" IS NULL) OR (\"ReviewStatus\" = 2 AND \"ReviewedSprayQuantity\" IS NOT NULL AND \"ReviewedOneLitreQuantity\" IS NOT NULL AND \"ReviewedFiveLitreQuantity\" IS NOT NULL AND \"ThresholdResult\" IS NOT NULL AND \"ReviewedAt\" IS NOT NULL AND \"ReviewedByUserId\" IS NOT NULL AND \"RejectionReason\" IS NULL) OR (\"ReviewStatus\" = 3 AND \"ReviewedSprayQuantity\" IS NULL AND \"ReviewedOneLitreQuantity\" IS NULL AND \"ReviewedFiveLitreQuantity\" IS NULL AND \"ThresholdResult\" IS NULL AND \"ReviewedAt\" IS NOT NULL AND \"ReviewedByUserId\" IS NOT NULL AND \"RejectionReason\" IS NOT NULL AND length(trim(\"RejectionReason\")) > 0)");
                    table.CheckConstraint("CK_AQGreenWeeklySalesDecisions_Status_Range", "\"ReviewStatus\" IN (1, 2, 3)");
                    table.CheckConstraint("CK_AQGreenWeeklySalesDecisions_TenantId_Positive", "\"TenantId\" > 0");
                    table.CheckConstraint("CK_AQGreenWeeklySalesDecisions_Threshold_Range", "\"ThresholdResult\" IS NULL OR \"ThresholdResult\" IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_AQGreenWeeklySalesEligibilityDecisions_EntryParticipations_~",
                        columns: x => new { x.TenantId, x.ParticipantId },
                        principalTable: "EntryParticipations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AQGreenWeeklySalesEvidenceReferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    DecisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    TechnicalReference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AQGreenWeeklySalesEvidenceReferences", x => x.Id);
                    table.CheckConstraint("CK_AQGreenWeeklySalesEvidence_Reference_NotBlank", "length(trim(\"TechnicalReference\")) > 0 AND \"TechnicalReference\" = trim(\"TechnicalReference\")");
                    table.CheckConstraint("CK_AQGreenWeeklySalesEvidence_Source_Range", "\"Source\" = 1");
                    table.CheckConstraint("CK_AQGreenWeeklySalesEvidence_TenantId_Positive", "\"TenantId\" > 0");
                    table.ForeignKey(
                        name: "FK_AQGreenWeeklySalesEvidenceReferences_AQGreenWeeklySalesElig~",
                        columns: x => new { x.TenantId, x.DecisionId },
                        principalTable: "AQGreenWeeklySalesEligibilityDecisions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenWeeklySalesEligibilityDecisions_TenantId_Participant~",
                table: "AQGreenWeeklySalesEligibilityDecisions",
                columns: new[] { "TenantId", "ParticipantId", "CommissionWeekStartUtc", "SalesEligibilityRulesVersion" },
                unique: true);

            migrationBuilder.Sql(
                """
                CREATE FUNCTION public."GuardAQGreenWeeklySalesDecisionMutation"()
                RETURNS trigger
                LANGUAGE plpgsql
                SET search_path = pg_catalog
                AS $function$
                BEGIN
                    IF TG_OP = 'TRUNCATE' OR TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'AQGreen weekly-sales decisions cannot be deleted or truncated.';
                    END IF;
                    IF TG_OP = 'INSERT' THEN
                        IF NEW."ReviewStatus" <> 1 THEN
                            RAISE EXCEPTION 'AQGreen weekly-sales decisions must begin HeldForEvidence.';
                        END IF;
                        RETURN NEW;
                    END IF;
                    IF OLD."ReviewStatus" <> 1 OR NEW."ReviewStatus" NOT IN (2, 3) THEN
                        RAISE EXCEPTION 'AQGreen weekly-sales decisions allow only HeldForEvidence to final transitions.';
                    END IF;
                    IF NEW."Id" IS DISTINCT FROM OLD."Id"
                       OR NEW."TenantId" IS DISTINCT FROM OLD."TenantId"
                       OR NEW."ParticipantId" IS DISTINCT FROM OLD."ParticipantId"
                       OR NEW."CommissionWeekStartUtc" IS DISTINCT FROM OLD."CommissionWeekStartUtc"
                       OR NEW."SalesEligibilityRulesVersion" IS DISTINCT FROM OLD."SalesEligibilityRulesVersion"
                       OR NEW."CreationTime" IS DISTINCT FROM OLD."CreationTime"
                       OR NEW."CreatorUserId" IS DISTINCT FROM OLD."CreatorUserId" THEN
                        RAISE EXCEPTION 'AQGreen weekly-sales decision identity and creation facts are immutable.';
                    END IF;
                    RETURN NEW;
                END;
                $function$;

                CREATE FUNCTION public."GuardAQGreenWeeklySalesEvidenceMutation"()
                RETURNS trigger
                LANGUAGE plpgsql
                SET search_path = pg_catalog
                AS $function$
                DECLARE parent_status integer;
                BEGIN
                    IF TG_OP = 'TRUNCATE' OR TG_OP IN ('UPDATE', 'DELETE') THEN
                        RAISE EXCEPTION 'AQGreen weekly-sales evidence is append-only.';
                    END IF;
                    SELECT "ReviewStatus" INTO parent_status
                    FROM public."AQGreenWeeklySalesEligibilityDecisions" decision
                    WHERE decision."TenantId" = NEW."TenantId"
                      AND decision."Id" = NEW."DecisionId";
                    IF parent_status IS DISTINCT FROM 1 THEN
                        RAISE EXCEPTION 'AQGreen weekly-sales evidence can be added only while the parent is HeldForEvidence.';
                    END IF;
                    RETURN NEW;
                END;
                $function$;

                CREATE FUNCTION public."EnsureAQGreenWeeklySalesFinalHasEvidence"()
                RETURNS trigger
                LANGUAGE plpgsql
                SET search_path = pg_catalog
                AS $function$
                BEGIN
                    IF NEW."ReviewStatus" IN (2, 3) AND NOT EXISTS (
                        SELECT 1 FROM public."AQGreenWeeklySalesEvidenceReferences" evidence
                        WHERE evidence."TenantId" = NEW."TenantId"
                          AND evidence."DecisionId" = NEW."Id") THEN
                        RAISE EXCEPTION 'A finalized AQGreen weekly-sales decision requires evidence.';
                    END IF;
                    RETURN NEW;
                END;
                $function$;

                CREATE TRIGGER "TR_AQGreenWeeklySalesDecisions_GuardRow"
                BEFORE INSERT OR UPDATE OR DELETE
                ON public."AQGreenWeeklySalesEligibilityDecisions"
                FOR EACH ROW EXECUTE FUNCTION public."GuardAQGreenWeeklySalesDecisionMutation"();
                ALTER TABLE public."AQGreenWeeklySalesEligibilityDecisions"
                    ENABLE ALWAYS TRIGGER "TR_AQGreenWeeklySalesDecisions_GuardRow";
                CREATE TRIGGER "TR_AQGreenWeeklySalesDecisions_PreventTruncate"
                BEFORE TRUNCATE ON public."AQGreenWeeklySalesEligibilityDecisions"
                FOR EACH STATEMENT EXECUTE FUNCTION public."GuardAQGreenWeeklySalesDecisionMutation"();
                ALTER TABLE public."AQGreenWeeklySalesEligibilityDecisions"
                    ENABLE ALWAYS TRIGGER "TR_AQGreenWeeklySalesDecisions_PreventTruncate";
                CREATE CONSTRAINT TRIGGER "TR_AQGreenWeeklySalesDecisions_RequireEvidence"
                AFTER INSERT OR UPDATE ON public."AQGreenWeeklySalesEligibilityDecisions"
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION public."EnsureAQGreenWeeklySalesFinalHasEvidence"();
                ALTER TABLE public."AQGreenWeeklySalesEligibilityDecisions"
                    ENABLE ALWAYS TRIGGER "TR_AQGreenWeeklySalesDecisions_RequireEvidence";

                CREATE TRIGGER "TR_AQGreenWeeklySalesEvidence_GuardRow"
                BEFORE INSERT OR UPDATE OR DELETE
                ON public."AQGreenWeeklySalesEvidenceReferences"
                FOR EACH ROW EXECUTE FUNCTION public."GuardAQGreenWeeklySalesEvidenceMutation"();
                ALTER TABLE public."AQGreenWeeklySalesEvidenceReferences"
                    ENABLE ALWAYS TRIGGER "TR_AQGreenWeeklySalesEvidence_GuardRow";
                CREATE TRIGGER "TR_AQGreenWeeklySalesEvidence_PreventTruncate"
                BEFORE TRUNCATE ON public."AQGreenWeeklySalesEvidenceReferences"
                FOR EACH STATEMENT EXECUTE FUNCTION public."GuardAQGreenWeeklySalesEvidenceMutation"();
                ALTER TABLE public."AQGreenWeeklySalesEvidenceReferences"
                    ENABLE ALWAYS TRIGGER "TR_AQGreenWeeklySalesEvidence_PreventTruncate";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenWeeklySalesEvidenceReferences_TenantId_DecisionId_So~",
                table: "AQGreenWeeklySalesEvidenceReferences",
                columns: new[] { "TenantId", "DecisionId", "Source", "TechnicalReference" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                LOCK TABLE public."AQGreenWeeklySalesEligibilityDecisions",
                           public."AQGreenWeeklySalesEvidenceReferences"
                IN ACCESS EXCLUSIVE MODE;
                DO $block$
                BEGIN
                    IF EXISTS (SELECT 1 FROM public."AQGreenWeeklySalesEvidenceReferences")
                       OR EXISTS (SELECT 1 FROM public."AQGreenWeeklySalesEligibilityDecisions") THEN
                        RAISE EXCEPTION 'Cannot remove AQGreen weekly-sales eligibility schema while durable review evidence exists.';
                    END IF;
                END;
                $block$;

                DROP TRIGGER "TR_AQGreenWeeklySalesEvidence_PreventTruncate"
                    ON public."AQGreenWeeklySalesEvidenceReferences";
                DROP TRIGGER "TR_AQGreenWeeklySalesEvidence_GuardRow"
                    ON public."AQGreenWeeklySalesEvidenceReferences";
                DROP TRIGGER "TR_AQGreenWeeklySalesDecisions_RequireEvidence"
                    ON public."AQGreenWeeklySalesEligibilityDecisions";
                DROP TRIGGER "TR_AQGreenWeeklySalesDecisions_PreventTruncate"
                    ON public."AQGreenWeeklySalesEligibilityDecisions";
                DROP TRIGGER "TR_AQGreenWeeklySalesDecisions_GuardRow"
                    ON public."AQGreenWeeklySalesEligibilityDecisions";
                DROP FUNCTION public."EnsureAQGreenWeeklySalesFinalHasEvidence"();
                DROP FUNCTION public."GuardAQGreenWeeklySalesEvidenceMutation"();
                DROP FUNCTION public."GuardAQGreenWeeklySalesDecisionMutation"();
                """);
            migrationBuilder.DropTable(
                name: "AQGreenWeeklySalesEvidenceReferences");

            migrationBuilder.DropTable(
                name: "AQGreenWeeklySalesEligibilityDecisions");
        }
    }
}
