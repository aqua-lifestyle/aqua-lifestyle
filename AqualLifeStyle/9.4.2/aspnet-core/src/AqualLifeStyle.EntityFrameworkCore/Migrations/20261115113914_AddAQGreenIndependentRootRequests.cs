using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    /// <inheritdoc />
    public partial class AddAQGreenIndependentRootRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AQGreenIndependentRootRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AQGreenIndependentRootRequests", x => x.Id);
                    table.CheckConstraint("CK_AQGIndependentRoot_Actor", "\"RequestedByUserId\" > 0");
                    table.CheckConstraint("CK_AQGIndependentRoot_Tenant", "\"TenantId\" > 0");
                    table.ForeignKey(
                        name: "FK_AQGreenIndependentRootRequests_EntryParticipations_TenantId~",
                        columns: x => new { x.TenantId, x.ParticipantId },
                        principalTable: "EntryParticipations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenIndependentRootRequests_TenantId_ParticipantId",
                table: "AQGreenIndependentRootRequests",
                columns: new[] { "TenantId", "ParticipantId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A rollback after customers have requested roots would erase
            // immutable provenance. An empty-table downgrade is still possible
            // during an explicitly reconciled failed rollout.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "AQGreenIndependentRootRequests") THEN
                        RAISE EXCEPTION 'AQGREEN_INDEPENDENT_ROOT_HISTORY_PRESENT';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "AQGreenIndependentRootRequests");
        }
    }
}
