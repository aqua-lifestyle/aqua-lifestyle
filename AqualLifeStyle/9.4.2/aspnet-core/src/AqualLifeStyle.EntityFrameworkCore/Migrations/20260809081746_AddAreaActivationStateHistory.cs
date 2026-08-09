using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    /// <inheritdoc />
    public partial class AddAreaActivationStateHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AreaActivationStateRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    EffectiveAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecordedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    Justification = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AreaActivationStateRecords", x => x.Id);
                    table.CheckConstraint("CK_AreaActivationStateRecords_EffectiveAt_RecordedAt", "\"EffectiveAt\" <= \"RecordedAt\"");
                    table.CheckConstraint("CK_AreaActivationStateRecords_Justification_NotBlank", "length(trim(\"Justification\")) > 0");
                    table.CheckConstraint("CK_AreaActivationStateRecords_Kind", "\"Kind\" >= 0 AND \"Kind\" <= 2");
                    table.ForeignKey(
                        name: "FK_AreaActivationStateRecords_AbpTenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "AbpTenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AreaActivationStateRecords_TenantId_EffectiveAt",
                table: "AreaActivationStateRecords",
                columns: new[] { "TenantId", "EffectiveAt" },
                unique: true);

            if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql("""
                    CREATE FUNCTION "PreventAreaActivationStateRecordMutation"()
                    RETURNS trigger
                    LANGUAGE plpgsql
                    AS $function$
                    BEGIN
                        RAISE EXCEPTION 'Area activation state records are append-only.';
                    END;
                    $function$;

                    CREATE TRIGGER "TR_AreaActivationStateRecords_AppendOnly"
                    BEFORE UPDATE OR DELETE ON "AreaActivationStateRecords"
                    FOR EACH ROW
                    EXECUTE FUNCTION "PreventAreaActivationStateRecordMutation"();

                    CREATE TRIGGER "TR_AreaActivationStateRecords_PreventTruncate"
                    BEFORE TRUNCATE ON "AreaActivationStateRecords"
                    FOR EACH STATEMENT
                    EXECUTE FUNCTION "PreventAreaActivationStateRecordMutation"();
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
                        IF EXISTS (SELECT 1 FROM "AreaActivationStateRecords")
                        THEN
                            RAISE EXCEPTION 'Cannot remove Area activation history after evidence has been recorded.';
                        END IF;
                    END;
                    $block$;

                    DROP TRIGGER "TR_AreaActivationStateRecords_AppendOnly"
                        ON "AreaActivationStateRecords";
                    DROP TRIGGER "TR_AreaActivationStateRecords_PreventTruncate"
                        ON "AreaActivationStateRecords";
                    DROP FUNCTION "PreventAreaActivationStateRecordMutation"();
                    """);
            }

            migrationBuilder.DropTable(
                name: "AreaActivationStateRecords");
        }
    }
}
