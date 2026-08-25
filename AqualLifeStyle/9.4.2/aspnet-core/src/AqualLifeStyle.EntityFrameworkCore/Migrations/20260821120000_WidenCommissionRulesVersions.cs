using AqualLifeStyle.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    [DbContext(typeof(AqualLifeStyleDbContext))]
    [Migration("20260821120000_WidenCommissionRulesVersions")]
    public partial class WidenCommissionRulesVersions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            AlterRulesVersionColumns(migrationBuilder, 64, "character varying(64)");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                LOCK TABLE
                    "EntryCommissionPeriods",
                    "EntryWeeklyCommissions",
                    "OnyxCommissionPeriods",
                    "OnyxWeeklyCommissions"
                IN ACCESS EXCLUSIVE MODE;

                DO $rules_version_rollback$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "EntryCommissionPeriods" WHERE length("RulesVersion") > 32
                        UNION ALL
                        SELECT 1 FROM "EntryWeeklyCommissions" WHERE length("RulesVersion") > 32
                        UNION ALL
                        SELECT 1 FROM "OnyxCommissionPeriods" WHERE length("RulesVersion") > 32
                        UNION ALL
                        SELECT 1 FROM "OnyxWeeklyCommissions" WHERE length("RulesVersion") > 32)
                    THEN
                        RAISE EXCEPTION 'Cannot narrow commission rules versions while values longer than 32 characters exist.';
                    END IF;
                END
                $rules_version_rollback$;
                """);

            AlterRulesVersionColumns(migrationBuilder, 32, "character varying(32)");
        }

        private static void AlterRulesVersionColumns(
            MigrationBuilder migrationBuilder,
            int maxLength,
            string columnType)
        {
            foreach (var table in new[]
                     {
                         "EntryCommissionPeriods",
                         "EntryWeeklyCommissions",
                         "OnyxCommissionPeriods",
                         "OnyxWeeklyCommissions"
                     })
            {
                migrationBuilder.AlterColumn<string>(
                    name: "RulesVersion",
                    table: table,
                    type: columnType,
                    maxLength: maxLength,
                    nullable: false,
                    oldClrType: typeof(string),
                    oldType: maxLength == 64
                        ? "character varying(32)"
                        : "character varying(64)",
                    oldMaxLength: maxLength == 64 ? 32 : 64);
            }
        }
    }
}
