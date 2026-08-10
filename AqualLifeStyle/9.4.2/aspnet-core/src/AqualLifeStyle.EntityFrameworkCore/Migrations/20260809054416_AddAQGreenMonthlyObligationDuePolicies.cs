using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    /// <inheritdoc />
    public partial class AddAQGreenMonthlyObligationDuePolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DuePolicyVersion",
                table: "EntryMonthlyObligations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EntryMonthlyObligationDuePolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DueDayOfMonth = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntryMonthlyObligationDuePolicies", x => x.Id);
                    table.UniqueConstraint("AK_EntryMonthlyObligationDuePolicies_Version", x => x.Version);
                    table.CheckConstraint("CK_EntryMonthlyObligationDuePolicies_DueDayOfMonth", "\"DueDayOfMonth\" >= 1 AND \"DueDayOfMonth\" <= 28");
                    table.CheckConstraint("CK_EntryMonthlyObligationDuePolicies_Version_NotBlank", "length(trim(\"Version\")) > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntryMonthlyObligations_DuePolicyVersion",
                table: "EntryMonthlyObligations",
                column: "DuePolicyVersion");

            migrationBuilder.CreateIndex(
                name: "IX_EntryMonthlyObligationDuePolicies_EffectiveFrom",
                table: "EntryMonthlyObligationDuePolicies",
                column: "EffectiveFrom");

            if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql("""
                    CREATE FUNCTION "PreventEntryMonthlyObligationDuePolicyMutation"()
                    RETURNS trigger
                    LANGUAGE plpgsql
                    AS $function$
                    BEGIN
                        RAISE EXCEPTION 'AQGreen monthly obligation due policies are append-only.';
                    END;
                    $function$;

                    CREATE TRIGGER "TR_EntryMonthlyObligationDuePolicies_AppendOnly"
                    BEFORE UPDATE OR DELETE ON "EntryMonthlyObligationDuePolicies"
                    FOR EACH ROW
                    EXECUTE FUNCTION "PreventEntryMonthlyObligationDuePolicyMutation"();
                    """);
            }

            migrationBuilder.AddForeignKey(
                name: "FK_EntryMonthlyObligations_EntryMonthlyObligationDuePolicies_D~",
                table: "EntryMonthlyObligations",
                column: "DuePolicyVersion",
                principalTable: "EntryMonthlyObligationDuePolicies",
                principalColumn: "Version",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql("""
                    DO $block$
                    BEGIN
                        IF EXISTS (SELECT 1 FROM "EntryMonthlyObligationDuePolicies")
                           OR EXISTS (
                               SELECT 1
                               FROM "EntryMonthlyObligations"
                               WHERE "DuePolicyVersion" IS NOT NULL)
                        THEN
                            RAISE EXCEPTION 'Cannot remove AQGreen due-policy schema after policy evidence has been recorded.';
                        END IF;
                    END;
                    $block$;

                    DROP TRIGGER "TR_EntryMonthlyObligationDuePolicies_AppendOnly"
                        ON "EntryMonthlyObligationDuePolicies";
                    DROP FUNCTION "PreventEntryMonthlyObligationDuePolicyMutation"();
                    """);
            }

            migrationBuilder.DropForeignKey(
                name: "FK_EntryMonthlyObligations_EntryMonthlyObligationDuePolicies_D~",
                table: "EntryMonthlyObligations");

            migrationBuilder.DropTable(
                name: "EntryMonthlyObligationDuePolicies");

            migrationBuilder.DropIndex(
                name: "IX_EntryMonthlyObligations_DuePolicyVersion",
                table: "EntryMonthlyObligations");

            migrationBuilder.DropColumn(
                name: "DuePolicyVersion",
                table: "EntryMonthlyObligations");
        }
    }
}
