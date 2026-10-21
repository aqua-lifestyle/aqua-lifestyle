using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    /// <inheritdoc />
    public partial class AddAQGreenSalesEvidenceInvalidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_AQGreenWeeklySalesEvidenceReferences_TenantId_DecisionId_Id",
                table: "AQGreenWeeklySalesEvidenceReferences",
                columns: new[] { "TenantId", "DecisionId", "Id" });

            migrationBuilder.CreateTable(
                name: "AQGreenSalesEvidenceInvalidations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    DecisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RecordedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AQGreenSalesEvidenceInvalidations", x => x.Id);
                    table.CheckConstraint("CK_AQGSalesInvalidation_Authority", "\"TenantId\" > 0 AND \"RecordedByUserId\" > 0");
                    table.CheckConstraint("CK_AQGSalesInvalidation_Reason", "length(trim(\"Reason\")) > 0 AND \"Reason\" = trim(\"Reason\")");
                    table.ForeignKey(
                        name: "FK_AQGreenSalesEvidenceInvalidations_AQGreenWeeklySalesEvidenc~",
                        columns: x => new { x.TenantId, x.DecisionId, x.EvidenceId },
                        principalTable: "AQGreenWeeklySalesEvidenceReferences",
                        principalColumns: new[] { "TenantId", "DecisionId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenSalesEvidenceInvalidations_TenantId_DecisionId",
                table: "AQGreenSalesEvidenceInvalidations",
                columns: new[] { "TenantId", "DecisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenSalesEvidenceInvalidations_TenantId_DecisionId_Evide~",
                table: "AQGreenSalesEvidenceInvalidations",
                columns: new[] { "TenantId", "DecisionId", "EvidenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenSalesEvidenceInvalidations_TenantId_EvidenceId",
                table: "AQGreenSalesEvidenceInvalidations",
                columns: new[] { "TenantId", "EvidenceId" },
                unique: true);
            if (ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
                migrationBuilder.Sql("""
                    CREATE TRIGGER "TR_AQGSalesInvalidation_AppendOnly"
                    BEFORE UPDATE OR DELETE ON public."AQGreenSalesEvidenceInvalidations"
                    FOR EACH ROW EXECUTE FUNCTION public."PreventAQGreenRecruitmentAttributionMutation"();
                    ALTER TABLE public."AQGreenSalesEvidenceInvalidations" ENABLE ALWAYS TRIGGER "TR_AQGSalesInvalidation_AppendOnly";
                    CREATE TRIGGER "TR_AQGSalesInvalidation_NoTruncate"
                    BEFORE TRUNCATE ON public."AQGreenSalesEvidenceInvalidations"
                    FOR EACH STATEMENT EXECUTE FUNCTION public."PreventAQGreenRecruitmentAttributionMutation"();
                    ALTER TABLE public."AQGreenSalesEvidenceInvalidations" ENABLE ALWAYS TRIGGER "TR_AQGSalesInvalidation_NoTruncate";
                    CREATE FUNCTION public."ValidateAQGreenSalesInvalidation"()
                    RETURNS trigger LANGUAGE plpgsql SET search_path = pg_catalog AS $function$
                    BEGIN
                        IF NOT EXISTS (
                            SELECT 1 FROM public."AQGreenWeeklySalesEligibilityDecisions" decision
                            WHERE decision."TenantId" = NEW."TenantId" AND decision."Id" = NEW."DecisionId"
                              AND decision."ReviewStatus" = 2 AND NEW."RecordedAtUtc" >= decision."ReviewedAt") THEN
                            RAISE EXCEPTION 'Invalidation requires finalized confirmed evidence and an authoritative later time.';
                        END IF;
                        RETURN NEW;
                    END;
                    $function$;
                    CREATE TRIGGER "TR_AQGSalesInvalidation_Validate"
                    BEFORE INSERT ON public."AQGreenSalesEvidenceInvalidations"
                    FOR EACH ROW EXECUTE FUNCTION public."ValidateAQGreenSalesInvalidation"();
                    ALTER TABLE public."AQGreenSalesEvidenceInvalidations" ENABLE ALWAYS TRIGGER "TR_AQGSalesInvalidation_Validate";
                    """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
                migrationBuilder.Sql("""
                    DO $guard$ BEGIN
                        IF EXISTS (SELECT 1 FROM public."AQGreenSalesEvidenceInvalidations") THEN
                            RAISE EXCEPTION 'Reconciliation evidence exists; destructive rollback is prohibited.';
                        END IF;
                    END $guard$;
                    DROP TRIGGER "TR_AQGSalesInvalidation_Validate" ON public."AQGreenSalesEvidenceInvalidations";
                    DROP FUNCTION public."ValidateAQGreenSalesInvalidation"();
                    """);
            migrationBuilder.DropTable(
                name: "AQGreenSalesEvidenceInvalidations");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_AQGreenWeeklySalesEvidenceReferences_TenantId_DecisionId_Id",
                table: "AQGreenWeeklySalesEvidenceReferences");
        }
    }
}
