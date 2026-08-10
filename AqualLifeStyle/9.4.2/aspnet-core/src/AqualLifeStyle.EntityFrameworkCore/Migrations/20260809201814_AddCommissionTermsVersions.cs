using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    /// <inheritdoc />
    public partial class AddCommissionTermsVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EntryCommissionTermsVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EffectiveAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LevelOneComponentAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    LevelTwoComponentAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    LevelThreeComponentAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntryCommissionTermsVersions", x => x.Id);
                    table.CheckConstraint("CK_EntryCommissionTermsVersions_Currency_ThreeLetters", "length(\"Currency\") = 3");
                    table.CheckConstraint("CK_EntryCommissionTermsVersions_LevelOneAmount_Positive", "\"LevelOneComponentAmount\" > 0");
                    table.CheckConstraint("CK_EntryCommissionTermsVersions_LevelThreeAmount_Positive", "\"LevelThreeComponentAmount\" > 0");
                    table.CheckConstraint("CK_EntryCommissionTermsVersions_LevelTwoAmount_Positive", "\"LevelTwoComponentAmount\" > 0");
                    table.CheckConstraint("CK_EntryCommissionTermsVersions_Version_NotBlank", "length(trim(\"Version\")) > 0");
                });

            migrationBuilder.CreateTable(
                name: "OnyxCommissionTermsVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EffectiveAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LevelOnePerPersonRate = table.Column<decimal>(type: "numeric", nullable: false),
                    LevelTwoPerPersonRate = table.Column<decimal>(type: "numeric", nullable: false),
                    LevelThreePerPersonRate = table.Column<decimal>(type: "numeric", nullable: false),
                    LevelFourPerPersonRate = table.Column<decimal>(type: "numeric", nullable: false),
                    LevelFivePerPersonRate = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnyxCommissionTermsVersions", x => x.Id);
                    table.CheckConstraint("CK_OnyxCommissionTermsVersions_Currency_ThreeLetters", "length(\"Currency\") = 3");
                    table.CheckConstraint("CK_OnyxCommissionTermsVersions_LevelFiveRate_Positive", "\"LevelFivePerPersonRate\" > 0");
                    table.CheckConstraint("CK_OnyxCommissionTermsVersions_LevelFourRate_Positive", "\"LevelFourPerPersonRate\" > 0");
                    table.CheckConstraint("CK_OnyxCommissionTermsVersions_LevelOneRate_Positive", "\"LevelOnePerPersonRate\" > 0");
                    table.CheckConstraint("CK_OnyxCommissionTermsVersions_LevelThreeRate_Positive", "\"LevelThreePerPersonRate\" > 0");
                    table.CheckConstraint("CK_OnyxCommissionTermsVersions_LevelTwoRate_Positive", "\"LevelTwoPerPersonRate\" > 0");
                    table.CheckConstraint("CK_OnyxCommissionTermsVersions_Version_NotBlank", "length(trim(\"Version\")) > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntryCommissionTermsVersions_EffectiveAt",
                table: "EntryCommissionTermsVersions",
                column: "EffectiveAt",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EntryCommissionTermsVersions_Version",
                table: "EntryCommissionTermsVersions",
                column: "Version",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnyxCommissionTermsVersions_EffectiveAt",
                table: "OnyxCommissionTermsVersions",
                column: "EffectiveAt",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnyxCommissionTermsVersions_Version",
                table: "OnyxCommissionTermsVersions",
                column: "Version",
                unique: true);

            if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql("""
                    CREATE FUNCTION "PreventCommissionTermsVersionMutation"()
                    RETURNS trigger
                    LANGUAGE plpgsql
                    AS $function$
                    BEGIN
                        RAISE EXCEPTION 'Commission terms versions are append-only.';
                    END;
                    $function$;

                    CREATE TRIGGER "TR_EntryCommissionTermsVersions_AppendOnly"
                    BEFORE UPDATE OR DELETE ON "EntryCommissionTermsVersions"
                    FOR EACH ROW
                    EXECUTE FUNCTION "PreventCommissionTermsVersionMutation"();

                    CREATE TRIGGER "TR_EntryCommissionTermsVersions_PreventTruncate"
                    BEFORE TRUNCATE ON "EntryCommissionTermsVersions"
                    FOR EACH STATEMENT
                    EXECUTE FUNCTION "PreventCommissionTermsVersionMutation"();

                    CREATE TRIGGER "TR_OnyxCommissionTermsVersions_AppendOnly"
                    BEFORE UPDATE OR DELETE ON "OnyxCommissionTermsVersions"
                    FOR EACH ROW
                    EXECUTE FUNCTION "PreventCommissionTermsVersionMutation"();

                    CREATE TRIGGER "TR_OnyxCommissionTermsVersions_PreventTruncate"
                    BEFORE TRUNCATE ON "OnyxCommissionTermsVersions"
                    FOR EACH STATEMENT
                    EXECUTE FUNCTION "PreventCommissionTermsVersionMutation"();
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql("""
                    DO $block$
                    BEGIN
                        IF EXISTS (SELECT 1 FROM "EntryCommissionTermsVersions")
                        THEN
                            RAISE EXCEPTION 'Cannot remove commission terms versions after evidence has been recorded.';
                        END IF;
                        IF EXISTS (SELECT 1 FROM "OnyxCommissionTermsVersions")
                        THEN
                            RAISE EXCEPTION 'Cannot remove Onyx commission terms versions after evidence has been recorded.';
                        END IF;
                    END;
                    $block$;

                    DROP TRIGGER "TR_EntryCommissionTermsVersions_AppendOnly"
                        ON "EntryCommissionTermsVersions";
                    DROP TRIGGER "TR_EntryCommissionTermsVersions_PreventTruncate"
                        ON "EntryCommissionTermsVersions";
                    DROP TRIGGER "TR_OnyxCommissionTermsVersions_AppendOnly"
                        ON "OnyxCommissionTermsVersions";
                    DROP TRIGGER "TR_OnyxCommissionTermsVersions_PreventTruncate"
                        ON "OnyxCommissionTermsVersions";
                    DROP FUNCTION "PreventCommissionTermsVersionMutation"();
                    """);
            }

            migrationBuilder.DropTable(
                name: "EntryCommissionTermsVersions");

            migrationBuilder.DropTable(
                name: "OnyxCommissionTermsVersions");
        }
    }
}
