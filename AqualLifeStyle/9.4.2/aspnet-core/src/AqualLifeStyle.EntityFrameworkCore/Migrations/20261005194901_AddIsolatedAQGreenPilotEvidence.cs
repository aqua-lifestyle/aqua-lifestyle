using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    /// <inheritdoc />
    public partial class AddIsolatedAQGreenPilotEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTestMoney",
                table: "EntryWeeklyCommissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AQGreenProspectiveRootAdmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdministratorUserId = table.Column<long>(type: "bigint", nullable: false),
                    AuthorizedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AQGreenProspectiveRootAdmissions", x => x.Id);
                    table.CheckConstraint("CK_AQGRootAdmission_Actor", "\"AdministratorUserId\" > 0");
                    table.CheckConstraint("CK_AQGRootAdmission_Reason", "length(trim(\"Reason\")) > 0");
                    table.ForeignKey(
                        name: "FK_AQGreenProspectiveRootAdmissions_EntryParticipations_Tenant~",
                        columns: x => new { x.TenantId, x.ParticipantId },
                        principalTable: "EntryParticipations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_EntryWeeklyCommissions_TestMoney_NoPayout",
                table: "EntryWeeklyCommissions",
                sql: "NOT \"IsTestMoney\" OR (\"PayoutStatus\" IN (0, 1, 2) AND \"ReleasedAt\" IS NULL AND \"PaidAt\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenProspectiveRootAdmissions_TenantId_ParticipantId",
                table: "AQGreenProspectiveRootAdmissions",
                columns: new[] { "TenantId", "ParticipantId" },
                unique: true);
            if (ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
                migrationBuilder.Sql("""
                    CREATE TRIGGER "TR_AQGPilotRoot_AppendOnly"
                    BEFORE UPDATE OR DELETE ON public."AQGreenProspectiveRootAdmissions"
                    FOR EACH ROW EXECUTE FUNCTION public."PreventAQGreenRecruitmentAttributionMutation"();
                    ALTER TABLE public."AQGreenProspectiveRootAdmissions" ENABLE ALWAYS TRIGGER "TR_AQGPilotRoot_AppendOnly";
                    CREATE TRIGGER "TR_AQGPilotRoot_NoTruncate"
                    BEFORE TRUNCATE ON public."AQGreenProspectiveRootAdmissions"
                    FOR EACH STATEMENT EXECUTE FUNCTION public."PreventAQGreenRecruitmentAttributionMutation"();
                    ALTER TABLE public."AQGreenProspectiveRootAdmissions" ENABLE ALWAYS TRIGGER "TR_AQGPilotRoot_NoTruncate";

                    CREATE FUNCTION public."PreventAQGreenTestMoneyConversion"()
                    RETURNS trigger LANGUAGE plpgsql SET search_path = pg_catalog AS $function$
                    BEGIN
                        IF OLD."IsTestMoney" AND NOT NEW."IsTestMoney" THEN
                            RAISE EXCEPTION 'Test money cannot be converted to real money.';
                        END IF;
                        RETURN NEW;
                    END;
                    $function$;
                    CREATE TRIGGER "TR_AQGTestMoney_NoConversion"
                    BEFORE UPDATE ON public."EntryWeeklyCommissions"
                    FOR EACH ROW EXECUTE FUNCTION public."PreventAQGreenTestMoneyConversion"();
                    ALTER TABLE public."EntryWeeklyCommissions" ENABLE ALWAYS TRIGGER "TR_AQGTestMoney_NoConversion";
                    """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
                migrationBuilder.Sql("""
                    DO $guard$ BEGIN
                        IF EXISTS (SELECT 1 FROM public."AQGreenProspectiveRootAdmissions") OR
                           EXISTS (SELECT 1 FROM public."EntryWeeklyCommissions" WHERE "IsTestMoney") THEN
                            RAISE EXCEPTION 'Pilot evidence exists; destructive rollback is prohibited.';
                        END IF;
                    END $guard$;
                    DROP TRIGGER "TR_AQGTestMoney_NoConversion" ON public."EntryWeeklyCommissions";
                    DROP FUNCTION public."PreventAQGreenTestMoneyConversion"();
                    """);
            migrationBuilder.DropTable(
                name: "AQGreenProspectiveRootAdmissions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EntryWeeklyCommissions_TestMoney_NoPayout",
                table: "EntryWeeklyCommissions");

            migrationBuilder.DropColumn(
                name: "IsTestMoney",
                table: "EntryWeeklyCommissions");
        }
    }
}
